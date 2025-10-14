namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSourceActive request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SourceActiveResponse"/> class.
    /// </remarks>
    /// <param name="videoActive">The value indicating whether the video is active.</param>
    /// <param name="videoShowing">The value indicating whether the video is showing.</param>
    [method: JsonConstructor]
    public class SourceActiveResponse(bool videoActive, bool videoShowing) : IResponse
    {
        /// <summary>
        /// Gets a value indicating whether the video is active.
        /// </summary>
        [JsonPropertyName("videoActive")]
        public bool VideoActive { get; } = videoActive;

        /// <summary>
        /// Gets a value indicating whether the video is showing.
        /// </summary>
        [JsonPropertyName("videoShowing")]
        public bool VideoShowing { get; } = videoShowing;
    }
}
