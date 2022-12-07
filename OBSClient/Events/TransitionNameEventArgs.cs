namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class TransitionNameEventArgs : EventArgs
    {
        [JsonPropertyName("transitionName")]
        public string TransitionName { get; }

        [JsonConstructor]
        public TransitionNameEventArgs(string transitionName)
        {
            this.TransitionName = transitionName;
        }
    }
}
