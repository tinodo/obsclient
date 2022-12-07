namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class ExtendedTransitionResponseData : Transition, IResponseData
    {
        [JsonPropertyName("transitionDuration")]
        public float? TransitionDuration { get; set; }

        [JsonPropertyName("transitionSettings")]
        public Dictionary<string, object> TransitionSettings { get; set; }

        [JsonConstructor]
        public ExtendedTransitionResponseData(float? transitionDuration, Dictionary<string, object> transitionSettings, bool transitionConfigurable, bool transitionFixed, string transitionKind, string transitionName) : base(transitionConfigurable, transitionFixed, transitionKind, transitionName)
        {
            this.TransitionDuration = transitionDuration;
            this.TransitionSettings = transitionSettings ?? new();
        }
    }
}
