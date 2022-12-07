namespace OBSStudioClient
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.MessageClasses;
    using OBSStudioClient.Messages;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;

    // https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md
    // CURRENT VERSION IMPLEMENTED: 5.1.0

    public partial class OBSClient : INotifyPropertyChanged
    {
        private const int SUPPORTED_RPC_VERSION = 1;

        private string _hostname = "localhost";
        private int _port = 4455;
        private string _password = string.Empty;
        private int _requestTimeout = 500;
        private EventSubscription _eventSubscription = EventSubscription.All;

        private CancellationTokenSource _cancellationTokenSource = new();
        private Task _receiverTask = Task.CompletedTask;
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private ClientWebSocket _client = new();

        private readonly ConcurrentDictionary<string, TaskCompletionSource<IMessageData>> _requests = new();

        /// <summary>
        /// The maximum amount of time, in milliseconds, to wait for an OBS Studio response.
        /// </summary>
        /// <remarks>
        /// The minimum value is 150. Please take into account that when sending Batch Requests, specifically with a long Sleep requests, this default value of 500 might not be enough.
        /// </remarks>
        public int RequestTimeout
        {
            get
            {
                return _requestTimeout;
            }
            set
            {
                if (value < 150)
                {
                    _requestTimeout = 150;
                }
                else
                {
                    _requestTimeout = value;
                }
            }
        }
        
        /// <summary>
        /// The current state of the connection to OBS Studio.
        /// </summary>
        public ConnectionState ConnectionState
        {
            get
            {
                return _connectionState;
            }
            private set
            {
                if (_connectionState != value)
                {
                    _connectionState = value;
                    this.OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Asynchronous event, triggered when the connection with OBS Studio is closed.
        /// </summary>
        public event AsyncEventHandler<ConnectionClosedEventArgs>? ConnectionClosed;

        /// <summary>
        /// Synchronous event, triggered when an important property changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        public delegate Task AsyncEventHandler<T>(object? sender, T e) where T : EventArgs;
        public delegate Task AsyncEventHandler(object? sender);

        /// <summary>
        /// Creates a new instance of the <see cref="OBSClient"/> class.
        /// </summary>
        public OBSClient()
        {
        }

        /// <summary>
        /// Opens the connection to OBS Studio and tries to authenticate the session.
        /// </summary>
        /// <param name="password">The OBS Studio WebSockets password or empty to connect without authentication. Defaults to empty.</param>
        /// <param name="hostname">The hostname of the computer running OBS Studio to connect to. Defaults to "localhost".</param>
        /// <param name="port">The Port on which the OBS Studio WebSocket interface is listenting. Default to 4455.</param>
        /// <param name="eventSubscription">The events to subscribe to. Defaults to All events.</param>
        /// <returns>True, when the connection was succesfully established, and False otherwise.</returns>
        /// <remarks>
        /// When True is returned, this does not mean that authentication has succeeded. Authentication will be handled asynchronously.
        /// You can use the <see cref="PropertyChanged"/> event to see whether the <see cref="ConnectionState"/> is Connected, which indicates succesfull authenticaiton.
        /// </remarks>
        public async Task<bool> ConnectAsync(string password = "", string hostname = "localhost", int port = 4455, EventSubscription eventSubscription = EventSubscription.All)
        {
            if (_connectionState != ConnectionState.Disconnected)
            {
                return true;
            }

            _hostname = hostname;
            _port = port;
            _password = password;
            _eventSubscription = eventSubscription;
            _cancellationTokenSource = new CancellationTokenSource();
            _client = new();

            Uri uri;
            try
            {
                uri = new($"ws://{_hostname}:{_port}");
            }
            catch (UriFormatException)
            {
                return false;
            }

            this.ConnectionState = ConnectionState.Connecting;
            try
            {
                await _client.ConnectAsync(uri, _cancellationTokenSource.Token);
            }
            catch (WebSocketException)
            {
                this.ConnectionState = ConnectionState.Disconnected;
                return false;
            }

            _receiverTask = Task.Run(() => Receiver(_cancellationTokenSource.Token));
            return true;
        }

        /// <summary>
        /// Disconnect from OBS Studio.
        /// </summary>
        public void Disconnect()
        {
            if (_connectionState != ConnectionState.Disconnected)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// Send a Batch Request.
        /// </summary>
        /// <param name="requestBatchExecutionType">The <see cref="RequestBatchExecutionType"/> of the batch.</param>
        /// <param name="requests">The Requests in the batch.</param>
        /// <param name="haltOnFailure">True, to continue processing requests even though one might have failed, or False to stop when any requests fails.</param>
        /// <returns>The responses for the individual requests.</returns>
        public RequestResponseMessage[] SendRequestBatch(RequestBatchExecutionType requestBatchExecutionType, RequestMessage[] requests, bool haltOnFailure = false)
        {
            TaskCompletionSource<IMessageData> tcs = new();
            CancellationTokenSource cts = new(_requestTimeout);
            cts.Token.Register(() => tcs.TrySetCanceled(), false);

            var executionType = (int)requestBatchExecutionType;
            var requestId = Guid.NewGuid().ToString();
            var d = new { requestId, haltOnFailure, executionType, requests };
            var op = (int)OpCode.RequestBatch;

            var result = this.SendAndWaitAsync(new { d, op });
            if (result is RequestBatchResponseMessage requestBatchResponseData)
            {
                return requestBatchResponseData.Results;
            }
            else
            {
                throw new OBSClientException("Unexpected response.");
            }
        }

        public async Task Reidentify(EventSubscription eventSubscription)
        {
            _eventSubscription = eventSubscription;
            ReidentifyMessage request = new(eventSubscription);
            OBSMessage message = new(request);
            await SendAsync(message);
        }

        /// <summary>
        /// Created a Qt Base64 encoded string with Size/Position data for a windowed projector.
        /// </summary>
        /// <param name="screenNumber">The screen number. See <see cref="GetMonitorList"/>.</param>
        /// <param name="x">The horizontal coordinate of the left top of the Window.</param>
        /// <param name="y">The vertical coordinate of the left top of the Window.</param>
        /// <param name="width">The width of the Window.</param>
        /// <param name="height">The height of the Window.</param>
        /// <param name="maximized">Whether the Window is Maximized. (This overrides x, y, width and height.)</param>
        /// <param name="fullscreen">Whether the Window is Full Screen. (This overrides maximized, x, y, width and height.)</param>
        /// <param name="screenWidth">The width of the screen. See <see cref="GetMonitorList"/>.</param>
        /// <returns>A Qt Base64 encoded string with Size/Position data for a windowed projector.</returns>
        /// <remarks>
        /// Used in <see cref="OpenSourceProjectorWindow(string, string)"/> and <see cref="OpenVideoMixProjectorWindow(MixType, string)"/>
        /// </remarks>
        public static string GetGeometry(int screenNumber, int x, int y, int width, int height, bool maximized, bool fullscreen, int screenWidth)
        {
            var magicNumber = new byte[] { 0x01, 0xD9, 0xD0, 0xCB };
            var majorVersion = new byte[] { 0x00, 0x03 };
            var minorVersion = new byte[] { 0x00, 0x00 };

            var w = BitConverter.GetBytes(screenWidth);
            var screenNr = BitConverter.GetBytes(screenNumber);
            var left = BitConverter.GetBytes(x);
            var top = BitConverter.GetBytes(y);
            var right = BitConverter.GetBytes(x + width);
            var bottom = BitConverter.GetBytes(y + height);
            var max = BitConverter.GetBytes(maximized);
            var full = BitConverter.GetBytes(fullscreen);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(w);
                Array.Reverse(screenNr);
                Array.Reverse(left);
                Array.Reverse(top);
                Array.Reverse(right);
                Array.Reverse(bottom);
            }

            var window = left.Concat(top).Concat(right).Concat(bottom);
            var result = magicNumber.Concat(majorVersion).Concat(minorVersion)
                .Concat(window).Concat(window)
                .Concat(screenNr).Concat(max).Concat(full).Concat(w)
                .Concat(window).ToArray();

            return Convert.ToBase64String(result);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string HashEncode(string input)
        {
            using var sha256 = SHA256.Create();
            var textBytes = Encoding.ASCII.GetBytes(input);
            var hash = sha256.ComputeHash(textBytes);
            return Convert.ToBase64String(hash);
        }

        private void ResponseReceived(OBSMessage responseMessage)
        {
            switch (responseMessage.Op)
            {
                case OpCode.Hello:
                    if (responseMessage.Data is HelloMessage helloResponseData)
                    {
                        this.ConnectionState = ConnectionState.Authenticating;
                        string? authentication = null;
                        if (helloResponseData.Authentication != null)
                        {
                            var base64secret = HashEncode(_password + helloResponseData.Authentication.Salt);
                            authentication = HashEncode(base64secret + helloResponseData.Authentication.Challenge);
                        }

                        IdentifyMessage identify = new(helloResponseData.RpcVersion, authentication, _eventSubscription);
                        OBSMessage message = new(identify);
                        _ = SendAsync(message);
                    }
                    else
                    {
                        throw new OBSClientException("responseMessage.Data is not expected HelloResponseData");
                    }

                    break;
                case OpCode.Identified:
                    // We have succesfully authenticated - OR - we did a Reidentify.
                    if (responseMessage.Data is not IdentifiedMessage)
                    {
                        throw new OBSClientException("responseMessage.Data is not expected IdentifyResponseData");
                    }

                    this.ConnectionState = ConnectionState.Connected;
                    break;
                case OpCode.RequestResponse:
                    if (responseMessage.Data is RequestResponseMessage requestResponseData)
                    {
                        if (_requests.TryGetValue(requestResponseData.RequestId, out var tcs))
                        {
                            tcs.SetResult(requestResponseData);
                        }
                    }
                    else
                    {
                        throw new OBSClientException("responseMessage.Data is not expected RequestResponseData");
                    }

                    break;
                case OpCode.Event:
                    if (responseMessage.Data is EventMessage eventResponseData)
                    {
                        switch (eventResponseData.EventType)
                        {
                            case EventType.ExitStarted:
                                this.ConnectionState = ConnectionState.Disconnecting;
                                this.InvokeAsyncEvent(this.ExitStarted);
                                break;
                            case EventType.VendorEvent:
                                this.InvokeAsyncEvent(this.VendorEvent, eventResponseData.EventData);
                                break;
                            case EventType.CustomEvent:
                                this.InvokeAsyncEvent(this.CustomEvent, eventResponseData.EventData);
                                break;
                            case EventType.CurrentSceneCollectionChanging:
                                this.InvokeAsyncEvent(this.CurrentSceneCollectionChanging, eventResponseData.EventData);
                                break;
                            case EventType.CurrentSceneCollectionChanged:
                                this.InvokeAsyncEvent(this.CurrentSceneCollectionChanged, eventResponseData.EventData);
                                break;
                            case EventType.SceneCollectionListChanged:
                                this.InvokeAsyncEvent(this.SceneCollectionListChanged, eventResponseData.EventData);
                                break;
                            case EventType.CurrentProfileChanging:
                                this.InvokeAsyncEvent(this.CurrentProfileChanging, eventResponseData.EventData);
                                break;
                            case EventType.CurrentProfileChanged:
                                this.InvokeAsyncEvent(this.CurrentProfileChanged, eventResponseData.EventData);
                                break;
                            case EventType.ProfileListChanged:
                                this.InvokeAsyncEvent(this.ProfileListChanged, eventResponseData.EventData);
                                break;
                            case EventType.SceneCreated:
                                this.InvokeAsyncEvent(this.SceneCreated, eventResponseData.EventData);
                                break;
                            case EventType.SceneRemoved:
                                this.InvokeAsyncEvent(this.SceneRemoved, eventResponseData.EventData);
                                break;
                            case EventType.SceneNameChanged:
                                this.InvokeAsyncEvent(this.SceneNameChanged, eventResponseData.EventData);
                                break;
                            case EventType.CurrentProgramSceneChanged:
                                this.InvokeAsyncEvent(this.CurrentProgramSceneChanged, eventResponseData.EventData);
                                break;
                            case EventType.CurrentPreviewSceneChanged:
                                this.InvokeAsyncEvent(this.CurrentPreviewSceneChanged, eventResponseData.EventData);
                                break;
                            case EventType.SceneListChanged:
                                this.InvokeAsyncEvent(this.SceneListChanged, eventResponseData.EventData);
                                break;
                            case EventType.InputCreated:
                                this.InvokeAsyncEvent(this.InputCreated, eventResponseData.EventData);
                                break;
                            case EventType.InputRemoved:
                                this.InvokeAsyncEvent(this.InputRemoved, eventResponseData.EventData);
                                break;
                            case EventType.InputNameChanged:
                                this.InvokeAsyncEvent(this.InputNameChanged, eventResponseData.EventData);
                                break;
                            case EventType.InputActiveStateChanged:
                                this.InvokeAsyncEvent(this.InputActiveStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.InputShowStateChanged:
                                this.InvokeAsyncEvent(this.InputShowStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.InputMuteStateChanged:
                                this.InvokeAsyncEvent(this.InputMuteStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.InputVolumeChanged:
                                this.InvokeAsyncEvent(this.InputVolumeChanged, eventResponseData.EventData);
                                break;
                            case EventType.InputAudioBalanceChanged:
                                this.InvokeAsyncEvent(this.InputAudioBalanceChanged, eventResponseData.EventData);
                                break;
                            case EventType.InputAudioSyncOffsetChanged:
                                this.InvokeAsyncEvent(this.InputAudioSyncOffsetChanged, eventResponseData.EventData);
                                break;
                            case EventType.InputAudioTracksChanged:
                                this.InvokeAsyncEvent(this.InputAudioTracksChanged, eventResponseData.EventData);
                                break;
                            case EventType.InputAudioMonitorTypeChanged:
                                this.InvokeAsyncEvent(this.InputAudioMonitorTypeChanged, eventResponseData.EventData);
                                break;
                            case EventType.InputVolumeMeters:
                                this.InvokeAsyncEvent(this.InputVolumeMeters, eventResponseData.EventData);
                                break;
                            case EventType.CurrentSceneTransitionChanged:
                                this.InvokeAsyncEvent(this.CurrentSceneTransitionChanged, eventResponseData.EventData);
                                break;
                            case EventType.CurrentSceneTransitionDurationChanged:
                                this.InvokeAsyncEvent(this.CurrentSceneTransitionDurationChanged, eventResponseData.EventData);
                                break;
                            case EventType.SceneTransitionStarted:
                                this.InvokeAsyncEvent(this.SceneTransitionStarted, eventResponseData.EventData);
                                break;
                            case EventType.SceneTransitionEnded:
                                this.InvokeAsyncEvent(this.SceneTransitionEnded, eventResponseData.EventData);
                                break;
                            case EventType.SceneTransitionVideoEnded:
                                this.InvokeAsyncEvent(this.SceneTransitionVideoEnded, eventResponseData.EventData);
                                break;
                            case EventType.SourceFilterListReindexed:
                                this.InvokeAsyncEvent(this.SourceFilterListReindexed, eventResponseData.EventData);
                                break;
                            case EventType.SourceFilterCreated:
                                this.InvokeAsyncEvent(this.SourceFilterCreated, eventResponseData.EventData);
                                break;
                            case EventType.SourceFilterRemoved:
                                this.InvokeAsyncEvent(this.SourceFilterRemoved, eventResponseData.EventData);
                                break;
                            case EventType.SourceFilterNameChanged:
                                this.InvokeAsyncEvent(this.SourceFilterNameChanged, eventResponseData.EventData);
                                break;
                            case EventType.SourceFilterEnableStateChanged:
                                this.InvokeAsyncEvent(this.SourceFilterEnableStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.SceneItemCreated:
                                this.InvokeAsyncEvent(this.SceneItemCreated, eventResponseData.EventData);
                                break;
                            case EventType.SceneItemRemoved:
                                this.InvokeAsyncEvent(this.SceneItemRemoved, eventResponseData.EventData);
                                break;
                            case EventType.SceneItemListReindexed:
                                this.InvokeAsyncEvent(this.SceneItemListReindexed, eventResponseData.EventData);
                                break;
                            case EventType.SceneItemEnableStateChanged:
                                this.InvokeAsyncEvent(this.SceneItemEnableStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.SceneItemLockStateChanged:
                                this.InvokeAsyncEvent(this.SceneItemLockStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.SceneItemSelected:
                                this.InvokeAsyncEvent(this.SceneItemSelected, eventResponseData.EventData);
                                break;
                            case EventType.SceneItemTransformChanged:
                                this.InvokeAsyncEvent(this.SceneItemTransformChanged, eventResponseData.EventData);
                                break;
                            case EventType.StreamStateChanged:
                                this.InvokeAsyncEvent(this.StreamStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.RecordStateChanged:
                                this.InvokeAsyncEvent(this.RecordStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.ReplayBufferStateChanged:
                                this.InvokeAsyncEvent(this.ReplayBufferStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.VirtualcamStateChanged:
                                this.InvokeAsyncEvent(this.VirtualcamStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.ReplayBufferSaved:
                                this.InvokeAsyncEvent(this.ReplayBufferSaved, eventResponseData.EventData);
                                break;
                            case EventType.MediaInputPlaybackStarted:
                                this.InvokeAsyncEvent(this.MediaInputPlaybackStarted, eventResponseData.EventData);
                                break;
                            case EventType.MediaInputPlaybackEnded:
                                this.InvokeAsyncEvent(this.MediaInputPlaybackEnded, eventResponseData.EventData);
                                break;
                            case EventType.MediaInputActionTriggered:
                                this.InvokeAsyncEvent(this.MediaInputActionTriggered, eventResponseData.EventData);
                                break;
                            case EventType.StudioModeStateChanged:
                                this.InvokeAsyncEvent(this.StudioModeStateChanged, eventResponseData.EventData);
                                break;
                            case EventType.ScreenshotSaved:
                                this.InvokeAsyncEvent(this.ScreenshotSaved, eventResponseData.EventData);
                                break;
                            default:
                                throw new OBSClientException($"Unknown Event '{eventResponseData.EventType}'");
                        }
                    }
                    else
                    {
                        throw new OBSClientException("responseMessage.Data is not expected EventResponseData");
                    }

                    break;
                case OpCode.RequestBatchResponse:
                    if (responseMessage.Data is RequestBatchResponseMessage requestBatchResponseData)
                    {
                        if (_requests.TryGetValue(requestBatchResponseData.RequestId, out var tcs))
                        {
                            tcs.SetResult(requestBatchResponseData);
                        }
                    }
                    else
                    {
                        throw new OBSClientException("responseMessage.Data is not expected RequestBatchResponseData");
                    }

                    break;
                case OpCode.Identify:
                case OpCode.Reidentify:
                case OpCode.Request:
                case OpCode.RequestBatch:
                    throw new OBSClientException($"OpCode '{responseMessage.Op}' is not expected to be received.");
                default:
                    throw new OBSClientException($"Unknown OpCode '{responseMessage.Op}'");
            }
        }

        private void InvokeAsyncEvent<T>(AsyncEventHandler<T>? asyncEventHandler, EventArgs? e) where T : EventArgs
        {
            if (asyncEventHandler is not null)
            {
                _ = Task.Factory.StartNew(async state =>
                {
                    try
                    {
                        await asyncEventHandler(this, (T)state!);
                    }
                    catch
                    {
                    }
                }, e ?? EventArgs.Empty, CancellationToken.None);
            }
        }

        private void InvokeAsyncEvent(AsyncEventHandler? asyncEventHandler)
        {
            if (asyncEventHandler is not null)
            {
                _ = Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        await asyncEventHandler(this);
                    }
                    catch
                    {
                    }
                }, CancellationToken.None);
            }
        }

        private async Task SendAsync(dynamic request)
        {
            if (_client.State != WebSocketState.Open)
            {
                throw new OBSClientException("Client is not connected.");
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
            var sendBuffer = new ArraySegment<byte>(bytes);
            await _client.SendAsync(sendBuffer, WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
        }

        private async Task Receiver(CancellationToken cancellationToken)
        {
            var connectionOpen = true;
            while (!cancellationToken.IsCancellationRequested && connectionOpen)
            {
                StringBuilder responseBuilder = new();
                var messageRead = false;
                // Read one message.
                do
                {
                    try
                    {
                        var bytes = new byte[4096];
                        var res = await _client.ReceiveAsync(bytes, cancellationToken);

                        if (res.MessageType == WebSocketMessageType.Close)
                        {
                            this.ConnectionState = ConnectionState.Disconnecting;
                            WebSocketCloseCode closeCode = res.CloseStatus.HasValue ? (WebSocketCloseCode)(int)res.CloseStatus.Value : WebSocketCloseCode.UnknownReason;
                            await _client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                            this.ConnectionState = ConnectionState.Disconnected;
                            this.InvokeAsyncEvent(this.ConnectionClosed, new ConnectionClosedEventArgs(closeCode, res.CloseStatusDescription ?? "Unknown"));
                            connectionOpen = false;
                        }
                        else
                        {
                            messageRead = res.EndOfMessage;
                            responseBuilder.Append(Encoding.UTF8.GetString(bytes[..res.Count]));
                        }
                    }
                    catch (TaskCanceledException)
                    {
                    }
                } while (!messageRead && connectionOpen && !cancellationToken.IsCancellationRequested);

                // Process message
                if (!cancellationToken.IsCancellationRequested && connectionOpen)
                {
                    var response = responseBuilder.ToString();
                    Debug.WriteLine($"OBS WEBSOCKET RECEIVED: {response}");
                    OBSMessage? responseMessage = JsonSerializer.Deserialize<OBSMessage>(response);
                    if (responseMessage == null)
                    {
                        throw new OBSClientException("Could not deserialize OBS Studio message.");
                    }
                    else
                    {
                        _ = Task.Run(() => this.ResponseReceived(responseMessage), CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }

            if (connectionOpen)
            {
                this.ConnectionState = ConnectionState.Disconnected;
                this.InvokeAsyncEvent(this.ConnectionClosed, new ConnectionClosedEventArgs(WebSocketCloseCode.NormalClosure, "Disconnecting due to request."));
            }
        }

        private IMessageData? SendAndWaitAsync(dynamic request)
        {
            if (_connectionState != ConnectionState.Connected)
            {
                throw new OBSClientException("Not connected.");
            }

            string requestId = request.d.requestId;
            TaskCompletionSource<IMessageData> tcs = new();
            CancellationTokenSource cts = new(_requestTimeout);
            cts.Token.Register(() => tcs.TrySetCanceled(), false);
            if (this._requests.TryAdd(requestId, tcs))
            {
                try
                {
                    _ = this.SendAsync(request);
                    var result = tcs.Task.Result;
                    return result;
                }
                catch (OBSResponseException)
                {
                    throw;
                }
                catch
                {

                }
                finally
                {
                    this._requests.TryRemove(requestId, out _);
                }
            }
            else
            {
                throw new OBSClientException("Unexpected");
            }

            throw new TimeoutException("Timeout waiting for OBS Studio response.");
        }

        private void SendRequest(object? requestData = null, [CallerMemberName] string requestType = "")
        {
            _ = SendRequestAndWait(requestType, requestData);
        }

        private T SendRequest<T>(object? requestData = null, [CallerMemberName] string requestType = "") where T : IResponseData
        {
            var requestResponseData = SendRequestAndWait(requestType, requestData);
            return (T)requestResponseData.ResponseData!;
        }

        private RequestResponseMessage SendRequestAndWait(string requestType, object? requestData = null)
        {
            var requestId = Guid.NewGuid().ToString();
            var d = new { requestType, requestId, requestData };
            var op = (int)OpCode.Request;
            var result = this.SendAndWaitAsync(new { d, op });
            if (result is RequestResponseMessage requestResponseData)
            {
                if (requestResponseData.RequestStatus.Result)
                {
                    return requestResponseData;
                }
                else
                {
                    throw new OBSResponseException(requestResponseData.RequestStatus);
                }
            }
            else
            {
                throw new OBSClientException("Unexpected response.");
            }
        }
    }
}