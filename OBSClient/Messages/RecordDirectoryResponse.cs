namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetRecordDirectory request.
    /// </summary>
    public class RecordDirectoryResponse : IResponse
    {
        /// <summary>
        /// Gets the output directory for the recording.
        /// </summary>
        [JsonPropertyName("recordDirectory")]
        public string RecordDirectory { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordDirectoryResponse"/> class.
        /// </summary>
        /// <param name="recordDirectory">The output directory for the recording.</param>
        [JsonConstructor]
        public RecordDirectoryResponse(string recordDirectory)
        {
            this.RecordDirectory = recordDirectory;
        }
    }
}
