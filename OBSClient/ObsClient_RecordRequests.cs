namespace OBSStudioClient
{
    using OBSStudioClient.Responses;

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
        public async Task<bool> ToggleRecord()
        {
            return (await this.SendRequestAsync<OutputActiveResponse>()).OutputActive;
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
        public async Task<bool> ToggleRecordPause()
        {
            return (await this.SendRequestAsync<OutputPausedResponse>()).OutputPaused;
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

        /// <summary>
        /// Sets the current directory that the record output writes files to.
        /// </summary>
        /// <param name="recordDirectory">The directory that the record output writes to.</param>
        public async Task SetRecordDirectory(string recordDirectory)
        {
            await this.SendRequestAsync(new { recordDirectory });
        }
    }
}
