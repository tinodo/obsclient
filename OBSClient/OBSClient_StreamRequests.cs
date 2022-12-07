namespace OBSStudioClient
{
    using OBSStudioClient.Messages;

    public partial class OBSClient
    {
        /// <summary>
        /// Gets the status of the stream output.
        /// </summary>
        /// <returns>A <see cref="OutputStatusResponseData"/></returns>
        public OutputStatusResponseData GetStreamStatus()
        {
            return this.SendRequest<OutputStatusResponseData>();
        }

        /// <summary>
        /// Toggles the status of the stream output.
        /// </summary>
        /// <returns>New state of the stream output</returns>
        public bool ToggleStream()
        {
            return this.SendRequest<OutputActiveResponseData>().OutputActive;
        }

        /// <summary>
        /// Starts the stream output.
        /// </summary>
        public void StartStream()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Stops the stream output.
        /// </summary>
        public void StopStream()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Sends CEA-608 caption text over the stream output.
        /// </summary>
        /// <param name="captionText">Caption text</param>
        public void SendStreamCaption(string captionText)
        {
            this.SendRequest(new { captionText });
        }
    }
}
