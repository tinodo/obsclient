namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputMute or ToggleInputMute request.
    /// </summary>
    public class InputMutedResponse : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the input is muted.
        /// </summary>
        [JsonPropertyName("inputMuted")]
        public bool InputMuted { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputMutedResponse"/> class.
        /// </summary>
        /// <param name="inputMuted">A value indicating whether the input is muted.</param>
        [JsonConstructor]
        public InputMutedResponse(bool inputMuted)
        {
            this.InputMuted = inputMuted;
        }
    }
}
