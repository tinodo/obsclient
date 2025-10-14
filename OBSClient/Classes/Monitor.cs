namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Monitor.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Monitor"/> class.
    /// </remarks>
    /// <param name="monitorHeight">The monitor height.</param>
    /// <param name="monitorIndex">The monitor index.</param>
    /// <param name="monitorName">The monitor name.</param>
    /// <param name="monitorPositionX">The monitors x position.</param>
    /// <param name="monitorPositionY">The monitors y position.</param>
    /// <param name="monitorWidth">The monitor height.</param>
    [method: JsonConstructor]
    public class Monitor(int monitorHeight, int monitorIndex, string monitorName, int monitorPositionX, int monitorPositionY, int monitorWidth)
    {
        /// <summary>
        /// Gets the monitor height.
        /// </summary>
        [JsonPropertyName("monitorHeight")]
        public int MonitorHeight { get; } = monitorHeight;

        /// <summary>
        /// Gets the monitor index.
        /// </summary>
        [JsonPropertyName("monitorIndex")]
        public int MonitorIndex { get; } = monitorIndex;

        /// <summary>
        /// Gets the monitor name.
        /// </summary>
        [JsonPropertyName("monitorName")]
        public string MonitorName { get; } = monitorName;

        /// <summary>
        /// Gets the monitors x coordinate.
        /// </summary>
        [JsonPropertyName("monitorPositionX")]
        public int MonitorPositionX { get; } = monitorPositionX;

        /// <summary>
        /// Gets the monitors y coordinate.
        /// </summary>
        [JsonPropertyName("monitorPositionY")]
        public int MonitorPositionY { get; } = monitorPositionY;

        /// <summary>
        /// Gets the monitor width.
        /// </summary>
        [JsonPropertyName("monitorWidth")]
        public int MonitorWidth { get; } = monitorWidth;
    }
}
