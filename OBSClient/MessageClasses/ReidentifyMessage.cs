namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class ReidentifyMessage : IMessage
    {
        [JsonPropertyName("eventSubscriptions")]
        public EventSubscriptions EventSubscriptions { get; }

        [JsonConstructor]
        public ReidentifyMessage(EventSubscriptions eventSubscriptions)
        {
            EventSubscriptions = eventSubscriptions;
        }
    }
}
