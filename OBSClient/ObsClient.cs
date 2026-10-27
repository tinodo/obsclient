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
    /// <para>
    /// The events of OBS Studio and <see cref="ConnectionClosed"/> are raised by a dispatcher of the client: one at a time, in the
    /// order in which they happened, on a thread of the thread pool that is not the thread that reads from OBS Studio. A handler can
    /// therefore wait for a response to a request, but the events behind it wait while it runs, and they queue up in memory when the
    /// handlers are slower than OBS Studio is. Once <see cref="Dispose()"/> was called nothing new is started: what was waiting is dropped,
    /// and a notification that is under way stops before its next handler. A handler that is running is neither interrupted nor waited
    /// for, so it can still be running when <see cref="Dispose()"/> returns.
    /// </para>
    /// <para>
    /// <see cref="PropertyChanged"/> and the statistics events (<see cref="TotalBytesSentChanged"/> and the others) are raised on the thread
    /// that causes the change, which can be the thread that reads from OBS Studio: keep those handlers short, and do not wait for a
    /// response from OBS Studio in them.
    /// </para>
    /// </remarks>
    public partial class ObsClient : INotifyPropertyChanged, IDisposable
    {
        private readonly int _receiveBufferSize = 4096;

        private readonly int _reconnectInterval = 3000;

        private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(2);

        private static readonly TimeSpan _authenticationTimeout = TimeSpan.FromSeconds(10);

        private static readonly TimeSpan _closeTimeout = TimeSpan.FromSeconds(1);

        // What became of an attempt to connect (ConnectionSession.Attempt): decided once.
        private const int AttemptUndecided = 0;

        private const int AttemptCompleted = 1;

        private const int AttemptCancelled = 2;

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

        private volatile bool _disposed = false;

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

        // Everything that the client reports because of what happens on the connection (the events of OBS Studio, and ConnectionClosed)
        // goes through this queue. The receive loop only adds to it: a task of the thread pool raises the notifications, one at a time and
        // in the order in which they were added. So a handler can wait for the response to a request, which the receive loop has to read,
        // without keeping it from doing so.
        private readonly ConcurrentQueue<Action> _notifications = new();

        // 1 while a task is raising the notifications of the queue.
        private int _delivering;

        // How many notifications are waiting, to warn when the handlers of the application are slower than OBS Studio is.
        private int _waitingNotifications;

        private const int NotificationBacklogWarning = 1000;

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
                this.SetAutoReconnect(value);
            }
        }

        // The setter of AutoReconnect. With a session it only changes the setting while that session is the active one: the check and the
        // change are one step with respect to activating another session, so what is done for a session that was replaced cannot change the
        // setting of the one that replaced it.
        private void SetAutoReconnect(bool value, ConnectionSession? onlyForSession = null)
        {
            lock (_syncRoot)
            {
                if (onlyForSession is not null && !ReferenceEquals(this._session, onlyForSession))
                {
                    return;
                }

                // The timer is disposed by Dispose, after it took this lock to set _disposed.
                if (!this._disposed)
                {
                    this._reconnectTimer.Change(value ? this._reconnectInterval : Timeout.Infinite, value ? this._reconnectInterval : Timeout.Infinite);
                }

                this._autoReconnect = value;
            }

            // Raised outside the lock and guarded, like ConnectionState: Disconnect() and ConnectAsync() set this
            // property, so a handler must not be able to deadlock the client or break those calls.
            this.RaisePropertyChangedSafely(nameof(AutoReconnect));
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
        /// Occurs when the connection with OBS Studio is closed, for whatever reason, a call of <see cref="Disconnect"/> included.
        /// </summary>
        /// <remarks>
        /// Raised by the dispatcher of the client, after the events of OBS Studio that arrived before the connection ended.
        /// A handler that throws is logged, and does not keep the other handlers from being called.
        /// See the remarks of <see cref="ObsClient"/>.
        /// </remarks>
        public event EventHandler<ConnectionClosedEventArgs>? ConnectionClosed;

        /// <summary>
        /// Occurs when an important property changes, <see cref="ConnectionState"/> for instance.
        /// </summary>
        /// <remarks>
        /// Raised on the thread that causes the change, which can be the thread that reads from OBS Studio: a handler must be quick, and
        /// must not wait for a response from OBS Studio.
        /// </remarks>
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
            this._logger = logger ?? NullLogger<ObsClient>.Instance;
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
        /// Used in <see cref="OpenSourceProjectorWindow(Classes.SourceRef, string, Guid?, CancellationToken)"/> and <see cref="OpenVideoMixProjectorWindow(MixType, string, CancellationToken)"/>
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
        /// <param name="cancellationToken">A token that ends the attempt to connect, as <see cref="Disconnect"/> would: the connection that is being opened is closed and <see cref="AutoReconnect"/> is switched off, so the client does not go on trying what the caller no longer wants.</param>
        /// <returns><see langword="true"/> when OBS Studio accepted the session: the connection is open, the authentication succeeded and <see cref="ConnectionState"/> is <see cref="ConnectionState.Connected"/>. <see langword="false"/> otherwise.</returns>
        /// <remarks>
        /// The task completes when the handshake with OBS Studio is done, so a request can be sent as soon as <see langword="true"/> is returned.
        /// It returns <see langword="false"/>, and does not throw, when OBS Studio cannot be reached (within 2 seconds), when the handshake is not completed within 10 seconds,
        /// when the password is refused, or when <see cref="Disconnect"/> is called in the meantime. With <paramref name="autoReconnect"/> the client keeps trying (see <see cref="AutoReconnect"/>).
        /// When the client is not <see cref="ConnectionState.Disconnected"/>, nothing is done and <see langword="true"/> is returned: call <see cref="Disconnect"/> first to connect with other settings.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is not between 1 and 65535.</exception>
        /// <exception cref="ArgumentException"><paramref name="hostname"/> is not a valid host name.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled before the call completed: the attempt was ended, and <see cref="AutoReconnect"/> switched off. A token that is cancelled after the call completed has no effect.</exception>
        public async Task<bool> ConnectAsync(bool autoReconnect = false, string password = "", string hostname = "localhost", int port = 4455, EventSubscriptions eventSubscription = EventSubscriptions.All, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            // The session is activated before its settings are written, never after: a change that a session that is being replaced still
            // makes for itself (see ApplyIfActive) is then refused, or overwritten by the settings that follow.
            var session = this.ActivateNewSession();
            this.AutoReconnect = autoReconnect;
            this._uri = uri;
            this._password = password;
            this._eventSubscriptions = eventSubscription;

            _logger.LogInformation("Connecting to {Hostname}:{Port}", hostname, port);

            return await this.StartAsync(session, cancellationToken).ConfigureAwait(false);
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
        /// <param name="cancellationToken">A token that ends the wait for the responses, which makes the method throw an <see cref="OperationCanceledException"/>. A batch that was already sent is not undone: OBS Studio may still run it.</param>
        /// <returns>The responses for the individual requests.</returns>
        /// <remarks>Since batch requests typically take more time than individual request, you have the opportunity here to override the default timeout for this specific call.</remarks>
        public async Task<RequestResponseMessage[]> SendRequestBatchAsync(RequestBatchMessage requestBatchMessage, int? timeOutInMilliseconds = null, CancellationToken cancellationToken = default)
        {
            timeOutInMilliseconds ??= Math.Max(this._requestTimeout, 150);

            var executionType = (int)requestBatchMessage.RequestBatchExecutionType;
            var requestId = requestBatchMessage.RequestId;
            var requests = requestBatchMessage.Requests.ToArray();
            var d = new { requestId, haltOnFailure = requestBatchMessage.HaltOnFailure, executionType, requests };
            var op = (int)OpCode.RequestBatch;

            var result = await this.SendAndWaitAsync(requestId, new { d, op }, timeOutInMilliseconds, cancellationToken).ConfigureAwait(false);
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
        /// <param name="cancellationToken">A token that cancels the call before the message is sent, which makes the method throw an <see cref="OperationCanceledException"/>.</param>
        /// <returns>An awaitable task.</returns>
        public async Task ReidentifyAsync(EventSubscriptions eventSubscription, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReidentifyMessage request = new(eventSubscription);
            ObsMessage message = new(request);

            // The events are remembered, so that a reconnect subscribes to the same ones, when the message has been written (in the order in
            // which messages are written): a call that was cancelled while it waited for its turn, or that failed, changes nothing. They are
            // remembered for the session that sent the message, and only while it is the active one: when the continuation of a write runs
            // late, after a handler started a connection in place of that session, they must not replace what that connection was made with.
            var session = this._session ?? throw new ObsClientException("Not connected.");
            await this.SendAsync(session, message, cancellationToken, () => this.ApplyIfActive(session, () => this._eventSubscriptions = eventSubscription)).ConfigureAwait(false);
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
            this._notifications.Clear();
        }

        // Makes a new session the active one, unless the client is disposed: Dispose could not end a connection that it did not know of.
        // The settings of the connection that it is for are written after this, never before.
        private ConnectionSession ActivateNewSession()
        {
            lock (this._syncRoot)
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException(nameof(ObsClient));
                }

                var session = new ConnectionSession();
                this._session = session;
                return session;
            }
        }

        // Runs a change that was made for a session, but only while that session is the active one. The check and the change are one step
        // with respect to activating another session: a session that was replaced cannot change the settings of the one that replaced it.
        private bool ApplyIfActive(ConnectionSession session, Action change)
        {
            lock (this._syncRoot)
            {
                if (!ReferenceEquals(this._session, session))
                {
                    return false;
                }

                change();
                return true;
            }
        }

        private async Task<bool> StartAsync(ConnectionSession session, CancellationToken cancellationToken = default)
        {
            this.ConnectionState = ConnectionState.Connecting;

            // Cancelling the token ends this attempt, the session that was made for it, and nothing else: Disconnect() ends whichever session is
            // active at the moment that the token is cancelled, which can by then be a connection that a handler started in its place. The
            // outcome of the attempt is decided once (here by the token, or at the end of this method by the attempt), so that a token that is
            // cancelled as the attempt completes either ends it, and the call throws, or no longer matters: it cannot do both. A token that is
            // cancelled already ends the session before its socket is opened. The registration only lives as long as the attempt.
            using var cancellation = cancellationToken.Register(() =>
            {
                if (Interlocked.CompareExchange(ref session.Attempt, AttemptCancelled, AttemptUndecided) == AttemptUndecided)
                {
                    this.EndAttempt(session);
                }
            });

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

            bool connected;
            if (session.Socket.State == WebSocketState.Open)
            {    
                _ = Task.Run(() => this.ReceiverAsync(session));
                connected = await this.WaitForAuthenticationAsync(session).ConfigureAwait(false);
            }
            else
            {
                AbandonPendingWork(session);
                this.SetConnectionState(session, ConnectionState.Disconnected);
                session.Dispose();
                connected = false;
            }

            // The decision: the token got there first and ended the attempt (the registration is disposed when this method ends, which waits
            // for its callback to finish), or the attempt is over and the token can no longer end anything.
            if (Interlocked.CompareExchange(ref session.Attempt, AttemptCompleted, AttemptUndecided) != AttemptUndecided)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return connected;
        }

        // What Disconnect() does, for the session of one attempt to connect and for nothing else: AutoReconnect is switched off, and the
        // session is ended. AutoReconnect is only changed while this session is still the active one, so a connection that a handler
        // started in its place keeps its settings.
        private void EndAttempt(ConnectionSession session)
        {
            _logger.LogInformation("Ending the attempt to connect to OBS Studio");
            this.SetAutoReconnect(false, session);
            session.Cancellation.Cancel();
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

                // The receive loop does not run the handlers: it queues the event, and goes on reading. An event that nobody listens
                // to is not queued at all.
                if (this._eventsMap.TryGetValue(eventResponseData.EventType.ToString(), out var field) && field.GetValue(this) is not null)
                {
                    this.Notify(() => this.RaiseEvent(eventResponseData));
                }
            }
            else
            {
                _logger.LogError("Unexpected response type in ProcessEventMessage");
                throw new ObsClientException($"Unexpected response type {responseMessage.Data?.GetType().Name} in {MethodBase.GetCurrentMethod()?.Name}");
            }
        }

        // Raises an event for the handlers that it has at this moment (not at the moment that it arrived: a handler that was removed in the
        // meantime does not get it). A handler that throws is logged, and does not keep the other handlers from getting their turn.
        private void RaiseEvent(EventMessage eventResponseData)
        {
            if (this._eventsMap.TryGetValue(eventResponseData.EventType.ToString(), out var field) && field.GetValue(this) is MulticastDelegate eventDelegate)
            {
                foreach (var handler in eventDelegate.GetInvocationList())
                {
                    // Before every handler, and not once for the event: Dispose does not wait for a handler that is running, so the
                    // handler that ran while the client was disposed must not be followed by the next one.
                    if (this._disposed)
                    {
                        return;
                    }

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

        // Like RaiseEvent: the handlers one by one, so that one that throws does not keep the others from being called, and none starts
        // once the client is disposed.
        private void RaiseConnectionClosed(ConnectionClosedEventArgs closed)
        {
            if (this.ConnectionClosed is not { } handlers)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                if (this._disposed)
                {
                    return;
                }

                try
                {
                    ((EventHandler<ConnectionClosedEventArgs>)handler)(this, closed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception in a ConnectionClosed handler");
                }
            }
        }

        // Adds a notification to the queue, and makes sure that a task is raising the notifications. Once the client is disposed nothing is
        // queued any more, and the notifications that are waiting are not raised.
        private void Notify(Action notification)
        {
            if (this._disposed)
            {
                return;
            }

            if (Interlocked.Increment(ref this._waitingNotifications) == NotificationBacklogWarning)
            {
                _logger.LogWarning(
                    "{Count} notifications are waiting for the handlers of the application, which are slower than OBS Studio is. A handler that waits for something holds up all the notifications behind it.",
                    NotificationBacklogWarning);
            }

            this._notifications.Enqueue(notification);
            if (Interlocked.CompareExchange(ref this._delivering, 1, 0) == 0)
            {
                _ = Task.Run(this.DeliverNotifications);
            }
        }

        // The one task that raises the notifications. It ends when the queue is empty, and the next notification starts another one.
        private void DeliverNotifications()
        {
            while (true)
            {
                while (!this._disposed && this._notifications.TryDequeue(out var notification))
                {
                    Interlocked.Decrement(ref this._waitingNotifications);
                    try
                    {
                        notification();
                    }
                    catch (Exception ex)
                    {
                        // The notifications handle the exceptions of their handlers themselves; this keeps the delivery alive whatever happens.
                        _logger.LogError(ex, "Exception while raising a notification");
                    }
                }

                // Interlocked, for the full fence: a notification that was added after the last look at the queue found this task still
                // delivering and started none, so the queue has to be looked at again after the flag was reset.
                Interlocked.Exchange(ref this._delivering, 0);
                if (this._disposed || this._notifications.IsEmpty || Interlocked.CompareExchange(ref this._delivering, 1, 0) != 0)
                {
                    return;
                }
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

        private async Task SendAsync(ConnectionSession session, object request, CancellationToken cancellationToken = default, Action? written = null)
        {
            var socket = session.Socket;
            if (socket.State != WebSocketState.Open)
            {
                throw new ObsClientException("Not connected.");
            }

            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(request);

            var sendBuffer = new ArraySegment<byte>(bytes);
            var lockTaken = false;
            CancellationTokenSource? waiting = null;
            try
            {
                // The caller can cancel the wait for a turn. It cannot cancel the writing of the message: a send that is cancelled
                // halfway aborts the whole WebSocket, so only the end of the session may interrupt that.
                var turn = session.Cancellation.Token;
                if (cancellationToken.CanBeCanceled)
                {
                    waiting = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation.Token, cancellationToken);
                    turn = waiting.Token;
                }

                // A WebSocket message must be written in one piece, so concurrent callers take turns.
                await session.SendLock.WaitAsync(turn).ConfigureAwait(false);
                lockTaken = true;

                // Logged when it is its turn, so that the log shows the messages that are written, in the order in which they are.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Sending: {Request}", RedactAuthentication(Encoding.UTF8.GetString(bytes)));
                }

                await socket.SendAsync(sendBuffer, WebSocketMessageType.Text, true, session.Cancellation.Token).ConfigureAwait(false);

                // Still holding the turn: whatever depends on the message having been written is done in the order of the messages.
                written?.Invoke();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is ObjectDisposedException or WebSocketException or OperationCanceledException)
            {
                throw new ObsClientException("Not connected.", ex);
            }
            finally
            {
                waiting?.Dispose();
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

            // Raised through the queue of notifications: after the events that arrived before the connection ended, and never by this loop,
            // so a handler that connects again, or waits for anything, cannot hold up the cleanup of the connection that just ended.
            var closed = new ConnectionClosedEventArgs(closeCode, closeDescription);
            this.Notify(() => this.RaiseConnectionClosed(closed));
        }

        private async Task<IMessage> SendAndWaitAsync(string requestId, object request, int? timeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
            using var callerCtr = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken), false);
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

                // The wait of the caller ends with its token or with the timeout, whatever the write is doing: when OBS Studio does not read,
                // a write can block, and it cannot be cancelled without aborting the connection. So it is left to finish by itself, still
                // holding the turn (nothing can be written in the middle of it), and what goes wrong with it is observed. A message that
                // still waits for its turn is given up together with the wait.
                using var turnCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                var write = this.SendAsync(session, request, turnCts.Token);
                if (await Task.WhenAny(write, tcs.Task).ConfigureAwait(false) == write)
                {
                    await write.ConfigureAwait(false);
                }
                else
                {
                    _ = this.ObserveWriteAsync(write);
                }

                return await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Only the timeout cancels anything otherwise (the response that is waited for, or the wait for the turn to write, which can end
                // with an OperationCanceledException that is not a TaskCanceledException): a cancelled token of the caller is a cancellation.
                throw new TimeoutException($"Timeout waiting for OBS Studio response to request {requestId}.");
            }
            finally
            {
                _ = ctr.Unregister();
                _ = session.Requests.TryRemove(requestId, out _);
            }
        }

        // A write that outlives the wait for it ends one way or another with nobody awaiting it: what went wrong with it is logged, not lost.
        private async Task ObserveWriteAsync(Task write)
        {
            try
            {
                await write.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "A write that the caller no longer waited for did not complete");
            }
        }

        private async Task SendRequestAsync(RequestType requestType, object? requestData = null, CancellationToken cancellationToken = default)
        {
            await this.SendRequestAndWaitAsync(requestType, requestData, cancellationToken).ConfigureAwait(false);
        }

        private async Task<T> SendRequestAsync<T>(RequestType requestType, object? requestData = null, CancellationToken cancellationToken = default) where T : IResponse
        {
            var requestResponseData = await this.SendRequestAndWaitAsync(requestType, requestData, cancellationToken).ConfigureAwait(false);
            return (T)requestResponseData.ResponseData!;
        }

        private async Task<RequestResponseMessage> SendRequestAndWaitAsync(RequestType requestType, object? requestData, CancellationToken cancellationToken)
        {
            var payload = RequestPayload.ToElement(requestData);
            int retryCount = 0;
            while (true)
            {
                var requestId = Guid.NewGuid().ToString();
                object d = payload is null ? new { requestType, requestId } : new { requestType, requestId, requestData = payload };
                var op = (int)OpCode.Request;
                var result = await this.SendAndWaitAsync(requestId, new { d, op }, null, cancellationToken).ConfigureAwait(false);
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
                            await Task.Delay(this._requestRetryInterval, cancellationToken).ConfigureAwait(false);
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
                        await this.StartAsync(this.ActivateNewSession());
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

            /// <summary>What became of the attempt of <see cref="ObsClient.ConnectAsync"/> that made this session: it is decided once, by the token or by the end of the attempt.</summary>
            public int Attempt;

            public void Dispose()
            {
                this.Socket.Dispose();
            }
        }
    }
}