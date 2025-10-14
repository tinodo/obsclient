namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Blend Mode.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeinterlaceMode
    {
        /// <summary>
        /// 
        /// </summary>
        OBS_DEINTERLACE_MODE_DISABLE,

        /// <summary>
        /// 
        /// </summary>
        OBS_DEINTERLACE_MODE_DISCARD,

        /// <summary>
        /// 
        /// </summary>
        OBS_DEINTERLACE_MODE_RETRO,

        /// <summary>
        /// 
        /// </summary>
        OBS_DEINTERLACE_MODE_BLEND,

        /// <summary>
        /// 
        /// </summary>
        OBS_DEINTERLACE_MODE_BLEND_2X,

        /// <summary>
        /// 
        /// </summary>
        OBS_DEINTERLACE_MODE_LINEAR,

        /// <summary>
        /// 
        /// </summary>
        OBS_DEINTERLACE_MODE_LINEAR_2X,

        /// <summary>
        /// 
        /// </summary>
        OBS_DEINTERLACE_MODE_YADIF,

        /// <summary>
        /// 
        /// </summary>
        OBS_DEINTERLACE_MODE_YADIF_2X,
    }
}
