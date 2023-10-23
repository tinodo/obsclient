namespace OBSStudioClient
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
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

    /// <summary>
    /// The OBS Studio WebSockets Client
    /// </summary>
    /// <remarks>
    /// Implements <see href="https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md"/>
    /// </remarks>
    public partial class ObsClient : INotifyPropertyChanged, IDisposable
    {
        private string _hostname = "localhost";

        private int _port = 4455;

        private string _password = string.Empty;

        private int _requestTimeout = 500;
        
        private EventSubscriptions _eventSubscriptions = EventSubscriptions.All;

        private CancellationTokenSource _cancellationTokenSource = new();
        
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        
        private ClientWebSocket _client = new();
        
        private TaskCompletionSource<bool> _authenticationComplete = new();

        private bool _disposed;
        
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IMessage>> _requests = new();

        /// <summary>
        /// The maximum amount of time, in milliseconds, to wait for an OBS Studio response after a request.
        /// </summary>
        /// <remarks>
        /// The minimum value is 150. Please take into account that when sending Batch Requests, specifically with long Sleep requests, this default value of 500 might not be enough.
        /// </remarks>
        public int RequestTimeout
        {
            get
            {
                return this._requestTimeout;
            }
            set
            {
                if (value < 150)
                {
                    this._requestTimeout = 150;
                }
                else
                {
                    this._requestTimeout = value;
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
                return this._connectionState;
            }
            private set
            {
                if (this._connectionState != value)
                {
                    this._connectionState = value;
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

        /// <summary>
        /// Async delegate for events that have event data.
        /// </summary>
        /// <typeparam name="T">The type of event data.</typeparam>
        /// <param name="sender">The <see cref="ObsClient"/> that initiated the event.</param>
        /// <param name="e">The event data.</param>
        /// <returns>A delegate for the event.</returns>
        public delegate Task AsyncEventHandler<in T>(object? sender, T e) where T : EventArgs;

        /// <summary>
        /// Async delegate for events that don't have event data.
        /// </summary>
        /// <param name="sender">The <see cref="ObsClient"/> that initiated the event.</param>
        /// <returns>A delegate for the event.</returns>
        public delegate Task AsyncEventHandler(object? sender);

        /// <summary>
        /// Creates a new instance of the <see cref="ObsClient"/> class.
        /// </summary>
        public ObsClient()
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
        public async Task<bool> ConnectAsync(string password = "", string hostname = "localhost", int port = 4455, EventSubscriptions eventSubscription = EventSubscriptions.All)
        {
            if (this._connectionState != ConnectionState.Disconnected)
            {
                return true;
            }

            this._hostname = hostname;
            this._port = port;
            this._password = password;
            this._eventSubscriptions = eventSubscription;
            this._cancellationTokenSource = new CancellationTokenSource();
            this._client = new();
            this._authenticationComplete = new();

            if (!Uri.TryCreate($"ws://{this._hostname}:{this._port}", UriKind.Absolute, out Uri? uri))
            {
                throw new ArgumentException("Invalid hostname or port number.");
            }

            this.ConnectionState = ConnectionState.Connecting;
            try
            {
                await this._client.ConnectAsync(uri, this._cancellationTokenSource.Token);
            }
            catch (WebSocketException)
            {
                this.ConnectionState = ConnectionState.Disconnected;
                return false;
            }

            _ = Task.Run(() => this.Receiver(this._cancellationTokenSource.Token));
            return this._authenticationComplete.Task.Result;
        }

        /// <summary>
        /// Disconnect from OBS Studio.
        /// </summary>
        public void Disconnect()
        {
            if (this._connectionState != ConnectionState.Disconnected)
            {
                this._cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// Send a Batch Request.
        /// </summary>
        /// <param name="requestBatchExecutionType">The <see cref="RequestBatchExecutionType"/> of the batch.</param>
        /// <param name="requests">The Requests in the batch.</param>
        /// <param name="haltOnFailure">True, to continue processing requests even though one might have failed, or False to stop when any requests fails.</param>
        /// <returns>The responses for the individual requests.</returns>
        public async Task<RequestResponseMessage[]> SendRequestBatch(RequestBatchExecutionType requestBatchExecutionType, RequestMessage[] requests, bool haltOnFailure = false)
        {
            TaskCompletionSource<IMessage> tcs = new();
            CancellationTokenSource cts = new(this._requestTimeout);
            cts.Token.Register(() => tcs.TrySetCanceled(), false);

            var executionType = (int)requestBatchExecutionType;
            var requestId = Guid.NewGuid().ToString();
            var d = new { requestId, haltOnFailure, executionType, requests };
            var op = (int)OpCode.RequestBatch;

            var result = await this.SendAndWaitAsync(new { d, op });
            if (result is RequestBatchResponseMessage requestBatchResponseData)
            {
                return requestBatchResponseData.Results;
            }
            else
            {
                throw new ObsClientException("Unexpected response.");
            }
        }

        /// <summary>
        /// Sends a Reidentify request to OBS Studio, typically to subscribe to a different set of events.
        /// </summary>
        /// <param name="eventSubscription">The events to subscribe to.</param>
        /// <returns>An awaitable task.</returns>
        public async Task ReidentifyAsync(EventSubscriptions eventSubscription)
        {
            this._eventSubscriptions = eventSubscription;
            ReidentifyMessage request = new(eventSubscription);
            ObsMessage message = new(request);
            await this.SendAsync(message);
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string HashEncode(string input)
        {
            using var sha256 = SHA256.Create();
            var textBytes = Encoding.ASCII.GetBytes(input);
            var hash = sha256.ComputeHash(textBytes);
            return Convert.ToBase64String(hash);
        }

        private async Task ProcessHelloMessageAsync(ObsMessage responseMessage)
        {
            if (responseMessage.Data is HelloMessage helloResponseData)
            {
                this.ConnectionState = ConnectionState.Authenticating;
                string? authentication = null;
                if (helloResponseData.Authentication != null)
                {
                    var base64secret = HashEncode(this._password + helloResponseData.Authentication.Salt);
                    authentication = HashEncode(base64secret + helloResponseData.Authentication.Challenge);
                }

                IdentifyMessage identify = new(helloResponseData.RpcVersion, authentication, this._eventSubscriptions);
                ObsMessage message = new(identify);
                await this.SendAsync(message);
            }
            else
            {
                throw new ObsClientException("responseMessage.Data is not expected HelloResponseData");
            }
        }

        private void ProcessIdentifiedMessage(ObsMessage responseMessage)
        {
            // We have succesfully authenticated - OR - we did a Reidentify.
            if (responseMessage.Data is not IdentifiedMessage)
            {
                throw new ObsClientException("responseMessage.Data is not expected IdentifyResponseData");
            }

            if (this._authenticationComplete.Task.Status != TaskStatus.RanToCompletion) this._authenticationComplete.SetResult(true);
            this.ConnectionState = ConnectionState.Connected;
        }

        private void ProcessRequestResponseMessage(ObsMessage responseMessage)
        {
            if (responseMessage.Data is RequestResponseMessage requestResponseData)
            {
                if (this._requests.TryGetValue(requestResponseData.RequestId, out var tcs))
                {
                    tcs.SetResult(requestResponseData);
                }
            }
            else
            {
                throw new ObsClientException("responseMessage.Data is not expected RequestResponseData");
            }
        }

        private void ProcessEventMessage(ObsMessage responseMessage)
        {
            if (responseMessage.Data is EventMessage eventResponseData)
            {
                switch (eventResponseData.EventType)
                {
                    case EventType.ExitStarted:
                        this.ConnectionState = ConnectionState.Disconnecting;
                        this.InvokeAsyncEvent(ExitStarted);
                        break;
                    case EventType.VendorEvent:
                        this.InvokeAsyncEvent(VendorEvent, eventResponseData.EventData);
                        break;
                    case EventType.CustomEvent:
                        this.InvokeAsyncEvent(CustomEvent, eventResponseData.EventData);
                        break;
                    case EventType.CurrentSceneCollectionChanging:
                        this.InvokeAsyncEvent(CurrentSceneCollectionChanging, eventResponseData.EventData);
                        break;
                    case EventType.CurrentSceneCollectionChanged:
                        this.InvokeAsyncEvent(CurrentSceneCollectionChanged, eventResponseData.EventData);
                        break;
                    case EventType.SceneCollectionListChanged:
                        this.InvokeAsyncEvent(SceneCollectionListChanged, eventResponseData.EventData);
                        break;
                    case EventType.CurrentProfileChanging:
                        this.InvokeAsyncEvent(CurrentProfileChanging, eventResponseData.EventData);
                        break;
                    case EventType.CurrentProfileChanged:
                        this.InvokeAsyncEvent(CurrentProfileChanged, eventResponseData.EventData);
                        break;
                    case EventType.ProfileListChanged:
                        this.InvokeAsyncEvent(ProfileListChanged, eventResponseData.EventData);
                        break;
                    case EventType.SceneCreated:
                        this.InvokeAsyncEvent(SceneCreated, eventResponseData.EventData);
                        break;
                    case EventType.SceneRemoved:
                        this.InvokeAsyncEvent(SceneRemoved, eventResponseData.EventData);
                        break;
                    case EventType.SceneNameChanged:
                        this.InvokeAsyncEvent(SceneNameChanged, eventResponseData.EventData);
                        break;
                    case EventType.CurrentProgramSceneChanged:
                        this.InvokeAsyncEvent(CurrentProgramSceneChanged, eventResponseData.EventData);
                        break;
                    case EventType.CurrentPreviewSceneChanged:
                        this.InvokeAsyncEvent(CurrentPreviewSceneChanged, eventResponseData.EventData);
                        break;
                    case EventType.SceneListChanged:
                        this.InvokeAsyncEvent(SceneListChanged, eventResponseData.EventData);
                        break;
                    case EventType.InputCreated:
                        this.InvokeAsyncEvent(InputCreated, eventResponseData.EventData);
                        break;
                    case EventType.InputRemoved:
                        this.InvokeAsyncEvent(InputRemoved, eventResponseData.EventData);
                        break;
                    case EventType.InputNameChanged:
                        this.InvokeAsyncEvent(InputNameChanged, eventResponseData.EventData);
                        break;
                    case EventType.InputActiveStateChanged:
                        this.InvokeAsyncEvent(InputActiveStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.InputShowStateChanged:
                        this.InvokeAsyncEvent(InputShowStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.InputMuteStateChanged:
                        this.InvokeAsyncEvent(InputMuteStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.InputVolumeChanged:
                        this.InvokeAsyncEvent(InputVolumeChanged, eventResponseData.EventData);
                        break;
                    case EventType.InputAudioBalanceChanged:
                        this.InvokeAsyncEvent(InputAudioBalanceChanged, eventResponseData.EventData);
                        break;
                    case EventType.InputAudioSyncOffsetChanged:
                        this.InvokeAsyncEvent(InputAudioSyncOffsetChanged, eventResponseData.EventData);
                        break;
                    case EventType.InputAudioTracksChanged:
                        this.InvokeAsyncEvent(InputAudioTracksChanged, eventResponseData.EventData);
                        break;
                    case EventType.InputAudioMonitorTypeChanged:
                        this.InvokeAsyncEvent(InputAudioMonitorTypeChanged, eventResponseData.EventData);
                        break;
                    case EventType.InputVolumeMeters:
                        this.InvokeAsyncEvent(InputVolumeMeters, eventResponseData.EventData);
                        break;
                    case EventType.CurrentSceneTransitionChanged:
                        this.InvokeAsyncEvent(CurrentSceneTransitionChanged, eventResponseData.EventData);
                        break;
                    case EventType.CurrentSceneTransitionDurationChanged:
                        this.InvokeAsyncEvent(CurrentSceneTransitionDurationChanged, eventResponseData.EventData);
                        break;
                    case EventType.SceneTransitionStarted:
                        this.InvokeAsyncEvent(SceneTransitionStarted, eventResponseData.EventData);
                        break;
                    case EventType.SceneTransitionEnded:
                        this.InvokeAsyncEvent(SceneTransitionEnded, eventResponseData.EventData);
                        break;
                    case EventType.SceneTransitionVideoEnded:
                        this.InvokeAsyncEvent(SceneTransitionVideoEnded, eventResponseData.EventData);
                        break;
                    case EventType.SourceFilterListReindexed:
                        this.InvokeAsyncEvent(SourceFilterListReindexed, eventResponseData.EventData);
                        break;
                    case EventType.SourceFilterCreated:
                        this.InvokeAsyncEvent(SourceFilterCreated, eventResponseData.EventData);
                        break;
                    case EventType.SourceFilterRemoved:
                        this.InvokeAsyncEvent(SourceFilterRemoved, eventResponseData.EventData);
                        break;
                    case EventType.SourceFilterNameChanged:
                        this.InvokeAsyncEvent(SourceFilterNameChanged, eventResponseData.EventData);
                        break;
                    case EventType.SourceFilterEnableStateChanged:
                        this.InvokeAsyncEvent(SourceFilterEnableStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.SceneItemCreated:
                        this.InvokeAsyncEvent(SceneItemCreated, eventResponseData.EventData);
                        break;
                    case EventType.SceneItemRemoved:
                        this.InvokeAsyncEvent(SceneItemRemoved, eventResponseData.EventData);
                        break;
                    case EventType.SceneItemListReindexed:
                        this.InvokeAsyncEvent(SceneItemListReindexed, eventResponseData.EventData);
                        break;
                    case EventType.SceneItemEnableStateChanged:
                        this.InvokeAsyncEvent(SceneItemEnableStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.SceneItemLockStateChanged:
                        this.InvokeAsyncEvent(SceneItemLockStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.SceneItemSelected:
                        this.InvokeAsyncEvent(SceneItemSelected, eventResponseData.EventData);
                        break;
                    case EventType.SceneItemTransformChanged:
                        this.InvokeAsyncEvent(SceneItemTransformChanged, eventResponseData.EventData);
                        break;
                    case EventType.StreamStateChanged:
                        this.InvokeAsyncEvent(StreamStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.RecordStateChanged:
                        this.InvokeAsyncEvent(RecordStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.ReplayBufferStateChanged:
                        this.InvokeAsyncEvent(ReplayBufferStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.VirtualcamStateChanged:
                        this.InvokeAsyncEvent(VirtualcamStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.ReplayBufferSaved:
                        this.InvokeAsyncEvent(ReplayBufferSaved, eventResponseData.EventData);
                        break;
                    case EventType.MediaInputPlaybackStarted:
                        this.InvokeAsyncEvent(MediaInputPlaybackStarted, eventResponseData.EventData);
                        break;
                    case EventType.MediaInputPlaybackEnded:
                        this.InvokeAsyncEvent(MediaInputPlaybackEnded, eventResponseData.EventData);
                        break;
                    case EventType.MediaInputActionTriggered:
                        this.InvokeAsyncEvent(MediaInputActionTriggered, eventResponseData.EventData);
                        break;
                    case EventType.StudioModeStateChanged:
                        this.InvokeAsyncEvent(StudioModeStateChanged, eventResponseData.EventData);
                        break;
                    case EventType.ScreenshotSaved:
                        this.InvokeAsyncEvent(ScreenshotSaved, eventResponseData.EventData);
                        break;
                    default:
                        throw new ObsClientException($"Unknown Event '{eventResponseData.EventType}'");
                }
            }
            else
            {
                throw new ObsClientException("responseMessage.Data is not expected EventResponseData");
            }
        }

        private void ProcessRequestBatchResponseMessage(ObsMessage responseMessage)
        {
            if (responseMessage.Data is RequestBatchResponseMessage requestBatchResponseData)
            {
                if (this._requests.TryGetValue(requestBatchResponseData.RequestId, out var tcs))
                {
                    tcs.SetResult(requestBatchResponseData);
                }
            }
            else
            {
                throw new ObsClientException("responseMessage.Data is not expected RequestBatchResponseData");
            }
        }

        private async Task ResponseReceived(ObsMessage responseMessage)
        {
            switch (responseMessage.Op)
            {
                case OpCode.Hello:
                    await this.ProcessHelloMessageAsync(responseMessage);
                    break;
                case OpCode.Identified:
                    this.ProcessIdentifiedMessage(responseMessage);
                    break;
                case OpCode.RequestResponse:
                    this.ProcessRequestResponseMessage(responseMessage);
                    break;
                case OpCode.Event:
                    this.ProcessEventMessage(responseMessage);
                    break;
                case OpCode.RequestBatchResponse:
                    this.ProcessRequestBatchResponseMessage(responseMessage);
                    break;
                case OpCode.Identify:
                case OpCode.Reidentify:
                case OpCode.Request:
                case OpCode.RequestBatch:
                    throw new ObsClientException($"OpCode '{responseMessage.Op}' is not expected to be received.");
                default:
                    throw new ObsClientException($"Unknown OpCode '{responseMessage.Op}'");
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
                        // We don't care about consumers not being able to handle the event.
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
                        // We don't care about consumers not being able to handle the event.
                    }
                }, CancellationToken.None);
            }
        }

        private async Task SendAsync(dynamic request)
        {
            if (this._client.State != WebSocketState.Open)
            {
                throw new ObsClientException("Client is not connected.");
            }

#if DEBUG
            Debug.WriteLine($"Sending: {JsonSerializer.Serialize(request)}");
#endif
            
            var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
            var sendBuffer = new ArraySegment<byte>(bytes);
            await this._client.SendAsync(sendBuffer, WebSocketMessageType.Text, true, this._cancellationTokenSource.Token);
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
                        var res = await this._client.ReceiveAsync(bytes, cancellationToken);
                        if (res.MessageType == WebSocketMessageType.Close)
                        {
                            this.ConnectionState = ConnectionState.Disconnecting;
                            WebSocketCloseCode closeCode = res.CloseStatus.HasValue ? (WebSocketCloseCode)(int)res.CloseStatus.Value : WebSocketCloseCode.UnknownReason;
                            await this._client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                            this.ConnectionState = ConnectionState.Disconnected;
                            this.InvokeAsyncEvent(ConnectionClosed, new ConnectionClosedEventArgs(closeCode, res.CloseStatusDescription ?? "Unknown"));
                            connectionOpen = false;
                            if (closeCode == WebSocketCloseCode.AuthenticationFailed && this._authenticationComplete.Task.Status != TaskStatus.RanToCompletion)
                            {
                                this._authenticationComplete.SetResult(false);
                            }
                        }
                        else
                        {
                            messageRead = res.EndOfMessage;
                            responseBuilder.Append(Encoding.UTF8.GetString(bytes[..res.Count]));
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // This is raised when the _cancellationTokenSource is Canceled. This is expected; this is how we close the listener.
                    }
                } while (!messageRead && connectionOpen && !cancellationToken.IsCancellationRequested);

                // Process message
                if (!cancellationToken.IsCancellationRequested && connectionOpen)
                {
                    var response = responseBuilder.ToString();

#if DEBUG
                    Debug.WriteLine($"Received: {response}");
#endif

                    ObsMessage? responseMessage = JsonSerializer.Deserialize<ObsMessage>(response);
                    if (responseMessage == null)
                    {
                        throw new ObsClientException("Could not deserialize OBS Studio message.");
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
                this.InvokeAsyncEvent(ConnectionClosed, new ConnectionClosedEventArgs(WebSocketCloseCode.NormalClosure, "Disconnecting due to request."));
            }
        }

        private async Task<IMessage?> SendAndWaitAsync(dynamic request)
        {
            if (this._connectionState != ConnectionState.Connected)
            {
                throw new ObsClientException("Not connected.");
            }

            string requestId = request.d.requestId;
            TaskCompletionSource<IMessage> tcs = new();
            CancellationTokenSource cts = new(this._requestTimeout);
            cts.Token.Register(() => tcs.TrySetCanceled(), false);
            if (this._requests.TryAdd(requestId, tcs))
            {
                try
                {
                    await SendAsync(request);
                    var result = tcs.Task.Result;
                    return result;
                }
                catch (ObsResponseException)
                {
                    throw;
                }
                catch
                {
                    // Something went wrong. We'll throw a generic error later.
                }
                finally
                {
                    this._requests.TryRemove(requestId, out _);
                }
            }
            else
            {
                throw new ObsClientException($"Unexpected error trying to add an already added tasks ({requestId}) to the task list.");
            }

            throw new TimeoutException($"Timeout waiting for OBS Studio response to request {requestId}.");
        }

        private async Task SendRequestAsync(object? requestData = null, [CallerMemberName] string requestType = "")
        {
            if (requestType.EndsWith("Async", StringComparison.InvariantCultureIgnoreCase))
            {
                requestType = requestType[..^5];
            }

            await this.SendRequestAndWaitAsync(requestType, requestData);
        }

        private async Task<T> SendRequestAsync<T>(object? requestData = null, [CallerMemberName] string requestType = "") where T : IResponse
        {
            if (requestType.EndsWith("Async", StringComparison.InvariantCultureIgnoreCase))
            {
                requestType = requestType[..^5];
            }

            var requestResponseData = await this.SendRequestAndWaitAsync(requestType, requestData);
            return (T)requestResponseData.ResponseData!;
        }

        private async Task<RequestResponseMessage> SendRequestAndWaitAsync(string requestType, object? requestData = null)
        {
            var requestId = Guid.NewGuid().ToString();
            var d = new { requestType, requestId, requestData };
            var op = (int)OpCode.Request;
            var result = await this.SendAndWaitAsync(new { d, op });
            if (result is RequestResponseMessage requestResponseData)
            {
                if (requestResponseData.RequestStatus.Result)
                {
                    return requestResponseData;
                }
                else
                {
                    throw new ObsResponseException(requestResponseData.RequestStatus);
                }
            }
            else
            {
                throw new ObsClientException($"Unexpected response from OBS Studio to request {requestId}.");
            }
        }

        /// <summary>
        /// Disposes of the <see cref="ObsClient"/>.
        /// </summary>
        /// <param name="disposing">A value indicating whether we already called the method.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    this.Disconnect();
                    this._client.Dispose();
                }

                this._requests.Clear();
                _disposed = true;
            }
        }

        /// <summary>
        /// Disposes of an <see cref="ObsClient"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}