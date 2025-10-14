namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetOutputList request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="OutputsResponse"/> class.
    /// </remarks>
    /// <param name="outputs">The list of <see cref="Output"/>.</param>
    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetOutputList request.
    /// </summary>
    [method: JsonConstructor]
    public class OutputsResponse(Output[] outputs) : IResponse
    {
        /// <summary>
        /// Gets the list of <see cref="Output"/>.
        /// </summary>
        [JsonPropertyName("outputs")]
        public Output[] Outputs { get; } = outputs ?? [];
    }
}
