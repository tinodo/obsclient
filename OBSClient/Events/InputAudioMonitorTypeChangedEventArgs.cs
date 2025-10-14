namespace OBSStudioClient.Events
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputAudioMonitorTypeChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputAudioMonitorTypeChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The name of the input.</param>
    /// <param name="inputUuid">The uuid of the input.</param>
    /// <param name="monitorType">The type of monitor.</param>
    [method: JsonConstructor]
    public class InputAudioMonitorTypeChangedEventArgs(string inputName, Guid inputUuid, MonitorType monitorType) : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the input uuid.
        /// </summary>
        [JsonPropertyName("inputUuid")]
        public Guid InputUuid { get; } = inputUuid;

        /// <summary>
        /// Gets the <see cref="MonitorType"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("monitorType")]
        public MonitorType MonitorType { get; } = monitorType;
    }
}
