namespace OBSStudioClient.Classes
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for SceneItem. 
    /// </summary>
    public class SceneItem
    {
        /// <summary>
        /// Gets the input kind.
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string? InputKind { get; }

        /// <summary>
        /// Gets a value indicating whether the scene item is a group.
        /// </summary>
        [JsonPropertyName("isGroup")]
        public bool? IsGroup { get; }

        /// <summary>
        /// Gets the scene item <see cref="BlendMode"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("sceneItemBlendMode")]
        public BlendMode SceneItemBlendMode { get; }

        /// <summary>
        /// Gets a value indicating whether the scene item is enabled.
        /// </summary>
        [JsonPropertyName("sceneItemEnabled")]
        public bool SceneItemEnabled { get; }

        /// <summary>
        /// Gets the scene item id.
        /// </summary>
        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        /// <summary>
        /// Gets the scene item index.
        /// </summary>
        [JsonPropertyName("sceneItemIndex")]
        public int SceneItemIndex { get; }

        /// <summary>
        /// Gets a value indicating whether the scene item is locked.
        /// </summary>
        [JsonPropertyName("sceneItemLocked")]
        public bool SceneItemLocked { get; }

        /// <summary>
        /// Gets the scene items <see cref="SceneItemTransform"/>.
        /// </summary>
        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; }

        /// <summary>
        /// Gets the source name for the scene item.
        /// </summary>
        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        /// <summary>
        /// Gets the <see cref="SourceType"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("sourceType")]
        public SourceType SourceType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SceneItem"/> class.
        /// </summary>
        /// <param name="inputKind">The input kind.</param>
        /// <param name="isGroup">A value indicating whether the scene item is a group.</param>
        /// <param name="sceneItemBlendMode">The scene item <see cref="BlendMode"/>.</param>
        /// <param name="sceneItemEnabled"></param>
        /// <param name="sceneItemId">The scene item id.</param>
        /// <param name="sceneItemIndex">The scene item index.</param>
        /// <param name="sceneItemLocked">A value indicating whether the scene item is locked.</param>
        /// <param name="sceneItemTransform">The scene item transform.</param>
        /// <param name="sourceName">The source name.</param>
        /// <param name="sourceType">The source type.</param>
        [JsonConstructor]
        public SceneItem(string? inputKind, bool? isGroup, BlendMode sceneItemBlendMode, bool sceneItemEnabled, int sceneItemId, int sceneItemIndex, bool sceneItemLocked, SceneItemTransform sceneItemTransform, string sourceName, SourceType sourceType)
        {
            this.InputKind = inputKind;
            this.IsGroup = isGroup;
            this.SceneItemBlendMode = sceneItemBlendMode;
            this.SceneItemEnabled = sceneItemEnabled;
            this.SceneItemId = sceneItemId;
            this.SceneItemIndex = sceneItemIndex;
            this.SceneItemLocked = sceneItemLocked;
            this.SceneItemTransform = sceneItemTransform;
            this.SourceName = sourceName;
            this.SourceType = sourceType;
        }
    }
}
