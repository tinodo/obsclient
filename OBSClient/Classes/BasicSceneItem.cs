namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for a scene item as obs-websocket describes it when a scene is reindexed: only its identifier and its index.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="BasicSceneItem"/> class.
    /// </remarks>
    /// <param name="sceneItemId">The numeric ID of the scene item.</param>
    /// <param name="sceneItemIndex">The index position of the scene item.</param>
    [method: JsonConstructor]
    public sealed class BasicSceneItem(int sceneItemId, int sceneItemIndex)
    {
        /// <summary>
        /// Gets the numeric ID of the scene item.
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; } = sceneItemId;

        /// <summary>
        /// Gets the index position of the scene item.
        /// </summary>
        [JsonPropertyName("sceneItemIndex")]
        public int SceneItemIndex { get; } = sceneItemIndex;
    }
}
