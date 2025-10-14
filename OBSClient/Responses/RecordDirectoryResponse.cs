namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetRecordDirectory request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="RecordDirectoryResponse"/> class.
    /// </remarks>
    /// <param name="recordDirectory">The output directory for the recording.</param>
    [method: JsonConstructor]
    public class RecordDirectoryResponse(string recordDirectory) : IResponse
    {
        /// <summary>
        /// Gets the output directory for the recording.
        /// </summary>
        [JsonPropertyName("recordDirectory")]
        public string RecordDirectory { get; } = recordDirectory;
    }
}
