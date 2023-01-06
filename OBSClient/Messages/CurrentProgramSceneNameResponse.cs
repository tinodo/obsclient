namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetCurrentProgramScene request.
    /// </summary>
    public class CurrentProgramSceneNameResponse : IResponse
    {
        /// <summary>
        /// Gets the name of the current active scene.
        /// </summary>
        [JsonPropertyName("currentProgramSceneName")]
        public string CurrentProgramSceneName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CurrentProgramSceneNameResponse"/> class.
        /// </summary>
        /// <param name="currentProgramSceneName">The name of the current program scene.</param>
        public CurrentProgramSceneNameResponse(string currentProgramSceneName)
        {
            this.CurrentProgramSceneName = currentProgramSceneName;
        }
    }
}
