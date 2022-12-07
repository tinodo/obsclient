namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class AudioTracks
    {
        [JsonPropertyName("1")]
        public bool Track1 { get; }

        [JsonPropertyName("2")]
        public bool Track2 { get; }

        [JsonPropertyName("3")]
        public bool Track3 { get; }

        [JsonPropertyName("4")]
        public bool Track4 { get; }

        [JsonPropertyName("5")]
        public bool Track5 { get; }

        [JsonPropertyName("6")]
        public bool Track8 { get; }

        [JsonConstructor]
        public AudioTracks(bool track1, bool track2, bool track3, bool track4, bool track5, bool track8)
        {
            Track1 = track1;
            Track2 = track2;
            Track3 = track3;
            Track4 = track4;
            Track5 = track5;
            Track8 = track8;
        }
    }
}
