namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneTransitionList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneTransitionListResponse"/> class.
    /// </remarks>
    /// <param name="currentSceneTransitionName">The current scene transition name.</param>
    /// <param name="currentSceneTransitionUuid">The current scene transition uuid.</param>
    /// <param name="currentSceneTransitionKind">The transition kind of the current scene transition.</param>
    /// <param name="transitions">The list of <see cref="Transition"/>.</param>
    public class SceneTransitionListResponse(string? currentSceneTransitionName, Guid? currentSceneTransitionUuid, string currentSceneTransitionKind, Transition[] transitions) : IResponse
    {
        /// <summary>
        /// Gets the current scene transition name.
        /// </summary>
        [JsonPropertyName("currentSceneTransitionName")]
        public string? CurrentSceneTransitionName { get; } = currentSceneTransitionName;

        /// <summary>
        /// Gets the current scene transition uuid.
        /// </summary>
        [JsonPropertyName("currentSceneTransitionUuid")]
        public Guid? CurrentSceneTransitionUuid { get; } = currentSceneTransitionUuid;

        /// <summary>
        /// Gets the transition kind of the current scene transition.
        /// </summary>
        [JsonPropertyName("currentSceneTransitionKind")]
        public string CurrentSceneTransitionKind { get; } = currentSceneTransitionKind;

        /// <summary>
        /// Gets the list of <see cref="Transition"/>.
        /// </summary>
        [JsonPropertyName("transitions")]
        public Transition[] Transitions { get; } = transitions;
    }
}
