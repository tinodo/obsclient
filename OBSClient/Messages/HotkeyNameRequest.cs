namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Hot Key Name Request Class
    /// </summary>
    public class HotkeyNameRequest
    {
        /// <summary>
        /// Name of the hotkey
        /// </summary>
        [JsonPropertyName("hotkeyName")]
        public string HotkeyName { get; set; }

        /// <summary>
        /// Creates a new instance of a <see cref="HotkeyNameRequest"/> object
        /// </summary>
        /// <param name="hotkeyName">Name of the hotkey</param>
        [JsonConstructor]
        public HotkeyNameRequest(string hotkeyName)
        {
            this.HotkeyName = hotkeyName;
        }
    }
}
