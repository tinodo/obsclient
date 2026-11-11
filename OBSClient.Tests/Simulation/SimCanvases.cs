namespace OBSStudioClient.Tests.Simulation
{
    /// <summary>
    /// The flags of a canvas, with the values of libobs (<c>obs_canvas_flags</c> in obs.h of OBS Studio 33), which obs-websocket 5.7.6 reports one by one in
    /// <c>canvasFlags</c>.
    /// </summary>
    internal static class SimCanvasFlags
    {
        /// <summary>The main canvas: the one that OBS Studio always has.</summary>
        public const int Main = 1;

        /// <summary>The canvas renders (<c>ACTIVATE</c>).</summary>
        public const int Activate = 2;

        /// <summary>The canvas mixes the audio of its scenes (<c>MIX_AUDIO</c>).</summary>
        public const int MixAudio = 4;

        /// <summary>The scenes of the canvas keep a reference on their sources (<c>SCENE_REF</c>).</summary>
        public const int SceneRef = 8;

        /// <summary>The canvas is not saved in the scene collection (<c>EPHEMERAL</c>).</summary>
        public const int Ephemeral = 16;

        /// <summary>The flags of the program canvas of libobs: <c>ACTIVATE | MIX_AUDIO | SCENE_REF</c>.</summary>
        public const int Program = Activate | MixAudio | SceneRef;

        /// <summary>The flags of a canvas of a device (libobs: <c>ACTIVATE | EPHEMERAL</c>).</summary>
        public const int Device = Activate | Ephemeral;
    }

    /// <summary>
    /// A canvas of OBS Studio that is not the main one: a plugin makes it (the user interface of OBS Studio 33 has no way to), the scene collection keeps it (unless it is ephemeral), and
    /// obs-websocket announces it with <c>CanvasCreated</c>, <c>CanvasNameChanged</c> and <c>CanvasRemoved</c>.
    /// </summary>
    internal sealed class SimCanvas
    {
        public SimCanvas(Guid uuid, string name, int flags, SimVideo? video)
        {
            Uuid = uuid;
            UuidText = uuid.ToString("D");
            Name = name;
            Flags = flags;
            Video = video;
        }

        /// <summary>Gets the UUID of the canvas. It never changes, and a saved canvas has the same one when its collection is loaded again.</summary>
        public Guid Uuid { get; }

        /// <summary>Gets the UUID as OBS Studio writes it: lower case, with hyphens.</summary>
        public string UuidText { get; }

        /// <summary>Gets or sets the name of the canvas.</summary>
        public string Name { get; set; }

        /// <summary>Gets the flags of the canvas (see <see cref="SimCanvasFlags"/>). They never change.</summary>
        public int Flags { get; }

        /// <summary>Gets the video settings of the canvas, which only a canvas that has a video mix has: the six fields are null for the others.</summary>
        public SimVideo? Video { get; }

        /// <summary>Gets or sets a value indicating whether the canvas was removed (<c>obs_canvas_remove</c>) and is still alive: something holds a reference on it.</summary>
        public bool Removed { get; set; }

        /// <summary>
        /// Gets or sets the time until which the canvas is still listed after it was removed (see <see cref="SimQuirks.CanvasRemovedLinger"/>). A canvas that was removed and is not held is destroyed at once,
        /// and is not in the simulation any more.
        /// </summary>
        public TimeSpan? ListedUntil { get; set; }

        /// <summary>Gets a value indicating whether the canvas is the main one.</summary>
        public bool IsMain => (Flags & SimCanvasFlags.Main) != 0;
    }

    /// <summary>A canvas as the snapshot of the simulation has it: what the model keeps of it, which is everything that never changes and the name.</summary>
    /// <param name="Uuid">The UUID of the canvas.</param>
    /// <param name="Name">The name of the canvas.</param>
    /// <param name="Flags">The flags of the canvas.</param>
    internal sealed record SimCanvasSnapshot(Guid Uuid, string Name, int Flags);

    /// <summary>
    /// A canvas as OBS Studio saves it in the file of a scene collection: the <c>info</c> of an entry of <c>canvases</c>, which has the name, the UUID and the flags. The video mix is not saved: a canvas that is
    /// loaded has none until a plugin makes one, so its video settings are null (read from <c>obs_load_canvas</c> in OBS Studio 33).
    /// </summary>
    /// <param name="Uuid">The UUID of the canvas.</param>
    /// <param name="Name">The name of the canvas.</param>
    /// <param name="Flags">The flags of the canvas.</param>
    internal sealed record SimSavedCanvas(Guid Uuid, string Name, int Flags);
}
