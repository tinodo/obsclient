namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class TriggerHotkeyBySequenceRequestData
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("keyId")]
        public ObsKey? KeyId { get; set; }

        [JsonPropertyName("keyModifiers")]
        public KeyModifiers? KeyModifiers { get; set; }

        [JsonConstructor]
        public TriggerHotkeyBySequenceRequestData(ObsKey? keyId, KeyModifiers? keyModifiers)
        {
            this.KeyId = keyId;
            this.KeyModifiers = keyModifiers;
        }

        public TriggerHotkeyBySequenceRequestData(ObsKey? keyId, KeyModifier? keyModifier)
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
