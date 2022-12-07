namespace OBSStudioClient.Events
{
    using OBSStudioClient.Messages;
    using System.Text.Json.Serialization;

    public class InputVolumeMetersEventArgs : EventArgs
    {
        [JsonPropertyName("inputs")]
        public Input[] Inputs { get; }

        [JsonConstructor]
        public InputVolumeMetersEventArgs(Input[] inputs)
        {
            this.Inputs = inputs ?? Array.Empty<Input>();
        }
    }
}
