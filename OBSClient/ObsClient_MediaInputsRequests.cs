namespace OBSStudioClient
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Responses;

    public partial class ObsClient
    {
        /// <summary>
        /// Gets the status of a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <returns>A <see cref="MediaInputStatusResponse"/></returns>
        public async Task<MediaInputStatusResponse> GetMediaInputStatus(string inputName)
        {
            return await this.GetMediaInputStatus(inputName, null);
        }

        /// <summary>
        /// Gets the status of a media input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the media input</param>
        /// <returns>A <see cref="MediaInputStatusResponse"/></returns>
        public async Task<MediaInputStatusResponse> GetMediaInputStatus(Guid inputUuid)
        {
            return await this.GetMediaInputStatus(null, inputUuid);
        }

        private async Task<MediaInputStatusResponse> GetMediaInputStatus(string? inputName, Guid? inputUuid)
        {
            return await this.SendRequestAsync<MediaInputStatusResponse>(new { inputName, inputUuid });
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
            await this.SetMediaInputCursor(inputName, null, mediaCursor);
        }

        /// <summary>
        /// Sets the cursor position of a media input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the media input</param>
        /// <param name="mediaCursor">New cursor position to set (>= 0)</param>
        /// <remarks>
        /// This request does not perform bounds checking of the cursor position.
        /// </remarks>
        public async Task SetMediaInputCursor(Guid inputUuid, int mediaCursor)
        {
            await this.SetMediaInputCursor(null, inputUuid, mediaCursor);
        }

        private async Task SetMediaInputCursor(string? inputName, Guid? inputUuid, int mediaCursor)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, mediaCursor });
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
            await this.OffsetMediaInputCursor(inputName, null, mediaCursorOffset);
        }

        /// <summary>
        /// Offsets the current cursor position of a media input by the specified value.
        /// </summary>
        /// <param name="inputUuid">Uuid of the media input</param>
        /// <param name="mediaCursorOffset">Value to offset the current cursor position by</param>
        /// <remarks>
        /// This request does not perform bounds checking of the cursor position.
        /// </remarks>
        public async Task OffsetMediaInputCursor(Guid inputUuid, int mediaCursorOffset)
        {
            await this.OffsetMediaInputCursor(null, inputUuid, mediaCursorOffset);
        }

        private async Task OffsetMediaInputCursor(string? inputName, Guid? inputUuid, int mediaCursorOffset)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, mediaCursorOffset });
        }

        /// <summary>
        /// Triggers an action on a media input.
        /// </summary>
        /// <param name="inputName">Name of the media input</param>
        /// <param name="mediaAction">Identifier of the ObsMediaInputAction enum</param>
        public async Task TriggerMediaInputAction(string inputName, ObsMediaInputAction mediaAction)
        {
            await this.TriggerMediaInputAction(inputName, null, mediaAction);
        }

        /// <summary>
        /// Triggers an action on a media input.
        /// </summary>
        /// <param name="inputUuid">Uuid of the media input</param>
        /// <param name="mediaAction">Identifier of the ObsMediaInputAction enum</param>
        public async Task TriggerMediaInputAction(Guid inputUuid, ObsMediaInputAction mediaAction)
        {
            await this.TriggerMediaInputAction(null, inputUuid, mediaAction);
        }

        private async Task TriggerMediaInputAction(string? inputName, Guid? inputUuid, ObsMediaInputAction mediaAction)
        {
            await this.SendRequestAsync(new { inputName, inputUuid, mediaAction });
        }
    }
}
