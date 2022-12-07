namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SceneTransitionResponseData : IResponseData
    {
        [JsonPropertyName("transitionName")]
        public string TransitionName { get; set; }

        [JsonPropertyName("transitionDuration")]
        public int TransitionDuration { get; set; }

        public SceneTransitionResponseData(string transitionName, int transitionDuration)
        {
            this.TransitionName = transitionName;
            this.TransitionDuration = transitionDuration;
        }
    }
}
