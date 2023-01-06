namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputKindList request.
    /// </summary>
    public class InputKindsResponse : IResponse
    {
        /// <summary>
        /// Gets the list of input kinds.
        /// </summary>
        [JsonPropertyName("inputKinds")]
        public string[] InputKinds { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputKindsResponse"/> class.
        /// </summary>
        /// <param name="inputKinds">List of input kinds.</param>
        public InputKindsResponse(string[] inputKinds)
        {
            this.InputKinds = inputKinds ?? Array.Empty<string>();
        }
    }
}
