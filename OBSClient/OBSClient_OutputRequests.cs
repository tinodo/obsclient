namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Messages;
    using System.Collections.Generic;

    public partial class OBSClient
    {
        /// <summary>
        /// Gets the status of the virtualcam output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public bool GetVirtualCamStatus()
        {
            return this.SendRequest<OutputActiveResponseData>().OutputActive;
        }

        /// <summary>
        /// Toggles the state of the virtualcam output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public bool ToggleVirtualCam()
        {
            return this.SendRequest<OutputActiveResponseData>().OutputActive;
        }

        /// <summary>
        /// Starts the virtualcam output.
        /// </summary>
        public void StartVirtualCam()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Stops the virtualcam output.
        /// </summary>
        public void StopVirtualCam()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Gets the status of the replay buffer output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public bool GetReplayBufferStatus()
        {
            return this.SendRequest<OutputActiveResponseData>().OutputActive;
        }

        /// <summary>
        /// Toggles the state of the replay buffer output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public bool ToggleReplayBuffer()
        {
            return this.SendRequest<OutputActiveResponseData>().OutputActive;
        }

        /// <summary>
        /// Starts the replay buffer output.
        /// </summary>
        public void StartReplayBuffer()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Stops the replay buffer output.
        /// </summary>
        public void StopReplayBuffer()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Saves the contents of the replay buffer output.
        /// </summary>
        public void SaveReplayBuffer()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Gets the filename of the last replay buffer save file.
        /// </summary>
        /// <returns>File path</returns>
        public string GetLastReplayBufferReplay()
        {
            return this.SendRequest<SavedReplayPathResponseData>().SavedReplayPath;
        }

        /// <summary>
        /// Gets the list of available outputs.
        /// </summary>
        /// <returns>Array of <see cref="Output"/></returns>
        public Output[] GetOutputList()
        {
            return this.SendRequest<OutputsResponseData>().Outputs;
        }

        /// <summary>
        /// Gets the status of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <returns>A <see cref="OutputStatusResponseData"/></returns>
        public OutputStatusResponseData GetOutputStatus(string outputName)
        {
            return this.SendRequest<OutputStatusResponseData>(new { outputName });
        }

        /// <summary>
        /// Toggles the status of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <returns>Whether the output is active</returns>
        public bool ToggleOutput(string outputName)
        {
            return this.SendRequest<OutputActiveResponseData>(new { outputName }).OutputActive;
        }

        /// <summary>
        /// Starts an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        public void StartOutput(string outputName)
        {
            this.SendRequest(new { outputName });
        }

        /// <summary>
        /// Stops an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        public void StopOutput(string outputName)
        {
            this.SendRequest(new { outputName });
        }

        /// <summary>
        /// Gets the settings of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <returns>A <see cref="OutputSettingsResponseData"/></returns>
        public Dictionary<string, object> GetOutputSettings(string outputName)
        {
            return this.SendRequest<OutputSettingsResponseData>(new { outputName }).OutputSettings;
        }

        /// <summary>
        /// Sets the settings of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <param name="outputSettings">Output settings</param>
        public void SetOutputSettings(string outputName, Dictionary<string, object> outputSettings)
        {
            this.SendRequest(new { outputName, outputSettings });
        }
    }
}
