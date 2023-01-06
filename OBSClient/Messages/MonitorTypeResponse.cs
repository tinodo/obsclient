namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputAudioMonitorType request.
    /// </summary>
    public class MonitorTypeResponse : IResponse
    {
        /// <summary>
        /// Gets the <see cref="MonitorType"/>.
        /// </summary>
        [JsonPropertyName("monitorType")]
        public MonitorType MonitorType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorTypeResponse"/> class.
        /// </summary>
        /// <param name="monitorType">The <see cref="MonitorType"/>.</param>
        public MonitorTypeResponse(MonitorType monitorType)
        {
            this.MonitorType = monitorType;
        }
    }
}
