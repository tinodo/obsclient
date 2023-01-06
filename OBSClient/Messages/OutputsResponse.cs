namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetOutputList request.
    /// </summary>
    public class OutputsResponse : IResponse
    {
        /// <summary>
        /// Gets the list of <see cref="Output"/>.
        /// </summary>
        [JsonPropertyName("outputs")]
        public Output[] Outputs { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputsResponse"/> class.
        /// </summary>
        /// <param name="outputs">The list of <see cref="Output"/>.</param>
        [JsonConstructor]
        public OutputsResponse(Output[] outputs)
        {
            this.Outputs = outputs ?? Array.Empty<Output>();
        }
    }
}
