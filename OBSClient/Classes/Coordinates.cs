namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class Coordinates
    {
        [JsonPropertyName("x")]
        public float X { get; }

        [JsonPropertyName("x_sign")]
        public string XSign { get; }

        [JsonPropertyName("y")]
        public float Y { get; }

        [JsonPropertyName("y_sign")]
        public string YSign { get; }

        [JsonConstructor]
        public Coordinates(float x, string xSign, float y, string ySign)
        {
            X = x;
            XSign = xSign;
            Y = y;
            YSign = ySign;
        }
    }
}
