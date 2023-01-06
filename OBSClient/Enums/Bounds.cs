namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Bounds.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Bounds
    {
        /// <summary>
        /// Strech
        /// </summary>
        OBS_BOUNDS_STRETCH,

        /// <summary>
        /// Scale Inner
        /// </summary>
        OBS_BOUNDS_SCALE_INNER,

        /// <summary>
        /// Scale Outer
        /// </summary>
        OBS_BOUNDS_SCALE_OUTER,
        /// <summary>
        /// Scale to Width
        /// </summary>
        OBS_BOUNDS_SCALE_TO_WIDTH,

        /// <summary>
        /// Scale to Height
        /// </summary>
        OBS_BOUNDS_SCALE_TO_HEIGHT,

        /// <summary>
        /// Max Only
        /// </summary>
        OBS_BOUNDS_MAX_ONLY,

        /// <summary>
        /// None
        /// </summary>
        OBS_BOUNDS_NONE
    }
}
