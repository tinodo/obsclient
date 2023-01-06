namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetMonitorList request.
    /// </summary>
    public class MonitorListResponse : IResponse
    {
        /// <summary>
        /// Gets the list of <see cref="Monitor"/>.
        /// </summary>
        [JsonPropertyName("monitors")]
        public Monitor[] Monitors { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorListResponse"/> class.
        /// </summary>
        /// <param name="monitors">List of <see cref="Monitor"/>.</param>
        public MonitorListResponse(Monitor[] monitors)
        {
            this.Monitors = monitors ?? Array.Empty<Monitor>();
        }
    }
}
