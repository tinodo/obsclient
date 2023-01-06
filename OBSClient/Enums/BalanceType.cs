namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Balance Type.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BalanceType
    {
        /// <summary>
        /// Balance Type "Sine Law"
        /// </summary>
        OBS_BALANCE_TYPE_SINE_LAW,

        /// <summary>
        /// Balance Type "Square Law"
        /// </summary>
        OBS_BALANCE_TYPE_SQUARE_LAW,

        /// <summary>
        /// Balance Type "Linear"
        /// </summary>
        OBS_BALANCE_TYPE_LINEAR,
    }
}
