namespace OBSStudioClient.Events
{
    using OBSStudioClient.Messages;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputVolumeMeters event.
    /// </summary>
    public class InputVolumeMetersEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a list of <see cref="Input"/>.
        /// </summary>
        [JsonPropertyName("inputs")]
        public Input[] Inputs { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputVolumeMetersEventArgs"/> class.
        /// </summary>
        /// <param name="inputs">A list of <see cref="Input"/>.</param>
        [JsonConstructor]
        public InputVolumeMetersEventArgs(Input[] inputs)
        {
            this.Inputs = inputs ?? Array.Empty<Input>();
        }
    }
}
