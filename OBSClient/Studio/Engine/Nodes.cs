namespace OBSStudioClient.Studio.Engine
{
    /// <summary>
    /// What OBS Studio thinks of a scene or an input. OBS counts references: what is removed while a scene item still uses it stays alive.
    /// </summary>
    internal enum SourceState
    {
        /// <summary>The object exists, and has not been removed.</summary>
        Live,

        /// <summary>OBS removed the object, and keeps it alive because scene items still use it.</summary>
        Detached,

        /// <summary>The object does not exist any more.</summary>
        Gone,
    }

    /// <summary>
    /// The identity of a scene item: the scene (or group) that contains it, and its id, which is unique within that scene and never used again.
    /// </summary>
    /// <param name="Scene">The UUID of the scene or group that contains the item.</param>
    /// <param name="Id">The id of the item.</param>
    internal readonly record struct ItemKey(Guid Scene, int Id);

    /// <summary>
    /// A part of the model that is read with one request, and that can be marked as "needs a read".
    /// </summary>
    internal sealed class ScopeState
    {
        /// <summary>Whether the part needs a read.</summary>
        public bool Dirty;

        /// <summary>Whether a read of the part is under way: there is only one at a time.</summary>
        public bool InFlight;

        /// <summary>The number of the last message that had arrived when the read that is under way was sent.</summary>
        public long SentSeq;

        /// <summary>The time before which the part is not read (a retry waits).</summary>
        public TimeSpan NotBefore;

        /// <summary>How many times in a row the read of the part had to be repeated.</summary>
        public int Attempts;

        /// <summary>Marks the part as needing a read, soon.</summary>
        public void MarkDirty()
        {
            this.Dirty = true;
        }

        /// <summary>Marks the part as needing a read, not before the given time (when it was marked before, the earliest of the times wins).</summary>
        public void MarkDirtyNotBefore(TimeSpan notBefore)
        {
            this.NotBefore = this.Dirty && this.NotBefore < notBefore ? this.NotBefore : notBefore;
            this.Dirty = true;
        }

        /// <summary>Gets whether the part can be read at this moment.</summary>
        public bool IsReady(TimeSpan now)
        {
            return this.Dirty && !this.InFlight && this.NotBefore <= now;
        }
    }

    /// <summary>
    /// A scene or an input.
    /// </summary>
    internal abstract class SourceNode
    {
        /// <summary>The name of the source.</summary>
        public Cell<string> Name;

        protected SourceNode(Guid uuid, string name, long createdSeq, TimeSpan createdAt, bool confirmed)
        {
            this.Uuid = uuid;
            this.Name = confirmed ? Cell<string>.FromRead(name, createdSeq) : new Cell<string>(name, createdSeq);
            this.CreatedSeq = createdSeq;
            this.CreatedAt = createdAt;
            this.Confirmed = confirmed;
        }

        /// <summary>Gets the UUID of the source: scenes and inputs share one namespace.</summary>
        public Guid Uuid { get; }

        /// <summary>Gets or sets the state of the source.</summary>
        public SourceState State { get; set; } = SourceState.Live;

        /// <summary>Gets or sets the number of the message that made the model know the source.</summary>
        public long CreatedSeq { get; set; }

        /// <summary>Gets or sets the time at which the model got to know the source.</summary>
        public TimeSpan CreatedAt { get; set; }

        /// <summary>Gets or sets whether a read that was sent after the model got to know the source lists it.</summary>
        public bool Confirmed { get; set; }

        /// <summary>
        /// Gets or sets whether OBS announced the removal of the source (InputRemoved or SceneRemoved). A scene that is Detached without it was found so by a
        /// read (a list of scenes lacked it, or a scene item showed it): the read is ahead of the events, so the model does not destroy it by counting scene items.
        /// </summary>
        public bool RemovedAnnounced { get; set; }

        /// <summary>
        /// Gets or sets whether the source was removed, and the model does not know yet whether OBS destroyed it or keeps it alive for scene items that it
        /// has not read yet. The source is Detached meanwhile; the scene items decide.
        /// </summary>
        public bool RemovalUnresolved { get; set; }

        /// <summary>
        /// Gets or sets the number of the last read that had been taken when the model started to doubt whether OBS still has a source that it removed (or
        /// that a list does not show), or 0 when it does not doubt. Only a read that is taken later can settle it: it says that OBS has the source (the list
        /// of inputs has it, the scene items of the scene can be read), or that it does not. What the model knows of the scene items can be behind
        /// what OBS has, so it does not settle it by counting them.
        /// </summary>
        public int CheckAfter { get; set; }

        /// <summary>Gets or sets whether the model told that the source exists (an Added change was made).</summary>
        public bool Announced { get; set; }

        /// <summary>Gets whether everything that makes the source visible in the model is known.</summary>
        public abstract bool IsComplete { get; }
    }

    /// <summary>
    /// A scene, or a group (which OBS treats as a scene).
    /// </summary>
    internal sealed class SceneNode : SourceNode
    {
        /// <summary>The position of the scene in the list of scenes: 0 is the first.</summary>
        public Cell<int> Index;

        public SceneNode(Guid uuid, string name, int index, long createdSeq, TimeSpan createdAt, bool confirmed)
            : base(uuid, name, createdSeq, createdAt, confirmed)
        {
            this.Index = confirmed ? Cell<int>.FromRead(index, createdSeq) : new Cell<int>(index, createdSeq);
        }

        /// <summary>Gets or sets whether the scene is a group.</summary>
        public bool IsGroup { get; set; }

        /// <summary>
        /// Gets or sets whether the model only inferred that the scene is Detached (it is the source of a scene item, and no list of scenes has it): a list of
        /// scenes that has it, or its creation event, makes it Live; its removal event makes it a removed scene like any other. Until one of them comes
        /// the model does not know whether OBS has removed it, so it never destroys it by counting its scene items.
        /// </summary>
        public bool Provisional { get; set; }

        /// <summary>Gets the scene items of the scene, by id.</summary>
        public Dictionary<int, ItemNode> Items { get; } = new();

        /// <summary>Gets the state of the read of the scene items.</summary>
        public ScopeState ItemsScope { get; } = new();

        /// <summary>Gets or sets whether the scene items were read at least once.</summary>
        public bool ItemsKnown { get; set; }

        /// <inheritdoc/>
        public override bool IsComplete => true;
    }

    /// <summary>
    /// An input.
    /// </summary>
    internal sealed class InputNode : SourceNode
    {
        /// <summary>The capability flag of an input kind that says that its inputs have audio (OBS_SOURCE_AUDIO).</summary>
        public const uint AudioCapability = 2;

        /// <summary>Whether the input is muted.</summary>
        public Cell<bool> Muted;

        /// <summary>The volume of the input, as a multiplier.</summary>
        public Cell<float> VolumeMul;

        /// <summary>The volume of the input, in decibel.</summary>
        public Cell<float> VolumeDb;

        public InputNode(Guid uuid, string name, string kind, string unversionedKind, uint? caps, long createdSeq, TimeSpan createdAt, bool confirmed)
            : base(uuid, name, createdSeq, createdAt, confirmed)
        {
            this.Kind = kind;
            this.UnversionedKind = unversionedKind;
            this.Caps = caps;
            this.Muted = new Cell<bool>(false, 0);
            this.VolumeMul = new Cell<float>(1f, 0);
            this.VolumeDb = new Cell<float>(0f, 0);
        }

        /// <summary>Gets or sets the kind of the input.</summary>
        public string Kind { get; set; }

        /// <summary>Gets or sets the kind of the input without its version.</summary>
        public string UnversionedKind { get; set; }

        /// <summary>
        /// Gets or sets the capability flags of the kind, or null when OBS does not send them (obs-websocket before 5.6.0 does not). Whether an input has
        /// audio is then found by reading its audio state.
        /// </summary>
        public uint? Caps { get; set; }

        /// <summary>Gets or sets whether OBS refused to read the audio state of the input, which is how it says that the input has no audio. Only used when the capabilities are not sent.</summary>
        public bool NoAudio { get; set; }

        /// <summary>Gets whether the input has audio: its capabilities say so or, when OBS does not send them, until OBS says that it has not.</summary>
        public bool HasAudio => this.Caps is { } caps ? (caps & AudioCapability) != 0 : !this.NoAudio;

        /// <summary>Gets the state of the read of the audio state (mute and volume).</summary>
        public ScopeState AudioScope { get; } = new();

        /// <summary>Gets or sets whether the audio state was read.</summary>
        public bool AudioKnown { get; set; }

        /// <inheritdoc/>
        public override bool IsComplete => !this.HasAudio || this.AudioKnown;
    }

    /// <summary>
    /// A scene item: a source in a scene.
    /// </summary>
    internal sealed class ItemNode
    {
        /// <summary>The position of the item in its scene: 0 is the bottom.</summary>
        public Cell<int> Index;

        /// <summary>Whether the item is visible.</summary>
        public Cell<bool> Enabled;

        /// <summary>Whether the item is locked.</summary>
        public Cell<bool> Locked;

        public ItemNode(SceneNode scene, int id, Guid sourceUuid, int index, long createdSeq, bool fromRead)
        {
            this.Scene = scene;
            this.Id = id;
            this.SourceUuid = sourceUuid;
            this.CreatedSeq = createdSeq;
            this.Index = fromRead ? Cell<int>.FromRead(index, createdSeq) : new Cell<int>(index, createdSeq);
            this.Enabled = new Cell<bool>(true, 0);
            this.Locked = new Cell<bool>(false, 0);
        }

        /// <summary>Gets the scene (or group) that contains the item.</summary>
        public SceneNode Scene { get; }

        /// <summary>Gets the id of the item, which is unique within its scene and never used again.</summary>
        public int Id { get; }

        /// <summary>Gets the UUID of the source of the item, an input or a scene.</summary>
        public Guid SourceUuid { get; }

        /// <summary>Gets the number of the message that made the model know the item.</summary>
        public long CreatedSeq { get; }

        /// <summary>Gets or sets whether enabled and locked are known: an event that creates an item does not carry them.</summary>
        public bool Known { get; set; }

        /// <summary>Gets or sets whether the model told that the item exists (an Added change was made).</summary>
        public bool Announced { get; set; }

        /// <summary>Gets the key of the item.</summary>
        public ItemKey Key => new(this.Scene.Uuid, this.Id);
    }
}
