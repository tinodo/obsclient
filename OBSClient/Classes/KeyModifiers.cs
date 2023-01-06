namespace OBSStudioClient.Classes
{
    using OBSStudioClient.Enums;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for KeyModifiers.
    /// </summary>
    public class KeyModifiers
    {
        /// <summary>
        /// Gets a value whether the SHIFT key is a modifier.
        /// </summary>
        [JsonPropertyName("shift")]
        public bool Shift { get; }

        /// <summary>
        /// Gets a value whether the CONTROL key is a modifier.
        /// </summary>
        [JsonPropertyName("control")]
        public bool Control { get; }

        /// <summary>
        /// Gets a value whether the ALT key is a modifier.
        /// </summary>
        [JsonPropertyName("alt")]
        public bool Alt { get; }

        /// <summary>
        /// Gets a value whether the COMMAND key is a modifier.
        /// </summary>
        /// <remarks>
        /// Typically for MacOS.
        /// </remarks>
        [JsonPropertyName("command")]
        public bool Command { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyModifiers"/> class.
        /// </summary>
        /// <param name="shift">Indication whether SHIFT is a modifier.</param>
        /// <param name="control">Indication whether CONTROL is a modifier.</param>
        /// <param name="alt">Indication whether ALT is a modifier.</param>
        /// <param name="command">Indication whether COMMAND is a modifier.</param>
        [JsonConstructor]
        public KeyModifiers(bool shift, bool control, bool alt, bool command)
        {
            this.Shift = shift;
            this.Control = control;
            this.Alt = alt;
            this.Command = command;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyModifiers"/> class.
        /// </summary>
        /// <param name="keyModifier">A <see cref="KeyModifier"/> with all key modifiers.</param>
        public KeyModifiers(KeyModifier? keyModifier)
        {
            if (keyModifier.HasValue)
            {
                this.Shift = keyModifier.Value.HasFlag(KeyModifier.Shift);
                this.Control = keyModifier.Value.HasFlag(KeyModifier.Control);
                this.Alt = keyModifier.Value.HasFlag(KeyModifier.Alt);
                this.Command = keyModifier.Value.HasFlag(KeyModifier.Command);
            }
        }
    }
}
