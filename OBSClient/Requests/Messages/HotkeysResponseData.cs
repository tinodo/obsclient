namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class HotkeysResponseData : IResponseData
    {
        [JsonPropertyName("hotkeys")]
        public string[] Hotkeys { get;set; }

        [JsonConstructor]
        public HotkeysResponseData(string[] hotkeys)
        {
            this.Hotkeys = hotkeys ?? Array.Empty<string>();
        }
    }
}
