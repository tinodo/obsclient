namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Source Types.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SourceType
    {
        /// <summary>
        /// Input.
        /// </summary>
        OBS_SOURCE_TYPE_INPUT,

        /// <summary>
        /// Filter.
        /// </summary>
        OBS_SOURCE_TYPE_FILTER,

        /// <summary>
        /// Transition.
        /// </summary>
        OBS_SOURCE_TYPE_TRANSITION,

        /// <summary>
        /// Scene.
        /// </summary>
        OBS_SOURCE_TYPE_SCENE,
    }
}
