namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputMute or ToggleInputMute request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputMutedResponse"/> class.
    /// </remarks>
    /// <param name="inputMuted">A value indicating whether the input is muted.</param>
    [method: JsonConstructor]
    public class InputMutedResponse(bool inputMuted) : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the input is muted.
        /// </summary>
        [JsonPropertyName("inputMuted")]
        public bool InputMuted { get; } = inputMuted;
    }
}
