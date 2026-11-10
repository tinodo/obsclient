namespace OBSStudioClient.Model
{
    /// <summary>
    /// A screenshot that OBS Studio made of a scene or an input.
    /// </summary>
    public sealed class ObsScreenshot
    {
        private readonly byte[] _data;

        internal ObsScreenshot(string format, byte[] data)
        {
            this.Format = format;
            this._data = data;
        }

        /// <summary>
        /// Gets the image format that was asked for, as OBS Studio names it: <c>png</c>, <c>jpg</c> and so on (see <see cref="ObsModel.SupportedImageFormats"/>).
        /// </summary>
        public string Format { get; }

        /// <summary>
        /// Gets the content of the image file: what a file of that format holds. Do not change it: it is the memory that the screenshot holds.
        /// </summary>
        public ReadOnlyMemory<byte> Data => this._data;

        /// <summary>
        /// Gets the media type of the image, for example <c>image/png</c>.
        /// </summary>
        public string MediaType => $"image/{this.Format}";

        /// <summary>
        /// Writes the screenshot to a file.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="cancellationToken">A token that ends the writing.</param>
        /// <returns>A task that completes when the file is written.</returns>
        public Task SaveAsync(string path, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            return File.WriteAllBytesAsync(path, this._data, cancellationToken);
        }

        /// <summary>
        /// Returns the screenshot as a data URI (<c>data:image/png;base64,...</c>), which a web page can show as an image.
        /// </summary>
        /// <returns>The data URI.</returns>
        public string ToDataUri()
        {
            return $"data:{this.MediaType};base64,{Convert.ToBase64String(this._data)}";
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"A {this.Format} screenshot of {this._data.Length} bytes";
        }

        // Reads what OBS Studio answers: a data URI, "data:image/png;base64,...". What comes after the comma is the image; an answer without the prefix is taken to be the image itself.
        internal static ObsScreenshot FromAnswer(string answer, string format)
        {
            var start = answer.IndexOf("base64,", StringComparison.Ordinal);
            var encoded = start < 0 ? answer : answer[(start + "base64,".Length)..];
            return new ObsScreenshot(format, Convert.FromBase64String(encoded));
        }
    }
}
