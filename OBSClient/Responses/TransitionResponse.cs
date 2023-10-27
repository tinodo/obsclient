namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetCurrentSceneTransition request.
    /// Also used as the Request Data in the Request Message sent to OBS Studio in a SetCurrentSceneTransitionSettings request.
    /// </summary>
    public class TransitionResponse : Transition, IResponse
    {
        /// <summary>
        /// Gets the duration of the transition.
        /// </summary>
        [JsonPropertyName("transitionDuration")]
        public float? TransitionDuration { get; }

        /// <summary>
        /// Gets a dictionary with transition settings
        /// </summary>
        [JsonPropertyName("transitionSettings")]
        public Dictionary<string, object> TransitionSettings { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransitionResponse"/> class.
        /// </summary>
        /// <param name="transitionDuration">Duration of the transition.</param>
        /// <param name="transitionSettings">Setting of the transition.</param>
        /// <param name="transitionConfigurable">Whether the transition is configurable.</param>
        /// <param name="transitionFixed">Whether the transition is fixed.</param>
        /// <param name="transitionKind">The kind of transition.</param>
        /// <param name="transitionName">The name of the transition.</param>
        [JsonConstructor]
        public TransitionResponse(float? transitionDuration, Dictionary<string, object> transitionSettings, bool transitionConfigurable, bool transitionFixed, string transitionKind, string transitionName) : base(transitionConfigurable, transitionFixed, transitionKind, transitionName)
        {
            this.TransitionDuration = transitionDuration;
            this.TransitionSettings = transitionSettings ?? new();
        }
    }
}
