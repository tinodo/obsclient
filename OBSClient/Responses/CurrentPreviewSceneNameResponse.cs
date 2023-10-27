namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetCurrentPreviewScene request.
    /// </summary>
    public class CurrentPreviewSceneNameResponse : IResponse
    {
        /// <summary>
        /// Gets the name of the current preview scene in Studio Mode.
        /// </summary>
        [JsonPropertyName("currentPreviewSceneName")]
        public string CurrentPreviewSceneName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CurrentPreviewSceneNameResponse"/> class.
        /// </summary>
        /// <param name="currentPreviewSceneName">The name of the preview scene in Studio Mode.</param>
        public CurrentPreviewSceneNameResponse(string currentPreviewSceneName)
        {
            this.CurrentPreviewSceneName = currentPreviewSceneName;
        }
    }
}
