namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a sceneName, sourceName and, potentially, a searchOffset.
    /// </summary>
    public class SceneItemIdRequest
    {
        /// <summary>
        /// Gets or sets the scene name.
        /// </summary>
        [JsonPropertyName("sceneName")]
        public string SceneName { get; set; }

        /// <summary>
        /// Gets or sets the source name.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }

        /// <summary>
        /// Gets or sets the search offset.
        /// </summary>
        [JsonPropertyName("searchOffset")]
        public int? SearchOffset { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItemIdRequest"/> class.
        /// </summary>
        /// <param name="sceneName">The scene name.</param>
        /// <param name="sourceName">The source name.</param>
        /// <param name="searchOffset">The search offset.</param>
        [JsonConstructor]
        public SceneItemIdRequest(string sceneName, string sourceName, int? searchOffset)
        {
            this.SceneName = sceneName;
            this.SourceName = sourceName;
            this.SearchOffset = searchOffset;
        }
    }
}
