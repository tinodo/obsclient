namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Monitor.
    /// </summary>
    public class Monitor
    {
        /// <summary>
        /// Gets the monitor height.
        /// </summary>
        [JsonPropertyName("monitorHeight")]
        public int MonitorHeight { get; }

        /// <summary>
        /// Gets the monitor index.
        /// </summary>
        [JsonPropertyName("monitorIndex")]
        public int MonitorIndex { get; }

        /// <summary>
        /// Gets the monitor name.
        /// </summary>
        [JsonPropertyName("monitorName")]
        public string MonitorName { get; }

        /// <summary>
        /// Gets the monitors x coordinate.
        /// </summary>
        [JsonPropertyName("monitorPositionX")]
        public int MonitorPositionX { get; }

        /// <summary>
        /// Gets the monitors y coordinate.
        /// </summary>
        [JsonPropertyName("monitorPositionY")]
        public int MonitorPositionY { get; }

        /// <summary>
        /// Gets the monitor width.
        /// </summary>
        [JsonPropertyName("monitorWidth")]
        public int MonitorWidth { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Monitor"/> class.
        /// </summary>
        /// <param name="monitorHeight">The monitor height.</param>
        /// <param name="monitorIndex">The monitor index.</param>
        /// <param name="monitorName">The monitor name.</param>
        /// <param name="monitorPositionX">The monitors x position.</param>
        /// <param name="monitorPositionY">The monitors y position.</param>
        /// <param name="monitorWidth">The monitor height.</param>
        [JsonConstructor]
        public Monitor(int monitorHeight, int monitorIndex, string monitorName, int monitorPositionX, int monitorPositionY, int monitorWidth)
        {
            this.MonitorHeight = monitorHeight;
            this.MonitorIndex = monitorIndex;
            this.MonitorName = monitorName;
            this.MonitorPositionX = monitorPositionX;
            this.MonitorPositionY = monitorPositionY;
            this.MonitorWidth = monitorWidth;
        }
    }
}
