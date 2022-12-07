using System;
namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class InputAudioTracksResponse : IResponse
    {
        [JsonPropertyName("inputAudioTracks")]
        public AudioTracks InputAudioTracks { get; set; }

        public InputAudioTracksResponse(AudioTracks inputAudioTracks)
        {
            this.InputAudioTracks = inputAudioTracks;
        }
    }
}
