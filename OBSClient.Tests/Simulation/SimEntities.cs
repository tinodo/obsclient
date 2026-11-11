namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;

    /// <summary>
    /// A source of OBS Studio: a scene or an input. Sources have a UUID (scenes and inputs share one namespace) and a name that is case
    /// sensitive, and they are reference counted (see <see cref="References"/>).
    /// </summary>
    internal abstract class SimSource
    {
        protected SimSource(Guid uuid, string name, long serial)
        {
            Uuid = uuid;
            UuidText = uuid.ToString("D");
            Name = name;
            Serial = serial;
        }

        /// <summary>Gets the UUID of the source.</summary>
        public Guid Uuid { get; }

        /// <summary>Gets the UUID as OBS Studio writes it: lower case, with hyphens.</summary>
        public string UuidText { get; }

        /// <summary>Gets or sets the name of the source.</summary>
        public string Name { get; set; }

        /// <summary>Gets the number of the source in the order of creation.</summary>
        public long Serial { get; }

        /// <summary>Gets or sets the number of scene items, in any scene (live or detached), that use the source.</summary>
        public int References { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the source was removed. A removed source that is still alive (because something refers to
        /// it) stays visible to everything that refers to it.
        /// </summary>
        public bool Removed { get; set; }

        /// <summary>Gets or sets a value indicating whether OBS Studio destroyed the source. Nothing can address it any more.</summary>
        public bool Destroyed { get; set; }

        /// <summary>Gets a value indicating whether the source is a scene.</summary>
        public abstract bool IsScene { get; }
    }

    /// <summary>A scene item: a place of a source in a scene.</summary>
    internal sealed class SimItem
    {
        public SimItem(int id, SimScene scene, SimSource source)
        {
            Id = id;
            Scene = scene;
            Source = source;
        }

        /// <summary>Gets the ID of the item. It is unique in its scene, and is never used again there.</summary>
        public int Id { get; }

        /// <summary>Gets the scene that the item is in.</summary>
        public SimScene Scene { get; }

        /// <summary>Gets the source that the item shows.</summary>
        public SimSource Source { get; }

        /// <summary>Gets or sets the index of the item in its scene. The item at 0 is at the bottom.</summary>
        public int Index { get; set; }

        /// <summary>Gets or sets a value indicating whether the item is enabled (visible).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the item is locked.</summary>
        public bool Locked { get; set; }
    }

    /// <summary>
    /// A scene. It is alive until it is removed and nothing refers to it any more: a removed scene that is an item of another scene stays
    /// alive, detached from the list of scenes (fact 7).
    /// </summary>
    internal sealed class SimScene : SimSource
    {
        public SimScene(Guid uuid, string name, long serial)
            : base(uuid, name, serial)
        {
        }

        /// <summary>Gets the items of the scene, in the order of their index (the first is at the bottom).</summary>
        public List<SimItem> Items { get; } = [];

        /// <summary>Gets or sets the ID that the next item of the scene gets.</summary>
        public int NextItemId { get; set; } = 1;

        /// <summary>Gets or sets the virtual time at which the scene was created.</summary>
        public TimeSpan CreatedAt { get; set; }

        /// <summary>Gets or sets the virtual time at which the scene was removed, or null while it is not.</summary>
        public TimeSpan? RemovedAt { get; set; }

        /// <summary>Gets or sets the lag of the list of scenes for this scene (fact 4).</summary>
        public TimeSpan Lag { get; set; }

        /// <summary>Gets a value indicating whether the scene is in the list of scenes (it was not removed).</summary>
        public bool IsLive => !Removed;

        /// <inheritdoc />
        public override bool IsScene => true;
    }

    /// <summary>
    /// An input. It is alive while a scene item refers to it or it is pinned: a pinned input has no scene items and is kept alive by the
    /// pin, as OBS Studio keeps its global audio devices (Desktop Audio) alive.
    /// </summary>
    internal sealed class SimInput : SimSource
    {
        public SimInput(Guid uuid, string name, long serial, SimInputKind kind, JsonObject settings)
            : base(uuid, name, serial)
        {
            Kind = kind;
            Settings = settings;
        }

        /// <summary>Gets the kind of the input.</summary>
        public SimInputKind Kind { get; }

        /// <summary>Gets or sets the settings that were given to the input (without the defaults), with the fields in the order of obs-websocket's JSON (sorted).</summary>
        public JsonObject Settings { get; set; }

        /// <summary>Gets or sets a value indicating whether the input is muted.</summary>
        public bool Muted { get; set; }

        /// <summary>Gets or sets the volume of the input, as OBS Studio keeps it: a single precision multiplier.</summary>
        public float Volume { get; set; } = 1f;

        /// <summary>Gets or sets the balance of the audio: from 0 (all left) to 1 (all right); 0.5 is the middle.</summary>
        public float Balance { get; set; } = 0.5f;

        /// <summary>Gets or sets the sync offset of the audio, in milliseconds.</summary>
        public int SyncOffset { get; set; }

        /// <summary>Gets or sets the monitor type, as obs-websocket writes it.</summary>
        public string MonitorType { get; set; } = "OBS_MONITORING_TYPE_NONE";

        /// <summary>Gets or sets the audio tracks that the input is mixed into: one bit for each track, track 1 is bit 0. A new input is on all six.</summary>
        public int Tracks { get; set; } = 63;

        /// <summary>Gets or sets what the media of the input does, as obs-websocket writes it. Only a media input moves from none: a media input that is not shown does not open its file until it is played.</summary>
        public string MediaState { get; set; } = "OBS_MEDIA_STATE_NONE";

        /// <summary>Gets or sets where the media is, in milliseconds. The simulation does not move it by itself: a media input that is not shown does not move either (measured with OBS Studio 33.0.0).</summary>
        public int MediaCursor { get; set; }

        /// <summary>Gets or sets the number of pins that keep the input alive without a scene item.</summary>
        public int Pins { get; set; }

        /// <inheritdoc />
        public override bool IsScene => false;
    }

    /// <summary>A request that OBS Studio answers with a status that is not a success. It is thrown by the simulation of a change and caught by whoever asked for it.</summary>
    internal sealed class SimFailure : Exception
    {
        public SimFailure(int code, string comment)
            : base(comment)
        {
            Code = code;
            Comment = comment;
        }

        /// <summary>Gets the code of the status of the request.</summary>
        public int Code { get; }

        /// <summary>Gets the comment of the status of the request.</summary>
        public string Comment { get; }

        public static SimFailure NotFound(string comment) => new(SimStatus.ResourceNotFound, comment);

        public static SimFailure Exists(string comment) => new(SimStatus.ResourceAlreadyExists, comment);
    }

    /// <summary>Thrown by the simulation of a request that a real OBS Studio never answers (fact 9).</summary>
    internal sealed class SimHang : Exception
    {
        public SimHang(string message)
            : base(message)
        {
        }
    }
}
