namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceActive request.
    /// </summary>
    public class SourceActiveResponse : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the video is active.
        /// </summary>
        [JsonPropertyName("videoActive")]
        public bool VideoActive { get; }

        /// <summary>
        /// Gets a value indicating whether the video is showing.
        /// </summary>
        [JsonPropertyName("videoShowing")]
        public bool VideoShowing { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SourceActiveResponse"/> class.
        /// </summary>
        /// <param name="videoActive">The value indicating whether the video is active.</param>
        /// <param name="videoShowing">The value indicating whether the video is showing.</param>
        [JsonConstructor]
        public SourceActiveResponse(bool videoActive, bool videoShowing)
        {
            this.VideoActive = videoActive;
            this.VideoShowing = videoShowing;
        }
    }
}
