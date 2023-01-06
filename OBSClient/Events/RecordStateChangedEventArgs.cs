namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the RecordStateChanged event.
    /// </summary>
    public class RecordStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a value indicating whether the output is active.
        /// </summary>
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; }

        /// <summary>
        /// Gets the <see cref="ObsOutputState"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("outputState")]
        public ObsOutputState OutputState { get; }

        /// <summary>
        /// Gets the output path.
        /// </summary>
        [JsonPropertyName("outputPath")]
        public string OutputPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="outputActive">A value indicating whether the output is active.</param>
        /// <param name="outputState">The <see cref="ObsOutputState"/>.</param>
        /// <param name="outputPath">The output path.</param>
        [JsonConstructor]
        public RecordStateChangedEventArgs(bool outputActive, ObsOutputState outputState, string outputPath)
        {
            this.OutputActive = outputActive;
            this.OutputState = outputState;
            this.OutputPath = outputPath;
        }
    }
}
