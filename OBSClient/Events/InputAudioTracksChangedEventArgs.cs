namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;

    public class InputAudioTracksChangedEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonPropertyName("inputAudioTracks")]
        public AudioTracks InputAudioTracks { get; }

        [JsonConstructor]
        public InputAudioTracksChangedEventArgs(string inputName, AudioTracks inputAudioTracks)
        {
            this.InputName = inputName;
            this.InputAudioTracks = inputAudioTracks;
        }
    }
}
