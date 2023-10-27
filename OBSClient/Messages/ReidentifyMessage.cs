namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Reidentify Messages
    /// </summary>
    public class ReidentifyMessage : IMessage
    {
        /// <summary>
        /// The events to subscribe to.
        /// </summary>
        [JsonPropertyName("eventSubscriptions")]
        public EventSubscriptions EventSubscriptions { get; }

        /// <summary>
        /// Creates a new instance of a <see cref="ReidentifyMessage"/> object.
        /// </summary>
        /// <param name="eventSubscriptions"></param>
        [JsonConstructor]
        public ReidentifyMessage(EventSubscriptions eventSubscriptions)
        {
            this.EventSubscriptions = eventSubscriptions;
        }
    }
}
