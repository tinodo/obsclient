namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneTransitionList request.
    /// </summary>
    public class SceneTransitionListResponse : IResponse
    {
        /// <summary>
        /// Gets the current scene transition name.
        /// </summary>
        [JsonPropertyName("currentSceneTransitionName")]
        public string CurrentSceneTransitionName { get; }

        /// <summary>
        /// Gets the transition kind of the current scene transition.
        /// </summary>
        [JsonPropertyName("currentSceneTransitionKind")]
        public string CurrentSceneTransitionKind { get; }

        /// <summary>
        /// Gets the list of <see cref="Transition"/>.
        /// </summary>
        [JsonPropertyName("transitions")]
        public Transition[] Transitions { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneTransitionListResponse"/> class.
        /// </summary>
        /// <param name="currentSceneTransitionName">The current scene transition name.</param>
        /// <param name="currentSceneTransitionKind">The transition kind of the current scene transition.</param>
        /// <param name="transitions">The list of <see cref="Transition"/>.</param>
        public SceneTransitionListResponse(string currentSceneTransitionName, string currentSceneTransitionKind, Transition[] transitions)
        {
            this.CurrentSceneTransitionName = currentSceneTransitionName;
            this.CurrentSceneTransitionKind = currentSceneTransitionKind;
            this.Transitions = transitions;
        }
    }
}
