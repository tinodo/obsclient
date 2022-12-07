namespace OBSStudioClient
{
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets the status of the stream output.
        /// </summary>
        /// <returns>A <see cref="OutputStatusResponseData"/></returns>
        public async Task<OutputStatusResponseData> GetStreamStatus()
        {
            return await this.SendRequestAsync<OutputStatusResponseData>();
        }

        /// <summary>
        /// Toggles the status of the stream output.
        /// </summary>
        /// <returns>New state of the stream output</returns>
        public async Task<bool> ToggleStream()
        {
            return (await this.SendRequestAsync<OutputActiveResponseData>()).OutputActive;
        }

        /// <summary>
        /// Starts the stream output.
        /// </summary>
        public async Task StartStream()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Stops the stream output.
        /// </summary>
        public async Task StopStream()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Sends CEA-608 caption text over the stream output.
        /// </summary>
        /// <param name="captionText">Caption text</param>
        public async Task SendStreamCaption(string captionText)
        {
            await this.SendRequestAsync(new { captionText });
        }
    }
}
