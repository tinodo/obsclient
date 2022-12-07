namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    public class SceneTransitionRequestData
    {
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        [JsonPropertyName("transitionName")]
        public string? TransitionName { get; set; }

        [JsonPropertyName("transitionDuration")]
        public int? TransitionDuration { get; set; }

        [JsonConstructor]
        public SceneTransitionRequestData(string sceneName, string? transitionName, int? transitionDuration)
        {
            this.SceneName = sceneName;
            this.TransitionName = transitionName;
            this.TransitionDuration = transitionDuration;
        }
    }
}
