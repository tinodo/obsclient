namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetHotkeyList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="HotkeysResponse"/> class.
    /// </remarks>
    /// <param name="hotkeys">List of hotkey names.</param>
    [method: JsonConstructor]
    public class HotkeysResponse(string[] hotkeys) : IResponse
    {
        /// <summary>
        /// Gets the list of hotkey names.
        /// </summary>
        [JsonPropertyName("hotkeys")]
        public string[] Hotkeys { get; } = hotkeys ?? [];
    }
}
