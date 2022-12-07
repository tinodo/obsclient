namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class HotkeyNameRequestData
    {
        [JsonPropertyName("hotkeyName")]
        public string HotkeyName { get; set; }

        [JsonConstructor]
        public HotkeyNameRequestData(string hotkeyName)
        {
            this.HotkeyName = hotkeyName;
        }
    }
}
