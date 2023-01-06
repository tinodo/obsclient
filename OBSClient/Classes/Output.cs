namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Output.
    /// </summary>
    public class Output
    {
        /// <summary>
        /// Gets a value indicating whether the output is active.
        /// </summary>
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; }

        /// <summary>
        /// Gets the output flags.
        /// </summary>
        [JsonPropertyName("outputFlags")]
        public Dictionary<string, bool> OutputFlags { get; }

        /// <summary>
        /// Gets the height of the output.
        /// </summary>
        [JsonPropertyName("outputHeight")]
        public int OutputHeight { get; }

        /// <summary>
        /// Gets the output kind.
        /// </summary>
        [JsonPropertyName("outputKind")]
        public string OutputKind { get; }

        /// <summary>
        /// Gets the output kind.
        /// </summary>
        [JsonPropertyName("outputName")]
        public string OutputName { get; }

        /// <summary>
        /// Gets the output width.
        /// </summary>
        [JsonPropertyName("outputWidth")]
        public int OutputWidth { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Monitor"/> class.
        /// </summary>
        /// <param name="outputActive">A value indicating whether the output is active.</param>
        /// <param name="outputFlags">The output flags.</param>
        /// <param name="outputHeight">The output height.</param>
        /// <param name="outputKind">The output kind.</param>
        /// <param name="outputName">The output name.</param>
        /// <param name="outputWidth">The output width.</param>
        [JsonConstructor]
        public Output(bool outputActive, Dictionary<string, bool> outputFlags, int outputHeight, string outputKind, string outputName, int outputWidth)
        {
            this.OutputActive = outputActive;
            this.OutputFlags = outputFlags;
            this.OutputHeight = outputHeight;
            this.OutputKind = outputKind;
            this.OutputName = outputName;
            this.OutputWidth = outputWidth;
        }
    }
}
