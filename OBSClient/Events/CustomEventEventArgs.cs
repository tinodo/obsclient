namespace OBSStudioClient.Events
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using OBSStudioClient.Converters;

    /// <summary>
    /// Provides the arguments of the <c>CustomEvent</c> event, which a client emits with <c>BroadcastCustomEvent</c>.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="CustomEventEventArgs"/> class.
    /// </remarks>
    /// <param name="eventData">The data that the client broadcast.</param>
    [JsonConverter(typeof(CustomEventEventArgsConverter))]
    public class CustomEventEventArgs(JsonElement? eventData) : EventArgs
    {
        /// <summary>
        /// The data that the client gave to <c>BroadcastCustomEvent</c>, as it sent it.
        /// </summary>
        /// <remarks>
        /// obs-websocket broadcasts that data as the data of the event itself, and not as a member <c>eventData</c> of it, which is
        /// what its documentation of the event says (checked in the source of 5.7.6, <c>RequestHandler::BroadcastCustomEvent</c>). Up to
        /// 3.0.1 this class read a member that is never there, so the data never arrived.
        /// </remarks>
        public JsonElement? EventData { get; } = eventData;
    }
}
