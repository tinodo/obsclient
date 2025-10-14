namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Output.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Monitor"/> class.
    /// </remarks>
    /// <param name="outputActive">A value indicating whether the output is active.</param>
    /// <param name="outputFlags">The output flags.</param>
    /// <param name="outputHeight">The output height.</param>
    /// <param name="outputKind">The output kind.</param>
    /// <param name="outputName">The output name.</param>
    /// <param name="outputWidth">The output width.</param>
    [method: JsonConstructor]
    public class Output(bool outputActive, Dictionary<string, bool> outputFlags, int outputHeight, string outputKind, string outputName, int outputWidth)
    {
        /// <summary>
        /// Gets a value indicating whether the output is active.
        /// </summary>
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; } = outputActive;

        /// <summary>
        /// Gets the output flags.
        /// </summary>
        [JsonPropertyName("outputFlags")]
        public Dictionary<string, bool> OutputFlags { get; } = outputFlags;

        /// <summary>
        /// Gets the height of the output.
        /// </summary>
        [JsonPropertyName("outputHeight")]
        public int OutputHeight { get; } = outputHeight;

        /// <summary>
        /// Gets the output kind.
        /// </summary>
        [JsonPropertyName("outputKind")]
        public string OutputKind { get; } = outputKind;

        /// <summary>
        /// Gets the output kind.
        /// </summary>
        [JsonPropertyName("outputName")]
        public string OutputName { get; } = outputName;

        /// <summary>
        /// Gets the output width.
        /// </summary>
        [JsonPropertyName("outputWidth")]
        public int OutputWidth { get; } = outputWidth;
    }
}
