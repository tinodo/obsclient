namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputSettings request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputSettingsResponse"/> class.
    /// </remarks>
    /// <param name="inputSettings">The input settings.</param>
    /// <param name="inputKind">The input kind.</param>
    [method: JsonConstructor]
    public class InputSettingsResponse(Dictionary<string, object> inputSettings, string inputKind) : IResponse
    {
        /// <summary>
        /// Gets the input settings.
        /// </summary>
        [JsonPropertyName("inputSettings")]
        public Dictionary<string, object> InputSettings { get; } = inputSettings ?? [];

        /// <summary>
        /// Gets the input kind.
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string InputKind { get; } = inputKind;
    }
}
