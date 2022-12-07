namespace OBSStudioClient
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    public partial class OBSClient
    {
        /// <summary>
        /// Gets the status of a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <returns>A <see cref="MediaInputStatusResponseData"/></returns>
        public MediaInputStatusResponseData GetMediaInputStatus(string inputName)
        {
            return this.SendRequest<MediaInputStatusResponseData>(new { inputName });
        }

        /// <summary>
        /// Sets the cursor position of a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <param name="mediaCursor">New cursor position to set (>= 0)</param>
        /// <remarks>
        /// This request does not perform bounds checking of the cursor position.
        /// </remarks>
        public void SetMediaInputCursor(string inputName, int mediaCursor)
        {
            this.SendRequest(new { inputName, mediaCursor });
        }

        /// <summary>
        /// Offsets the current cursor position of a media input by the specified value.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <param name="mediaCursorOffset">Value to offset the current cursor position by</param>
        /// <remarks>
        /// This request does not perform bounds checking of the cursor position.
        /// </remarks>
        public void OffsetMediaInputCursor(string inputName, int mediaCursorOffset)
        {
            this.SendRequest(new { inputName, mediaCursorOffset });
        }

        /// <summary>
        /// Triggers an action on a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <param name="mediaAction">Identifier of the ObsMediaInputAction enum</param>
        public void TriggerMediaInputAction(string inputName, ObsMediaInputAction mediaAction)
        {
            this.SendRequest(new { inputName, mediaAction });
        }
    }
}
