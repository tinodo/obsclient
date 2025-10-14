namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputAudioBalance request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputAudioBalanceResponse"/> class.
    /// </remarks>
    /// <param name="inputAudioBalance">The input audio balance as a value between 0.0 and 1.0.</param>
    public class InputAudioBalanceResponse(float inputAudioBalance) : IResponse
    {
        /// <summary>
        /// Gets the input audio balance as a value between 0.0 and 1.0.
        /// </summary>
        [JsonPropertyName("inputAudioBalance")]
        public float InputAudioBalance { get; } = inputAudioBalance;
    }
}
