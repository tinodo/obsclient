using System;
namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class InputAudioTracksResponseData : IResponseData
    {
        [JsonPropertyName("inputAudioTracks")]
        public AudioTracks InputAudioTracks { get; set; }

        public InputAudioTracksResponseData(AudioTracks inputAudioTracks)
        {
            this.InputAudioTracks = inputAudioTracks;
        }
    }
}
