namespace OBSStudioClient
{
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets an array of all available transition kinds.
        /// </summary>
        /// <returns>Array of transition kinds</returns>
        public async Task<string[]> GetTransitionKindList()
        {
            return (await this.SendRequestAsync<TransitionKindsResponse>()).TransitionKinds;
        }

        /// <summary>
        /// Gets an array of all scene transitions in OBS.
        /// </summary>
        /// <returns>A <see cref="SceneTransitionListResponse"/></returns>
        public async Task<SceneTransitionListResponse> GetSceneTransitionList()
        {
            return await this.SendRequestAsync<SceneTransitionListResponse>();
        }

        /// <summary>
        /// Gets information about the current scene transition.
        /// </summary>
        /// <returns>A <see cref="ExtendedTransitionResponse"/></returns>
        public async Task<ExtendedTransitionResponse> GetCurrentSceneTransition()
        {
            return await this.SendRequestAsync<ExtendedTransitionResponse>();
        }

        /// <summary>
        /// Sets the current scene transition.
        /// </summary>
        /// <param name="transitionName">Name of the transition to make active</param>
        /// <remarks>
        /// Small note: While the namespace of scene transitions is generally unique, that uniqueness is not a guarantee as it is with other resources like inputs.
        /// </remarks>
        public async Task SetCurrentSceneTransition(string transitionName)
        {
            await this.SendRequestAsync(new { transitionName });
        }

        /// <summary>
        /// Sets the duration of the current scene transition, if it is not fixed.
        /// </summary>
        /// <param name="transitionDuration">Duration in milliseconds.</param>
        public async Task SetCurrentSceneTransitionDuration(float transitionDuration)
        {
            await this.SendRequestAsync(new { transitionDuration });
        }

        /// <summary>
        /// Sets the settings of the current scene transition.
        /// </summary>
        /// <param name="transitionSettings">Settings object to apply to the transition. Can be {}</param>
        /// <param name="overlay">Whether to overlay over the current settings or replace them</param>
        public async Task SetCurrentSceneTransitionSettings(ExtendedTransitionResponse? transitionSettings, bool overlay = true)
        {
            await this.SendRequestAsync(new { transitionSettings, overlay });
        }

        /// <summary>
        /// Gets the cursor position of the current scene transition.
        /// </summary>
        /// <returns>Cursor position, between 0.0 and 1.0</returns>
        /// <remarks>
        /// Note: transitionCursor will return 1.0 when the transition is inactive.
        /// </remarks>
        public async Task<float> GetCurrentSceneTransitionCursor()
        {
            return (await this.SendRequestAsync<TransitionCursorResponse>()).TransitionCursor;
        }

        /// <summary>
        /// Triggers the current scene transition. Same functionality as the Transition button in studio mode.
        /// </summary>
        public async Task TriggerStudioModeTransition()
        {
            await this.SendRequestAsync();
        }

        /// <summary>
        /// Sets the position of the TBar.
        /// </summary>
        /// <param name="position">New position.</param>
        /// <param name="release">Whether to release the TBar. Only set false if you know that you will be sending another position update</param>
        /// <remarks>
        /// Very important note: This will be deprecated and replaced in a future version of obs-websocket.
        /// </remarks>
        public async Task SetTBarPosition(float position, bool release = true)
        {
            await this.SendRequestAsync(new { position, release });
        }
    }
}
