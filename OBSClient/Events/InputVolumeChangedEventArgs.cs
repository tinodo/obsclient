namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class InputVolumeChangedEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonPropertyName("inputVolumeMul")]
        public float InputVolumeMul { get; }

        [JsonPropertyName("inputVolumeDb")]
        public float InputVolumeDb { get; }

        [JsonConstructor]
        public InputVolumeChangedEventArgs(string inputName, float inputVolumeMul, float inputVolumeDb)
        {
            this.InputName = inputName;
            this.InputVolumeMul = inputVolumeMul;
            this.InputVolumeDb = inputVolumeDb;
        }
    }
}
