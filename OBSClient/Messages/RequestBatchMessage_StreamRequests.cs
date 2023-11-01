namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Responses;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get the status of the stream output.
        /// </summary>
        /// <returns>A <see cref="OutputStatusResponse"/></returns>
        public void AddGetStreamStatusRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to toggle the status of the stream output.
        /// </summary>
        /// <returns>New state of the stream output</returns>
        public void AddToggleStreamRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to start the stream output.
        /// </summary>
        public void AddStartStreamRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to stop the stream output.
        /// </summary>
        public void AddStopStreamRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to send CEA-608 caption text over the stream output.
        /// </summary>
        /// <param name="captionText">Caption text</param>
        public void AddSendStreamCaptionRequest(string captionText)
        {
            this._requests.Add(new(new { captionText }));
        }
    }
}
