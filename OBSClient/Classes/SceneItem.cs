namespace OBSStudioClient.Classes
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class SceneItem
    {
        [JsonPropertyName("inputKind")]
        public string? InputKind { get; }

        [JsonPropertyName("isGroup")]
        public bool? IsGroup { get; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("sceneItemBlendMode")]
        public BlendMode SceneItemBlendMode { get; }

        [JsonPropertyName("sceneItemEnabled")]
        public bool SceneItemEnabled { get; }

        [JsonPropertyName("sceneItemId")]
        public int SceneItemId { get; }

        [JsonPropertyName("sceneItemIndex")]
        public int SceneItemIndex { get; }

        [JsonPropertyName("sceneItemLocked")]
        public bool SceneItemLocked { get; }

        [JsonPropertyName("sceneItemTransform")]
        public SceneItemTransform SceneItemTransform { get; }

        [JsonPropertyName("sourceName")]
        public string SourceName { get; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("sourceType")]
        public SourceType SourceType { get; }

        [JsonConstructor]
        public SceneItem(string? inputKind, bool? isGroup, BlendMode sceneItemBlendMode, bool sceneItemEnabled, int sceneItemId, int sceneItemIndex, bool sceneItemLocked, SceneItemTransform sceneItemTransform, string sourceName, SourceType sourceType)
        {
            InputKind = inputKind;
            IsGroup = isGroup;
            SceneItemBlendMode = sceneItemBlendMode;
            SceneItemEnabled = sceneItemEnabled;
            SceneItemId = sceneItemId;
            SceneItemIndex = sceneItemIndex;
            SceneItemLocked = sceneItemLocked;
            SceneItemTransform = sceneItemTransform;
            SourceName = sourceName;
            SourceType = sourceType;
        }
    }
}
