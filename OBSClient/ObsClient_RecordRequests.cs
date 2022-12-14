namespace OBSStudioClient
{
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets the status of the record output.
        /// </summary>
        /// <returns>A <see cref="RecordStatusResponse"/></returns>
        public async Task<RecordStatusResponse> GetRecordStatus()
        {
            return await this.SendRequestAsync<RecordStatusResponse>();
        }

        /// <summary>
        /// Toggles the status of the record output.
        /// </summary>
        public async Task ToggleRecord()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Starts the record output.
        /// </summary>
        public async Task StartRecord()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Stops the record output.
        /// </summary>
        /// <returns>File name for the saved recording</returns>
        public async Task<string> StopRecord()
        {
            return (await this.SendRequestAsync<OutputPathResponse>()).OutputPath;
        }

        /// <summary>
        /// Toggles pause on the record output.
        /// </summary>
        public async Task ToggleRecordPause()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Pauses the record output.
        /// </summary>
        public async Task PauseRecord()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Resumes the record output.
        /// </summary>
        public async Task ResumeRecord()
        {
            await this.SendRequestAsync();
        }
    }
}
