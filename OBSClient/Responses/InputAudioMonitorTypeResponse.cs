namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputAudioMonitorType request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputAudioMonitorTypeResponse"/> class.
    /// </remarks>
    /// <param name="monitorType">The <see cref="MonitorType"/>.</param>
    public class InputAudioMonitorTypeResponse(MonitorType monitorType) : IResponse
    {
        /// <summary>
        /// Gets the <see cref="MonitorType"/>.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("monitorType")]
        public MonitorType MonitorType { get; } = monitorType;
    }
}
