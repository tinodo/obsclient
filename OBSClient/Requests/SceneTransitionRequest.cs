namespace OBSStudioClient.Requests
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sceneName, an optional transitionName and an optional transitionDuration.
    /// </summary>
    public class SceneTransitionRequest
    {
        /// <summary>
        /// Gets or sets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        /// <summary>
        /// Gets or sets the transition name.
        /// </summary>
        [JsonPropertyName("transitionName")]
        public string? TransitionName { get; set; }

        /// <summary>
        /// Gets or sets the transition duration.
        /// </summary>
        [JsonPropertyName("transitionDuration")]
        public long? TransitionDuration { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneTransitionRequest"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="transitionName">The transition name.</param>
        /// <param name="transitionDuration">The transition duration.</param>
        [JsonConstructor]
        public SceneTransitionRequest(string sceneName, string? transitionName, long? transitionDuration)
        {
            this.SceneName = sceneName;
            this.TransitionName = transitionName;
            this.TransitionDuration = transitionDuration;
        }
    }
}
