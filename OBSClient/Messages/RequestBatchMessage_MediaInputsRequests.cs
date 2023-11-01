namespace OBSStudioClient.Messages
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Responses;

    public partial class RequestBatchMessage
    {
        /// <summary>
        /// Adds a request to get the status of a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <returns>A <see cref="MediaInputStatusResponse"/></returns>
        public void AddGetMediaInputStatusRequest(string inputName)
        {
            this._requests.Add(new(new { inputName }));
        }

        /// <summary>
        /// Adds a request to get the cursor position of a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <param name="mediaCursor">New cursor position to set (>= 0)</param>
        /// <remarks>
        /// This request does not perform bounds checking of the cursor position.
        /// </remarks>
        public void AddSetMediaInputCursorRequest(string inputName, int mediaCursor)
        {
            this._requests.Add(new(new { inputName, mediaCursor }));
        }

        /// <summary>
        /// Adds a request to offset the current cursor position of a media input by the specified value.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <param name="mediaCursorOffset">Value to offset the current cursor position by</param>
        /// <remarks>
        /// This request does not perform bounds checking of the cursor position.
        /// </remarks>
        public void AddOffsetMediaInputCursorRequest(string inputName, int mediaCursorOffset)
        {
            this._requests.Add(new(new { inputName, mediaCursorOffset }));
        }

        /// <summary>
        /// Adds a request to trigger an action on a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <param name="mediaAction">Identifier of the ObsMediaInputAction enum</param>
        public void AddTriggerMediaInputActionRequest(string inputName, ObsMediaInputAction mediaAction)
        {
            this._requests.Add(new(new { inputName, mediaAction }));
        }
    }
}
