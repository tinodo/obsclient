namespace OBSStudioClient.Model
{
    using System.Text.Json;

    /// <summary>
    /// One choice of a list property of an input: an entry of the dropdown that OBS Studio shows in the properties of the input (a display of a display capture, a device of a video
    /// capture device, a font style).
    /// </summary>
    public sealed class ObsPropertyItem
    {
        internal ObsPropertyItem(string name, JsonElement? value, bool isEnabled)
        {
            this.Name = name;
            this.Value = value;
            this.IsEnabled = isEnabled;
        }

        /// <summary>
        /// Gets the text that OBS Studio shows for the choice.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the value of the choice, as OBS Studio sent it: what the setting of the input has to be to make this choice. It is a string, a whole number or a floating point number
        /// (<see cref="JsonElement.GetString"/>, <see cref="JsonElement.GetInt64"/> and <see cref="JsonElement.GetDouble"/>) depending on the list, and null for a list of another
        /// format. A setting that is set from it has to be set as the same kind.
        /// </summary>
        public JsonElement? Value { get; }

        /// <summary>
        /// Gets whether the choice can be made: OBS Studio shows a choice that is not enabled (a device that is gone), and does not let a person pick it.
        /// </summary>
        public bool IsEnabled { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.IsEnabled ? this.Name : $"{this.Name} (not enabled)";
        }
    }
}
