namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model.Engine;
    using OBSStudioClient.Responses;

    /// <summary>
    /// The stream of OBS Studio to its streaming service. Besides what every output has, it can send captions and tell how it does.
    /// </summary>
    public sealed class ObsStreamOutput : ObsOutput
    {
        internal ObsStreamOutput(ObsModel model)
            : base(model, ObsOutputKind.Stream)
        {
        }

        /// <summary>
        /// Asks OBS Studio for the figures of the stream: how long it runs, how many bytes it has sent, how many frames were skipped, and how congested the connection to the streaming service is.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>
        /// The figures at the moment that OBS Studio answered. <see cref="OutputStatusResponse.OutputActive"/> and <see cref="OutputStatusResponse.OutputReconnecting"/> are the state at that moment too;
        /// <see cref="ObsOutput.Status"/> is the one that the model keeps current.
        /// </returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// The figures change all the time, and OBS Studio sends no event for them: the model does not keep them, ask when you need them. The request is made in its turn, after the changes
        /// that were asked for before it (see <see cref="ObsSceneItem.SetEnabledAsync"/>). The figures of a stream that does not run are zero.
        /// </remarks>
        public Task<OutputStatusResponse> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<OutputStatusResponse>(
                new WriteJob
                {
                    Description = "asking for the figures of the stream",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetStreamStatus, null)],
                    ReadResponses = static responses => (OutputStatusResponse)responses[0].ResponseData!,
                },
                cancellationToken);
        }

        /// <summary>
        /// Sends a caption (CEA-608 text) over the stream, and returns when OBS Studio has accepted it.
        /// </summary>
        /// <param name="text">The text of the caption. It can be empty.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it sent the caption.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the caption: the stream is not running.</exception>
        /// <remarks>
        /// A caption changes nothing that the model shows, so the call does not wait for the model. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task SendCaptionAsync(string text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "sending a caption over the stream",
                    BuildRequests = () => [new WriteRequest(RequestType.SendStreamCaption, new { captionText = text })],
                },
                cancellationToken);
        }
    }
}
