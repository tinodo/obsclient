namespace OBSStudioClient
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Messages;
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;

    /// <summary>
    /// The OBS Studio WebSockets Client
    /// </summary>
    /// <remarks>
    /// Implements <see href="https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md"/>
    /// </remarks>
    public partial class ObsClient : INotifyPropertyChanged, IDisposable
    {
        private readonly int _receiveBufferSize = 4096;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<IMessage>> _requests = new();

        private readonly int _reconnectInterval = 3000;

        private readonly Timer _reconnectTimer;

        private readonly Dictionary<string, FieldInfo> _eventsMap = new();

        private Uri _uri = new("ws://localhost:4455");

        private string _password = string.Empty;

        private int _requestTimeout = 500;

        private EventSubscriptions _eventSubscriptions = EventSubscriptions.All;

        /// <summary>
        /// Used to cancel the ReceiverAsync task, e.g. you called the Disconnect() method.
        /// </summary>
        private CancellationTokenSource _receiver = new();

        private ConnectionState _connectionState = ConnectionState.Disconnected;

        private ClientWebSocket _client = new();

        private TaskCompletionSource<bool> _authenticationComplete = new();

        private bool _disposed = false;

        private bool _autoReconnect = false;

        private long _totalBytesReceived = 0; // Would allow > 9000PB

        private long _totalBytesSent = 0; // Would allow > 9000PB

        private int _totalMessagesReceived = 0;

        private int _totalMessagesSent = 0;

        private long _sessionBytesReceived = 0; // Would allow > 9000PB

        private long _sessionBytesSent = 0; // Would allow > 9000PB

        private int _sessionMessagesReceived = 0;

        private int _sessionMessagesSent = 0;

        /// <summary>
        /// Gets or sets the maximum amount of time, in milliseconds, the <see cref="ObsClient"/> to wait for an OBS Studio response after making a request.
        /// </summary>
        /// <remarks>
        /// The minimum value is 150. Please take into account that when sending Batch Requests, specifically with long Sleep requests, this default value of 500 might not be enough.
        /// Should a response not be received in time, an Exception will be thrown.
        /// </remarks>
        public int RequestTimeout
        {
            get
            {
                return this._requestTimeout;
            }
            set
            {
                value = Math.Max(value, 150);
                if (this._requestTimeout != value)
                {
                    this._requestTimeout = value;
                    this.OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the current state of the connection to OBS Studio.
        /// </summary>
        /// <remarks>
        /// You should only call ConnectAsync when this state is <see cref="ConnectionState.Disconnected"/>.
        /// </remarks>
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
        /// Gets or sets the value that indicates whether the <see cref="ObsClient"/> should automatically try to reconnect to OBS Studio.
        /// </summary>
        /// <remarks>
        /// When the value is True, the client will automatically try to reconnect to OBS Studio when:
        /// - The connection was closed by OBS Studio
        /// - You were kicked
        /// - OBS Studio closed
        /// - You sent an invalid message or your password was incorrect.
        /// When you call <see cref="Disconnect"/>, this setting is automatically set to False."/>
        /// Setting it back to True, after calling <see cref="Disconnect"/>, will automatically try to reconnect.
        /// </remarks>
        public bool AutoReconnect
        {
            get
            {
                return this._autoReconnect;
            }
            set
            {
                if (this._autoReconnect != value)
                {
                    if (value)
                    {
                        this._reconnectTimer.Change(this._reconnectInterval, this._reconnectInterval);
                    }
                    else
                    {
                        this._reconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    }

                    this._autoReconnect = value;
                    this.OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the number of bytes sent to OBS Studio.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public long TotalBytesSent
        {
            get
            {
                return this._totalBytesSent;
            }
        }

        /// <summary>
        /// Gets the total number of bytes received from OBS Studio throughout the lifetime of the <see cref="ObsClient"/>.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public long TotalBytesReceived
        {
            get
            {
                return this._totalBytesReceived;
            }
        }

        /// <summary>
        /// Gets the total number of messages sent to OBS Studio throughout the lifetime of the <see cref="ObsClient"/>.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public int TotalMessagesSent
        {
            get
            {
                return this._totalMessagesSent;
            }
        }

        /// <summary>
        /// Gets the total number of messages received from OBS Studio throughout the lifetime of the <see cref="ObsClient"/>.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public int TotalMessagesReceived
        {
            get
            {
                return this._totalMessagesReceived;
            }
        }

        /// <summary>
        /// Gets the number of bytes sent to OBS Studio.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public long SessionBytesSent
        {
            get
            {
                return this._sessionBytesSent;
            }
        }

        /// <summary>
        /// Gets the number of bytes received from OBS Studio in the current session.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public long SessionBytesReceived
        {
            get
            {
                return this._sessionBytesReceived;
            }
        }

        /// <summary>
        /// Gets the number of messages sent to OBS Studio in the current session.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public int SessionMessagesSent
        {
            get
            {
                return this._sessionMessagesSent;
            }
        }

        /// <summary>
        /// Gets the number of messages received from OBS Studio in the current session.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public int SessionMessagesReceived
        {
            get
            {
                return this._sessionMessagesReceived;
            }
        }

        /// <summary>
        /// Asynchronous event, triggered when the connection with OBS Studio is closed.
        /// </summary>
        public event EventHandler<ConnectionClosedEventArgs>? ConnectionClosed;

        /// <summary>
        /// Synchronous event, triggered when an important property changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Creates a new instance of the <see cref="ObsClient"/> class.
        /// </summary>
        public ObsClient()
        {
            this._reconnectTimer = new(this.ReconnectTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // The _eventsMap is merely a helper, such that we don't have to invoke these reflection calls for every event.
            foreach (var field in this.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Where(f => f.FieldType != typeof(PropertyChangedEventHandler) && f.FieldType.BaseType == typeof(MulticastDelegate)))
            {
                this._eventsMap.Add(field.Name, field);
            }
        }

        /// <summary>
        /// Creates a Qt Base64 encoded string with Size/Position data for a windowed projector.
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

        private static string HashEncode(string input)
        {
            using var sha256 = SHA256.Create();
            var textBytes = Encoding.ASCII.GetBytes(input);
            var hash = sha256.ComputeHash(textBytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Opens the connection to OBS Studio and tries to authenticate the session.
        /// </summary>
        /// <param name="autoReconnect">Value to indicate whether the client should automatically try to reconnect to OBS Studio.</param>
        /// <param name="password">The OBS Studio WebSockets password or empty to connect without authentication. Defaults to empty.</param>
        /// <param name="hostname">The hostname of the computer running OBS Studio to connect to. Defaults to "localhost".</param>
        /// <param name="port">The Port on which the OBS Studio WebSocket interface is listenting. Default to 4455.</param>
        /// <param name="eventSubscription">The events to subscribe to. Defaults to All events.</param>
        /// <returns>True, when the connection was succesfully established, and False otherwise.</returns>
        /// <remarks>
        /// When True is returned, this does not mean that authentication has succeeded. Authentication will be handled asynchronously.
        /// You can use the <see cref="PropertyChanged"/> event to see whether the <see cref="ConnectionState"/> is Connected, which indicates succesfull authenticaiton.
        /// When the client is already connected, disconnect first.
        /// </remarks>
        public async Task<bool> ConnectAsync(bool autoReconnect = false, string password = "", string hostname = "localhost", int port = 4455, EventSubscriptions eventSubscription = EventSubscriptions.All)
        {
            if (this._connectionState != ConnectionState.Disconnected)
            {
                return true;
            }

            if (port is < 1 or > 65534)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "Port number must be between 1 and 65534.");
            }

            if (!Uri.TryCreate($"ws://{hostname}:{port}", UriKind.Absolute, out Uri? uri))
            {
                throw new ArgumentException("Invalid hostname.", nameof(hostname));
            }

            this.AutoReconnect = autoReconnect;
            this._uri = uri;
            this._password = password;
            this._eventSubscriptions = eventSubscription;
            this._receiver = new CancellationTokenSource();
            this._authenticationComplete = new();

            return await this.StartAsync();
        }

        /// <summary>
        /// Closes the connection to OBS Studio.
        /// </summary>
        public void Disconnect()
        {
            this.AutoReconnect = false;
            this._receiver.Cancel();
        }

        /// <summary>
        /// Sends a Batch Request.
        /// </summary>
        /// <param name="requestBatchMessage">The <see cref="RequestBatchMessage"/> to send.</param>
        /// <param name="timeOutInMilliseconds">The timeout for the request. (Defauls to <see cref="RequestTimeout"/>.)</param>
        /// <returns>The responses for the individual requests.</returns>
        /// <remarks>Since batch requests typically take more time than individual request, you have the opportunity here to override the default timeout for this specific call.</remarks>
        public async Task<RequestResponseMessage[]> SendRequestBatchAsync(RequestBatchMessage requestBatchMessage, int? timeOutInMilliseconds = null)
        {
            timeOutInMilliseconds ??= Math.Max(this._requestTimeout, 150);

            var executionType = (int)requestBatchMessage.RequestBatchExecutionType;
            var requestId = requestBatchMessage.RequestId;
            var requests = requestBatchMessage.Requests.ToArray();
            var d = new { requestId, requestBatchMessage.HaltOnFailure, executionType, requests };
            var op = (int)OpCode.RequestBatch;

            var result = await this.SendAndWaitAsync(new { d, op }, timeOutInMilliseconds);
            if (result is RequestBatchResponseMessage requestBatchResponseData)
            {
                return requestBatchResponseData.Results;
            }

            throw new ObsClientException($"Unexpected response type {result?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
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
        /// Disposes of an <see cref="ObsClient"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the <see cref="ObsClient"/>.
        /// </summary>
        /// <param name="disposing">A value indicating whether we already called the method.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                this._reconnectTimer.Dispose();

                if (disposing)
                {
                    this.Disconnect();
                    this._client.Dispose();
                }

                this._requests.Clear();
                this._eventsMap.Clear();
                _disposed = true;
            }
        }

        private async Task<bool> StartAsync()
        {
            this._client = new();
            this.ConnectionState = ConnectionState.Connecting;
            TaskCompletionSource<IMessage> tcs = new();
            using CancellationTokenSource cts = new(500);
            using var ctr = cts.Token.Register(() => tcs.TrySetCanceled(), false);
            try
            {
                await this._client.ConnectAsync(this._uri, cts.Token);
            }
            catch (WebSocketException)
            {
                // Could not connect.
            }
            catch (TaskCanceledException)
            {
                // Connection timed out.
            }
            finally
            {
                _ = ctr.Unregister();
            }

            if (this._client?.State == WebSocketState.Open)
            {
                _ = Task.Run(() => this.ReceiverAsync(this._receiver.Token));
                return await this._authenticationComplete.Task;
            }
            else
            {
                this.ConnectionState = ConnectionState.Disconnected;
                this._client?.Dispose();
                return false;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
            }
        }

        private void ProcessIdentifiedMessage(ObsMessage responseMessage)
        {
            // We have succesfully authenticated - OR - we did a Reidentify.
            if (responseMessage.Data is not IdentifiedMessage)
            {
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
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
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
            }
        }

        private void ProcessEventMessage(ObsMessage responseMessage)
        {
            if (responseMessage.Data is EventMessage eventResponseData)
            {
                if (this._eventsMap.TryGetValue(eventResponseData.EventType.ToString(), out var field) && field.GetValue(this) is MulticastDelegate eventDelegate)
                {
                    object[] args = eventResponseData.EventData == null ? new object[] { this } : new object[] { this, eventResponseData.EventData };
                    _ = Task.WhenAll(eventDelegate.GetInvocationList().Select(handler => Task.Run(() => handler.Method.Invoke(handler.Target, args))));
                }
            }
            else
            {
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
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
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
            }
        }

        private async Task ProcessReceivedMessageAsync(StringBuilder responseBuilder)
        {
            if (responseBuilder.Length == 0)
            {
                return;
            }

            this._totalMessagesReceived++;
            this._sessionMessagesReceived++;
            var response = responseBuilder.ToString();
            Debug.WriteLine($"Received: {response}");
            if (JsonSerializer.Deserialize<ObsMessage>(response) is not ObsMessage message)
            {
                throw new ObsClientException($"Could not read message from OBS Studio.");
            }

            switch (message.Op)
            {
                case OpCode.Hello:
                    await this.ProcessHelloMessageAsync(message);
                    break;
                case OpCode.Identified:
                    this.ProcessIdentifiedMessage(message);
                    break;
                case OpCode.RequestResponse:
                    this.ProcessRequestResponseMessage(message);
                    break;
                case OpCode.Event:
                    this.ProcessEventMessage(message);
                    break;
                case OpCode.RequestBatchResponse:
                    this.ProcessRequestBatchResponseMessage(message);
                    break;
                case OpCode.Identify:
                case OpCode.Reidentify:
                case OpCode.Request:
                case OpCode.RequestBatch:
                    break;
            }
        }

        private async Task SendAsync(dynamic request)
        {
            if (this._client.State != WebSocketState.Open)
            {
                throw new ObsClientException("Not connected.");
            }

            Debug.WriteLine($"Sending: {JsonSerializer.Serialize(request)}");
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(request);
            var sendBuffer = new ArraySegment<byte>(bytes);
            await this._client.SendAsync(sendBuffer, WebSocketMessageType.Text, true, this._receiver.Token);
            this._totalBytesSent += bytes.LongLength;
            this._sessionBytesSent += bytes.LongLength;
            this._totalMessagesSent++;
            this._sessionMessagesSent++;
        }

        private async Task ReceiverAsync(CancellationToken ct)
        {
            while (true)
            {
                StringBuilder responseBuilder = new();
                WebSocketReceiveResult received;
                var receiveBuffer = new byte[_receiveBufferSize];

                do
                {
                    try
                    {
                        received = await this._client.ReceiveAsync(receiveBuffer, ct);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (WebSocketException)
                    {
                        break;
                    }

                    this._totalBytesReceived += received.Count;
                    this._sessionBytesReceived += received.Count;
                    responseBuilder.Append(Encoding.UTF8.GetString(receiveBuffer[..received.Count]));
                } while (!received.EndOfMessage);

                if (ct.IsCancellationRequested || this._client.State == WebSocketState.CloseReceived || this._client.State == WebSocketState.Aborted) break;

                _ = Task.Run(() => this.ProcessReceivedMessageAsync(responseBuilder), CancellationToken.None).ConfigureAwait(false);
            }

            await this.CloseConnectionAsync(ct.IsCancellationRequested);
        }

        private async Task CloseConnectionAsync(bool closeRequested)
        {
            WebSocketCloseCode closeCode;
            string closeDescription;
            if (closeRequested)
            {
                // Closed the connection because you called Disconnect().
                closeCode = WebSocketCloseCode.NormalClosure;
                closeDescription = "Disconnecting due to request.";
            }
            else
            {
                // Closed the connection because OBS Studio closed it or the network connection was interrupted.
                closeCode = this._client.CloseStatus.HasValue ? (WebSocketCloseCode)(int)this._client.CloseStatus.Value : WebSocketCloseCode.UnknownReason;
                closeDescription = this._client.CloseStatusDescription ?? "Connection lost";
            }

            if (this._client.State == WebSocketState.Open || this._client.State == WebSocketState.CloseReceived)
            {
                this.ConnectionState = ConnectionState.Disconnecting;
                await this._client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }

            this.ConnectionState = ConnectionState.Disconnected;
            _ = Task.Run(() => this.ConnectionClosed?.Invoke(this, new ConnectionClosedEventArgs(closeCode, closeDescription)));
            this._sessionBytesReceived = 0;
            this._sessionBytesSent = 0;
            this._sessionMessagesReceived = 0;
            this._sessionMessagesSent = 0;
            if (closeCode == WebSocketCloseCode.AuthenticationFailed && this._authenticationComplete.Task.Status != TaskStatus.RanToCompletion)
            {
                this._authenticationComplete.SetResult(false);
            }
        }

        private async Task<IMessage> SendAndWaitAsync(dynamic request, int? timeout = null)
        {
            if (this._connectionState != ConnectionState.Connected)
            {
                throw new ObsClientException("Not connected.");
            }

            string requestId = request.d.requestId;
            TaskCompletionSource<IMessage> tcs = new();
            using CancellationTokenSource cts = new(timeout ?? this._requestTimeout);
            using var ctr = cts.Token.Register(() => tcs.TrySetCanceled(), false);
            if (this._requests.TryAdd(requestId, tcs))
            {
                try
                {
                    await SendAsync(request);
                    return await tcs.Task;
                }
                catch (TaskCanceledException)
                {
                    throw new TimeoutException($"Timeout waiting for OBS Studio response to request {requestId}.");
                }
                finally
                {
                    _ = ctr.Unregister();
                    _ = this._requests.TryRemove(requestId, out _);
                }
            }
            else
            {
                throw new ObsClientException($"Could not add request {requestId} to the request queue.");
            }
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

            throw new ObsClientException($"Unexpected response type {result?.GetType().Name} for request {requestId} in {MethodBase.GetCurrentMethod()?.Name}");
        }

        private void ReconnectTimerCallback(object? state)
        {
            if (this._autoReconnect && this._connectionState == ConnectionState.Disconnected)
            {
                this.ConnectionState = ConnectionState.Connecting;
                _ = Task.Run(async () => await this.StartAsync());
            }
        }
    }
}