namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Interfaces;

    public class InputVolumeResponseData : IResponseData
    {
        [JsonPropertyName("inputVolumeMul")]
        public float InputVolumeMul { get; set; }

        [JsonPropertyName("inputVolumeDb")]
        public float InputVolumeDb { get; set; }

        [JsonConstructor]
        public InputVolumeResponseData(float inputVolumeMul, float inputVolumeDb)
        {
            this.InputVolumeMul = inputVolumeMul;
            this.InputVolumeDb = inputVolumeDb;
        }
    }
}
