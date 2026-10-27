namespace OBSStudioClient
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Interfaces;
    using OBSStudioClient.Messages;
    using System;
    using System.Buffers;
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

        private readonly int _reconnectInterval = 3000;

        private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(2);

        private static readonly TimeSpan _authenticationTimeout = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan _closeTimeout = TimeSpan.FromSeconds(1);

        private readonly Timer _reconnectTimer;

        private readonly ConcurrentDictionary<string, FieldInfo> _eventsMap = new();

        private Uri _uri = new("ws://localhost:4455");

        private string _password = string.Empty;

        private int _requestTimeout = 500;

        private EventSubscriptions _eventSubscriptions = EventSubscriptions.All;

        private ConnectionState _connectionState = ConnectionState.Disconnected;

        // The connection that is currently active (or being established). Every connection attempt gets its own
        // session, so a receive loop that is still winding down can never touch the state of a newer connection.
        private volatile ConnectionSession? _session;

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

        private int _maxRequestRetries = 10;

        private int _requestRetryInterval = 500;

        private readonly ILogger<ObsClient> _logger;

        private readonly object _syncRoot = new();

        // The volume meters are the only events that are throttled: obs-websocket sends them many times a second, and each one is a
        // sample of the levels of every input, so the latest replaces the ones that were skipped. The other events that obs-websocket
        // counts as high-volume (an active or show state, a transform) each describe a change of one input or one scene item, and
        // none can be left out without the application missing that change: a scene switch changes the active state of many
        // inputs at the same moment. (Until 4.0.0 all four were throttled by type, which dropped those changes.)
        private const long VolumeMetersIntervalMilliseconds = 100;

        private long _lastVolumeMetersTick = long.MinValue / 2;

        /// <summary>
        /// Gets or sets the maximum amount of time, in milliseconds, the <see cref="ObsClient"/> to wait for an OBS Studio response after making a request.
        /// </summary>
        /// <remarks>
        /// The minimum value is 150. Please take into account that when sending Batch Requests, specifically with long Sleep requests, this default value of 500 might not be enough.
        /// Should a response not be received in time, a <see cref="TimeoutException"/> is thrown.
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
                lock (_syncRoot)
                {
                    return this._connectionState;
                }
            }
            private set
            {
                bool changed;
                lock (_syncRoot)
                {
                    changed = this._connectionState != value;
                    if (changed)
                    {
                        this._connectionState = value;
                    }
                }

                // Raised outside the lock: a handler that blocks (for example by marshalling to a UI thread that is
                // itself waiting to read ConnectionState) must not be able to deadlock the client.
                if (changed)
                {
                    this.RaisePropertyChangedSafely();
                }
            }
        }

        /// <summary>
        /// Gets or sets the value that indicates whether the <see cref="ObsClient"/> should automatically try to reconnect to OBS Studio.
        /// </summary>
        /// <remarks>
        /// When the value is True, the client will automatically try to reconnect to OBS Studio, every 3 seconds, when:
        /// - The connection was closed by OBS Studio
        /// - OBS Studio closed
        /// - You sent an invalid message or your password was incorrect.
        ///
        /// When you call <see cref="Disconnect"/>, this setting is automatically set to False.
        /// Setting it back to True, after calling <see cref="Disconnect"/>, will automatically try to reconnect.
        ///
        /// It is also set to False when OBS Studio ends the session on purpose, with the Kick button of its session list
        /// (<see cref="WebSocketCloseCode.SessionInvalidated"/>): obs-websocket requires that a client does not reconnect automatically then.
        /// </remarks>
        public bool AutoReconnect
        {
            get
            {
                lock (_syncRoot)
                {
                    return this._autoReconnect;
                }
            }
            set
            {
                lock (_syncRoot)
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
                }

                // Raised outside the lock and guarded, like ConnectionState: Disconnect() and ConnectAsync() set this
                // property, so a handler must not be able to deadlock the client or break those calls.
                this.RaisePropertyChangedSafely();
            }
        }

        /// <summary>
        /// Gets the number of bytes sent to OBS Studio.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public long TotalBytesSent => this._totalBytesSent;

        /// <summary>
        /// Gets the total number of bytes received from OBS Studio throughout the lifetime of the <see cref="ObsClient"/>.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public long TotalBytesReceived => this._totalBytesReceived;

        /// <summary>
        /// Gets the total number of messages sent to OBS Studio throughout the lifetime of the <see cref="ObsClient"/>.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public int TotalMessagesSent => this._totalMessagesSent;

        /// <summary>
        /// Gets the total number of messages received from OBS Studio throughout the lifetime of the <see cref="ObsClient"/>.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public int TotalMessagesReceived => this._totalMessagesReceived;            

        /// <summary>
        /// Gets the number of bytes sent to OBS Studio.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public long SessionBytesSent => this._sessionBytesSent; 

        /// <summary>
        /// Gets the number of bytes received from OBS Studio in the current session.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public long SessionBytesReceived => this._sessionBytesReceived;

        /// <summary>
        /// Gets the number of messages sent to OBS Studio in the current session.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public int SessionMessagesSent => this._sessionMessagesSent;

        /// <summary>
        /// Gets the number of messages received from OBS Studio in the current session.
        /// </summary>
        /// <remarks>
        /// You will not be notified through the PropertyChanged event when this value changes.
        /// </remarks>
        public int SessionMessagesReceived => this._sessionMessagesReceived;

        /// <summary>
        /// Gets or sets the maximum number of times the <see cref="ObsClient"/> should retry a request when OBS Studio is not ready to perform the request.
        /// </summary>
        /// <remarks>
        /// This typically occurs when OBS Studio is not ready to perform the request, e.g. when it is still starting up. It will authenticate the client, but not yet accept requests.
        /// </remarks>
        public int MaxRequestRetries
        {
            get
            {
                return this._maxRequestRetries;
            }
            set
            {
                if (this._maxRequestRetries != value && value >= 0)
                {
                    this._maxRequestRetries = value;
                    this.OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the interval, in milliseconds, the <see cref="ObsClient"/> should wait before retrying a request when OBS Studio is not ready to perform the request.
        /// </summary>
        public int RequestRetryInterval
        {
            get
            {
                return this._requestRetryInterval;
            }
            set
            {
                if (this._requestRetryInterval != value && value >= 0)
                {
                    this._requestRetryInterval = value;
                    this.OnPropertyChanged();
                }
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
        /// Occurs when the number of bytes sent changes.
        /// </summary>
        public event EventHandler<long>? TotalBytesSentChanged;
        
        /// <summary>
        /// Occurs when the number of bytes received changes.
        /// </summary>
        /// <remarks>This event is triggered whenever there is an update to the total number of bytes
        /// received. Subscribers can use this event to monitor data transfer progress or handle updates in real
        /// time.</remarks>
        public event EventHandler<long>? TotalBytesReceivedChanged;

        /// <summary>
        /// Occurs when the number of messages sent changes.
        /// </summary>
        public event EventHandler<int>? TotalMessagesSentChanged;

        /// <summary>
        /// Occurs when the number of messages received changes.
        /// </summary>
        public event EventHandler<int>? TotalMessagesReceivedChanged;

        /// <summary>
        /// Occurs when the number of bytes sent changes in the current session.
        /// </summary>
        public event EventHandler<long>? SessionBytesSentChanged;

        /// <summary>
        /// Occurs when the number of bytes received changes in the current session.
        /// </summary>
        /// <remarks>This event is triggered whenever there is an update to the total number of bytes
        /// received in the current session. Subscribers can use this event to monitor data transfer progress
        /// or handle updates in real time.</remarks>
        public event EventHandler<long>? SessionBytesReceivedChanged;

        /// <summary>
        /// Occurs when the number of messages sent changes in the current session.
        /// </summary>
        public event EventHandler<int>? SessionMessagesSentChanged;

        /// <summary>
        /// Occurs when the number of messages received changes in the current session.
        /// </summary>
        public event EventHandler<int>? SessionMessagesReceivedChanged;

        /// <summary>
        /// Creates a new instance of the <see cref="ObsClient"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public ObsClient(ILogger<ObsClient>? logger = null)
        {
            this._logger = logger ?? new NullLogger<ObsClient>();
            this._reconnectTimer = new(this.ReconnectTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

            // Use TryAdd for ConcurrentDictionary
            foreach (var field in this.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.FieldType != typeof(PropertyChangedEventHandler) && f.FieldType.BaseType == typeof(MulticastDelegate)))
            {
                this._eventsMap.TryAdd(field.Name, field);
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
        /// Used in <see cref="OpenSourceProjectorWindow(Classes.SourceRef, string, Guid?)"/> and <see cref="OpenVideoMixProjectorWindow(MixType, string)"/>
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
            // obs-websocket hashes the UTF-8 bytes of the password/salt/challenge (QByteArray / QString::toUtf8()).
            var textBytes = Encoding.UTF8.GetBytes(input);
            var hash = SHA256.HashData(textBytes);
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
        /// <returns><see langword="true"/> when OBS Studio accepted the session: the connection is open, the authentication succeeded and <see cref="ConnectionState"/> is <see cref="ConnectionState.Connected"/>. <see langword="false"/> otherwise.</returns>
        /// <remarks>
        /// The task completes when the handshake with OBS Studio is done, so a request can be sent as soon as <see langword="true"/> is returned.
        /// It returns <see langword="false"/>, and does not throw, when OBS Studio cannot be reached (within 2 seconds), when the handshake is not completed within 10 seconds,
        /// when the password is refused, or when <see cref="Disconnect"/> is called in the meantime. With <paramref name="autoReconnect"/> the client keeps trying (see <see cref="AutoReconnect"/>).
        /// When the client is not <see cref="ConnectionState.Disconnected"/>, nothing is done and <see langword="true"/> is returned: call <see cref="Disconnect"/> first to connect with other settings.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not between 1 and 65535.</exception>
        /// <exception cref="ArgumentException"><paramref name="hostname"/> is not a valid host name.</exception>
        public async Task<bool> ConnectAsync(bool autoReconnect = false, string password = "", string hostname = "localhost", int port = 4455, EventSubscriptions eventSubscription = EventSubscriptions.All)
        {
            if (this._connectionState != ConnectionState.Disconnected)
            {
                return true;
            }

            if (port is < 1 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port), "Port number must be between 1 and 65535.");
            }

            if (!Uri.TryCreate($"ws://{hostname}:{port}", UriKind.Absolute, out Uri? uri))
            {
                throw new ArgumentException("Invalid hostname.", nameof(hostname));
            }

            this.AutoReconnect = autoReconnect;
            this._uri = uri;
            this._password = password;
            this._eventSubscriptions = eventSubscription;

            _logger.LogInformation("Connecting to {Hostname}:{Port}", hostname, port);

            return await this.StartAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Closes the connection to OBS Studio.
        /// </summary>
        public void Disconnect()
        {
            _logger.LogInformation("Disconnecting from OBS Studio");
            this.AutoReconnect = false;
            this._session?.Cancellation.Cancel();
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
            var d = new { requestId, haltOnFailure = requestBatchMessage.HaltOnFailure, executionType, requests };
            var op = (int)OpCode.RequestBatch;

            var result = await this.SendAndWaitAsync(requestId, new { d, op }, timeOutInMilliseconds).ConfigureAwait(false);
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
            await this.SendAsync(this._session ?? throw new ObsClientException("Not connected."), message).ConfigureAwait(false);
        }

        /// <summary>
        /// Disposes of an <see cref="ObsClient"/>.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes of the <see cref="ObsClient"/>.
        /// </summary>
        /// <param name="disposing">A value indicating whether we already called the method.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            if (disposing)
            {
                // Stops AutoReconnect and ends the active session. The session's receive loop owns the socket and
                // disposes it, and fails any request that is still waiting for a response.
                this.Disconnect();
            }

            this._reconnectTimer.Dispose();
            this._eventsMap.Clear();
        }

        private async Task<bool> StartAsync()
        {
            var session = new ConnectionSession();
            this._session = session;
            this.ConnectionState = ConnectionState.Connecting;

            try
            {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation.Token);
                connectCts.CancelAfter(_connectTimeout);
                await session.Socket.ConnectAsync(this._uri, connectCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Could not connect, the attempt timed out, or Disconnect() was called while connecting.
                _logger.LogDebug(ex, "Could not connect to OBS Studio at {Uri}", this._uri);
            }

            if (session.Socket.State == WebSocketState.Open)
            {    
                _ = Task.Run(() => this.ReceiverAsync(session));
                return await this.WaitForAuthenticationAsync(session).ConfigureAwait(false);
            }
            else
            {
                AbandonPendingWork(session);
                this.SetConnectionState(session, ConnectionState.Disconnected);
                session.Dispose();
                return false;
            }
        }

        private async Task<bool> WaitForAuthenticationAsync(ConnectionSession session)
        {
            try
            {
                return await session.Authenticated.Task.WaitAsync(_authenticationTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Something accepted the connection but never completed the obs-websocket handshake.
                _logger.LogWarning("OBS Studio did not complete the authentication handshake within {Timeout}; closing the connection.", _authenticationTimeout);
                session.Cancellation.Cancel();
                return await session.Authenticated.Task.ConfigureAwait(false);
            }
        }

        private void SetConnectionState(ConnectionSession session, ConnectionState state)
        {
            // Only the active session may report the connection state: a session that has already been replaced by
            // a newer connection attempt must not overwrite the state of that newer connection.
            if (ReferenceEquals(this._session, session))
            {
                this.ConnectionState = state;
            }
        }

        // Fails everything that is still waiting on the session. Safe to call more than once.
        private static void AbandonPendingWork(ConnectionSession session)
        {
            if (Interlocked.Exchange(ref session.Closed, 1) != 0)
            {
                return;
            }

            foreach (var pending in session.Requests)
            {
                pending.Value.TrySetException(new ObsClientException("The connection to OBS Studio was closed before a response was received."));
            }

            session.Authenticated.TrySetResult(false);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // For the notifications that the client raises from its own flow (the receive loop, Disconnect, ConnectAsync):
        // an exception of a handler is logged, so it cannot break that flow.
        private void RaisePropertyChangedSafely([CallerMemberName] string? propertyName = null)
        {
            try
            {
                this.OnPropertyChanged(propertyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in a PropertyChanged handler for {Property}", propertyName);
            }
        }

        private async Task ProcessHelloMessageAsync(ConnectionSession session, ObsMessage responseMessage)
        {
            if (responseMessage.Data is HelloMessage helloResponseData)
            {
                this.SetConnectionState(session, ConnectionState.Authenticating);
                string? authentication = null;
                if (helloResponseData.Authentication != null)
                {
                    var base64secret = HashEncode(this._password + helloResponseData.Authentication.Salt);
                    authentication = HashEncode(base64secret + helloResponseData.Authentication.Challenge);
                }

                IdentifyMessage identify = new(helloResponseData.RpcVersion, authentication, this._eventSubscriptions);
                ObsMessage message = new(identify);
                await this.SendAsync(session, message).ConfigureAwait(false);
            }
            else
            {
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
            }
        }

        private void ProcessIdentifiedMessage(ConnectionSession session, ObsMessage responseMessage)
        {
            // We have succesfully authenticated - OR - we did a Reidentify.
            if (responseMessage.Data is not IdentifiedMessage)
            {
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
            }

            // Set ConnectionState before completing the authentication task: awaiters of that task (e.g. a
            // caller's `await ConnectAsync(...)`) could otherwise observe the still-stale previous state if
            // their continuation runs synchronously on this thread as part of TrySetResult().
            this.SetConnectionState(session, ConnectionState.Connected);
            session.Authenticated.TrySetResult(true);
        }

        private void ProcessRequestResponseMessage(ConnectionSession session, ObsMessage responseMessage)
        {
            if (responseMessage.Data is RequestResponseMessage requestResponseData)
            {
                this.CompleteRequest(session, requestResponseData.RequestId, requestResponseData);
            }
            else
            {
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
            }
        }

        private void CompleteRequest(ConnectionSession session, string requestId, IMessage response)
        {
            // TryRemove + TrySetResult, never SetResult: the waiter may have timed out (and cancelled its
            // task) an instant before this response arrived, and that must not be able to throw here.
            if (session.Requests.TryRemove(requestId, out var tcs))
            {
                tcs.TrySetResult(response);
            }
            else
            {
                _logger.LogDebug("Received a response for request {RequestId}, which is no longer waiting for one (it timed out or was cancelled).", requestId);
            }
        }

        private void ProcessEventMessage(ObsMessage responseMessage)
        {
            if (responseMessage.Data is EventMessage eventResponseData)
            {
                var eventType = eventResponseData.EventType.ToString();

                // Throttle the volume meters (the tick count does not go back when the clock is set).
                if (eventResponseData.EventType == EventType.InputVolumeMeters)
                {
                    var now = Environment.TickCount64;
                    if (now - this._lastVolumeMetersTick < VolumeMetersIntervalMilliseconds)
                    {
                        return;
                    }

                    this._lastVolumeMetersTick = now;
                }

                if (this._eventsMap.TryGetValue(eventType, out var field) && field.GetValue(this) is MulticastDelegate eventDelegate)
                {
                    foreach (var handler in eventDelegate.GetInvocationList())
                    {
                        try
                        {
                            if (eventResponseData.EventData == null)
                            {
                                if (handler is EventHandler eh)
                                    eh(this, EventArgs.Empty);
                                else
                                    handler.DynamicInvoke(this);
                            }
                            else
                            {
                                if (handler is EventHandler<object> ehObj)
                                    ehObj(this, eventResponseData.EventData);
                                else
                                    handler.DynamicInvoke(this, eventResponseData.EventData);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Exception in event handler for {EventType}", eventResponseData.EventType);
                        }
                    }
                }
            }
            else
            {
                _logger.LogError("Unexpected response type in ProcessEventMessage");
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
            }
        }

        private void ProcessRequestBatchResponseMessage(ConnectionSession session, ObsMessage responseMessage)
        {
            if (responseMessage.Data is RequestBatchResponseMessage requestBatchResponseData)
            {
                this.CompleteRequest(session, requestBatchResponseData.RequestId, requestBatchResponseData);
            }
            else
            {
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
            }
        }

        private async Task ProcessReceivedMessageAsync(ConnectionSession session, StringBuilder responseBuilder)
        {
            if (responseBuilder.Length == 0)
            {
                return;
            }

            var response = responseBuilder.ToString();
            _logger.LogDebug("Received: {Response}", response);

            ObsMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<ObsMessage>(response);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.HandleUnreadableMessage(session, response, ex);
                return;
            }

            if (message is null)
            {
                this.HandleUnreadableMessage(session, response, new ObsClientException("Could not read message from OBS Studio."));
                return;
            }

            switch (message.Op)
            {
                case OpCode.Hello:
                    await this.ProcessHelloMessageAsync(session, message).ConfigureAwait(false);
                    break;
                case OpCode.Identified:
                    this.ProcessIdentifiedMessage(session, message);
                    break;
                case OpCode.RequestResponse:
                    this.ProcessRequestResponseMessage(session, message);
                    break;
                case OpCode.Event:
                    try
                    {
                        this.ProcessEventMessage(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process an event from OBS Studio; the event is ignored.");
                    }

                    break;
                case OpCode.RequestBatchResponse:
                    this.ProcessRequestBatchResponseMessage(session, message);
                    break;
                case OpCode.Identify:
                case OpCode.Reidentify:
                case OpCode.Request:
                case OpCode.RequestBatch:
                    break;
                default:
                    _logger.LogWarning("Received unknown OpCode: {OpCode} in ProcessReceivedMessageAsync. Raw message: {RawMessage}", message.Op, response);
                    break;
            }
        }

        // A message that cannot be read must not take the whole connection down with it: it only ever affects the
        // request it is the response to (which then fails immediately with a descriptive exception instead of
        // timing out), or it is an event that is skipped. Only a broken handshake is fatal.
        private void HandleUnreadableMessage(ConnectionSession session, string response, Exception exception)
        {
            var hasEnvelope = TryReadEnvelope(response, out OpCode op, out string? requestId);

            if (hasEnvelope && (op == OpCode.Hello || op == OpCode.Identified))
            {
                throw new ObsClientException("OBS Studio sent a handshake message that could not be read.", exception);
            }

            var preview = response.Length > 512 ? string.Concat(response.AsSpan(0, 512), "...") : response;
            if (hasEnvelope && (op == OpCode.RequestResponse || op == OpCode.RequestBatchResponse) && requestId is not null)
            {
                _logger.LogError(exception, "The response to request {RequestId} could not be read: {Message}", requestId, preview);
                if (session.Requests.TryRemove(requestId, out var tcs))
                {
                    tcs.TrySetException(new ObsClientException($"The response to request {requestId} could not be read.", exception));
                }

                return;
            }

            _logger.LogError(exception, "A message from OBS Studio could not be read and is ignored: {Message}", preview);
        }

        private static bool TryReadEnvelope(string json, out OpCode op, out string? requestId)
        {
            op = default;
            requestId = null;

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("op", out var opElement) || !opElement.TryGetInt32(out var opValue))
                {
                    return false;
                }

                op = (OpCode)opValue;
                if (root.TryGetProperty("d", out var data) && data.ValueKind == JsonValueKind.Object
                    && data.TryGetProperty("requestId", out var requestIdElement) && requestIdElement.ValueKind == JsonValueKind.String)
                {
                    requestId = requestIdElement.GetString();
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string RedactAuthentication(string json)
        {
            // The answer to the challenge of OBS Studio is not the password, but it is derived from it: it stays out of the log.
            const string marker = "\"authentication\":\"";
            var start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return json;
            }

            start += marker.Length;
            var end = json.IndexOf('"', start);
            return end < 0 ? json : json[..start] + "***" + json[end..];
        }

        private async Task SendAsync(ConnectionSession session, object request)
        {
            var socket = session.Socket;
            if (socket.State != WebSocketState.Open)
            {
                throw new ObsClientException("Not connected.");
            }

            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(request);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Sending: {Request}", RedactAuthentication(Encoding.UTF8.GetString(bytes)));
            }

            var sendBuffer = new ArraySegment<byte>(bytes);
            var lockTaken = false;
            try
            {
                // A WebSocket message must be written in one piece, so concurrent callers take turns.
                await session.SendLock.WaitAsync(session.Cancellation.Token).ConfigureAwait(false);
                lockTaken = true;
                await socket.SendAsync(sendBuffer, WebSocketMessageType.Text, true, session.Cancellation.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or WebSocketException or OperationCanceledException)
            {
                throw new ObsClientException("Not connected.", ex);
            }
            finally
            {
                if (lockTaken)
                {
                    session.SendLock.Release();
                }
            }

            Interlocked.Add(ref this._totalBytesSent, bytes.LongLength);
            Interlocked.Increment(ref this._totalMessagesSent);
            Interlocked.Add(ref this._sessionBytesSent, bytes.LongLength);
            Interlocked.Increment(ref this._sessionMessagesSent);

            this.RaiseSafely(TotalBytesSentChanged, this._totalBytesSent);
            this.RaiseSafely(TotalMessagesSentChanged, this._totalMessagesSent);
            this.RaiseSafely(SessionBytesSentChanged, this._sessionBytesSent);
            this.RaiseSafely(SessionMessagesSentChanged, this._sessionMessagesSent);
        }

        // These statistics events are raised on whichever thread sends or receives; a handler that throws must
        // never be able to disturb the connection.
        private void RaiseSafely<T>(EventHandler<T>? handler, T value)
        {
            if (handler is null)
            {
                return;
            }

            try
            {
                handler(this, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in a statistics event handler");
            }
        }

        private async Task ReceiverAsync(ConnectionSession session)
        {
            var ct = session.Cancellation.Token;
            var bufferPool = ArrayPool<byte>.Shared;
            byte[] receiveBuffer = bufferPool.Rent(_receiveBufferSize);

            // Decoding UTF-8 never yields more chars than bytes; the headroom covers the (at most 3) bytes of a
            // multi-byte character that the stateful decoder carries over from the previous chunk.
            var charBuffer = new char[_receiveBufferSize + 16];

            try
            {
                try
                {
                    while (true)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }

                        StringBuilder responseBuilder = StringBuilderCache.Acquire();
                        try
                        {
                            bool receiveFailed = await ReceiveWebSocketMessageAsync(session, receiveBuffer, charBuffer, responseBuilder, ct).ConfigureAwait(false);

                            if (receiveFailed || ct.IsCancellationRequested || session.Socket.State == WebSocketState.CloseReceived || session.Socket.State == WebSocketState.Aborted)
                                break;

                            await this.ProcessReceivedMessageAsync(session, responseBuilder).ConfigureAwait(false);
                        }
                        finally
                        {
                            StringBuilderCache.Release(responseBuilder);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when Disconnect() cancels the token mid-receive.
                }
                catch (Exception ex)
                {
                    // Only a transport failure or a broken handshake gets here: a single unreadable message is
                    // handled in ProcessReceivedMessageAsync. Either way the receiver must never be left silently dead
                    // while ConnectionState is still reporting Connected. See https://github.com/tinodo/obsclient/issues/17.
                    _logger.LogError(ex, "Unhandled exception in the receiver loop; closing the connection");
                }

                // Guaranteed to run whenever the loop above stops, for any reason, so ConnectionState always
                // reaches Disconnected and ConnectionClosed always fires.
                await this.CloseConnectionAsync(session).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception while closing the connection");
            }
            finally
            {
                // Nothing may be left waiting on a connection that no longer exists, whatever happened above.
                AbandonPendingWork(session);
                bufferPool.Return(receiveBuffer);
                session.Dispose();

                _logger.LogDebug("Receiver ended");
            }
        }

        /// <summary>
        /// Receives a single, possibly multi-frame, WebSocket message into <paramref name="responseBuilder"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the receive failed and the connection should be considered lost; otherwise <see langword="false"/>.</returns>
        private async Task<bool> ReceiveWebSocketMessageAsync(ConnectionSession session, byte[] receiveBuffer, char[] charBuffer, StringBuilder responseBuilder, CancellationToken ct)
        {
            // A UTF-8 encoded character can straddle two receive buffers (or two WebSocket frames), so the bytes
            // have to be decoded statefully rather than one chunk at a time.
            var decoder = Encoding.UTF8.GetDecoder();
            WebSocketReceiveResult received;
            do
            {
                if (ct.IsCancellationRequested)
                {
                    return true;
                }

                try
                {
                    received = await session.Socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, 0, _receiveBufferSize), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException e)
                {
                    _logger.LogDebug(e, "Receive cancelled in ReceiverAsync");
                    return true;
                }
                catch (WebSocketException e)
                {
                    _logger.LogError(e, "Handled Exception in ReceiverAsync");
                    return true;
                }
                catch (ObjectDisposedException e)
                {
                    // The underlying socket was disposed concurrently.
                    _logger.LogError(e, "Handled Exception in ReceiverAsync");
                    return true;
                }

                if (received.MessageType == WebSocketMessageType.Close)
                {
                    // The caller notices the CloseReceived state and ends the receive loop.
                    return false;
                }

                Interlocked.Add(ref this._totalBytesReceived, received.Count);
                Interlocked.Add(ref this._sessionBytesReceived, received.Count);
                var charCount = decoder.GetChars(receiveBuffer, 0, received.Count, charBuffer, 0, received.EndOfMessage);
                responseBuilder.Append(charBuffer, 0, charCount);

                this.RaiseSafely(TotalBytesReceivedChanged, this._totalBytesReceived);
                this.RaiseSafely(SessionBytesReceivedChanged, this._sessionBytesReceived);
            } while (!received.EndOfMessage);

            Interlocked.Increment(ref this._totalMessagesReceived);
            Interlocked.Increment(ref this._sessionMessagesReceived);
            this.RaiseSafely(TotalMessagesReceivedChanged, this._totalMessagesReceived);
            this.RaiseSafely(SessionMessagesReceivedChanged, this._sessionMessagesReceived);

            return false;
        }

        private async Task CloseConnectionAsync(ConnectionSession session)
        {
            var socket = session.Socket;
            WebSocketCloseCode closeCode = WebSocketCloseCode.UnknownReason;
            string closeDescription = "Connection lost";

            try
            {
                if (session.Cancellation.IsCancellationRequested)
                {
                    // Closed the connection because you called Disconnect().
                    closeCode = WebSocketCloseCode.NormalClosure;
                    closeDescription = "Disconnecting due to request.";
                }
                else
                {
                    // Closed the connection because OBS Studio closed it or the network connection was interrupted.
                    closeCode = socket.CloseStatus.HasValue ? (WebSocketCloseCode)(int)socket.CloseStatus.Value : WebSocketCloseCode.UnknownReason;
                    closeDescription = socket.CloseStatusDescription ?? "Connection lost";

                    if (closeCode == WebSocketCloseCode.SessionInvalidated && ReferenceEquals(this._session, session))
                    {
                        // OBS Studio ended the session on purpose (the Kick button of its session list), and obs-websocket
                        // documents that a client must not reconnect automatically after this close code. This is done before
                        // the state becomes Disconnected, which is what the reconnect timer waits for.
                        this.AutoReconnect = false;
                    }
                }

                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    this.SetConnectionState(session, ConnectionState.Disconnecting);

                    // Not the session's own token: when this runs because of Disconnect() that token is already
                    // cancelled, which would make the close handshake fail before it even starts.
                    using var closeTimeout = new CancellationTokenSource(_closeTimeout);
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, closeTimeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Best-effort graceful close only; the connection is already going away and we must still
                // transition to Disconnected below so ConnectionState can never get stuck.
                _logger.LogDebug(ex, "Ignoring exception while closing an already-failed connection.");
            }

            // Everything still waiting on this connection (requests, a pending ConnectAsync) learns right away
            // that it is gone, instead of having to wait for a timeout.
            AbandonPendingWork(session);

            if (ReferenceEquals(this._session, session))
            {
                // The statistics are reset before the disconnect is reported, so a handler that reconnects
                // immediately cannot have the counters of its new session wiped.
                Interlocked.Exchange(ref this._sessionBytesReceived, 0);
                Interlocked.Exchange(ref this._sessionBytesSent, 0);
                Interlocked.Exchange(ref this._sessionMessagesReceived, 0);
                Interlocked.Exchange(ref this._sessionMessagesSent, 0);
                this.ConnectionState = ConnectionState.Disconnected;
            }

            // Intentionally not passing a cancellation token to Task.Run: the session's is frequently already
            // cancelled here (the normal Disconnect() path), and Task.Run with an already-cancelled token would skip
            // invoking the delegate entirely, silently suppressing the ConnectionClosed event on every deliberate disconnect.
            _ = Task.Run(() =>
            {
                try
                {
                    this.ConnectionClosed?.Invoke(this, new ConnectionClosedEventArgs(closeCode, closeDescription));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception in a ConnectionClosed handler");
                }
            });
        }

        private async Task<IMessage> SendAndWaitAsync(string requestId, object request, int? timeout = null)
        {
            var session = this._session;
            if (session is null || this._connectionState != ConnectionState.Connected)
            {
                throw new ObsClientException("Not connected.");
            }

            // RunContinuationsAsynchronously keeps this awaiter's continuation off the receiver loop's thread when
            // CompleteRequest completes the task, which would otherwise delay the receiver from reading the next message.
            TaskCompletionSource<IMessage> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource cts = new(timeout ?? this._requestTimeout);
            using var ctr = cts.Token.Register(() => tcs.TrySetCanceled(), false);
            if (!session.Requests.TryAdd(requestId, tcs))
            {
                throw new ObsClientException($"Could not add request {requestId} to the request queue.");
            }

            try
            {
                // The connection can end between the state check and registering the request. The pending requests
                // were failed by then, so this one would otherwise have to sit out its full timeout.
                if (Volatile.Read(ref session.Closed) != 0)
                {
                    throw new ObsClientException("Not connected.");
                }

                await this.SendAsync(session, request).ConfigureAwait(false);
                return await tcs.Task.ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException($"Timeout waiting for OBS Studio response to request {requestId}.");
            }
            finally
            {
                _ = ctr.Unregister();
                _ = session.Requests.TryRemove(requestId, out _);
            }
        }

        private async Task SendRequestAsync(RequestType requestType, object? requestData = null)
        {
            await this.SendRequestAndWaitAsync(requestType, requestData).ConfigureAwait(false);
        }

        private async Task<T> SendRequestAsync<T>(RequestType requestType, object? requestData = null) where T : IResponse
        {
            var requestResponseData = await this.SendRequestAndWaitAsync(requestType, requestData).ConfigureAwait(false);
            return (T)requestResponseData.ResponseData!;
        }

        private async Task<RequestResponseMessage> SendRequestAndWaitAsync(RequestType requestType, object? requestData)
        {
            var payload = RequestPayload.ToElement(requestData);
            int retryCount = 0;
            while (true)
            {
                var requestId = Guid.NewGuid().ToString();
                object d = payload is null ? new { requestType, requestId } : new { requestType, requestId, requestData = payload };
                var op = (int)OpCode.Request;
                var result = await this.SendAndWaitAsync(requestId, new { d, op }).ConfigureAwait(false);
                if (result is RequestResponseMessage requestResponseData)
                {
                    if (requestResponseData.RequestStatus.Result)
                    {
                        return requestResponseData;
                    }
                    else
                    {
                        if (requestResponseData.RequestStatus.Code == RequestStatusCode.NotReady && retryCount++ < this._maxRequestRetries)
                        {
                            await Task.Delay(this._requestRetryInterval).ConfigureAwait(false);
                        }
                        else
                        {
                            throw new ObsResponseException(requestResponseData.RequestStatus);
                        }
                    }
                }
                else
                {
                    throw new ObsClientException($"Unexpected response type {result?.GetType().Name} for request {requestId} in {MethodBase.GetCurrentMethod()?.Name}");
                }
            }
        }

        private void ReconnectTimerCallback(object? state)
        {
            if (this._autoReconnect && this._connectionState == ConnectionState.Disconnected)
            {
                this.ConnectionState = ConnectionState.Connecting;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await this.StartAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Reconnect failed");
                    }
                });
            }
        }

        /// <summary>
        /// Everything that belongs to one physical connection to OBS Studio. A new instance is created for every
        /// connection attempt, so the receive loop of a connection that is winding down can never dispose, complete
        /// or otherwise disturb the socket, pending requests or handshake of a newer connection.
        /// </summary>
        private sealed class ConnectionSession : IDisposable
        {
            /// <summary>Gets the socket for this connection. It is disposed together with the session.</summary>
            public ClientWebSocket Socket { get; } = new();

            /// <summary>
            /// Gets the source that is cancelled to end this connection (<see cref="ObsClient.Disconnect"/>).
            /// Intentionally never disposed: it owns no timers or linked registrations, and a Disconnect() call can
            /// race with the end of the session.
            /// </summary>
            public CancellationTokenSource Cancellation { get; } = new();

            /// <summary>Gets the task that completes when the handshake succeeded (true) or the connection ended first (false).</summary>
            public TaskCompletionSource<bool> Authenticated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>Gets the requests that are waiting for a response on this connection.</summary>
            public ConcurrentDictionary<string, TaskCompletionSource<IMessage>> Requests { get; } = new();

            /// <summary>Gets the lock that makes concurrent senders take turns writing messages.</summary>
            public SemaphoreSlim SendLock { get; } = new(1, 1);

            /// <summary>1 once the connection has ended and everything that was waiting on it has been failed.</summary>
            public int Closed;

            public void Dispose()
            {
                this.Socket.Dispose();
            }
        }
    }
}