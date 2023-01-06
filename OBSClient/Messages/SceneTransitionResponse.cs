namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneSceneTransitionOverride request.
    /// </summary>
    public class SceneTransitionResponse : IResponse
    {
        /// <summary>
        /// Gets the name of the transition.
        /// </summary>
        [JsonPropertyName("transitionName")]
        public string TransitionName { get; }

        /// <summary>
        /// Gets the duration of the transition in milliseconds.
        /// </summary>
        [JsonPropertyName("transitionDuration")]
        public long TransitionDuration { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneTransitionResponse"/> class.
        /// </summary>
        /// <param name="transitionName">The name of the transition.</param>
        /// <param name="transitionDuration">The duration of the transition in milliseconds.</param>
        [JsonConstructor]
        public SceneTransitionResponse(string transitionName, long transitionDuration)
        {
            this.TransitionName = transitionName;
            this.TransitionDuration = transitionDuration;
        }
    }
}
