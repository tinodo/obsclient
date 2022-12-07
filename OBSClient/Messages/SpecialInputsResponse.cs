namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class SpecialInputsResponse : IResponse
    {
        [JsonPropertyName("desktop1")]
        public string Desktop1 { get; set; }

        [JsonPropertyName("desktop2")]
        public string Desktop2 { get; set; }

        [JsonPropertyName("mic1")]
        public string Mic1 { get; set; }

        [JsonPropertyName("mic2")]
        public string Mic2 { get; set; }

        [JsonPropertyName("mic3")]
        public string Mic3 { get; set; }

        [JsonPropertyName("mic4")]
        public string Mic4 { get; set; }

        [JsonConstructor]
        public SpecialInputsResponse(string desktop1, string desktop2, string mic1, string mic2, string mic3, string mic4)
        {
            this.Desktop1 = desktop1;
            this.Desktop2 = desktop2;
            this.Mic1 = mic1;
            this.Mic2 = mic2;
            this.Mic3 = mic3;
            this.Mic4 = mic4;
        }
    }
}
