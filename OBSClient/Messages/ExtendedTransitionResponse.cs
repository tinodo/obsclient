namespace OBSStudioClient.Messages
{
    using System.Text.Json.Serialization;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Interfaces;

    public class ExtendedTransitionResponse : Transition, IResponse
    {
        [JsonPropertyName("transitionDuration")]
        public float? TransitionDuration { get; set; }

        [JsonPropertyName("transitionSettings")]
        public Dictionary<string, object> TransitionSettings { get; set; }

        [JsonConstructor]
        public ExtendedTransitionResponse(float? transitionDuration, Dictionary<string, object> transitionSettings, bool transitionConfigurable, bool transitionFixed, string transitionKind, string transitionName) : base(transitionConfigurable, transitionFixed, transitionKind, transitionName)
        {
            this.TransitionDuration = transitionDuration;
            this.TransitionSettings = transitionSettings ?? new();
        }
    }
}
