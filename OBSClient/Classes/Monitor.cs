namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class Monitor
    {
        [JsonPropertyName("monitorHeight")]
        public int MonitorHeight { get; }

        [JsonPropertyName("monitorIndex")]
        public int MonitorIndex { get; }

        [JsonPropertyName("monitorName")]
        public string MonitorName { get; }

        [JsonPropertyName("monitorPositionX")]
        public int MonitorPositionX { get; }

        [JsonPropertyName("monitorPositionY")]
        public int MonitorPositionY { get; }

        [JsonPropertyName("monitorWidth")]
        public int MonitorWidth { get; }

        [JsonConstructor]
        public Monitor(int monitorHeight, int monitorIndex, string monitorName, int monitorPositionX, int monitorPositionY, int monitorWidth)
        {
            MonitorHeight = monitorHeight;
            MonitorIndex = monitorIndex;
            MonitorName = monitorName;
            MonitorPositionX = monitorPositionX;
            MonitorPositionY = monitorPositionY;
            MonitorWidth = monitorWidth;
        }
    }
}
