namespace OBSStudioClient.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;

    // What OBS Studio can be asked about a scene or an input, and told to do with it, that the model does not keep: whether it is on the air, a screenshot, a hotkey, a projector.
    public abstract partial class ObsSource
    {
        /// <summary>
        /// Triggers a hotkey that this scene or input has (<c>libobs.mute</c> of an input, for example), as if the key was pressed and released, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="name">The name of the hotkey: one of <see cref="ObsModel.GetHotkeysAsync"/>.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
        /// <exception cref="ObsRemovedException">The scene or input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it triggered the hotkey.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: this scene or input has no hotkey of that name (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.ResourceNotFound"/>).</exception>
        /// <remarks>
        /// It is <see cref="ObsModel.TriggerHotkeyAsync(string, string?, CancellationToken)"/> with the name of this scene or input as the context; the name is the one that it has when the request is
        /// made (after the changes that were asked for before it: a rename included). Prefer the method of the model that does the same as the hotkey, when there is one (<see cref="ObsInput.SetMutedAsync"/>).
        /// </remarks>
        public Task TriggerHotkeyAsync(string name, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"triggering the hotkey '{name}' of {this.Label}",
                    Subject = this.Label,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.TriggerHotkeyByName, TriggerHotkeyByNameRequestData.Create(name, this.Name))],
                    Map = static (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceNotFound ? new ObsResponseException(response.RequestStatus) : null,
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio whether the scene or the input is on the air, and whether it is shown anywhere.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>
        /// <see cref="SourceActiveResponse.VideoActive"/>: whether it is shown in the program. <see cref="SourceActiveResponse.VideoShowing"/>: whether it is shown in the user interface of OBS Studio
        /// (the preview, a projector, the window of its properties).
        /// </returns>
        /// <exception cref="ObsRemovedException">The scene or input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request.</exception>
        /// <remarks>
        /// The answer is the state at the moment that OBS Studio answered; it changes as the program changes. The request is made in its turn, after the changes that were asked for before it (see
        /// <see cref="ObsSceneItem.SetEnabledAsync"/>), but a change that OBS Studio has answered may not have reached the picture yet.
        /// </remarks>
        public Task<SourceActiveResponse> GetActivityAsync(CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<SourceActiveResponse>(
                new WriteJob
                {
                    Description = $"asking whether {this.Label} is active",
                    Subject = this.Label,
                    ReadOnly = true,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.GetSourceActive, GetSourceActiveRequestData.Create(this.Uuid, null))],
                    ReadResponses = static responses => (SourceActiveResponse)responses[0].ResponseData!,
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for a screenshot of the scene or the input, as it is rendered at the moment.
        /// </summary>
        /// <param name="format">The image format: one of <see cref="ObsModel.SupportedImageFormats"/> (<c>png</c> and <c>jpg</c> are always among them).</param>
        /// <param name="width">The width to scale the image to, from 8 to 4096, or null. With a height, the image is scaled to fit inside both, and keeps its proportions.</param>
        /// <param name="height">The height to scale the image to, from 8 to 4096, or null. Without a width and a height the image has the size of the source.</param>
        /// <param name="quality">The compression quality, from 0 (the smallest file) to 100 (uncompressed), or -1 or null for the default of the format.</param>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The screenshot.</returns>
        /// <exception cref="ArgumentException"><paramref name="format"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is not from 8 to 4096, or <paramref name="quality"/> is not from -1 to 100.</exception>
        /// <exception cref="ObsRemovedException">The scene or input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: it does not support the format, or it could not render or encode the image.</exception>
        /// <remarks>
        /// The image travels over the connection as text: a large screenshot of a source with a high resolution takes time and memory (and a message that large can be refused by the settings
        /// of the server); ask for a size that you need, or use <see cref="SaveScreenshotAsync"/> to have OBS Studio write the file. See <see cref="GetActivityAsync"/> for the moment that the
        /// image shows.
        /// </remarks>
        public Task<ObsScreenshot> GetScreenshotAsync(string format = "png", int? width = null, int? height = null, int? quality = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(format);
            CheckImageArguments(width, height, quality);
            return this.Model.Writer.ExecuteAsync<ObsScreenshot>(
                new WriteJob
                {
                    Description = $"taking a screenshot of {this.Label}",
                    Subject = this.Label,
                    ReadOnly = true,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.GetSourceScreenshot, GetSourceScreenshotRequestData.Create(this.Uuid, format, width, height, quality, null))],
                    ReadResponses = responses => ObsScreenshot.FromAnswer(((ImageDataResponse)responses[0].ResponseData!).ImageData, format),
                },
                cancellationToken);
        }

        /// <summary>
        /// Has OBS Studio write a screenshot of the scene or the input to a file, and returns when the file is written.
        /// </summary>
        /// <param name="path">The path of the file, on the computer that OBS Studio runs on: for example <c>C:\Users\me\Desktop\screenshot.png</c>. The folder has to exist.</param>
        /// <param name="format">The image format: one of <see cref="ObsModel.SupportedImageFormats"/>. It is not taken from the extension of the path.</param>
        /// <param name="width">The width to scale the image to, from 8 to 4096, or null.</param>
        /// <param name="height">The height to scale the image to, from 8 to 4096, or null.</param>
        /// <param name="quality">The compression quality, from 0 (the smallest file) to 100 (uncompressed), or -1 or null for the default of the format.</param>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has written the file.</returns>
        /// <exception cref="ArgumentException"><paramref name="path"/> or <paramref name="format"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is not from 8 to 4096, or <paramref name="quality"/> is not from -1 to 100.</exception>
        /// <exception cref="ObsRemovedException">The scene or input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it wrote the file.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: it does not support the format, the folder does not exist, or it could not render, encode or write the image.</exception>
        /// <remarks>
        /// The file is written on the computer of OBS Studio. OBS Studio does not send <see cref="ObsModel.ScreenshotSaved"/> for a screenshot that a request made: the call returns when the file is
        /// written. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task SaveScreenshotAsync(string path, string format = "png", int? width = null, int? height = null, int? quality = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentException.ThrowIfNullOrEmpty(format);
            CheckImageArguments(width, height, quality);
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"saving a screenshot of {this.Label} to '{path}'",
                    Subject = this.Label,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.SaveSourceScreenshot, SaveSourceScreenshotRequestData.Create(this.Uuid, format, path, width, height, quality, null))],

                    // OBS Studio also says "not found" for a folder that does not exist: that is the path, not the source.
                    Map = static (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceNotFound && response.RequestStatus.Comment?.Contains("directory", StringComparison.OrdinalIgnoreCase) == true
                        ? new ObsResponseException(response.RequestStatus)
                        : null,
                },
                cancellationToken);
        }

        /// <summary>
        /// Opens a projector of the scene or the input on a monitor or in a window of OBS Studio, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="monitorIndex">The index of the monitor that the projector fills (see <see cref="ObsModel.GetMonitorsAsync"/>), or null (or -1) for a window.</param>
        /// <param name="geometry">The position and size of the window, as Qt writes them (base64), for a projector in a window; it can not be given together with a monitor, or null.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ObsRemovedException">The scene or input was destroyed.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it opened the projector.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request (a monitor and a geometry were both given, or the monitor does not exist).</exception>
        /// <remarks>It changes nothing in the model, so the call does not wait for it. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.</remarks>
        public Task OpenProjectorAsync(int? monitorIndex = null, string? geometry = null, CancellationToken cancellationToken = default)
        {
            return this.Model.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"opening a projector of {this.Label}",
                    Subject = this.Label,
                    Check = this.CheckNotDestroyed,
                    BuildRequests = () => [new WriteRequest(RequestType.OpenSourceProjector, OpenSourceProjectorRequestData.Create(this.Uuid, monitorIndex, geometry, null))],
                },
                cancellationToken);
        }

        private static void CheckImageArguments(int? width, int? height, int? quality)
        {
            if (width is { } w)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(w, 8, nameof(width));
                ArgumentOutOfRangeException.ThrowIfGreaterThan(w, 4096, nameof(width));
            }

            if (height is { } h)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(h, 8, nameof(height));
                ArgumentOutOfRangeException.ThrowIfGreaterThan(h, 4096, nameof(height));
            }

            if (quality is { } q)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(q, -1, nameof(quality));
                ArgumentOutOfRangeException.ThrowIfGreaterThan(q, 100, nameof(quality));
            }
        }
    }
}
