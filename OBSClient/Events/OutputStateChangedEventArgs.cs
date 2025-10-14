namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the StreamStateChanged, ReplayBufferChanged and VirtualcamStateChanged events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="OutputStateChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="outputActive">A value indicating whether the output is active.</param>
    /// <param name="outputState">The <see cref="OutputState"/>.</param>
    [method: JsonConstructor]
    public class OutputStateChangedEventArgs(bool outputActive, ObsOutputState outputState) : EventArgs
    {
        /// <summary>
        /// Gets a value indicating whether the output is active.
        /// </summary>
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; } = outputActive;

        /// <summary>
        /// Gets the <see cref="OutputState"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("outputState")]
        public ObsOutputState OutputState { get; } = outputState;
    }
}
