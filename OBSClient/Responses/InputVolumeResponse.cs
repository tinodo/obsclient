namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputVolume request.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="InputVolumeResponse"/> class.
    /// </remarks>
    /// <param name="inputVolumeMul">The input volume setting in mul.</param>
    /// <param name="inputVolumeDb">The input volume setting in dB.</param>
    [method: JsonConstructor]
    public class InputVolumeResponse(float inputVolumeMul, float inputVolumeDb) : IResponse
    {
        /// <summary>
        /// Gets the input volume setting in mul.
        /// </summary>
        /// <remarks>It will be a value between 0 and 20.</remarks>
        [JsonPropertyName("inputVolumeMul")]
        public float InputVolumeMul { get; } = inputVolumeMul;

        /// <summary>
        /// Gets the input volume setting in dB.
        /// </summary>
        /// <remarks>It will be a value between -100 and 26.</remarks>
        [JsonPropertyName("inputVolumeDb")]
        public float InputVolumeDb { get; } = inputVolumeDb;
    }
}
