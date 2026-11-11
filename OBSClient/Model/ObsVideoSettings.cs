namespace OBSStudioClient.Model
{
    using System.Globalization;

    /// <summary>
    /// The video settings of OBS Studio (<em>Settings</em>, <em>Video</em>) of the profile that is current: the size of the canvas that the scenes are drawn on, the size of the video that it outputs, and the frame rate.
    /// </summary>
    /// <remarks>
    /// It is a snapshot: it does not change when OBS Studio does (<see cref="ObsModel.GetVideoSettingsAsync"/> asks again). The settings belong to the profile, so another profile has other ones (<see cref="ObsProfiles"/>).
    /// </remarks>
    public sealed class ObsVideoSettings
    {
        internal ObsVideoSettings(int fpsNumerator, int fpsDenominator, int baseWidth, int baseHeight, int outputWidth, int outputHeight)
        {
            this.FpsNumerator = fpsNumerator;
            this.FpsDenominator = fpsDenominator;
            this.BaseWidth = baseWidth;
            this.BaseHeight = baseHeight;
            this.OutputWidth = outputWidth;
            this.OutputHeight = outputHeight;
        }

        /// <summary>
        /// Gets the numerator of the frame rate: 60000 for 59.94 frames per second (60000/1001).
        /// </summary>
        public int FpsNumerator { get; }

        /// <summary>
        /// Gets the denominator of the frame rate: 1001 for 59.94 frames per second (60000/1001).
        /// </summary>
        public int FpsDenominator { get; }

        /// <summary>
        /// Gets the frame rate in frames per second: the numerator divided by the denominator.
        /// </summary>
        public double FramesPerSecond => this.FpsDenominator == 0 ? 0 : (double)this.FpsNumerator / this.FpsDenominator;

        /// <summary>
        /// Gets the width of the base (canvas) resolution in pixels: what the scenes are drawn on.
        /// </summary>
        public int BaseWidth { get; }

        /// <summary>
        /// Gets the height of the base (canvas) resolution in pixels.
        /// </summary>
        public int BaseHeight { get; }

        /// <summary>
        /// Gets the width of the output resolution in pixels: what is streamed and recorded (the canvas scaled to it).
        /// </summary>
        public int OutputWidth { get; }

        /// <summary>
        /// Gets the height of the output resolution in pixels.
        /// </summary>
        public int OutputHeight { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Video {this.BaseWidth}x{this.BaseHeight} canvas, {this.OutputWidth}x{this.OutputHeight} output, {this.FramesPerSecond:0.###} fps ({this.FpsNumerator}/{this.FpsDenominator})");
        }
    }
}
