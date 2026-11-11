namespace OBSStudioClient.Model
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
    /// OBS Studio identifies most things by a UUID, or by an id or an index. An <see cref="ObsModel"/> reads the scenes, inputs and scene items when it
    /// connects, listens to the events that OBS Studio sends when they change, and keeps a copy in memory. The copy is what you ask for, by name:
    /// <c>obs.Scenes["Gaming"].Items["Webcam"].IsEnabled</c> is answered at once, without a request to OBS Studio.
    /// </para>
    /// <para>
    /// It is built on the <see cref="ObsClient"/>, which it owns the connection of, and which stays available as <see cref="Client"/>: every request and
    /// every event of OBS Studio can still be used through it. Do not call <see cref="ObsClient.ConnectAsync"/>, <see cref="ObsClient.Disconnect"/> or
    /// <see cref="ObsClient.ReidentifyAsync"/> on it while it belongs to an <see cref="ObsModel"/>: use <see cref="ConnectAsync"/> and
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
    /// outside that thread (see <see cref="ObsModelOptions.NotificationContext"/>).
    /// </para>
    /// <para>
    /// Changes are methods on the objects, for example <see cref="ObsSceneItem.SetEnabledAsync"/>, <see cref="ObsInput.SetMutedAsync"/> and
    /// <see cref="ObsScene.SwitchToAsync"/>. They are made one at a time, in the order in which you ask for them (OBS Studio can answer requests that arrive together in any
    /// order), and each returns when the model shows the change, so that what you read next is what you changed. A change is never made twice: when OBS Studio does not
    /// answer, or the connection ends, or the answer can not be read, the call ends with an <see cref="ObsUnknownOutcomeException"/>, and the model reads again what the change was about. A change is not
    /// sent when there is no connection, or when the connection that it was asked for on has ended and another was made (<see cref="ObsNotConnectedException"/>), while OBS
    /// Studio changes its scene collection (<see cref="ObsNotReadyException"/>), or to something that OBS Studio removed (<see cref="ObsRemovedException"/>). The model applies
    /// what the client reads a moment later, so the checks do not rest on the model alone: they are made again when the change is about to be written (which can be much later than
    /// when it was asked for), they look at the connection that it is written on, and a change of the scene collection that the client has read and the model has not applied yet
    /// stops the changes until it has. What is left is the time between OBS Studio sending the event and the client reading it (a few milliseconds on a local connection): a change
    /// that is sent then is refused by OBS Studio while it changes its scene collection (it answers that it is not ready, and the call ends with the same
    /// <see cref="ObsNotReadyException"/>), and nothing was changed. When OBS Studio
    /// made the change and the model can not show it (the events did not come in time, and the model could not be read again in time) the call ends with an
    /// <see cref="ObsNotConfirmedException"/>: the change was made, so do not send it again. A token that ends a call ends the wait at once and keeps a change that was not
    /// sent yet from being sent; one that was sent is not undone, and the next change still waits for the answer of OBS Studio to it, so that the order holds.
    /// </para>
    /// <para>
    /// Scenes, inputs and scene items are made, renamed and removed in the same way: <see cref="CreateSceneAsync"/>, <see cref="ObsScene.CreateInputAsync"/>,
    /// <see cref="ObsScene.AddAsync"/>, <see cref="ObsScene.RenameAsync"/>, <see cref="ObsScene.RemoveAsync"/>, <see cref="ObsInput.RenameAsync"/> and
    /// <see cref="ObsInput.RemoveAsync"/>. A name that is taken is refused with an <see cref="ObsNameInUseException"/> that says what holds it. OBS Studio keeps an input or a scene
    /// that was removed alive for as long as scene items show it (it is <see cref="ObsObjectState.Detached"/>): the name of the input stays taken, and so does the name of the scene
    /// with OBS Studio 31.0 and older (OBS Studio 31.1 and newer free the name of a scene that was removed at once). <c>RemoveAsync</c> removes those scene items too, so that OBS Studio
    /// destroys the object (<see cref="ObsObjectState.Gone"/>) and the name is free.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await using var obs = new ObsModel();
    /// await obs.ConnectAsync("localhost", 4455, "password");
    ///
    /// var gaming = obs.Scenes["Gaming"];
    /// Console.WriteLine($"{gaming.Name} has {gaming.Items.Count} scene items; the webcam is {(gaming.Items["Webcam"].IsEnabled ? "on" : "off")}.");
    ///
    /// obs.Changed += (_, e) => Console.WriteLine(e);
    /// </code>
    /// </example>
    public sealed partial class ObsModel : INotifyPropertyChanged, IDisposable, IAsyncDisposable
    {
        private static readonly TimeSpan ClosingTimeout = TimeSpan.FromSeconds(5);

        // The longest wait that the model asks a timer or a request for: they take whole milliseconds as an int.
        private static readonly TimeSpan LongestTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

        // The first version of OBS Studio from which a scene that was removed gives up its name at once: libobs 31.1.0 takes it out of the list of its canvas in obs_source_remove (what OBS Studio
        // 31.0 and older do not). It is the version of OBS Studio, not that of obs-websocket: OBS Studio 31.1 and 32.0 come with obs-websocket 5.6.
        private static readonly Version ScenesFreeTheirNameFrom = new(31, 1, 0);

        private readonly ObsClient _client;
        private readonly bool _ownsClient;
        private readonly ObsModelOptions _options;
        private readonly ILogger _logger;
        private readonly ModelNotifier _notifier;
        private readonly ModelHost _host;
        private readonly ModelWriter _writer;
        private readonly object _stateLock = new();
        private volatile ModelView _view;
        private volatile ObsModelState _state;
        private volatile bool _everSynchronized;
        private volatile bool _disposed;

        // Whether a scene that OBS Studio removed and keeps alive keeps its name taken: with OBS Studio 31.0 and older it does, from 31.1 on the name is free at once. It is what the version of
        // OBS Studio that the connection reports says (ModelHost.HandleVersion). Until the connection has said it, and when what it says can not be read, the newest behaviour is assumed:
        // a connection starts with it, and what the connection before it said is not kept (a reconnect can be to another OBS Studio). A change that is asked for in that time is not held back
        // for the version, so the message of a name that is taken (ModelNames) is the one of the newest behaviour then.
        private volatile bool _removedScenesKeepTheirName;

        // The number of the call of ConnectAsync that has the connection: only that call may end it (see AbandonConnect). Under _stateLock.
        private int _connectTicket;

        // The call of ConnectAsync that is under way, or null: what ends it (DisconnectAsync and Dispose cancel it), and what says that it has ended. A call is
        // under way from the moment that it enters Connecting until it has unwound: it owns the state until then, and no other call can start. Under _stateLock.
        private ConnectCall? _call;

        /// <summary>
        /// Creates an <see cref="ObsModel"/> with a new <see cref="ObsClient"/>, which it disposes of when it is disposed.
        /// </summary>
        /// <param name="options">The settings, or null for the defaults.</param>
        /// <param name="loggerFactory">The logger factory, or null to log nothing.</param>
        public ObsModel(ObsModelOptions? options = null, ILoggerFactory? loggerFactory = null)
            : this(NewClient(options, loggerFactory), true, options, loggerFactory?.CreateLogger<ObsModel>())
        {
        }

        /// <summary>
        /// Creates an <see cref="ObsModel"/> on an <see cref="ObsClient"/> that you have made. The client has to be disconnected, and it can have only one
        /// <see cref="ObsModel"/>. You remain the owner of the client: disposing of the <see cref="ObsModel"/> disconnects it, and does not dispose of it.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="options">The settings, or null for the defaults.</param>
        /// <param name="logger">The logger, or null to log nothing.</param>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The client is connected or connecting, or has an <see cref="ObsModel"/> already.</exception>
        public ObsModel(ObsClient client, ObsModelOptions? options = null, ILogger<ObsModel>? logger = null)
            : this(client ?? throw new ArgumentNullException(nameof(client)), false, options, logger)
        {
        }

        private ObsModel(ObsClient client, bool ownsClient, ObsModelOptions? options, ILogger? logger)
        {
            if (client.ConnectionState != ConnectionState.Disconnected)
            {
                throw new InvalidOperationException("The ObsClient is connected or connecting: pass a client that is disconnected, and let the ObsModel connect it.");
            }

            this._options = options?.Clone() ?? new ObsModelOptions();
            Validate(this._options);
            this._client = client;
            this._ownsClient = ownsClient;
            this._logger = logger ?? NullLogger.Instance;
            this.Outputs = new ObsOutputs(this);
            this.Transitions = new ObsTransitions(this);
            this.Profiles = new ObsProfiles(this);
            this.SceneCollections = new ObsSceneCollections(this);
            this._notifier = new ModelNotifier(this._options.NotificationContext, this._logger, this._options.MaxPendingNotifications, this.RaiseEverything);
            this._host = new ModelHost(this, client, this._options, this._logger, this._notifier);
            this._writer = new ModelWriter(client, this._host, this._options, this._logger);
            this._view = this._host.View;
            try
            {
                this._host.Start();
                this._writer.Start();
            }
            catch
            {
                this._writer.Stop();
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
        /// <see cref="Resynchronized"/> is raised when it is complete. What does not depend on the scene collection that is read is the exception: the file that an output wrote
        /// (<see cref="ObsRecordOutput.CurrentFile"/>, <see cref="ObsReplayBufferOutput.LastSavedPath"/>) and that OBS Studio is exiting (<see cref="IsExiting"/>) are told when they happen, in front of
        /// the events that go with them (<see cref="ObsRecordOutput.FileChanged"/>, <see cref="ObsReplayBufferOutput.Saved"/>, <see cref="ExitStarted"/>), which are told at once as well.
        /// </para>
        /// <para>
        /// The PropertyChanged events are not lost in that time. The objects that you hold are brought in line with OBS Studio as it is read, and when the model
        /// is complete each of them raises PropertyChanged once for each property that changed (the <see cref="ObsInput.IsMuted"/> of an input that was muted while
        /// the connection was down, the <see cref="ObsSource.Name"/> of a scene that was renamed, the <see cref="ObsSource.IsRemoved"/> of an input that was removed,
        /// and the <see cref="Scenes"/> and <see cref="Inputs"/> of the <see cref="ObsModel"/> itself). The first time that the model is complete, only the
        /// <see cref="ObsModel"/> does: the objects are new to you then. There is no record in <see cref="Changed"/> for any of these:
        /// <see cref="Resynchronized"/> comes after them, and says that you should read again what you hold. When more properties changed than may be kept
        /// meanwhile (see <see cref="ObsModelOptions.MaxPendingNotifications"/>), each object raises PropertyChanged once with an empty property name instead,
        /// which says that all of its properties changed.
        /// </para>
        /// <para>
        /// There is one record for each change of a value or of a list, not one for each object that the change moves: when a scene item is moved, the
        /// record is a change of <see cref="ObsScene.Items"/> of its scene; the <see cref="ObsSceneItem.Index"/> of the items that moved are announced by their PropertyChanged events.
        /// </para>
        /// </remarks>
        public event EventHandler<ObsChangedEventArgs>? Changed;

        /// <summary>
        /// Occurs when the model has been read completely: after the first connection, after a connection that was lost is restored, and after OBS Studio changed its
        /// scene collection. Everything that you hold may have changed (scenes and inputs that were added, removed or renamed while the connection was down), so read what you
        /// need again. When OBS Studio changes its scene collection every scene, input and scene item that you hold is <see cref="ObsObjectState.Gone"/>: they belong to the
        /// collection that was left, and what is in the new one is new objects, also when a scene of the same name (or UUID) is in both. The same happens when the connection
        /// is lost while OBS Studio is changing its scene collection and is restored afterwards: the model cannot tell whether OBS Studio went to another collection or came
        /// back to the same one, so it takes it for a change. It is
        /// also raised when notifications were dropped because your handlers were too slow (see <see cref="ObsModelOptions.MaxPendingNotifications"/>).
        /// </summary>
        /// <remarks>
        /// It is raised after the PropertyChanged events of what changed while the model was read (see <see cref="Changed"/>), so an application that is bound
        /// to the objects is told about each of those first. Like the other notifications it is raised on a thread of its own (or on
        /// <see cref="ObsModelOptions.NotificationContext"/>), so the one of the first connection can come after <see cref="ConnectAsync"/> has returned: add your
        /// handler before you connect.
        /// </remarks>
        public event EventHandler? Resynchronized;

        /// <summary>
        /// Gets the <see cref="ObsClient"/> that the model is built on. Every request and every event of OBS Studio is available through it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use it to send the requests that this class does not offer yet. The model sees what they do through the events of OBS Studio, as it does for what any other
        /// program does. Do not connect or disconnect it yourself: see <see cref="ConnectAsync"/> and <see cref="DisconnectAsync"/>. The client switches
        /// <see cref="ObsClient.AutoReconnect"/> off by itself when OBS Studio ends the session on purpose (the Kick button of its session list): the
        /// <see cref="State"/> is then <see cref="ObsModelState.Disconnected"/>, and you can connect again.
        /// </para>
        /// <para>
        /// While OBS Studio changes its scene collection it answers every request with 207 (not ready). The client repeats a single request that is answered so (see
        /// <see cref="ObsClient.MaxRequestRetries"/> and <see cref="ObsClient.RequestRetryInterval"/>); it does not repeat a batch: the results of a batch that was sent in that
        /// time say 207, and it is for you to look at them and to send the batch again. A request that changes something and was held up that way is carried out in the
        /// collection that OBS Studio has loaded when it is ready again, not in the one that you were looking at when you sent it. And the requests that change
        /// the collection (SetCurrentSceneCollection and CreateSceneCollection) are answered when OBS Studio is done, which takes longer than the default
        /// <see cref="ObsClient.RequestTimeout"/>: raise it before you send them, or the call throws a <see cref="TimeoutException"/> while OBS Studio switches all the same.
        /// </para>
        /// </remarks>
        public ObsClient Client => this._client;

        /// <summary>
        /// Gets the state of the model: whether there is a connection, and whether the model is complete. After the <see cref="ObsModel"/> is disposed of it
        /// is <see cref="ObsModelState.Disconnected"/>.
        /// </summary>
        public ObsModelState State => this._disposed ? ObsModelState.Disconnected : this._state;

        /// <summary>
        /// Gets whether the values that the model holds may be out of date: the model was complete once, and is not now (the connection is lost, or the model is
        /// being read again, or the <see cref="ObsModel"/> is disposed of). The values are the last that were known. Before the first connection there are no
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

        /// <summary>Gets every input that OBS Studio still has: <see cref="Inputs"/>, and the ones that were removed and are kept alive for a scene item that shows them.</summary>
        internal IReadOnlyList<ObsInput> AllInputs => this._view.AllInputs;

        /// <summary>
        /// Gets the program scene: the scene that is on the air. It is null until it is known.
        /// </summary>
        /// <remarks>
        /// While a transition runs (<see cref="ObsTransitions.IsRunning"/>) it is the scene that OBS Studio switches to: it says so as soon as the transition starts, and the scene is on the air when the transition has ended.
        /// </remarks>
        public ObsScene? ProgramScene => this._view.ProgramScene;

        /// <summary>
        /// Gets the preview scene: the scene that studio mode shows next to the program scene, and that becomes the program scene when the transition is triggered
        /// (<see cref="ObsTransitions.TriggerAsync"/>). It is null while studio mode is off (<see cref="IsStudioModeEnabled"/>).
        /// </summary>
        public ObsScene? PreviewScene => this._view.PreviewScene;

        /// <summary>
        /// Gets whether studio mode is on. <see cref="SetStudioModeEnabledAsync"/> switches it.
        /// </summary>
        public bool IsStudioModeEnabled => this._view.StudioMode;

        /// <summary>
        /// Gets the scene transitions: the current one, how long it takes, whether one is running, and the calls that change them. It is the same object for as long as the <see cref="ObsModel"/> lives.
        /// </summary>
        public ObsTransitions Transitions { get; }

        /// <summary>
        /// Gets the outputs: the stream, the recording, the virtual camera and the replay buffer. They are the same objects for as long as the <see cref="ObsModel"/> lives.
        /// </summary>
        public ObsOutputs Outputs { get; }

        /// <summary>
        /// Gets the profiles of OBS Studio: which one is current, their names, and the calls that switch to one, make one and remove one. It is the same object for as long as the <see cref="ObsModel"/> lives.
        /// </summary>
        public ObsProfiles Profiles { get; }

        /// <summary>
        /// Gets the scene collections of OBS Studio: which one is loaded, their names, and the calls that load one and make one. It is the same object for as long as the <see cref="ObsModel"/> lives.
        /// </summary>
        public ObsSceneCollections SceneCollections { get; }

        internal bool IgnoreCase => this._options.IgnoreCase;

        // What the model follows of what OBS Studio sends very often.
        internal ObsHighVolume HighVolume => this._options.HighVolume;

        // The host, for the tests that bring about an order in which things reach it, or make it fail.
        internal ModelHost Host => this._host;

        // The line that the changes of the application go through (ObsSceneItem.SetEnabledAsync, and so on).
        internal ModelWriter Writer => this._writer;

        // How long a disconnect and a stop wait for the model and the client. For the tests.
        internal TimeSpan DisconnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

        // Called when a call of ConnectAsync that failed is about to end the connection that it made. For the tests: the order in which a newer call can meet it.
        internal Action? BeforeAbandon { get; set; }

        // Called by ConnectAsync after the client has closed what it had, and before the call becomes the one that the model waits for. For the tests: what can
        // happen to a call at that point.
        internal Action? BeforeBegin { get; set; }

        // Called by ConnectAsync when everything is ready, and before it asks the client to connect. For the tests: what can happen to a call at that point.
        internal Action? BeforeConnect { get; set; }

        // Checks the options before a client is made for them, so that a refusal leaves nothing behind that has to be disposed of.
        private static ObsClient NewClient(ObsModelOptions? options, ILoggerFactory? loggerFactory)
        {
            if (options is not null)
            {
                Validate(options);
            }

            return new ObsClient(loggerFactory?.CreateLogger<ObsClient>());
        }

        private static void Validate(ObsModelOptions options)
        {
            ValidateTimeout(options.ReadTimeout, nameof(options.ReadTimeout));
            ValidateTimeout(options.SynchronizationTimeout, nameof(options.SynchronizationTimeout));
            ValidateTimeout(options.ConfirmationTimeout, nameof(options.ConfirmationTimeout));
            ValidateTimeout(options.WriteTimeout, nameof(options.WriteTimeout));
            ValidateTimeout(options.OutputStepTimeout, nameof(options.OutputStepTimeout));
            ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxPendingNotifications, 1, nameof(options.MaxPendingNotifications));
            if ((options.HighVolume & ~ObsHighVolume.All) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.HighVolume), options.HighVolume, "HighVolume has a flag that does not exist.");
            }
        }

        // A timeout is waited for in whole milliseconds, by a wait that cannot be longer than int.MaxValue of them (about 24.8 days): a time that is not in that range
        // would fail later (after the connection was made), or be taken for no time at all.
        private static void ValidateTimeout(TimeSpan timeout, string name)
        {
            if (timeout < TimeSpan.FromMilliseconds(1) || timeout > LongestTimeout)
            {
                throw new ArgumentOutOfRangeException(name, timeout, "A timeout must be between 1 millisecond and 24 days.");
            }
        }

        /// <summary>
        /// Connects to OBS Studio, and reads its scenes, inputs and scene items. The task completes when the model is complete.
        /// </summary>
        /// <param name="hostname">The name or address of the computer that OBS Studio runs on.</param>
        /// <param name="port">The port of the WebSocket server of OBS Studio.</param>
        /// <param name="password">The password of the WebSocket server, or empty when it has none.</param>
        /// <param name="cancellationToken">A token that ends the attempt.</param>
        /// <exception cref="InvalidOperationException">The model is connected or connecting already.</exception>
        /// <exception cref="ObjectDisposedException">The <see cref="ObsModel"/> is disposed.</exception>
        /// <exception cref="ObsNotConnectedException">OBS Studio could not be reached, refused the password, or the connection ended before the model was complete.</exception>
        /// <exception cref="ObsVersionNotSupportedException">OBS Studio is older than 30.1.</exception>
        /// <exception cref="TimeoutException">The model was not complete within <see cref="ObsModelOptions.SynchronizationTimeout"/>.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled, or <see cref="DisconnectAsync"/> was called, or the <see cref="ObsModel"/> was disposed, while it was connecting.</exception>
        /// <remarks>
        /// When it fails, nothing is left connected, and nothing keeps trying. After it succeeds, the connection is restored when it is lost
        /// (<see cref="ObsModelOptions.AutoReconnect"/>), and the model is read again.
        /// </remarks>
        public async Task ConnectAsync(string hostname = "localhost", int port = 4455, string password = "", CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            // What ends the call from outside: the token of the caller, and DisconnectAsync and Dispose, which cancel it while the call is under way.
            using var connecting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (!this.TryEnterConnecting(connecting, out var ticket, out var call))
            {
                throw new InvalidOperationException($"The model is {this._state}: connect when it is Disconnected.");
            }

            try
            {
                // The model is told that a connection ended a little before the client says that it is closed: a connection that ended just now is
                // allowed to finish closing. What is still not closed after that is somebody else's connection. A call of DisconnectAsync, or a disposal,
                // ends the wait.
                ModelHost.ConnectAttempt attempt;
                try
                {
                    await this.WaitForClientDisconnectedAsync(ClosingTimeout, connecting.Token).ConfigureAwait(false);
                    if (this._client.ConnectionState != ConnectionState.Disconnected)
                    {
                        throw new InvalidOperationException($"The client is {this._client.ConnectionState}: connect when it is Disconnected, and let the ObsModel connect it.");
                    }

                    this.BeforeBegin?.Invoke();

                    // The call becomes the one that the model waits for in one step with the check that it still has the connection: it can have been ended while
                    // it waited for the client, and the application can have connected again meanwhile. The attempt of that newer call is not replaced.
                    lock (this._stateLock)
                    {
                        if (this._connectTicket != ticket || connecting.IsCancellationRequested || this._disposed)
                        {
                            throw new OperationCanceledException(connecting.Token);
                        }

                        attempt = this._host.BeginConnect();
                    }
                }
                catch
                {
                    this.ReleaseConnecting(ticket);
                    throw;
                }

                try
                {
                    this.BeforeConnect?.Invoke();

                    // A call of DisconnectAsync, or a disposal, that came before the client was asked to connect: nothing is connected then.
                    connecting.Token.ThrowIfCancellationRequested();
                    ObjectDisposedException.ThrowIf(this._disposed, this);
                    var events = ModelHost.RequiredEvents | ModelHost.SubscriptionsFor(this._options.HighVolume) | this._options.AdditionalEvents;

                    // The client ends the attempt when this token is cancelled (a connection that the client has not made yet is not made), at once, whenever that is: it
                    // looks at the token when it activates its session, so a call of DisconnectAsync or Dispose cannot miss the session of a call that is about to start one.
                    var connected = await this._client.ConnectAsync(this._options.AutoReconnect, password, hostname, port, events, connecting.Token).ConfigureAwait(false);
                    if (!connected)
                    {
                        connecting.Token.ThrowIfCancellationRequested();
                        throw new ObsNotConnectedException($"Could not connect to OBS Studio at {hostname}:{port}. Check that OBS Studio is running, that its WebSocket server is switched on, and the address, the port and the password.");
                    }

                    await attempt.Task.WaitAsync(this._options.SynchronizationTimeout, connecting.Token).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    this.AbandonConnect(ticket, call);
                    throw new TimeoutException($"OBS Studio did not deliver its scenes and inputs within {this._options.SynchronizationTimeout}. The connection was closed.");
                }
                catch
                {
                    this.AbandonConnect(ticket, call);
                    throw;
                }
                finally
                {
                    this._host.EndConnect(attempt);
                }
            }
            finally
            {
                this.ForgetConnecting(call);
            }
        }

        /// <summary>
        /// Closes the connection to OBS Studio. The model keeps the last values that it knew (<see cref="IsStale"/>). The task completes when the connection is closed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The <see cref="ObsModel"/> is disposed.</exception>
        /// <exception cref="TimeoutException">The connection was not closed within ten seconds, or the call of <see cref="ConnectAsync"/> that this ended did not end within that time.</exception>
        /// <remarks>
        /// A call of <see cref="ConnectAsync"/> that is under way is ended: it throws an <see cref="OperationCanceledException"/>. This returns when that call has ended, so
        /// that <see cref="ConnectAsync"/> can be called again as soon as it has returned.
        /// </remarks>
        public async Task DisconnectAsync()
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            var callEnded = this.CancelConnecting();
            this._client.Disconnect();
            await this._host.WaitUntilDisconnectedAsync(this.DisconnectTimeout).ConfigureAwait(false);
            await this.WaitForClientDisconnectedAsync(this.DisconnectTimeout).ConfigureAwait(false);
            if (callEnded is not null)
            {
                try
                {
                    await callEnded.WaitAsync(this.DisconnectTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    throw new TimeoutException($"The call of ConnectAsync that was under way did not end within {this.DisconnectTimeout}.");
                }
            }

            if (this._client.ConnectionState != ConnectionState.Disconnected)
            {
                throw new TimeoutException($"The connection to OBS Studio was not closed within {this.DisconnectTimeout}.");
            }
        }

        /// <summary>
        /// Waits until the model has applied everything that has arrived and has nothing left to read.
        /// </summary>
        /// <param name="timeout">How long to wait, or null (or <see cref="Timeout.InfiniteTimeSpan"/>) to wait for as long as it takes. It can be up to 24 days.</param>
        /// <param name="cancellationToken">A token that ends the wait.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative, or longer than 24 days.</exception>
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
            if (timeout is { } given && given != Timeout.InfiniteTimeSpan && (given < TimeSpan.Zero || given > LongestTimeout))
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The timeout must be between zero and 24 days, or Timeout.InfiniteTimeSpan.");
            }

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
        /// Ends the connection and stops the model. Disposes of the <see cref="ObsClient"/> when this <see cref="ObsModel"/> made it.
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
        /// Ends the connection and stops the model. Disposes of the <see cref="ObsClient"/> when this <see cref="ObsModel"/> made it.
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
                this._logger.LogWarning("The model did not stop within {Timeout}.", this.DisconnectTimeout);
            }
        }

        // Called by the model, on its own thread.
        internal void SetView(ModelView view)
        {
            this._view = view;
        }

        // Called by the model, and by ConnectAsync and DisconnectAsync.
        internal void SetState(ObsModelState state)
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
                if (state == ObsModelState.Synchronized)
                {
                    this._everSynchronized = true;
                }

                this.QueueNotice(new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, null, nameof(this.State), old, state), this, nameof(this.State)));
                if (this.IsStaleIn(state) != wasStale)
                {
                    this.QueueNotice(new ModelNotice(null, this, nameof(this.IsStale)));
                }
            }
        }

        private bool IsStaleIn(ObsModelState state)
        {
            return this._everSynchronized && state != ObsModelState.Synchronized;
        }

        // Called by the model: changes the state only when it is what the model saw. A call of ConnectAsync owns the state while it is under way, and what the
        // model decided about an older connection must not take it away.
        internal bool TrySetState(ObsModelState expected, ObsModelState state)
        {
            lock (this._stateLock)
            {
                if (this._state != expected)
                {
                    return false;
                }

                this.SetState(state);
                return true;
            }
        }

        // Whether the model has been complete at any time: before that, the objects are new to the application.
        internal bool EverSynchronized
        {
            get
            {
                lock (this._stateLock)
                {
                    return this._everSynchronized;
                }
            }
        }

        internal void QueueNotice(ModelNotice notice)
        {
            this._notifier.Enqueue(() => this.RaiseNotice(notice));
        }

        internal void QueueResynchronized()
        {
            this._notifier.Enqueue(this.RaiseResynchronized);
        }

        // More changed while the model was read than the notifications that are held for the application can be (see ModelNoticeHold): it is told once that
        // everything may have changed, when the model is complete.
        internal void QueueEverything(bool onlyModel)
        {
            this._notifier.Enqueue(() => this.RaiseEverything(onlyModel));
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

        // Whether a scene that OBS Studio removed and keeps alive keeps its name taken (see _removedScenesKeepTheirName).
        internal bool RemovedScenesKeepTheirName => this._removedScenesKeepTheirName;

        // Called by the loop of the model when a connection starts (with null: what the connection before it said about its OBS Studio says nothing about this one) and when it says which version of
        // OBS Studio it is connected to (null when the version can not be read): without a version the newest behaviour is assumed.
        internal void SetObsVersion(Version? version)
        {
            this._removedScenesKeepTheirName = version is not null && version < ScenesFreeTheirNameFrom;
        }

        // The group of the moment that has the name: a group is a scene that the collection of scenes does not list, and its name is taken like that of any source.
        internal ObsScene? FindLiveGroup(string name)
        {
            foreach (var scene in this._view.AllScenes)
            {
                if (scene.IsGroup && scene.State == ObsObjectState.Live && string.Equals(scene.Name, name, StringComparison.Ordinal))
                {
                    return scene;
                }
            }

            return null;
        }

        // The scene items that show a scene or an input that OBS Studio removed and keeps alive (Detached), the name of which is given: while they exist, so does the source, with
        // its name. An input keeps its name until it is destroyed; a scene keeps it only with OBS Studio 31.0 and older (libobs 31.1 takes the name of a scene out of the list of its canvas when it
        // is removed, and every version of obs-websocket looks scene names up there or in the sources that it lists). They are the items of one source: when a scene and an input have the name (OBS Studio does not allow it, and the
        // model can show both for a moment), the one of the kind that is preferred.
        internal IReadOnlyList<ObsSceneItem> FindItemsOfRemovedSource(string name, bool preferScene)
        {
            var scenesKeepTheirName = this.RemovedScenesKeepTheirName;
            List<ObsSceneItem>? found = null;
            foreach (var scene in this._view.AllScenes)
            {
                foreach (var item in scene.Items)
                {
                    if (item.Source is { State: ObsObjectState.Detached } source && (scenesKeepTheirName || source is not ObsScene) && string.Equals(source.Name, name, StringComparison.Ordinal))
                    {
                        (found ??= []).Add(item);
                    }
                }
            }

            if (found is null)
            {
                return [];
            }

            var chosen = found.Select(item => item.Source).FirstOrDefault(source => (source is ObsScene) == preferScene) ?? found[0].Source;
            return found.Where(item => ReferenceEquals(item.Source, chosen)).ToArray();
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

        // Enters Connecting for a call of ConnectAsync, which is then the call that can be ended from outside. Under the lock of the state, so that this is
        // one step with a disposal (a model that is disposed of is never made to connect) and with another call of ConnectAsync. The state is Connecting until the
        // call has unwound (it is the call that says Disconnected, when it fails), so no other call can enter before that.
        private bool TryEnterConnecting(CancellationTokenSource connecting, out int ticket, out ConnectCall call)
        {
            lock (this._stateLock)
            {
                ObjectDisposedException.ThrowIf(this._disposed, this);
                if (this._state != ObsModelState.Disconnected)
                {
                    ticket = 0;
                    call = null!;
                    return false;
                }

                ticket = ++this._connectTicket;
                call = new ConnectCall(connecting);
                this._call = call;
                this.SetState(ObsModelState.Connecting);
                return true;
            }
        }

        // The call of ConnectAsync that is under way, if there is one, ends: a connection that it has not made yet is not made. Returns a task that completes when
        // the call has unwound, or null when there was none.
        private Task? CancelConnecting()
        {
            ConnectCall? call;
            lock (this._stateLock)
            {
                call = this._call;
            }

            if (call is null)
            {
                return null;
            }

            try
            {
                call.Source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The call has ended, and has disposed of its source.
            }

            return call.Ended;
        }

        // The call of ConnectAsync ended: it can no longer be ended from outside (and its source is disposed of by the call, not before), and whoever waits for it
        // to unwind (DisconnectAsync) is told.
        private void ForgetConnecting(ConnectCall call)
        {
            lock (this._stateLock)
            {
                if (ReferenceEquals(this._call, call))
                {
                    this._call = null;
                }
            }

            call.End();
        }

        // A call of ConnectAsync ended before it connected anything.
        private void ReleaseConnecting(int ticket)
        {
            lock (this._stateLock)
            {
                if (this._connectTicket == ticket)
                {
                    this.SetState(ObsModelState.Disconnected);
                }
            }
        }

        // The client says that it is disconnected when the last of its connection is closed, which is a little after the receive loop ended and the
        // model was told. Returns when it says so, or after the time: the caller looks at the state. A token that ends first ends the wait with an
        // OperationCanceledException.
        internal async Task WaitForClientDisconnectedAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
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
                    await closed.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
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
        // has the connection may end it (the ticket: a call cannot start while another one has the state, but the check costs nothing and the end is not undone by
        // a call that was left behind). The check and the end are one step with the entry of a call. The call is over for everybody else when it has ended what it
        // connected: the notification of the client about that is raised after that, and after the lock is let go of, because a handler is the application's, and
        // can wait for something that needs the call to be over (the DisconnectAsync of another thread, for example).
        private void AbandonConnect(int ticket, ConnectCall call)
        {
            this.BeforeAbandon?.Invoke();
            Action? raise = null;
            lock (this._stateLock)
            {
                if (this._connectTicket == ticket)
                {
                    try
                    {
                        raise = this._client.DisconnectDeferred();
                    }
                    catch (ObjectDisposedException)
                    {
                    }

                    this.SetState(ObsModelState.Disconnected);
                }

                if (ReferenceEquals(this._call, call))
                {
                    this._call = null;
                }
            }

            call.End();
            raise?.Invoke();
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
            // The changes that wait are not sent, and the ones that are under way are given up, before the connection ends: otherwise a change that waits could be sent to a
            // connection that is closing, and say that it is not connected, when what happened is that the model was disposed of.
            this._writer.Stop();
            this.CancelConnecting();
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

        private void RaiseNotice(ModelNotice notice)
        {
            if (notice.Args is { } args)
            {
                this.RaiseChanged(args);
            }

            if (notice.Raise is { } raise)
            {
                // Something that happened: the event of the model or of the object that it is about. A handler that throws is logged (see Raise).
                raise();
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
                    case ObsFilter filter:
                        filter.RaisePropertyChanged(property);
                        break;
                    case ObsMedia media:
                        media.RaisePropertyChanged(property);
                        break;
                    case ObsTransitions transitions:
                        transitions.RaisePropertyChanged(property);
                        break;
                    case ObsProfiles profiles:
                        profiles.RaisePropertyChanged(property);
                        break;
                    case ObsSceneCollections collections:
                        collections.RaisePropertyChanged(property);
                        break;
                    case ObsOutput output:
                        output.RaisePropertyChanged(property);
                        break;
                    case ObsModel:
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

        // Notifications were dropped because the handlers of the application were too slow (see ObsModelOptions.MaxPendingNotifications): everything may
        // have changed. A PropertyChanged event without a property name says that to what is bound to an object, and Resynchronized says it to the rest.
        private void RaiseEverything()
        {
            this.RaiseEverything(onlyModel: false);
        }

        // The same for the notifications that were held while the model was read, when there were more than could be held. The first time that the model is read
        // the objects are new to the application, so only the model itself, which it can be bound to, says that everything changed.
        private void RaiseEverything(bool onlyModel)
        {
            this.Raise(this.PropertyChanged, this, string.Empty);

            // The outputs are the same objects before and after, and the application can hold them from the start: they say it whatever the model does.
            foreach (var output in this.Outputs)
            {
                output.RaisePropertyChanged(string.Empty);
            }

            this.Transitions.RaisePropertyChanged(string.Empty);
            this.Profiles.RaisePropertyChanged(string.Empty);
            this.SceneCollections.RaisePropertyChanged(string.Empty);

            if (!onlyModel)
            {
                var view = this._view;

                // The inputs of the view, and then the inputs that OBS removed and keeps alive for the scene items that show them: they are not among the inputs
                // of the view, and the application can hold them through ObsSceneItem.Source.
                var inputs = new List<ObsInput>(view.Inputs);
                var raised = new HashSet<ObsInput>(inputs, ReferenceEqualityComparer.Instance);
                foreach (var scene in view.AllScenes)
                {
                    scene.RaisePropertyChanged(string.Empty);
                    RaiseFilters(scene);
                    foreach (var item in scene.Items)
                    {
                        item.RaisePropertyChanged(string.Empty);
                        if (item.Source is ObsInput shown && raised.Add(shown))
                        {
                            inputs.Add(shown);
                        }
                    }
                }

                foreach (var input in inputs)
                {
                    input.RaisePropertyChanged(string.Empty);
                    input.Media?.RaisePropertyChanged(string.Empty);
                    RaiseFilters(input);
                }
            }

            this.RaiseResynchronized();
        }

        // The filters are objects that the application can be bound to, like the source that has them: they say that everything changed when it does.
        private static void RaiseFilters(ObsSource source)
        {
            foreach (var filter in source.Filters)
            {
                filter.RaisePropertyChanged(string.Empty);
            }
        }

        // A call of ConnectAsync that is under way.
        private sealed class ConnectCall(CancellationTokenSource source)
        {
            private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

            // What ends the call from outside.
            public CancellationTokenSource Source { get; } = source;

            // Completes when the call has unwound.
            public Task Ended => this._ended.Task;

            public void End()
            {
                this._ended.TrySetResult();
            }
        }
    }
}
