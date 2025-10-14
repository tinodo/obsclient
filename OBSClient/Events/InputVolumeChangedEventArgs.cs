namespace OBSStudioClient.Events
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides arguments for the InputVolumeChanged event.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputVolumeChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="inputName">The input name.</param>
    /// <param name="inputUuid">The input uuid.</param>
    /// <param name="inputVolumeMul">The volume in Mul.</param>
    /// <param name="inputVolumeDb">The volume in Db.</param>
    [method: JsonConstructor]
    public class InputVolumeChangedEventArgs(string inputName, Guid inputUuid, float inputVolumeMul, float inputVolumeDb) : EventArgs
    {
        /// <summary>
        /// Gets the name of the input.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the input uuid.
        /// </summary>
        [JsonPropertyName("inputUuid")]
        public Guid InputUuid { get; } = inputUuid;

        /// <summary>
        /// Gets the volume of the input in Mul.
        /// </summary>
        [JsonPropertyName("inputVolumeMul")]
        public float InputVolumeMul { get; } = inputVolumeMul;

        /// <summary>
        /// Gets the volume of the input in Db.
        /// </summary>
        [JsonPropertyName("inputVolumeDb")]
        public float InputVolumeDb { get; } = inputVolumeDb;
    }
}
