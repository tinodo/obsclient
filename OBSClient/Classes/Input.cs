namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for Input.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Input"/> class.
    /// </remarks>
    /// <param name="inputKind">The input kind.</param>
    /// <param name="inputName">The input name.</param>
    /// <param name="unversionedInputKind">The unversioned input kind.</param>
    /// <summary>
    /// Provides a class for Input.
    /// </summary>
    [method: JsonConstructor]
    public class Input(string inputKind, string inputName, string unversionedInputKind)
    {
        /// <summary>
        /// Gets the input kind.
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string InputKind { get; } = inputKind;

        /// <summary>
        /// Gets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the unversioned input kind.
        /// </summary>
        [JsonPropertyName("unversionedInputKind")]
        public string UnversionedInputKind { get; } = unversionedInputKind;
    }

    /// <summary>
    /// Provides a class for InputVolumeMeterResponse.
    /// </summary>
    /// <param name="inputLevelsMul">The input levels multiplied.</param>
    /// <param name="inputKind">The input kind.</param>
    /// <param name="inputName">The input name.</param>
    /// <param name="unversionedInputKind">The unversioned input kind.</param>
    [method: JsonConstructor]
    public class InputVolumeMeterResponse(float[][] inputLevelsMul, string inputKind, string inputName, string unversionedInputKind)
    {
        /// <summary>
        /// Gets the input kind.
        /// </summary>
        [JsonPropertyName("inputKind")]
        public string InputKind { get; } = inputKind;

        /// <summary>
        /// Gets the input name.
        /// </summary>
        [JsonPropertyName("inputName")]
        public string InputName { get; } = inputName;

        /// <summary>
        /// Gets the unversioned input kind.
        /// </summary>
        [JsonPropertyName("unversionedInputKind")]
        public string UnversionedInputKind { get; } = unversionedInputKind;

        /// <summary>
        /// Gets the input levels multiplied.
        /// </summary>
        [JsonPropertyName("inputLevelsMul")]
        public float[][] InputLevelsMul { get; } = inputLevelsMul;

        /// <summary>
        /// Gets the level in decibels for a specific channel index.
        /// </summary>
        /// <param name="channelIndex">The index of the channel.</param>
        /// <returns>The level in decibels.</returns>
        public float GetLevelDb(int channelIndex)
        {
            if (InputLevelsMul != null && InputLevelsMul.Length > channelIndex && InputLevelsMul[channelIndex].Length > 0)
            {
                return AudioMath.MulToDb(InputLevelsMul[channelIndex][0]);
            }
            return float.NegativeInfinity; // o un valore di default
        }
    }

    internal static class AudioMath
    {
        // Evita -Infinity quando mul = 0
        public static float MulToDb(float mul, float floorDb = -100f)
        {
            if (mul <= 0f)
                return floorDb;

            float db = 20f * MathF.Log10(mul);
            return MathF.Max(db, floorDb);
        }

        public static float DbToMul(float db)
        {
            return MathF.Pow(10f, db / 20f);
        }
    }
}
