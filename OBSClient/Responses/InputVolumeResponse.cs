namespace OBSStudioClient.Responses
{
    using OBSStudioClient.Interfaces;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides the Response Data (<see cref="IResponse"/>) in the Response Message (<see cref="IMessage"/>) returned by OBS Studio after sending a successful GetInputVolume request.
    /// </summary>
    public class InputVolumeResponse : IResponse
    {
        /// <summary>
        /// Gets the input volume setting in mul.
        /// </summary>
        /// <remarks>It will be a value between 0 and 20.</remarks>
        [JsonPropertyName("inputVolumeMul")]
        public float InputVolumeMul { get; }

        /// <summary>
        /// Gets the input volume setting in dB.
        /// </summary>
        /// <remarks>It will be a value between -100 and 26.</remarks>
        [JsonPropertyName("inputVolumeDb")]
        public float InputVolumeDb { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputVolumeResponse"/> class.
        /// </summary>
        /// <param name="inputVolumeMul">The input volume setting in mul.</param>
        /// <param name="inputVolumeDb">The input volume setting in dB.</param>
        [JsonConstructor]
        public InputVolumeResponse(float inputVolumeMul, float inputVolumeDb)
        {
            this.InputVolumeMul = inputVolumeMul;
            this.InputVolumeDb = inputVolumeDb;
        }
    }
}
