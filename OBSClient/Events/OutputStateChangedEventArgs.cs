namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the StreamStateChanged, ReplayBufferChanged and VirtualcamStateChanged events.
    /// </summary>
    public class OutputStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a value indicating whether the output is active.
        /// </summary>
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; }

        /// <summary>
        /// Gets the <see cref="OutputState"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("outputState")]
        public ObsOutputState OutputState { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="outputActive">A value indicating whether the output is active.</param>
        /// <param name="outputState">The <see cref="OutputState"/>.</param>
        [JsonConstructor]
        public OutputStateChangedEventArgs(bool outputActive, ObsOutputState outputState)
        {
            this.OutputActive = outputActive;
            this.OutputState = outputState;
        }
    }
}
