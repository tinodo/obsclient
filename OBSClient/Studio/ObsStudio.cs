namespace OBSStudioClient.Studio
{
    using System.ComponentModel;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;

    /// <summary>
    /// A model of OBS Studio that keeps itself up to date, and that finds scenes, inputs and scene items by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OBS Studio identifies most things by a UUID, or by an id or an index. An <see cref="ObsStudio"/> reads the scenes, inputs and scene items when it
    /// connects, listens to the events that OBS Studio sends when they change, and keeps a copy in memory. The copy is what you ask for, by name:
    /// <c>obs.Scenes["Gaming"].Items["Webcam"].IsEnabled</c> is answered at once, without a request to OBS Studio.
    /// </para>
    /// <para>
    /// It is built on the <see cref="ObsClient"/>, which it owns the connection of, and which stays available as <see cref="Client"/>: every request and
    /// every event of OBS Studio can still be used through it. Do not call <see cref="ObsClient.ConnectAsync"/>, <see cref="ObsClient.Disconnect"/> or
    /// <see cref="ObsClient.ReidentifyAsync"/> on it while it belongs to an <see cref="ObsStudio"/>: use <see cref="ConnectAsync"/> and
    /// <see cref="DisconnectAsync"/>.
    /// </para>
    /// <para>
    /// The model needs OBS Studio 30.1 or newer (obs-websocket 5.4), the first version that identifies scenes and inputs by UUID. Older versions work with
    /// the <see cref="ObsClient"/> alone. From OBS Studio 31.1 (obs-websocket 5.6) OBS Studio says which inputs have audio. Before that the model asks for
    /// the audio state of every input, which OBS Studio refuses for an input without audio, and takes the refusal as the answer: the model is the same, and
    /// connecting takes two more requests for each input without audio.
    /// </para>
    /// <para>
    /// Everything that is a property is answered from the model, and can be read from any thread. The model is updated on a thread of its own; what
    /// you see changes between two reads. The notifications (<see cref="Changed"/> and the PropertyChanged events) are raised one at a time, in order,
    /// outside that thread (see <see cref="ObsStudioOptions.NotificationContext"/>).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await using var obs = new ObsStudio();
    /// await obs.ConnectAsync("localhost", 4455, "password");
    ///
    /// var gaming = obs.Scenes["Gaming"];
    /// Console.WriteLine($"{gaming.Name} has {gaming.Items.Count} scene items; the webcam is {(gaming.Items["Webcam"].IsEnabled ? "on" : "off")}.");
    ///
    /// obs.Changed += (_, e) => Console.WriteLine(e);
    /// </code>
    /// </example>
    public sealed class ObsStudio : INotifyPropertyChanged, IDisposable, IAsyncDisposable
    {
        private static readonly TimeSpan ClosingTimeout = TimeSpan.FromSeconds(5);

        private readonly ObsClient _client;
        private readonly bool _ownsClient;
        private readonly ObsStudioOptions _options;
        private readonly ILogger _logger;
        private readonly StudioNotifier _notifier;
        private readonly StudioHost _host;
        private readonly object _stateLock = new();
        private volatile StudioView _view;
        private volatile ObsStudioState _state;
        private volatile bool _everSynchronized;
        private volatile bool _disposed;

        // The number of the call of ConnectAsync that has the connection: only that call may end it (see AbandonConnect). Under _stateLock.
        private int _connectTicket;

        /// <summary>
        /// Creates an <see cref="ObsStudio"/> with a new <see cref="ObsClient"/>, which it disposes of when it is disposed.
        /// </summary>
        /// <param name="options">The settings, or null for the defaults.</param>
        /// <param name="loggerFactory">The logger factory, or null to log nothing.</param>
        public ObsStudio(ObsStudioOptions? options = null, ILoggerFactory? loggerFactory = null)
            : this(NewClient(options, loggerFactory), true, options, loggerFactory?.CreateLogger<ObsStudio>())
        {
        }

        /// <summary>
        /// Creates an <see cref="ObsStudio"/> on an <see cref="ObsClient"/> that you have made. The client has to be disconnected, and it can have only one
        /// <see cref="ObsStudio"/>. You remain the owner of the client: disposing of the <see cref="ObsStudio"/> disconnects it, and does not dispose of it.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="options">The settings, or null for the defaults.</param>
        /// <param name="logger">The logger, or null to log nothing.</param>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The client is connected or connecting, or has an <see cref="ObsStudio"/> already.</exception>
        public ObsStudio(ObsClient client, ObsStudioOptions? options = null, ILogger<ObsStudio>? logger = null)
            : this(client ?? throw new ArgumentNullException(nameof(client)), false, options, logger)
        {
        }

        private ObsStudio(ObsClient client, bool ownsClient, ObsStudioOptions? options, ILogger? logger)
        {
            if (client.ConnectionState != ConnectionState.Disconnected)
            {
                throw new InvalidOperationException("The ObsClient is connected or connecting: pass a client that is disconnected, and let the ObsStudio connect it.");
            }

            this._options = options?.Clone() ?? new ObsStudioOptions();
            Validate(this._options);
            this._client = client;
            this._ownsClient = ownsClient;
            this._logger = logger ?? NullLogger.Instance;
            this._notifier = new StudioNotifier(this._options.NotificationContext, this._logger);
            this._host = new StudioHost(this, client, this._options, this._logger, this._notifier);
            this._view = this._host.View;
            try
            {
                this._host.Start();
            }
            catch
            {
                if (ownsClient)
                {
                    client.Dispose();
                }

                throw;
            }
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Occurs when something in the model changes: an object appeared or went, a value changed, or the model corrected itself. The records come in the order in which
        /// the changes happened, one at a time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A handler sees the model as it is when the handler runs, which can be newer than the change that it is told about: the properties are always current,
        /// the change is a notification. While the model is being read (after a connection is made, and after one is restored) the changes are not raised one by one:
        /// <see cref="Resynchronized"/> is raised when it is complete.
        /// </para>
        /// <para>
        /// There is one record for each change of a value or of a list, not one for each object that the change moves: when a scene item is moved, the
        /// record is a change of <see cref="ObsScene.Items"/> of its scene; the <see cref="ObsSceneItem.Index"/> of the items that moved are announced by their PropertyChanged events.
        /// </para>
        /// </remarks>
        public event EventHandler<ObsChangedEventArgs>? Changed;

        /// <summary>
        /// Occurs when the model has been read completely: after the first connection, and after a connection that was lost is restored. Everything that you
        /// hold may have changed (scenes and inputs that were added, removed or renamed while the connection was down), so read what you need again.
        /// </summary>
        public event EventHandler? Resynchronized;

        /// <summary>
        /// Gets the <see cref="ObsClient"/> that the model is built on. Every request and every event of OBS Studio is available through it.
        /// </summary>
        /// <remarks>
        /// Use it to send the requests that this class does not offer yet. The model sees what they do through the events of OBS Studio, as it does for what any other
        /// program does. Do not connect or disconnect it yourself: see <see cref="ConnectAsync"/> and <see cref="DisconnectAsync"/>. The client switches
        /// <see cref="ObsClient.AutoReconnect"/> off by itself when OBS Studio ends the session on purpose (the Kick button of its session list): the
        /// <see cref="State"/> is then <see cref="ObsStudioState.Disconnected"/>, and you can connect again.
        /// </remarks>
        public ObsClient Client => this._client;

        /// <summary>
        /// Gets the state of the model: whether there is a connection, and whether the model is complete. After the <see cref="ObsStudio"/> is disposed of it
        /// is <see cref="ObsStudioState.Disconnected"/>.
        /// </summary>
        public ObsStudioState State => this._disposed ? ObsStudioState.Disconnected : this._state;

        /// <summary>
        /// Gets whether the values that the model holds may be out of date: the model was complete once, and is not now (the connection is lost, or the model is
        /// being read again, or the <see cref="ObsStudio"/> is disposed of). The values are the last that were known. Before the first connection there are no
        /// values, and this is false.
        /// </summary>
        public bool IsStale => this.IsStaleIn(this.State);

        /// <summary>
        /// Gets the scenes, in the order of the list of scenes of OBS Studio. Groups, and scenes that OBS Studio removed but keeps alive, are not in the list.
        /// </summary>
        public ObsNamedCollection<ObsScene> Scenes => this._view.Scenes;

        /// <summary>
        /// Gets the inputs that were not removed, ordered by name.
        /// </summary>
        public ObsNamedCollection<ObsInput> Inputs => this._view.Inputs;

        /// <summary>
        /// Gets the program scene: the scene that is on the air. It is null until it is known.
        /// </summary>
        public ObsScene? ProgramScene => this._view.ProgramScene;

        /// <summary>
        /// Gets whether studio mode is on.
        /// </summary>
        public bool IsStudioModeEnabled => this._view.StudioMode;

        internal bool IgnoreCase => this._options.IgnoreCase;

        // The host, for the tests that bring about an order in which things reach it, or make it fail.
        internal StudioHost Host => this._host;

        // How long a disconnect and a stop wait for the model and the client. For the tests.
        internal TimeSpan DisconnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

        // Called when a call of ConnectAsync that failed is about to end the connection that it made. For the tests: the order in which a newer call can meet it.
        internal Action? BeforeAbandon { get; set; }

        // Checks the options before a client is made for them, so that a refusal leaves nothing behind that has to be disposed of.
        private static ObsClient NewClient(ObsStudioOptions? options, ILoggerFactory? loggerFactory)
        {
            if (options is not null)
            {
                Validate(options);
            }

            return new ObsClient(loggerFactory?.CreateLogger<ObsClient>());
        }

        private static void Validate(ObsStudioOptions options)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ReadTimeout, TimeSpan.Zero, nameof(options.ReadTimeout));
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.SynchronizationTimeout, TimeSpan.Zero, nameof(options.SynchronizationTimeout));
        }

        /// <summary>
        /// Connects to OBS Studio, and reads its scenes, inputs and scene items. The task completes when the model is complete.
        /// </summary>
        /// <param name="hostname">The name or address of the computer that OBS Studio runs on.</param>
        /// <param name="port">The port of the WebSocket server of OBS Studio.</param>
        /// <param name="password">The password of the WebSocket server, or empty when it has none.</param>
        /// <param name="cancellationToken">A token that ends the attempt.</param>
        /// <exception cref="InvalidOperationException">The model is connected or connecting already.</exception>
        /// <exception cref="ObsNotConnectedException">OBS Studio could not be reached, refused the password, or the connection ended before the model was complete.</exception>
        /// <exception cref="ObsVersionNotSupportedException">OBS Studio is older than 30.1.</exception>
        /// <exception cref="TimeoutException">The model was not complete within <see cref="ObsStudioOptions.SynchronizationTimeout"/>.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        /// <remarks>
        /// When it fails, nothing is left connected, and nothing keeps trying. After it succeeds, the connection is restored when it is lost
        /// (<see cref="ObsStudioOptions.AutoReconnect"/>), and the model is read again.
        /// </remarks>
        public async Task ConnectAsync(string hostname = "localhost", int port = 4455, string password = "", CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (!this.TryEnterConnecting(out var ticket))
            {
                throw new InvalidOperationException($"The studio is {this._state}: connect when it is Disconnected.");
            }

            // The model is told that a connection ended a little before the client says that it is closed: a connection that ended just now is
            // allowed to finish closing. What is still not closed after that is somebody else's connection.
            try
            {
                await this.WaitForClientDisconnectedAsync(ClosingTimeout).ConfigureAwait(false);
                if (this._client.ConnectionState != ConnectionState.Disconnected)
                {
                    throw new InvalidOperationException($"The client is {this._client.ConnectionState}: connect when it is Disconnected, and let the ObsStudio connect it.");
                }
            }
            catch
            {
                this.ReleaseConnecting(ticket);
                throw;
            }

            var attempt = this._host.BeginConnect();
            try
            {
                var events = StudioHost.RequiredEvents | this._options.AdditionalEvents;
                var connected = await this._client.ConnectAsync(this._options.AutoReconnect, password, hostname, port, events, cancellationToken).ConfigureAwait(false);
                if (!connected)
                {
                    throw new ObsNotConnectedException($"Could not connect to OBS Studio at {hostname}:{port}. Check that OBS Studio is running, that its WebSocket server is switched on, and the address, the port and the password.");
                }

                await attempt.Task.WaitAsync(this._options.SynchronizationTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                this.AbandonConnect(ticket);
                throw new TimeoutException($"OBS Studio did not deliver its scenes and inputs within {this._options.SynchronizationTimeout}. The connection was closed.");
            }
            catch
            {
                this.AbandonConnect(ticket);
                throw;
            }
            finally
            {
                this._host.EndConnect(attempt);
            }
        }

        /// <summary>
        /// Closes the connection to OBS Studio. The model keeps the last values that it knew (<see cref="IsStale"/>). The task completes when the connection is closed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The <see cref="ObsStudio"/> is disposed.</exception>
        /// <exception cref="TimeoutException">The connection was not closed within ten seconds.</exception>
        public async Task DisconnectAsync()
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            this._client.Disconnect();
            await this._host.WaitUntilDisconnectedAsync(this.DisconnectTimeout).ConfigureAwait(false);
            await this.WaitForClientDisconnectedAsync(this.DisconnectTimeout).ConfigureAwait(false);
            if (this._client.ConnectionState != ConnectionState.Disconnected)
            {
                throw new TimeoutException($"The connection to OBS Studio was not closed within {this.DisconnectTimeout}.");
            }
        }

        /// <summary>
        /// Waits until the model has applied everything that has arrived and has nothing left to read.
        /// </summary>
        /// <param name="timeout">How long to wait, or null to wait for as long as it takes.</param>
        /// <param name="cancellationToken">A token that ends the wait.</param>
        /// <exception cref="ObsNotConnectedException">There is no connection (or it ended during the wait).</exception>
        /// <exception cref="TimeoutException">The model was not up to date within the time.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        /// <remarks>
        /// OBS Studio announces a change with an event, which can arrive a few milliseconds after the response to the request that made the change. An event that
        /// OBS Studio has not sent yet cannot be waited for: this waits for what has arrived.
        /// </remarks>
        public async Task WaitUntilSettledAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);

            // The host is given a token that ends with the time or the cancellation, so that it forgets the wait when this call ends.
            using var limit = timeout is { } span ? new CancellationTokenSource(span) : null;
            using var linked = limit is null ? null : CancellationTokenSource.CreateLinkedTokenSource(limit.Token, cancellationToken);
            try
            {
                await this._host.WaitUntilSettledAsync(linked?.Token ?? cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (limit is { IsCancellationRequested: true } && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"The model was not up to date within {timeout}.");
            }
        }

        /// <summary>
        /// Ends the connection and stops the model. Disposes of the <see cref="ObsClient"/> when this <see cref="ObsStudio"/> made it.
        /// </summary>
        public void Dispose()
        {
            if (!this.BeginDispose())
            {
                return;
            }

            this.StopEverything();
            try
            {
                this._host.Completion.Wait(this.DisconnectTimeout);
            }
            catch (AggregateException)
            {
                // The loop ended with an error that it has logged.
            }
        }

        /// <summary>
        /// Ends the connection and stops the model. Disposes of the <see cref="ObsClient"/> when this <see cref="ObsStudio"/> made it.
        /// </summary>
        /// <returns>A task that completes when the model has stopped.</returns>
        public async ValueTask DisposeAsync()
        {
            if (!this.BeginDispose())
            {
                return;
            }

            this.StopEverything();
            try
            {
                await this._host.Completion.WaitAsync(this.DisconnectTimeout).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
            {
                this._logger.LogWarning("The studio model did not stop within {Timeout}.", this.DisconnectTimeout);
            }
        }

        // Called by the model, on its own thread.
        internal void SetView(StudioView view)
        {
            this._view = view;
        }

        // Called by the model, and by ConnectAsync and DisconnectAsync.
        internal void SetState(ObsStudioState state)
        {
            lock (this._stateLock)
            {
                var old = this._state;
                if (old == state)
                {
                    return;
                }

                var wasStale = this.IsStaleIn(old);
                this._state = state;
                if (state == ObsStudioState.Synchronized)
                {
                    this._everSynchronized = true;
                }

                this.QueueNotice(new StudioNotice(new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, null, nameof(this.State), old, state), this, nameof(this.State)));
                if (this.IsStaleIn(state) != wasStale)
                {
                    this.QueueNotice(new StudioNotice(null, this, nameof(this.IsStale)));
                }
            }
        }

        private bool IsStaleIn(ObsStudioState state)
        {
            return this._everSynchronized && state != ObsStudioState.Synchronized;
        }

        internal void QueueNotice(StudioNotice notice)
        {
            this._notifier.Enqueue(() => this.RaiseNotice(notice));
        }

        internal void QueueResynchronized()
        {
            this._notifier.Enqueue(this.RaiseResynchronized);
        }

        // The scene items that show a scene or an input, in the scenes of the moment.
        internal IReadOnlyList<ObsSceneItem> FindItemsShowing(ObsSource source)
        {
            List<ObsSceneItem>? found = null;
            foreach (var scene in this._view.AllScenes)
            {
                foreach (var item in scene.Items)
                {
                    if (ReferenceEquals(item.Source, source))
                    {
                        (found ??= []).Add(item);
                    }
                }
            }

            return found?.ToArray() ?? [];
        }

        // Raises a PropertyChanged event so that a handler that throws does not keep the other handlers from running.
        internal void Raise(PropertyChangedEventHandler? handler, object sender, string propertyName)
        {
            if (handler is null)
            {
                return;
            }

            var args = new PropertyChangedEventArgs(propertyName);
            foreach (var subscriber in handler.GetInvocationList().Cast<PropertyChangedEventHandler>())
            {
                try
                {
                    subscriber(sender, args);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Exception in a PropertyChanged handler for {Property}", propertyName);
                }
            }
        }

        private bool TryEnterConnecting(out int ticket)
        {
            lock (this._stateLock)
            {
                if (this._state != ObsStudioState.Disconnected)
                {
                    ticket = 0;
                    return false;
                }

                ticket = ++this._connectTicket;
                this.SetState(ObsStudioState.Connecting);
                return true;
            }
        }

        // A call of ConnectAsync ended before it connected anything.
        private void ReleaseConnecting(int ticket)
        {
            lock (this._stateLock)
            {
                if (this._connectTicket == ticket)
                {
                    this.SetState(ObsStudioState.Disconnected);
                }
            }
        }

        // The client says that it is disconnected when the last of its connection is closed, which is a little after the receive loop ended and the
        // model was told. Returns when it says so, or after the time: the caller looks at the state.
        private async Task WaitForClientDisconnectedAsync(TimeSpan timeout)
        {
            if (this._client.ConnectionState == ConnectionState.Disconnected)
            {
                return;
            }

            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(ObsClient.ConnectionState) && this._client.ConnectionState == ConnectionState.Disconnected)
                {
                    closed.TrySetResult();
                }
            }

            this._client.PropertyChanged += OnChanged;
            try
            {
                if (this._client.ConnectionState != ConnectionState.Disconnected)
                {
                    await closed.Task.WaitAsync(timeout).ConfigureAwait(false);
                }
            }
            catch (TimeoutException)
            {
            }
            finally
            {
                this._client.PropertyChanged -= OnChanged;
            }
        }

        // A call of ConnectAsync failed or was cancelled: what it connected must not stay connected, and the client must not keep trying. Only the call that
        // has the connection may end it. A newer call has its own: the state can be Disconnected before the older call has unwound (the application
        // disconnected meanwhile), and the application can connect again then. The check and the end are one step with the entry of a call.
        private void AbandonConnect(int ticket)
        {
            this.BeforeAbandon?.Invoke();
            lock (this._stateLock)
            {
                if (this._connectTicket != ticket)
                {
                    return;
                }

                try
                {
                    this._client.Disconnect();
                }
                catch (ObjectDisposedException)
                {
                }

                this.SetState(ObsStudioState.Disconnected);
            }
        }

        private bool BeginDispose()
        {
            lock (this._stateLock)
            {
                if (this._disposed)
                {
                    return false;
                }

                this._disposed = true;
                return true;
            }
        }

        private void StopEverything()
        {
            try
            {
                this._client.Disconnect();
            }
            catch (ObjectDisposedException)
            {
            }

            this._host.Stop();
            if (this._ownsClient)
            {
                this._client.Dispose();
            }
        }

        private void RaiseNotice(StudioNotice notice)
        {
            if (notice.Args is { } args)
            {
                this.RaiseChanged(args);
            }

            if (notice.Property is { } property)
            {
                switch (notice.Entity)
                {
                    case ObsSource source:
                        source.RaisePropertyChanged(property);
                        break;
                    case ObsSceneItem item:
                        item.RaisePropertyChanged(property);
                        break;
                    case ObsStudio:
                        this.Raise(this.PropertyChanged, this, property);
                        break;
                }
            }
        }

        private void RaiseChanged(ObsChangedEventArgs args)
        {
            var handler = this.Changed;
            if (handler is null)
            {
                return;
            }

            foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler<ObsChangedEventArgs>>())
            {
                try
                {
                    subscriber(this, args);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Exception in a Changed handler");
                }
            }
        }

        private void RaiseResynchronized()
        {
            var handler = this.Resynchronized;
            if (handler is null)
            {
                return;
            }

            foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler>())
            {
                try
                {
                    subscriber(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "Exception in a Resynchronized handler");
                }
            }
        }
    }
}
