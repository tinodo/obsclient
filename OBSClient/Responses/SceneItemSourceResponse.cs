namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSceneItemSource request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SceneItemIdResponse"/> class.
    /// </remarks>
    /// <param name="sourceName">The name of the source associated with the scene item.</param>
    /// <param name="sourceUuid">The UUID of the source associated with the scene item..</param>
    [method: JsonConstructor]
    public class SceneItemSourceResponse(string sourceName, string sourceUuid) : IResponse
    {
        /// <summary>
        /// Get the name of the source associated with the scene item.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; } = sourceName;

        /// <summary>
        /// Get the UUID of the source associated with the scene item.
        /// </summary>
        [JsonPropertyName("sourceUuid")]
        public string SourceUuid { get; } = sourceUuid;
    }
}
