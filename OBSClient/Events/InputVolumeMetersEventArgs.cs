namespace OBSStudioClient.Events
{
    using OBSStudioClient.Classes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputVolumeMeters event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputVolumeMetersEventArgs"/> class.
    /// </remarks>
    /// <param name="inputs">A list of <see cref="Input"/>.</param>
    [method: JsonConstructor]
    public class InputVolumeMetersEventArgs(Input[] inputs) : EventArgs
    {
        /// <summary>
        /// Gets a list of <see cref="Input"/>.
        /// </summary>
        [JsonPropertyName("inputs")]
        public Input[] Inputs { get; } = inputs ?? [];
    }
}
