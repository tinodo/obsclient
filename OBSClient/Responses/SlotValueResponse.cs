namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetPersistentData request.
    /// </summary>
    public class SlotValueResponse : IResponse
    {
        /// <summary>
        /// Gets the value of the slot.
        /// </summary>
        [JsonPropertyName("slotValue")]
        public object? SlotValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SlotValueResponse"/> class.
        /// </summary>
        /// <param name="slotValue">The value of the slot.</param>
        [JsonConstructor]
        public SlotValueResponse(object? slotValue)
        {
            this.SlotValue = slotValue;
        }
    }
}
