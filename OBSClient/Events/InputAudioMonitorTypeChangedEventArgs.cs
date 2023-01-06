namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputAudioMonitorTypeChanged event.
    /// </summary>
    public class InputAudioMonitorTypeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets the <see cref="MonitorType"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("monitorType")]
        public MonitorType MonitorType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputAudioMonitorTypeChangedEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The name of the input.</param>
        /// <param name="monitorType">The type of monitor.</param>
        [JsonConstructor]
        public InputAudioMonitorTypeChangedEventArgs(string inputName, MonitorType monitorType)
        {
            this.InputName = inputName;
            this.MonitorType = monitorType;
        }
    }
}
