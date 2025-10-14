namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CurrenSceneTransitionChanged, SceneTransitionStarted, SceneTransitionEnded and SceneTransitionVideoEnded events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="TransitionNameEventArgs"/> class.
    /// </remarks>
    /// <param name="transitionName">The name of the transition.</param>
    /// <param name="transitionUuid">The uuid of the transition.</param>
    [method: JsonConstructor]
    public class TransitionNameEventArgs(string transitionName, Guid transitionUuid) : EventArgs
    {
        /// <summary>
        /// Gets the name of the transition.
        /// </summary>
        [JsonPropertyName("transitionName")]
        public string TransitionName { get; } = transitionName;

        /// <summary>
        /// Gets the transition uuid.
        /// </summary>
        [JsonPropertyName("transitionUuid")]
        public Guid TransitionUuid { get; } = transitionUuid;
    }
}
