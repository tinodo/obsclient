namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CurrenSceneTransitionDurationChanged event.
    /// </summary>
    public class TransitionDurationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the duration of the transition.
        /// </summary>
        [JsonPropertyName("transitionDuration")]
        public int TransitionDuration { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransitionDurationEventArgs"/> class.
        /// </summary>
        /// <param name="transitionDuration">The duration of the transition.</param>
        [JsonConstructor]
        public TransitionDurationEventArgs(int transitionDuration)
        {
            this.TransitionDuration = transitionDuration;
        }
    }
}
