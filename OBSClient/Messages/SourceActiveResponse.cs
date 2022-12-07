namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SourceActiveResponse : IResponse
    {
        [JsonPropertyName("videoActive")]
        public bool VideoActive { get; set; }

        [JsonPropertyName("videoShowing")]
        public bool VideoShowing { get; set; }

        [JsonConstructor]
        public SourceActiveResponse(bool videoActive, bool videoShowing)
        {
            this.VideoActive = videoActive;
            this.VideoShowing = videoShowing;
        }
    }
}
