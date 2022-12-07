namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SourceActiveResponseData : IResponseData
    {
        [JsonPropertyName("videoActive")]
        public bool VideoActive { get; set; }

        [JsonPropertyName("videoShowing")]
        public bool VideoShowing { get; set; }

        [JsonConstructor]
        public SourceActiveResponseData(bool videoActive, bool videoShowing)
        {
            this.VideoActive = videoActive;
            this.VideoShowing = videoShowing;
        }
    }
}
