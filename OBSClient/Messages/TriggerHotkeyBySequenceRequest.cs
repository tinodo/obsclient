namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Request Data for OBS Client Batch requests that require a keyId and keyModifiers.
    /// </summary>
    public class TriggerHotkeyBySequenceRequest
    {
        /// <summary>
        /// Gets or sets the (optional) key ID.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("keyId")]
        public ObsKey? KeyId { get; set; }

        /// <summary>
        /// Gets or sets the (optional) <see cref="KeyModifier"/>.
        /// </summary>
        [JsonPropertyName("keyModifiers")]
        public KeyModifiers? KeyModifiers { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TriggerHotkeyBySequenceRequest"/> class.
        /// </summary>
        /// <param name="keyId">The key ID.</param>
        /// <param name="keyModifiers">The <see cref="KeyModifier"/>.</param>
        [JsonConstructor]
        public TriggerHotkeyBySequenceRequest(ObsKey? keyId, KeyModifiers? keyModifiers)
        {
            this.KeyId = keyId;
            this.KeyModifiers = keyModifiers;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TriggerHotkeyBySequenceRequest"/> class.
        /// </summary>
        /// <param name="keyId">The key ID.</param>
        /// <param name="keyModifier">The <see cref="KeyModifier"/>.</param>
        public TriggerHotkeyBySequenceRequest(ObsKey? keyId, KeyModifier? keyModifier)
        {
            this.KeyId = keyId;
            if (keyModifier != null)
            {
                KeyModifiers keyModifiers = new(keyModifier.Value);
                this.KeyModifiers = keyModifiers;
            }
        }

    }
}
