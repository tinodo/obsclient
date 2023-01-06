namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetSpecialInputs request.
    /// </summary>
    public class SpecialInputsResponse : IResponse
    {
        /// <summary>
        /// Gets the name of the desktop audio input.
        /// </summary>
        [JsonPropertyName("desktop1")]
        public string Desktop1 { get; }

        /// <summary>
        /// Gets the name of the desktop audio 2 input.
        /// </summary>
        [JsonPropertyName("desktop2")]
        public string Desktop2 { get; }

        /// <summary>
        /// Gets the name of the mic/auxiliary audio input.
        /// </summary>
        [JsonPropertyName("mic1")]
        public string Mic1 { get; }

        /// <summary>
        /// Gets the name of the mic/auxiliary audio 2 input.
        /// </summary>
        [JsonPropertyName("mic2")]
        public string Mic2 { get; }

        /// <summary>
        /// Gets the name of the mic/auxiliary audio 3 input.
        /// </summary>
        [JsonPropertyName("mic3")]
        public string Mic3 { get; }

        /// <summary>
        /// Gets the name of the mic/auxiliary audio 4 input.
        /// </summary>
        [JsonPropertyName("mic4")]
        public string Mic4 { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpecialInputsResponse"/> class.
        /// </summary>
        /// <param name="desktop1">The name of the desktop audio input.</param>
        /// <param name="desktop2">The name of the desktop audio 2 input.</param>
        /// <param name="mic1">The name of the mic/auxiliary audio input.</param>
        /// <param name="mic2">The name of the mic/auxiliary audio 2 input.</param>
        /// <param name="mic3">The name of the mic/auxiliary audio 3 input.</param>
        /// <param name="mic4">The name of the mic/auxiliary audio 4 input.</param>
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
