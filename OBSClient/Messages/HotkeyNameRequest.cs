namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class HotkeyNameRequest
    {
        [JsonPropertyName("hotkeyName")]
        public string HotkeyName { get; set; }

        [JsonConstructor]
        public HotkeyNameRequest(string hotkeyName)
        {
            this.HotkeyName = hotkeyName;
        }
    }
}
