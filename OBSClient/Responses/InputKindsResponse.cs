namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputKindList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputKindsResponse"/> class.
    /// </remarks>
    /// <param name="inputKinds">List of input kinds.</param>
    public class InputKindsResponse(string[] inputKinds) : IResponse
    {
        /// <summary>
        /// Gets the list of input kinds.
        /// </summary>
        [JsonPropertyName("inputKinds")]
        public string[] InputKinds { get; } = inputKinds ?? [];
    }
}
