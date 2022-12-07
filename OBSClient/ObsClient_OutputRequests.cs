namespace OBSStudioClient
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Messages;
    using System.Collections.Generic;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets the status of the virtualcam output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public async Task<bool> GetVirtualCamStatus()
        {
            return (await this.SendRequestAsync<OutputActiveResponse>()).OutputActive;
        }

        /// <summary>
        /// Toggles the state of the virtualcam output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public async Task<bool> ToggleVirtualCam()
        {
            return (await this.SendRequestAsync<OutputActiveResponse>()).OutputActive;
        }

        /// <summary>
        /// Starts the virtualcam output.
        /// </summary>
        public async Task StartVirtualCam()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Stops the virtualcam output.
        /// </summary>
        public async Task StopVirtualCam()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Gets the status of the replay buffer output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public async Task<bool> GetReplayBufferStatus()
        {
            return (await this.SendRequestAsync<OutputActiveResponse>()).OutputActive;
        }

        /// <summary>
        /// Toggles the state of the replay buffer output.
        /// </summary>
        /// <returns>Whether the output is active</returns>
        public async Task<bool> ToggleReplayBuffer()
        {
            return (await this.SendRequestAsync<OutputActiveResponse>()).OutputActive;
        }

        /// <summary>
        /// Starts the replay buffer output.
        /// </summary>
        public async Task StartReplayBuffer()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Stops the replay buffer output.
        /// </summary>
        public async Task StopReplayBuffer()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Saves the contents of the replay buffer output.
        /// </summary>
        public async Task SaveReplayBuffer()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Gets the filename of the last replay buffer save file.
        /// </summary>
        /// <returns>File path</returns>
        public async Task<string> GetLastReplayBufferReplay()
        {
            return (await this.SendRequestAsync<SavedReplayPathResponse>()).SavedReplayPath;
        }

        /// <summary>
        /// Gets the list of available outputs.
        /// </summary>
        /// <returns>Array of <see cref="Output"/></returns>
        public async Task<Output[]> GetOutputList()
        {
            return (await this.SendRequestAsync<OutputsResponse>()).Outputs;
        }

        /// <summary>
        /// Gets the status of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <returns>A <see cref="OutputStatusResponse"/></returns>
        public async Task<OutputStatusResponse> GetOutputStatus(string outputName)
        {
            return await this.SendRequestAsync<OutputStatusResponse>(new { outputName });
        }

        /// <summary>
        /// Toggles the status of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <returns>Whether the output is active</returns>
        public async Task<bool> ToggleOutput(string outputName)
        {
            return (await this.SendRequestAsync<OutputActiveResponse>(new { outputName })).OutputActive;
        }

        /// <summary>
        /// Starts an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        public async Task StartOutput(string outputName)
        {
            await this.SendRequestAsync(new { outputName });
        }

        /// <summary>
        /// Stops an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        public async Task StopOutput(string outputName)
        {
            await this.SendRequestAsync(new { outputName });
        }

        /// <summary>
        /// Gets the settings of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <returns>A <see cref="OutputSettingsResponse"/></returns>
        public async Task<Dictionary<string, object>> GetOutputSettings(string outputName)
        {
            return (await this.SendRequestAsync<OutputSettingsResponse>(new { outputName })).OutputSettings;
        }

        /// <summary>
        /// Sets the settings of an output.
        /// </summary>
        /// <param name="outputName">Output name</param>
        /// <param name="outputSettings">Output settings</param>
        public async Task SetOutputSettings(string outputName, Dictionary<string, object> outputSettings)
        {
            await this.SendRequestAsync(new { outputName, outputSettings });
        }
    }
}
