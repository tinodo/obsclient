namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputsResponse : IResponse
    {
        [JsonPropertyName("inputs")]
        public Input[] Inputs { get; set; }

        public InputsResponse(Input[] inputs)
        {
            this.Inputs = inputs ?? Array.Empty<Input>();
        }
    }
}
