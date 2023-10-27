namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful ToggleRecordPaused request.
    /// </summary>
    public class OutputPausedResponse : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the output is active.
        /// </summary>
        [JsonPropertyName("outputPaused")]
        public bool OutputPaused { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputPausedResponse"/> class.
        /// </summary>
        /// <param name="outputPaused">a value indicating whether the output is paused.</param>
        [JsonConstructor]
        public OutputPausedResponse(bool outputPaused)
        {
            this.OutputPaused = outputPaused;
        }
    }
}
