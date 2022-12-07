namespace OBSStudioClient.MessageClasses
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    public class ReidentifyMessage : IMessageData
    {
        [JsonPropertyName("eventSubscriptions")]
        public EventSubscription EventSubscriptions { get; }

        [JsonConstructor]
        public ReidentifyMessage(EventSubscription eventSubscriptions)
        {
            EventSubscriptions = eventSubscriptions;
        }
    }
}
