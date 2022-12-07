namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class HotkeysResponse : IResponse
    {
        [JsonPropertyName("hotkeys")]
        public string[] Hotkeys { get;set; }

        [JsonConstructor]
        public HotkeysResponse(string[] hotkeys)
        {
            this.Hotkeys = hotkeys ?? Array.Empty<string>();
        }
    }
}
