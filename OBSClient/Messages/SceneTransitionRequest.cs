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
        public int? TransitionDuration { get; set; }

        [JsonConstructor]
        public SceneTransitionRequest(string sceneName, string? transitionName, int? transitionDuration)
        {
            this.SceneName = sceneName;
            this.TransitionName = transitionName;
            this.TransitionDuration = transitionDuration;
        }
    }
}
