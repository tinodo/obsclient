namespace OBSStudioClient.Model
{
    /// <summary>
    /// The global audio inputs of OBS Studio: the audio devices that it has chosen for all scenes (<em>Settings</em>, <em>Audio</em>, <em>Global Audio Devices</em>). They are inputs of
    /// the model like any other, and are in no scene.
    /// </summary>
    public sealed class ObsSpecialInputs
    {
        internal ObsSpecialInputs(ObsInput? desktop1, ObsInput? desktop2, ObsInput? mic1, ObsInput? mic2, ObsInput? mic3, ObsInput? mic4)
        {
            this.Desktop1 = desktop1;
            this.Desktop2 = desktop2;
            this.Mic1 = mic1;
            this.Mic2 = mic2;
            this.Mic3 = mic3;
            this.Mic4 = mic4;
        }

        /// <summary>
        /// Gets the <em>Desktop Audio</em> input, or null when no device is chosen for it (or the model does not have it).
        /// </summary>
        public ObsInput? Desktop1 { get; }

        /// <summary>
        /// Gets the <em>Desktop Audio 2</em> input, or null when no device is chosen for it.
        /// </summary>
        public ObsInput? Desktop2 { get; }

        /// <summary>
        /// Gets the <em>Mic/Auxiliary Audio</em> input, or null when no device is chosen for it.
        /// </summary>
        public ObsInput? Mic1 { get; }

        /// <summary>
        /// Gets the <em>Mic/Auxiliary Audio 2</em> input, or null when no device is chosen for it.
        /// </summary>
        public ObsInput? Mic2 { get; }

        /// <summary>
        /// Gets the <em>Mic/Auxiliary Audio 3</em> input, or null when no device is chosen for it.
        /// </summary>
        public ObsInput? Mic3 { get; }

        /// <summary>
        /// Gets the <em>Mic/Auxiliary Audio 4</em> input, or null when no device is chosen for it.
        /// </summary>
        public ObsInput? Mic4 { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            var chosen = new[] { this.Desktop1, this.Desktop2, this.Mic1, this.Mic2, this.Mic3, this.Mic4 }.Where(input => input is not null).Select(input => $"'{input!.Name}'");
            return $"Global audio inputs: {string.Join(", ", chosen)}";
        }
    }
}
