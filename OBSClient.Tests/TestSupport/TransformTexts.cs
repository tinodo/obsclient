namespace OBSStudioClient.Tests.TestSupport
{
    using System.Globalization;
    using OBSStudioClient.Classes;

    /// <summary>The transform of a scene item as text, for the comparisons of what the model holds with what OBS Studio (or the simulation of it) holds.</summary>
    internal static class TransformTexts
    {
        /// <summary>
        /// Writes every member of the transform, the numbers as the shortest text that gives the same single precision number again: a difference that is only rounding is not one that the model can have.
        /// </summary>
        /// <param name="transform">The transform.</param>
        /// <returns>The text.</returns>
        public static string Of(SceneItemTransform transform)
        {
            static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
            static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);
            return $"transform position ({Number(transform.PositionX)}, {Number(transform.PositionY)}), rotation {Number(transform.Rotation)}, scale ({Number(transform.ScaleX)}, {Number(transform.ScaleY)}), size {Number(transform.Width)} x {Number(transform.Height)} of {Number(transform.SourceWidth)} x {Number(transform.SourceHeight)}, alignment {Whole(transform.Alignment)}, bounds {transform.BoundsType} {Number(transform.BoundsWidth)} x {Number(transform.BoundsHeight)} alignment {Whole(transform.BoundsAlignment)}, crop ({Whole(transform.CropLeft)}, {Whole(transform.CropTop)}, {Whole(transform.CropRight)}, {Whole(transform.CropBottom)}), crop to bounds {transform.CropToBounds?.ToString() ?? "(not sent)"}";
        }
    }
}
