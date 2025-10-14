namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetTransitionKindList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="TransitionKindsResponse"/> class.
    /// </remarks>
    /// <param name="transitionKinds">The list of transition kinds.</param>
    [method: JsonConstructor]
    public class TransitionKindsResponse(string[] transitionKinds) : IResponse
    {
        /// <summary>
        /// Gets the list of transition kinds.
        /// </summary>
        [JsonPropertyName("transitionKinds")]
        public string[] TransitionKinds { get; } = transitionKinds ?? [];
    }
}
