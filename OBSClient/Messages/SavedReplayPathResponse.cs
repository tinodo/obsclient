namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetLastReplayBufferReplay request.
    /// </summary>
    public class SavedReplayPathResponse : IResponse
    {
        /// <summary>
        /// Gets the path of the saved replay file.
        /// </summary>
        [JsonPropertyName("savedReplayPath")]
        public string SavedReplayPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SavedReplayPathResponse"/> class.
        /// </summary>
        /// <param name="savedReplayPath">The path of the saved replay file.</param>
        [JsonConstructor]
        public SavedReplayPathResponse(string savedReplayPath)
        {
            this.SavedReplayPath = savedReplayPath;
        }
    }
}
