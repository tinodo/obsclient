namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetPersistentData request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SlotValueResponse"/> class.
    /// </remarks>
    /// <param name="slotValue">The value of the slot.</param>
    [method: JsonConstructor]
    public class SlotValueResponse(object? slotValue) : IResponse
    {
        /// <summary>
        /// Gets the value of the slot.
        /// </summary>
        [JsonPropertyName("slotValue")]
        public object? SlotValue { get; } = slotValue;
    }
}
