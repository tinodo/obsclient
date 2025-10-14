namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetLastReplayBufferReplay request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SavedReplayPathResponse"/> class.
    /// </remarks>
    /// <param name="savedReplayPath">The path of the saved replay file.</param>
    [method: JsonConstructor]
    public class SavedReplayPathResponse(string savedReplayPath) : IResponse
    {
        /// <summary>
        /// Gets the path of the saved replay file.
        /// </summary>
        [JsonPropertyName("savedReplayPath")]
        public string SavedReplayPath { get; } = savedReplayPath;
    }
}
