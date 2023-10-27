namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful StopRecord request.
    /// </summary>
    public class OutputPathResponse : IResponse
    {
        /// <summary>
        /// Gets the output path.
        /// </summary>
        [JsonPropertyName("outputPath")]
        public string OutputPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputPathResponse"/> class.
        /// </summary>
        /// <param name="outputPath">The output path.</param>
        [JsonConstructor]
        public OutputPathResponse(string outputPath)
        {
            this.OutputPath = outputPath;
        }
    }
}
