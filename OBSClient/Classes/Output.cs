namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class Output
    {
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; }

        [JsonPropertyName("outputFlags")]
        public Dictionary<string, bool> OutputFlags { get; }

        [JsonPropertyName("outputHeight")]
        public int OutputHeight { get; }

        [JsonPropertyName("outputKind")]
        public string OutputKind { get; }

        [JsonPropertyName("outputName")]
        public string OutputName { get; }

        [JsonPropertyName("outputWidth")]
        public int OutputWidth { get; }

        [JsonConstructor]
        public Output(bool outputActive, Dictionary<string, bool> outputFlags, int outputHeight, string outputKind, string outputName, int outputWidth)
        {
            OutputActive = outputActive;
            OutputFlags = outputFlags;
            OutputHeight = outputHeight;
            OutputKind = outputKind;
            OutputName = outputName;
            OutputWidth = outputWidth;
        }
    }
}
