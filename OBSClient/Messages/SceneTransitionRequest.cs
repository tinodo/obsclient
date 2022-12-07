namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SceneTransitionRequest
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("transitionName")]
        public string? TransitionName { get; set; }

        [JsonPropertyName("transitionDuration")]
        public long? TransitionDuration { get; set; }

        [JsonConstructor]
        public SceneTransitionRequest(string sceneName, string? transitionName, long? transitionDuration)
        {
            this.SceneName = sceneName;
            this.TransitionName = transitionName;
            this.TransitionDuration = transitionDuration;
        }
    }
}
