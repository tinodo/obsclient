namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Responses;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets an array of canvases in OBS.
        /// </summary>
        /// <returns>Array of <see cref="Canvas"/>.</returns>
        /// <remarks>
        /// Canvases let OBS Studio render multiple independent outputs; available since obs-websocket 5.7.0.
        /// </remarks>
        public async Task<Canvas[]> GetCanvasList()
        {
            return (await this.SendRequestAsync<CanvasListResponse>()).Canvases;
        }
    }
}
