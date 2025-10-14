namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputDeinterlaceMode request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputDeinterlaceModeResponse"/> class.
    /// </remarks>
    /// <param name="inputDeinterlaceMode">The deinterlace mode of the input.</param>
    public class InputDeinterlaceModeResponse(DeinterlaceMode inputDeinterlaceMode) : IResponse
    {
        /// <summary>
        /// Gets the deinterlace mode of the input.
        /// </summary>
        [JsonPropertyName("inputDeinterlaceMode")]
        public DeinterlaceMode InputDeinterlaceMode { get; } = inputDeinterlaceMode;
    }
}
