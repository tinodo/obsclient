namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetCurrentSceneTransitionCursor request.
    /// </summary>
    public class TransitionCursorResponse : IResponse
    {
        /// <summary>
        /// Gets the transition cursor, between 0.0 and 1.0.
        /// </summary>
        [JsonPropertyName("transitionCursor")]
        public float TransitionCursor { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransitionCursorResponse"/> class.
        /// </summary>
        /// <param name="transitionCursor">The transition cursor.</param>
        [JsonConstructor]
        public TransitionCursorResponse(float transitionCursor)
        {
            this.TransitionCursor = transitionCursor;
        }
    }
}
