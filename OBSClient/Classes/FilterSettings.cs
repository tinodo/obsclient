namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    public class FilterSettings
    {
        [JsonPropertyName("bounds")]
        public Coordinates Bounds { get; }

        [JsonPropertyName("crop")]
        public Crop Crop { get; }

        [JsonPropertyName("duration")]
        public int Duration { get; }

        [JsonPropertyName("pos")]
        public Coordinates Pos { get; }

        [JsonPropertyName("rot")]
        public float Rot { get; }

        [JsonPropertyName("rot_sign")]
        public string RotSign { get; }

        [JsonPropertyName("scale")]
        public Coordinates Scale { get; }

        [JsonPropertyName("source")]
        public string Source { get; }

        [JsonPropertyName("transform_text")]
        public string TransformText { get; }

        [JsonConstructor]
        public FilterSettings(Coordinates bounds, Crop crop, int duration, Coordinates pos, float rot, string rotSign, Coordinates scale, string source, string transformText)
        {
            Bounds = bounds;
            Crop = crop;
            Duration = duration;
            Pos = pos;
            Rot = rot;
            RotSign = rotSign;
            Scale = scale;
            Source = source;
            TransformText = transformText;
        }
    }
}
