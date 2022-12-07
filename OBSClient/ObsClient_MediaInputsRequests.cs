namespace OBSStudioClient
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets the status of a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <returns>A <see cref="MediaInputStatusResponseData"/></returns>
        public async Task<MediaInputStatusResponseData> GetMediaInputStatus(string inputName)
        {
            return await this.SendRequestAsync<MediaInputStatusResponseData>(new { inputName });
        }

        /// <summary>
        /// Sets the cursor position of a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <param name="mediaCursor">New cursor position to set (>= 0)</param>
        /// <remarks>
        /// This request does not perform bounds checking of the cursor position.
        /// </remarks>
        public async Task SetMediaInputCursor(string inputName, int mediaCursor)
        {
            await this.SendRequestAsync(new { inputName, mediaCursor });
        }

        /// <summary>
        /// Offsets the current cursor position of a media input by the specified value.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <param name="mediaCursorOffset">Value to offset the current cursor position by</param>
        /// <remarks>
        /// This request does not perform bounds checking of the cursor position.
        /// </remarks>
        public async Task OffsetMediaInputCursor(string inputName, int mediaCursorOffset)
        {
            await this.SendRequestAsync(new { inputName, mediaCursorOffset });
        }

        /// <summary>
        /// Triggers an action on a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <param name="mediaAction">Identifier of the ObsMediaInputAction enum</param>
        public async Task TriggerMediaInputAction(string inputName, ObsMediaInputAction mediaAction)
        {
            await this.SendRequestAsync(new { inputName, mediaAction });
        }
    }
}
