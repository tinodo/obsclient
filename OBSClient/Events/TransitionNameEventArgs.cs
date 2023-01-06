namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CurrenSceneTransitionChanged, SceneTransitionStarted, SceneTransitionEnded and SceneTransitionVideoEnded events.
    /// </summary>
    public class TransitionNameEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the transition.
        /// </summary>
        [JsonPropertyName("transitionName")]
        public string TransitionName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransitionNameEventArgs"/> class.
        /// </summary>
        /// <param name="transitionName">The name of the transition.</param>
        [JsonConstructor]
        public TransitionNameEventArgs(string transitionName)
        {
            this.TransitionName = transitionName;
        }
    }
}
