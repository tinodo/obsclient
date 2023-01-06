namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Monitor Type.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MonitorType
    {
        /// <summary>
        /// None.
        /// </summary>
        OBS_MONITORING_TYPE_NONE,

        /// <summary>
        /// Monitor Only.
        /// </summary>
        OBS_MONITORING_TYPE_MONITOR_ONLY,

        /// <summary>
        /// Monitor and Output.
        /// </summary>
        OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT,
    }
}
