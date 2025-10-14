namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetCurrentSceneTransitionCursor request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="TransitionCursorResponse"/> class.
    /// </remarks>
    /// <param name="transitionCursor">The transition cursor.</param>
    [method: JsonConstructor]
    public class TransitionCursorResponse(float transitionCursor) : IResponse
    {
        /// <summary>
        /// Gets the transition cursor, between 0.0 and 1.0.
        /// </summary>
        [JsonPropertyName("transitionCursor")]
        public float TransitionCursor { get; } = transitionCursor;
    }
}
