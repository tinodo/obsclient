namespace OBSStudioClient
{
    using OBSStudioClient.Responses;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets the status of the stream output.
        /// </summary>
        /// <returns>A <see cref="OutputStatusResponse"/></returns>
        public async Task<OutputStatusResponse> GetStreamStatus()
        {
            return await this.SendRequestAsync<OutputStatusResponse>();
        }

        /// <summary>
        /// Toggles the status of the stream output.
        /// </summary>
        /// <returns>New state of the stream output</returns>
        public async Task<bool> ToggleStream()
        {
            return (await this.SendRequestAsync<OutputActiveResponse>()).OutputActive;
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
