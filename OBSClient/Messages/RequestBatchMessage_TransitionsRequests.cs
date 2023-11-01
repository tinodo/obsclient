namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Responses;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get an array of all available transition kinds.
        /// </summary>
        /// <returns>Array of transition kinds</returns>
        public void AddGetTransitionKindListRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to get an array of all scene transitions in OBS.
        /// </summary>
        /// <returns>A <see cref="SceneTransitionListResponse"/></returns>
        public void AddGetSceneTransitionListRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to get information about the current scene transition.
        /// </summary>
        /// <returns>A <see cref="TransitionResponse"/></returns>
        public void AddGetCurrentSceneTransitionRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to set the current scene transition.
        /// </summary>
        /// <param name="transitionName">Name of the transition to make active</param>
        /// <remarks>
        /// Small note: While the namespace of scene transitions is generally unique, that uniqueness is not a guarantee as it is with other resources like inputs.
        /// </remarks>
        public void AddSetCurrentSceneTransitionRequest(string transitionName)
        {
            this.Requests.Add(new(new { transitionName }));
        }

        /// <summary>
        /// Adds a request to set the duration of the current scene transition, if it is not fixed.
        /// </summary>
        /// <param name="transitionDuration">Duration in milliseconds.</param>
        public void AddSetCurrentSceneTransitionDurationRequest(float transitionDuration)
        {
            this.Requests.Add(new(new { transitionDuration }));
        }

        /// <summary>
        /// Adds a request to set the settings of the current scene transition.
        /// </summary>
        /// <param name="transitionSettings">Settings object to apply to the transition. Can be {}</param>
        /// <param name="overlay">Whether to overlay over the current settings or replace them</param>
        public void AddSetCurrentSceneTransitionSettingsRequest(TransitionResponse? transitionSettings, bool overlay = true)
        {
            this.Requests.Add(new(new { transitionSettings, overlay }));
        }

        /// <summary>
        /// Adds a request to get the cursor position of the current scene transition.
        /// </summary>
        /// <returns>Cursor position, between 0.0 and 1.0</returns>
        /// <remarks>
        /// Note: transitionCursor will return 1.0 when the transition is inactive.
        /// </remarks>
        public void AddGetCurrentSceneTransitionCursorRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to trigger the current scene transition. Same functionality as the Transition button in studio mode.
        /// </summary>
        public void AddTriggerStudioModeTransitionRequest()
        {
            this.Requests.Add(new());
        }

        /// <summary>
        /// Adds a request to set the position of the TBar.
        /// </summary>
        /// <param name="position">New position.</param>
        /// <param name="release">Whether to release the TBar. Only set false if you know that you will be sending another position update</param>
        /// <remarks>
        /// Very important note: This will be deprecated and replaced in a future version of obs-websocket.
        /// </remarks>
        public void AddSetTBarPosition(float position, bool release = true)
        {
            this.Requests.Add(new(new { position, release }));
        }
    }
}
