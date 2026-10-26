namespace OBSStudioClient.Messages
{
    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get an array of canvases in OBS.
        /// </summary>
        /// <returns>Array of canvases</returns>
        /// <remarks>
        /// Canvases let OBS Studio render multiple independent outputs; available since obs-websocket 5.7.0.
        /// </remarks>
        public void AddGetCanvasListRequest()
        {
            this._requests.Add(new());
        }
    }
}
