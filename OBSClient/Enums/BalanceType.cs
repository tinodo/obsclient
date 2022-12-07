namespace OBSStudioClient.Enums
{
    using System.Text.Json.Serialization;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BalanceType
    {
        OBS_BALANCE_TYPE_SINE_LAW,
        OBS_BALANCE_TYPE_SQUARE_LAW,
        OBS_BALANCE_TYPE_LINEAR,
    }
}
