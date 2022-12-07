namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class InputNameChangedEventArgs : EventArgs
    {
        [JsonPropertyName("oldInputName")]
        public string OldInputName { get; }

        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonConstructor]
        public InputNameChangedEventArgs(string oldInputName, string inputName)
        {
            this.OldInputName = oldInputName;
            this.InputName = inputName;
        }
    }
}
