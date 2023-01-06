namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Blend Mode.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BlendMode
    {
        /// <summary>
        /// Normal Blend Mode
        /// </summary>
        OBS_BLEND_NORMAL,

        /// <summary>
        /// Additive Blend Mode
        /// </summary>
        OBS_BLEND_ADDITIVE,

        /// <summary>
        /// Subtract Blend Mode
        /// </summary>
        OBS_BLEND_SUBTRACT,

        /// <summary>
        /// Screen Blend Mode
        /// </summary>
        OBS_BLEND_SCREEN,

        /// <summary>
        /// Multiply Blend Mode
        /// </summary>
        OBS_BLEND_MULTIPLY,
        /// <summary>
        /// Lighten Blend Mode
        /// </summary>
        OBS_BLEND_LIGHTEN,

        /// <summary>
        /// Darken Blend Mode
        /// </summary>
        OBS_BLEND_DARKEN,
    }
}
