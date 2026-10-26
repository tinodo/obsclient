namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Responses;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get the status of the record output.
        /// </summary>
        /// <returns>A <see cref="RecordStatusResponse"/></returns>
        public void AddGetRecordStatusRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to toggle the status of the record output.
        /// </summary>
        public void AddToggleRecordRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to start the record output.
        /// </summary>
        public void AddStartRecordRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to stop the record output.
        /// </summary>
        /// <returns>File name for the saved recording</returns>
        public void AddStopRecordRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to toggle pause on the record output.
        /// </summary>
        public void AddToggleRecordPauseRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to pause the record output.
        /// </summary>
        public void AddPauseRecordRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to resume the record output.
        /// </summary>
        public void AddResumeRecordRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to set the current directory that the record output writes files to.
        /// </summary>
        /// <param name="recordDirectory">The directory that the record output writes to.</param>
        public void AddSetRecordDirectoryRequest(string recordDirectory)
        {
            this._requests.Add(new(new { recordDirectory }));
        }

        /// <summary>
        /// Adds a request to split the current file being recorded into a new file.
        /// </summary>
        public void AddSplitRecordFileRequest()
        {
            this._requests.Add(new());
        }

        /// <summary>
        /// Adds a request to add a new chapter marker to the file currently being recorded.
        /// </summary>
        /// <param name="chapterName">Name of the new chapter. When omitted, OBS Studio generates a default name.</param>
        /// <remarks>
        /// As of OBS 30.2.0, the only file format supporting this feature is Hybrid MP4.
        /// </remarks>
        public void AddCreateRecordChapterRequest(string? chapterName = null)
        {
            this._requests.Add(new(new { chapterName }));
        }
    }
}
