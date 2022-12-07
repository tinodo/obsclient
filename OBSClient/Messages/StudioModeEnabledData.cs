namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class StudioModeEnabledData : IResponseData
    {
        [JsonPropertyName("studioModeEnabled")]
        public bool StudioModeEnabled { get; set; }

        [JsonConstructor]
        public StudioModeEnabledData(bool studioModeEnabled)
        {
            this.StudioModeEnabled = studioModeEnabled;
        }
    }
}
