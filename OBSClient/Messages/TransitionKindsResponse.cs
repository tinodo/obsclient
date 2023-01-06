namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetTransitionKindList request.
    /// </summary>
    public class TransitionKindsResponse : IResponse
    {
        /// <summary>
        /// Gets the list of transition kinds.
        /// </summary>
        [JsonPropertyName("transitionKinds")]
        public string[] TransitionKinds { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransitionKindsResponse"/> class.
        /// </summary>
        /// <param name="transitionKinds">The list of transition kinds.</param>
        [JsonConstructor]
        public TransitionKindsResponse(string[] transitionKinds)
        {
            this.TransitionKinds = transitionKinds ?? Array.Empty<string>();
        }
    }
}
