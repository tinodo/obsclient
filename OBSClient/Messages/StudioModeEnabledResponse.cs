namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class StudioModeEnabledResponse : IResponse
    {
        [JsonPropertyName("studioModeEnabled")]
        public bool StudioModeEnabled { get; set; }

        [JsonConstructor]
        public StudioModeEnabledResponse(bool studioModeEnabled)
        {
            this.StudioModeEnabled = studioModeEnabled;
        }
    }
}
