namespace OBSStudioClient
{
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets the active and show state of a source.
        /// </summary>
        /// <param name="sourceName">Name of the source to get the active state of</param>
        /// <returns>A <see cref="SourceActiveResponse"/></returns>
        /// <remarks>
        /// Compatible with inputs and scenes.
        /// </remarks>
        public async Task<SourceActiveResponse> GetSourceActive(string sourceName)
        {
            return await this.SendRequestAsync<SourceActiveResponse>(new { sourceName });
        }

        /// <summary>
        /// Gets a Base64-encoded screenshot of a source.
        /// </summary>
        /// <param name="sourceName">Name of the source to take a screenshot of</param>
        /// <param name="imageFormat">Image compression format to use. Use GetVersion to get compatible image formats</param>
        /// <param name="imageWidth">Width to scale the screenshot to (between 8 and 4096)</param>
        /// <param name="imageHeight">Height to scale the screenshot to (between 8 and 4096)</param>
        /// <param name="imageCompressionQuality">Compression quality to use. 0 for high compression, 100 for uncompressed. -1 to use "default" (whatever that means, idk) (between -1 and 100)</param>
        /// <returns>Base64-encoded screenshot</returns>
        /// <remarks>
        /// The imageWidth and imageHeight parameters are treated as "scale to inner", meaning the smallest ratio will be used and the aspect ratio of the original resolution is kept. If imageWidth and imageHeight are not specified, the compressed image will use the full resolution of the source.
        /// Compatible with inputs and scenes.
        /// </remarks>
        public async Task<string> GetSourceScreenshot(string sourceName, string imageFormat, int? imageWidth = null, int? imageHeight = null, int? imageCompressionQuality = -1)
        {
            return (await this.SendRequestAsync<ImageDataResponse>(new { sourceName, imageFormat, imageWidth, imageHeight, imageCompressionQuality })).ImageData;
        }

        /// <summary>
        /// Saves a screenshot of a source to the filesystem.
        /// </summary>
        /// <param name="sourceName">Name of the source to take a screenshot of</param>
        /// <param name="imageFormat">Image compression format to use. Use GetVersion to get compatible image formats</param>
        /// <param name="imageFilePath">Path to save the screenshot file to. Eg. C:\Users\user\Desktop\screenshot.png</param>
        /// <param name="imageWidth">Width to scale the screenshot to (between 8 and 4096)</param>
        /// <param name="imageHeight">Height to scale the screenshot to (between 8 and 4096)</param>
        /// <param name="imageCompressionQuality">Compression quality to use. 0 for high compression, 100 for uncompressed. -1 to use "default" (whatever that means, idk) (between -1 and 100)</param>
        /// <returns>Base64-encoded screenshot</returns>
        /// <remarks>
        /// The imageWidth and imageHeight parameters are treated as "scale to inner", meaning the smallest ratio will be used and the aspect ratio of the original resolution is kept. If imageWidth and imageHeight are not specified, the compressed image will use the full resolution of the source.
        /// Compatible with inputs and scenes.
        /// </remarks>
        public async Task<string> SaveSourceScreenshot(string sourceName, string imageFormat, string imageFilePath, int? imageWidth = null, int? imageHeight = null, int? imageCompressionQuality = -1)
        {
            return (await this.SendRequestAsync<ImageDataResponse>(new { sourceName, imageFormat, imageFilePath, imageWidth, imageHeight, imageCompressionQuality })).ImageData;
        }
    }
}
