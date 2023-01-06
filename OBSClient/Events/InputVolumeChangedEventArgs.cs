namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputVolumeChanged event.
    /// </summary>
    public class InputVolumeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; }

        /// <summary>
        /// Gets the volume of the input in Mul.
        /// </summary>
        [JsonPropertyName("inputVolumeMul")]
        public float InputVolumeMul { get; }

        /// <summary>
        /// Gets the volume of the input in Db.
        /// </summary>
        [JsonPropertyName("inputVolumeDb")]
        public float InputVolumeDb { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputVolumeChangedEventArgs"/> class.
        /// </summary>
        /// <param name="inputName">The input name.</param>
        /// <param name="inputVolumeMul">The volume in Mul.</param>
        /// <param name="inputVolumeDb">The volume in Db.</param>
        [JsonConstructor]
        public InputVolumeChangedEventArgs(string inputName, float inputVolumeMul, float inputVolumeDb)
        {
            this.InputName = inputName;
            this.InputVolumeMul = inputVolumeMul;
            this.InputVolumeDb = inputVolumeDb;
        }
    }
}
