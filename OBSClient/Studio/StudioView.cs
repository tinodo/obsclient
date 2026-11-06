namespace OBSStudioClient.Studio
{
    /// <summary>
    /// What an <see cref="ObsStudio"/> shows at one moment: the objects, the program scene and studio mode, as one consistent set. The model
    /// makes a new one after each step that changed something, and swaps it in; readers never see one that is half made.
    /// </summary>
    internal sealed class StudioView
    {
        public StudioView(
            ObsNamedCollection<ObsScene> scenes,
            ObsNamedCollection<ObsInput> inputs,
            ObsScene[] allScenes,
            ObsScene? programScene,
            bool studioMode)
        {
            this.Scenes = scenes;
            this.Inputs = inputs;
            this.AllScenes = allScenes;
            this.ProgramScene = programScene;
            this.StudioMode = studioMode;
        }

        /// <summary>Gets the scenes in the list of scenes of OBS Studio (not the groups, and not the scenes that were removed but are kept alive).</summary>
        public ObsNamedCollection<ObsScene> Scenes { get; }

        /// <summary>Gets the inputs that were not removed, ordered by name.</summary>
        public ObsNamedCollection<ObsInput> Inputs { get; }

        /// <summary>Gets every scene: those of <see cref="Scenes"/> first, then the groups and the scenes that were removed but are kept alive.</summary>
        public ObsScene[] AllScenes { get; }

        /// <summary>Gets the program scene, or null while it is not known.</summary>
        public ObsScene? ProgramScene { get; }

        /// <summary>Gets whether studio mode is on.</summary>
        public bool StudioMode { get; }
    }
}
