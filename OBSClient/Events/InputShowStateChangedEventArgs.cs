namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class InputShowStateChangedEventArgs : EventArgs
    {
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        [JsonPropertyName("videoShowing")]
        public bool VideoShowing { get; }

        [JsonConstructor]
        public InputShowStateChangedEventArgs(string inputName, bool videoShowing)
        {
            this.InputName = inputName;
            this.VideoShowing = videoShowing;
        }
    }
}
