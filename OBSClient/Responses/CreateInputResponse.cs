namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after a successfully CreateInput request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CreateInputResponse"/> class.
    /// </remarks>
    /// <param name="inputUuid">The UUID of the newly created input.</param>
    /// <param name="sceneItemId">The identifier of the scene item.</param>
    [method: JsonConstructor]
    public class CreateInputResponse(Guid inputUuid, int sceneItemId) : IResponse
    {
        /// <summary>
        /// Gets the UUID of the newly created input
        /// </summary>
        [JsonPropertyName("inputUuid")]
        public Guid InputUuid { get; } = inputUuid;

        /// <summary>
        /// Get the identifier of the scene item.
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; } = sceneItemId;
    }
}
