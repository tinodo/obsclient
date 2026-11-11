namespace OBSStudioClient.Model
{
    /// <summary>
    /// What a canvas of OBS Studio is, and does (<see cref="ObsCanvas.Flags"/>). They are set when the canvas is made and do not change.
    /// </summary>
    /// <remarks>
    /// obs-websocket says them as five booleans (<c>MAIN</c>, <c>ACTIVATE</c>, <c>MIX_AUDIO</c>, <c>SCENE_REF</c>, <c>EPHEMERAL</c>) that are the flags of libobs; the values here are the bits of libobs, and
    /// are not part of the protocol: test for a flag with <see cref="Enum.HasFlag"/>.
    /// </remarks>
    [Flags]
    public enum ObsCanvasFlags
    {
        /// <summary>
        /// No flag.
        /// </summary>
        None = 0,

        /// <summary>
        /// The main canvas, which libobs makes itself: it can not be renamed, reset or removed. There is one, and it holds the scenes that <see cref="ObsModel.Scenes"/> lists.
        /// </summary>
        Main = 1,

        /// <summary>
        /// The sources of the canvas become active when they are visible.
        /// </summary>
        Activate = 2,

        /// <summary>
        /// The audio of the sources of the canvas is mixed into the audio output of OBS Studio.
        /// </summary>
        MixAudio = 4,

        /// <summary>
        /// The canvas holds a reference to each of its scenes, so that they live as long as it does.
        /// </summary>
        SceneRef = 8,

        /// <summary>
        /// The canvas is not saved with the scene collection.
        /// </summary>
        Ephemeral = 16,
    }
}
