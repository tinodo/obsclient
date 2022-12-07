namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputVolumeResponse : IResponse
    {
        [JsonPropertyName("inputVolumeMul")]
        public float InputVolumeMul { get; set; }

        [JsonPropertyName("inputVolumeDb")]
        public float InputVolumeDb { get; set; }

        [JsonConstructor]
        public InputVolumeResponse(float inputVolumeMul, float inputVolumeDb)
        {
            this.InputVolumeMul = inputVolumeMul;
            this.InputVolumeDb = inputVolumeDb;
        }
    }
}
