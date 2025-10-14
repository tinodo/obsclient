namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the CurrenSceneTransitionDurationChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="TransitionDurationEventArgs"/> class.
    /// </remarks>
    /// <param name="transitionDuration">The duration of the transition.</param>
    [method: JsonConstructor]
    public class TransitionDurationEventArgs(int transitionDuration) : EventArgs
    {
        /// <summary>
        /// Gets the duration of the transition.
        /// </summary>
        [JsonPropertyName("transitionDuration")]
        public int TransitionDuration { get; } = transitionDuration;
    }
}
