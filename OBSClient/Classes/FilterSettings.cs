namespace OBSStudioClient.Classes
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Provides a class for FilterSettings.
    /// </summary>
    public class FilterSettings
    {
        /// <summary>
        /// Gets the <see cref="Filter"/> bounds (<see cref="Coordinates"/>).
        /// </summary>
        [JsonPropertyName("bounds")]
        public Coordinates Bounds { get; }

        /// <summary>
        /// Gets the <see cref="Filter"/> <see cref="Crop"/>.
        /// </summary>
        [JsonPropertyName("crop")]
        public Crop Crop { get; }

        /// <summary>
        /// Gets the duration of the <see cref="Filter"/>.
        /// </summary>
        [JsonPropertyName("duration")]
        public int Duration { get; }

        /// <summary>
        /// Gets the position of the <see cref="Filter"/> (<see cref="Coordinates"/>).
        /// </summary>
        [JsonPropertyName("pos")]
        public Coordinates Pos { get; }

        /// <summary>
        /// Gets the rotation of the <see cref="Filter"/>.
        /// </summary>
        [JsonPropertyName("rot")]
        public float Rot { get; }

        /// <summary>
        /// Gets the sign for the rotation of the <see cref="Filter"/>.
        /// </summary>
        [JsonPropertyName("rot_sign")]
        public string RotSign { get; }

        /// <summary>
        /// Gets the scale of the <see cref="Filter"/> (<see cref="Coordinates"/>).
        /// </summary>
        [JsonPropertyName("scale")]
        public Coordinates Scale { get; }

        /// <summary>
        /// Gets the name of the source the <see cref="Filter"/> is on.
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; }

        /// <summary>
        /// Gets the text representation of the transform of the <see cref="Filter"/>.
        /// </summary>
        [JsonPropertyName("transform_text")]
        public string TransformText { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterSettings"/> class.
        /// </summary>
        /// <param name="bounds">The bounds of the filter.</param>
        /// <param name="crop">The crop of the filter.</param>
        /// <param name="duration">The duration of the filter.</param>
        /// <param name="pos">The position of the filter.</param>
        /// <param name="rot">The rotation of the filter.</param>
        /// <param name="rotSign">The sign of the rotation of the filter.</param>
        /// <param name="scale">The scale of the filter.</param>
        /// <param name="source">The name of the source the filter is on.</param>
        /// <param name="transformText">The text representation of the transform of the filter.</param>
        [JsonConstructor]
        public FilterSettings(Coordinates bounds, Crop crop, int duration, Coordinates pos, float rot, string rotSign, Coordinates scale, string source, string transformText)
        {
            this.Bounds = bounds;
            this.Crop = crop;
            this.Duration = duration;
            this.Pos = pos;
            this.Rot = rot;
            this.RotSign = rotSign;
            this.Scale = scale;
            this.Source = source;
            this.TransformText = transformText;
        }
    }
}
