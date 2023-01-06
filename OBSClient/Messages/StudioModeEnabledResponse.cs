namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetStudioModeEnabled request.
    /// </summary>
    public class StudioModeEnabledResponse : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether Studio Mode is enabled.
        /// </summary>
        [JsonPropertyName("studioModeEnabled")]
        public bool StudioModeEnabled { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StudioModeEnabledResponse"/> class.
        /// </summary>
        /// <param name="studioModeEnabled">A value indicating whether Studio Mode is enabled.</param>
        [JsonConstructor]
        public StudioModeEnabledResponse(bool studioModeEnabled)
        {
            this.StudioModeEnabled = studioModeEnabled;
        }
    }
}
