namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    public class TransitionDurationEventArgs : EventArgs
    {
        [JsonPropertyName("transitionDuration")]
        public int TransitionDuration { get; }

        [JsonConstructor]
        public TransitionDurationEventArgs(int transitionDuration)
        {
            this.TransitionDuration = transitionDuration;
        }
    }
}
