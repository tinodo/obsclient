namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful StopRecord request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="OutputPathResponse"/> class.
    /// </remarks>
    /// <param name="outputPath">The output path.</param>
    [method: JsonConstructor]
    public class OutputPathResponse(string outputPath) : IResponse
    {
        /// <summary>
        /// Gets the output path.
        /// </summary>
        [JsonPropertyName("outputPath")]
        public string OutputPath { get; } = outputPath;
    }
}
