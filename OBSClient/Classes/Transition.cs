namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class Transition
    {
        [JsonPropertyName("transitionConfigurable")]
        public bool TransitionConfigurable { get; }

        [JsonPropertyName("transitionFixed")]
        public bool TransitionFixed { get; }

        [JsonPropertyName("transitionKind")]
        public string TransitionKind { get; }

        [JsonPropertyName("transitionName")]
        public string TransitionName { get; }

        [JsonConstructor]
        public Transition(bool transitionConfigurable, bool transitionFixed, string transitionKind, string transitionName)
        {
            TransitionConfigurable = transitionConfigurable;
            TransitionFixed = transitionFixed;
            TransitionKind = transitionKind;
            TransitionName = transitionName;
        }
    }
}
