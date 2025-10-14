namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Class for Reidentify Messages
    /// </summary>
    /// <remarks>
    /// Creates a new instance of a <see cref="ReidentifyMessage"/> object.
    /// </remarks>
    /// <param name="eventSubscriptions"></param>
    [method: JsonConstructor]
    public class ReidentifyMessage(EventSubscriptions eventSubscriptions) : IMessage
    {
        /// <summary>
        /// The events to subscribe to.
        /// </summary>
        [JsonPropertyName("eventSubscriptions")]
        public EventSubscriptions EventSubscriptions { get; } = eventSubscriptions;
    }
}
