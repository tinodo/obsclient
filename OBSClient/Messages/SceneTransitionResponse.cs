namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneTransitionResponse : IResponse
    {
        [JsonPropertyName("transitionName")]
        public string TransitionName { get; set; }

        [JsonPropertyName("transitionDuration")]
        public long TransitionDuration { get; set; }

        public SceneTransitionResponse(string transitionName, long transitionDuration)
        {
            this.TransitionName = transitionName;
            this.TransitionDuration = transitionDuration;
        }
    }
}
