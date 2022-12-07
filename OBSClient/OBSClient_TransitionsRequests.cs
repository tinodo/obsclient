namespace OBSStudioClient
{
    using OBSStudioClient.Messages;

    public partial class OBSClient
    {
        /// <summary>
        /// Gets an array of all available transition kinds.
        /// </summary>
        /// <returns>Array of transition kinds</returns>
        public string[] GetTransitionKindList()
        {
            return this.SendRequest<TransitionKindsResponseData>().TransitionKinds;
        }

        /// <summary>
        /// Gets an array of all scene transitions in OBS.
        /// </summary>
        /// <returns>A <see cref="SceneTransitionListResponseData"/></returns>
        public SceneTransitionListResponseData GetSceneTransitionList()
        {
            return this.SendRequest<SceneTransitionListResponseData>();
        }

        /// <summary>
        /// Gets information about the current scene transition.
        /// </summary>
        /// <returns>A <see cref="ExtendedTransitionResponseData"/></returns>
        public ExtendedTransitionResponseData GetCurrentSceneTransition()
        {
            return this.SendRequest<ExtendedTransitionResponseData>();
        }

        /// <summary>
        /// Sets the current scene transition.
        /// </summary>
        /// <param name="transitionName">Name of the transition to make active</param>
        /// <remarks>
        /// Small note: While the namespace of scene transitions is generally unique, that uniqueness is not a guarantee as it is with other resources like inputs.
        /// </remarks>
        public void SetCurrentSceneTransition(string transitionName)
        {
            this.SendRequest(new { transitionName });
        }

        /// <summary>
        /// Sets the duration of the current scene transition, if it is not fixed.
        /// </summary>
        /// <param name="transitionDuration">Duration in milliseconds (>= 50, <= 20000)</param>
        public void SetCurrentSceneTransitionDuration(float transitionDuration)
        {
            this.SendRequest(new { transitionDuration });
        }

        /// <summary>
        /// Sets the settings of the current scene transition.
        /// </summary>
        /// <param name="transitionSettings">Settings object to apply to the transition. Can be {}</param>
        /// <param name="overlay">Whether to overlay over the current settings or replace them</param>
        public void SetCurrentSceneTransitionSettings(ExtendedTransitionResponseData? transitionSettings, bool overlay = true)
        {
            this.SendRequest(new { transitionSettings, overlay });
        }

        /// <summary>
        /// Gets the cursor position of the current scene transition.
        /// </summary>
        /// <returns>Cursor position, between 0.0 and 1.0</returns>
        /// <remarks>
        /// Note: transitionCursor will return 1.0 when the transition is inactive.
        /// </remarks>
        public float GetCurrentSceneTransitionCursor()
        {
            return this.SendRequest<TransitionCursorResponseData>().TransitionCursor;
        }

        /// <summary>
        /// Triggers the current scene transition. Same functionality as the Transition button in studio mode.
        /// </summary>
        public void TriggerStudioModeTransition()
        {
            this.SendRequest();
        }

        /// <summary>
        /// Sets the position of the TBar.
        /// </summary>
        /// <param name="position">New position (>= 0.0, <= 1.0)</param>
        /// <param name="release">Whether to release the TBar. Only set false if you know that you will be sending another position update</param>
        /// <remarks>
        /// Very important note: This will be deprecated and replaced in a future version of obs-websocket.
        /// </remarks>
        public void SetTBarPosition(float position, bool release = true)
        {
            this.SendRequest(new { position, release });
        }
    }
}
