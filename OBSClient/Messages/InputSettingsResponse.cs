namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputSettings request.
    /// </summary>
    public class InputSettingsResponse : IResponse
    {
        /// <summary>
        /// Gets the input settings.
        /// </summary>
        [JsonPropertyName("inputSettings")]
        public Dictionary<string, object> InputSettings { get; }

        /// <summary>
        /// Gets the input kind.
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string InputKind { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputSettingsResponse"/> class.
        /// </summary>
        /// <param name="inputSettings">The input settings.</param>
        /// <param name="inputKind">The input kind.</param>
        [JsonConstructor]
        public InputSettingsResponse(Dictionary<string, object> inputSettings, string inputKind)
        {
            this.InputSettings = inputSettings ?? new();
            this.InputKind = inputKind;
        }
    }
}
