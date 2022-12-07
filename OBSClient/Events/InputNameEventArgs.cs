namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class InputNameEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonConstructor]
        public InputNameEventArgs(string inputName)
        {
            this.InputName = inputName;
        }
    }
}
