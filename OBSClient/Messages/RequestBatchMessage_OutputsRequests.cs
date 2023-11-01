namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Responses;
    using System.Collections.Generic;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get the status of the virtualcam output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public void AddGetVirtualCamStatusRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to toggle the state of the virtualcam output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public void AddToggleVirtualCamRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to start the virtualcam output.
        /// </summary>
        public void AddStartVirtualCamRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to stop the virtualcam output.
        /// </summary>
        public void AddStopVirtualCamRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to get the status of the replay buffer output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public void AddGetReplayBufferStatusRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to toggle the state of the replay buffer output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public void AddToggleReplayBufferRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to start the replay buffer output.
        /// </summary>
        public void AddStartReplayBufferRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to stop the replay buffer output.
        /// </summary>
        public void AddStopReplayBufferRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to save the contents of the replay buffer output.
        /// </summary>
        public void AddSaveReplayBufferRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to get the filename of the last replay buffer save file.
        /// </summary>
        /// <returns>File path</returns>
        public void AddGetLastReplayBufferReplayRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to get the list of available outputs.
        /// </summary>
        /// <returns>Array of <see cref="Output"/></returns>
        public void AddGetOutputListRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to get the status of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <returns>A <see cref="OutputStatusResponse"/></returns>
        public void AddGetOutputStatusRequest(string outputName)
        {
            this.Requests.Add(new(new { outputName }));
        }

        /// <summary>
        /// Adds a request to toggle the status of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <returns>Whether the output is active</returns>
        public void AddToggleOutputRequest(string outputName)
        {
            this.Requests.Add(new(new { outputName }));
        }

        /// <summary>
        /// Adds a request to start an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        public void AddStartOutputRequest(string outputName)
        {
            this.Requests.Add(new(new { outputName }));
        }

        /// <summary>
        /// Adds a request to stop an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        public void AddStopOutputRequest(string outputName)
        {
            this.Requests.Add(new(new { outputName }));
        }

        /// <summary>
        /// Adds a request to get the settings of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <returns>A <see cref="OutputSettingsResponse"/></returns>
        public void AddGetOutputSettingsRequest(string outputName)
        {
            this.Requests.Add(new(new { outputName }));
        }

        /// <summary>
        /// Adds a request to set the settings of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <param name="outputSettings">Output settings</param>
        public void AddSetOutputSettingsRequest(string outputName, Dictionary<string, object> outputSettings)
        {
            this.Requests.Add(new(new { outputName, outputSettings }));
        }
    }
}
