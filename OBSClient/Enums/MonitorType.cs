namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MonitorType
    {
        OBS_MONITORING_TYPE_NONE,
        OBS_MONITORING_TYPE_MONITOR_ONLY,
        OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT,
    }
}
