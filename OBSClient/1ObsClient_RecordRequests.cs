namespace OBSStudioClient
{
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets the status of the record output.
        /// </summary>
        /// <returns>A <see cref="RecordStatusResponseData"/></returns>
        public RecordStatusResponseData GetRecordStatus()
        {
            return this.SendRequest<RecordStatusResponseData>();
        }

        /// <summary>
        /// Toggles the status of the record output.
        /// </summary>
        public void ToggleRecord()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Starts the record output.
        /// </summary>
        public void StartRecord()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Stops the record output.
        /// </summary>
        /// <returns>File name for the saved recording</returns>
        public string StopRecord()
        {
            return this.SendRequest<OutputPathResponseData>().OutputPath;
        }

        /// <summary>
        /// Toggles pause on the record output.
        /// </summary>
        public void ToggleRecordPause()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Pauses the record output.
        /// </summary>
        public void PauseRecord()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Resumes the record output.
        /// </summary>
        public void ResumeRecord()
        {
            this.SendRequest();
        }
    }
}
