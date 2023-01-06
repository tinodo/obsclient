namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputAudioTracks request.
    /// </summary>
    public class InputAudioTracksResponse : IResponse
    {
        /// <summary>
        /// Gets the <see cref="AudioTracks"/>.
        /// </summary>
        [JsonPropertyName("inputAudioTracks")]
        public AudioTracks InputAudioTracks { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputAudioTracksResponse"/> class.
        /// </summary>
        /// <param name="inputAudioTracks">The <see cref="AudioTracks"/>.</param>
        public InputAudioTracksResponse(AudioTracks inputAudioTracks)
        {
            this.InputAudioTracks = inputAudioTracks;
        }
    }
}
