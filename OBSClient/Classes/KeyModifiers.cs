namespace OBSStudioClient.Classes
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    public class KeyModifiers
    {
        [JsonPropertyName("shift")]
        public bool Shift { get; }

        [JsonPropertyName("control")]
        public bool Control { get; }

        [JsonPropertyName("alt")]
        public bool Alt { get; }

        [JsonPropertyName("command")]
        public bool Command { get; }

        [JsonConstructor]
        public KeyModifiers(bool shift, bool control, bool alt, bool command)
        {
            Shift = shift;
            Control = control;
            Alt = alt;
            Command = command;
        }

        public KeyModifiers(KeyModifier? keyModifier)
        {
            if (keyModifier.HasValue)
            {
                Shift = keyModifier.Value.HasFlag(KeyModifier.Shift);
                Control = keyModifier.Value.HasFlag(KeyModifier.Control);
                Alt = keyModifier.Value.HasFlag(KeyModifier.Alt);
                Command = keyModifier.Value.HasFlag(KeyModifier.Command);
            }
        }
    }
}
