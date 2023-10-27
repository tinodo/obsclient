namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetHotkeyList request.
    /// </summary>
    public class HotkeysResponse : IResponse
    {
        /// <summary>
        /// Gets the list of hotkey names.
        /// </summary>
        [JsonPropertyName("hotkeys")]
        public string[] Hotkeys { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HotkeysResponse"/> class.
        /// </summary>
        /// <param name="hotkeys">List of hotkey names.</param>
        [JsonConstructor]
        public HotkeysResponse(string[] hotkeys)
        {
            this.Hotkeys = hotkeys ?? Array.Empty<string>();
        }
    }
}
