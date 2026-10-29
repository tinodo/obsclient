namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;


    /// <summary>
    /// Provides a class for InputVolumeMeterResponse.
    /// </summary>
    /// <param name="inputLevelsMul">The input levels multiplied.</param>
    /// <param name="inputKind">The input kind. OBS Studio does not send it with the volume meters, so it is <see langword="null"/>.</param>
    /// <param name="inputName">The input name.</param>
    /// <param name="unversionedInputKind">The unversioned input kind. OBS Studio does not send it with the volume meters, so it is <see langword="null"/>.</param>
    [method: JsonConstructor]
    public sealed class InputVolumeMeterResponse(float[][] inputLevelsMul, string? inputKind, string inputName, string? unversionedInputKind)
    {
        /// <summary>
        /// Gets the input kind: <see langword="null"/>, because OBS Studio does not send it with the volume meters
        /// (obs-websocket 5.7.6 sends the name, the UUID and the levels of an input).
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string? InputKind { get; } = inputKind;

        /// <summary>
        /// Gets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the UUID of the input.
        /// </summary>
        /// <remarks>
        /// It is an <see langword="init"/> property, rather than a constructor parameter, to keep the public constructor of
        /// this class unchanged.
        /// </remarks>
        [JsonPropertyName("inputUuid")]
        public Guid? InputUuid { get; init; }

        /// <summary>
        /// Gets the unversioned input kind: <see langword="null"/>, because OBS Studio does not send it with the volume meters.
        /// </summary>
        [JsonPropertyName("unversionedInputKind")]
        public string? UnversionedInputKind { get; } = unversionedInputKind;

        /// <summary>
        /// Gets the input levels multiplied.
        /// </summary>
        [JsonPropertyName("inputLevelsMul")]
        public float[][] InputLevelsMul { get; } = inputLevelsMul;

        /// <summary>
        /// Gets the level in decibels for a specific channel index.
        /// </summary>
        /// <param name="channelIndex">The index of the channel.</param>
        /// <returns>
        /// The level of the channel in decibels: its first level (OBS Studio sends three numbers for a channel), and never less than -100 dB,
        /// which is what a silent channel gives. <see cref="float.NegativeInfinity"/> when the input has no such channel: OBS Studio sends an
        /// empty list of levels for some inputs, and a negative index is not a channel either.
        /// </returns>
        public float GetLevelDb(int channelIndex)
        {
            if (InputLevelsMul != null && channelIndex >= 0 && InputLevelsMul.Length > channelIndex && InputLevelsMul[channelIndex].Length > 0)
            {
                return AudioMath.MulToDb(InputLevelsMul[channelIndex][0]);
            }

            return float.NegativeInfinity;
        }
    }
}