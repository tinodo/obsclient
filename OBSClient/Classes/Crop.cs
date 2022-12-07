namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class Crop
    {
        [JsonPropertyName("bottom")]
        public float Bottom { get; }

        [JsonPropertyName("bottom_sign")]
        public string Bottom_sign { get; }

        [JsonPropertyName("left")]
        public float Left { get; }

        [JsonPropertyName("left_sign")]
        public string Left_sign { get; }

        [JsonPropertyName("right")]
        public float Right { get; }

        [JsonPropertyName("right_sign")]
        public string Right_sign { get; }

        [JsonPropertyName("top")]
        public float Top { get; }

        [JsonPropertyName("top_sign")]
        public string Top_sign { get; }

        [JsonConstructor]
        public Crop(float bottom, string bottom_sign, float left, string left_sign, float right, string right_sign, float top, string top_sign)
        {
            Bottom = bottom;
            Bottom_sign = bottom_sign;
            Left = left;
            Left_sign = left_sign;
            Right = right;
            Right_sign = right_sign;
            Top = top;
            Top_sign = top_sign;
        }
    }
}
