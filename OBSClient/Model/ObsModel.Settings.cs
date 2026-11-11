namespace OBSStudioClient.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;

    // The settings of OBS Studio that belong to the current profile and that it does not announce when they change (its video, its stream service, the folder for recordings), and the data that it keeps for an
    // application. Nothing of it is kept in the model: a call asks, or tells, and ends when OBS Studio has answered.
    public sealed partial class ObsModel
    {
        private const int LargestVideoSize = 4096;

        /// <summary>
        /// Asks OBS Studio for its video settings: the base (canvas) resolution, the output resolution and the frame rate of the profile that is current.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The settings at the moment that OBS Studio answered.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// OBS Studio does not announce a change of the video settings, so the model does not keep them: ask when you need them. They belong to the profile (<see cref="ObsProfiles"/>), so they are other
        /// ones after a switch to another profile. The request is made in its turn, after the changes that were asked for before it (see <see cref="ObsSceneItem.SetEnabledAsync"/>).
        /// </remarks>
        public Task<ObsVideoSettings> GetVideoSettingsAsync(CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<ObsVideoSettings>(
                new WriteJob
                {
                    Description = "asking for the video settings of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetVideoSettings, null)],
                    ReadResponses = static responses => ToVideoSettings((VideoSettingsResponse)responses[0].ResponseData!),
                },
                cancellationToken);
        }

        /// <summary>
        /// Changes the video settings of OBS Studio (the profile that is current), and returns when OBS Studio has made the change.
        /// </summary>
        /// <param name="baseResolution">The base (canvas) resolution in pixels, the size of what the scenes are drawn on: its width and its height; or null to leave it as it is.</param>
        /// <param name="outputResolution">The output resolution in pixels, the size of what is streamed and recorded: its width and its height; or null to leave it as it is.</param>
        /// <param name="frameRate">The frame rate as a fraction, so that 59.94 frames per second can be said exactly: the numerator and the denominator (60000 and 1001); or null to leave it as it is.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered that it made the change.</returns>
        /// <exception cref="ArgumentException">None of the three is given.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A size is not from 1 to 4096, or a part of the frame rate is less than 1.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: an output is active (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.OutputRunning"/>), or it does not take the values (<see cref="RequestStatusCode.RequestFieldOutOfRange"/>).</exception>
        /// <remarks>
        /// <para>
        /// The width and the height of a resolution, and the numerator and the denominator of the frame rate, go together: a call that gives one of a pair gives both. All that is given is changed with one request, so OBS
        /// Studio resets its video once. OBS Studio refuses the change while an output is active (obs-websocket answers <c>OutputRunning</c> with "Video settings cannot be changed while an output is active."; measured while recording): stop the output first. It takes the
        /// sizes from 1 to 4096 as obs-websocket documents them, and in its source (obs-websocket 5.7.6) the smallest size that it accepts is 8.
        /// </para>
        /// <para>
        /// OBS Studio does not announce a change of the video settings and the model does not keep them, so the call has nothing to wait for but the answer of OBS Studio: ask what it holds with
        /// <see cref="GetVideoSettingsAsync"/>. The settings belong to the profile that is current when the request is made. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </para>
        /// </remarks>
        public Task SetVideoSettingsAsync(
            (int Width, int Height)? baseResolution = null,
            (int Width, int Height)? outputResolution = null,
            (int Numerator, int Denominator)? frameRate = null,
            CancellationToken cancellationToken = default)
        {
            if (baseResolution is null && outputResolution is null && frameRate is null)
            {
                throw new ArgumentException("Give at least one of the base resolution, the output resolution and the frame rate: there would be nothing to change.");
            }

            var fields = new Dictionary<string, int>(6);
            if (baseResolution is { } canvas)
            {
                CheckVideoSize(canvas, nameof(baseResolution));
                fields["baseWidth"] = canvas.Width;
                fields["baseHeight"] = canvas.Height;
            }

            if (outputResolution is { } output)
            {
                CheckVideoSize(output, nameof(outputResolution));
                fields["outputWidth"] = output.Width;
                fields["outputHeight"] = output.Height;
            }

            if (frameRate is { } rate)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(rate.Numerator, 1, nameof(frameRate));
                ArgumentOutOfRangeException.ThrowIfLessThan(rate.Denominator, 1, nameof(frameRate));
                fields["fpsNumerator"] = rate.Numerator;
                fields["fpsDenominator"] = rate.Denominator;
            }

            return this._writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "changing the video settings of OBS Studio",
                    BuildRequests = () => [new WriteRequest(RequestType.SetVideoSettings, fields)],
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for its stream service: where the stream of the profile that is current goes.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The type of the service and its settings, **the stream key** among them.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// OBS Studio does not announce a change of the stream service, so the model does not keep it: ask when you need it. It belongs to the profile (<see cref="ObsProfiles"/>). Treat the settings as a
        /// secret: they hold the stream key, and OBS Studio gives them without it being asked for (<see cref="ObsStreamService.ToString"/> does not show them).
        /// </remarks>
        public Task<ObsStreamService> GetStreamServiceAsync(CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<ObsStreamService>(
                new WriteJob
                {
                    Description = "asking for the stream service of OBS Studio",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetStreamServiceSettings, null)],
                    ReadResponses = static responses =>
                    {
                        var response = (StreamServiceSettingsResponse)responses[0].ResponseData!;
                        return new ObsStreamService(response.StreamServiceType, ModelJson.AsValue(response.StreamServiceSettings, nameof(response.StreamServiceSettings), "the settings of the stream service"));
                    },
                },
                cancellationToken);
        }

        /// <summary>
        /// Changes the stream service of OBS Studio (the profile that is current), and returns when OBS Studio has made the change.
        /// </summary>
        /// <param name="type">The type of the service: <c>rtmp_common</c> for one of the services that OBS Studio knows, <c>rtmp_custom</c> for a server and a key that you give, or the type of a plugin.</param>
        /// <param name="settings">The settings of the service: an object (for example <c>new { server = "rtmp://live.example.com/app", key = "..." }</c>), a dictionary, or a <see cref="JsonElement"/>. What the fields are called depends on the type. It can not be empty.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered that it made the change.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="type"/> is empty, or <paramref name="settings"/> is not an object, or can not be written as JSON.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: it is streaming (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.OutputRunning"/>), the settings are empty (<see cref="RequestStatusCode.RequestFieldEmpty"/>), or it has no service of that type (<see cref="RequestStatusCode.ResourceCreationFailed"/>).</exception>
        /// <remarks>
        /// <para>
        /// When the type is the one of the service that is current, the settings that you give are laid over the ones that it holds: a field that you leave out stays as it is. Another type makes OBS Studio
        /// make a new service of that type, with the settings that you give and the defaults of the type for the rest; the settings of the old service are gone. OBS Studio saves the service to the profile
        /// at once, and does not change it while it streams.
        /// </para>
        /// <para>
        /// The settings hold the stream key: do not log them. OBS Studio does not announce a change of the stream service and the model does not keep it, so the call has nothing to wait for but the answer
        /// of OBS Studio: ask what it holds with <see cref="GetStreamServiceAsync"/>. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </para>
        /// </remarks>
        public Task SetStreamServiceAsync(string type, object settings, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(type);
            ArgumentNullException.ThrowIfNull(settings);
            var given = ModelJson.AsObject(settings, nameof(settings), "the settings of a stream service", "new { server = \"rtmp://live.example.com/app\", key = \"...\" }");
            return this._writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"changing the stream service of OBS Studio to '{type}'",
                    BuildRequests = () => [new WriteRequest(RequestType.SetStreamServiceSettings, SetStreamServiceSettingsRequestData.Create(type, given))],
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for the folder that it records to.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The folder, as OBS Studio writes it (a path of the computer that OBS Studio runs on).</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// The folder belongs to the profile (<see cref="ObsProfiles"/>), and OBS Studio does not announce a change of it, so the model does not keep it: ask when you need it. It is the folder of
        /// the output mode that the profile is in (simple or advanced). The file that a recording is written to is said by the output itself (<see cref="ObsRecordOutput.CurrentFile"/>).
        /// </remarks>
        public Task<string> GetRecordDirectoryAsync(CancellationToken cancellationToken = default)
        {
            return this._writer.ExecuteAsync<string>(
                new WriteJob
                {
                    Description = "asking for the folder that OBS Studio records to",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetRecordDirectory, null)],
                    ReadResponses = static responses => ((RecordDirectoryResponse)responses[0].ResponseData!).RecordDirectory,
                },
                cancellationToken);
        }

        /// <summary>
        /// Changes the folder that OBS Studio records to (for the profile that is current), and returns when OBS Studio has made the change.
        /// </summary>
        /// <param name="directory">The folder: a path of the computer that OBS Studio runs on.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered that it made the change.</returns>
        /// <exception cref="ArgumentException"><paramref name="directory"/> is empty.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: it is recording (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.OutputRunning"/>).</exception>
        /// <remarks>
        /// OBS Studio sets the folder of both output modes, simple and advanced, and does not check that it exists. It does not change it while it records. The model does not keep the folder, so the call
        /// has nothing to wait for but the answer of OBS Studio: ask what it holds with <see cref="GetRecordDirectoryAsync"/>. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task SetRecordDirectoryAsync(string directory, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(directory);
            return this._writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = "changing the folder that OBS Studio records to",
                    BuildRequests = () => [new WriteRequest(RequestType.SetRecordDirectory, SetRecordDirectoryRequestData.Create(directory))],
                },
                cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio for the data that it keeps for an application in a slot.
        /// </summary>
        /// <param name="realm">Where the slot is: for all profiles, or for the profile that is current.</param>
        /// <param name="slotName">The name of the slot.</param>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>What the slot holds, as JSON; or null when nothing was put in the slot.</returns>
        /// <exception cref="ArgumentException"><paramref name="slotName"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="realm"/> is not a realm.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <remarks>
        /// <para>
        /// OBS Studio keeps the slots for the programs that control it, to hold what they want to remember (a setting, the state of a show): they are in files of OBS Studio, so they stay when OBS Studio
        /// closes. The slots of <see cref="ObsDataRealm.Global"/> are the same for every profile; those of <see cref="ObsDataRealm.Profile"/> belong to the profile that is current, so a switch to another profile shows
        /// other ones. Every program that is connected reads and writes the same slots: pick a name that is yours. The names of the slots are case sensitive.
        /// </para>
        /// <para>
        /// The slots are not part of the model. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of requests.
        /// </para>
        /// </remarks>
        public Task<JsonElement?> GetPersistentDataAsync(ObsDataRealm realm, string slotName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(slotName);
            var wireRealm = ModelEnums.ToWire(realm);
            return this._writer.ExecuteAsync<JsonElement?>(
                new WriteJob
                {
                    Description = $"asking for the persistent data '{slotName}'",
                    ReadOnly = true,
                    BuildRequests = () => [new WriteRequest(RequestType.GetPersistentData, GetPersistentDataRequestData.Create(wireRealm, slotName))],
                    ReadResponses = static responses => ToSlotValue(((SlotValueResponse)responses[0].ResponseData!).SlotValue),
                },
                cancellationToken);
        }

        /// <summary>
        /// Puts data in a slot that OBS Studio keeps for an application, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="realm">Where the slot is: for all profiles, or for the profile that is current.</param>
        /// <param name="slotName">The name of the slot. A slot that does not exist is made.</param>
        /// <param name="value">What to keep: text, a number, true or false, an object (for example <c>new { volume = 7 }</c>), a list, or a <see cref="JsonElement"/>. It replaces what the slot held. It can not be null: OBS Studio takes a null for a value that is missing.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered that it kept the data.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="slotName"/> is empty, or <paramref name="value"/> can not be written as JSON, or is a JSON null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="realm"/> is not a realm.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it kept the data.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: it could not write the file that holds the slots (<see cref="ObsResponseException.ErrorCode"/> is <see cref="RequestStatusCode.RequestProcessingFailed"/>).</exception>
        /// <remarks>
        /// OBS Studio writes the slots to a file at once, so they are kept when OBS Studio closes. It has no request that removes a slot, and a null is not kept (measured with OBS Studio 33.0.0: it
        /// refuses it as a missing field): put an empty value in the slot (an empty string, or an empty object) to say that there is nothing. Every program that is connected reads and writes the same slots: pick a name that is yours
        /// (see <see cref="GetPersistentDataAsync"/>). The slots are not part of the model, so the call has nothing to wait for but the answer of OBS Studio. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task SetPersistentDataAsync(ObsDataRealm realm, string slotName, object value, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(slotName);
            ArgumentNullException.ThrowIfNull(value);
            var wireRealm = ModelEnums.ToWire(realm);
            var slotValue = ModelJson.AsValue(value, nameof(value), "the value of persistent data");
            if (slotValue.ValueKind == JsonValueKind.Null)
            {
                throw new ArgumentException("OBS Studio does not keep a null: it takes it for a value that is missing. Put an empty value in the slot (an empty string, or an empty object) to say that there is nothing.", nameof(value));
            }

            return this._writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"keeping the persistent data '{slotName}'",
                    BuildRequests = () => [new WriteRequest(RequestType.SetPersistentData, SetPersistentDataRequestData.Create(wireRealm, slotName, slotValue))],
                },
                cancellationToken);
        }

        private static ObsVideoSettings ToVideoSettings(VideoSettingsResponse response)
        {
            return new ObsVideoSettings(
                (int)Math.Round(response.FpsNumerator),
                (int)Math.Round(response.FpsDenominator),
                response.BaseWidth,
                response.BaseHeight,
                response.OutputWidth,
                response.OutputHeight);
        }

        private static JsonElement? ToSlotValue(object? slotValue)
        {
            return slotValue is null ? null : ModelJson.AsValue(slotValue, nameof(slotValue), "the value of persistent data");
        }

        private static void CheckVideoSize((int Width, int Height) size, string parameter)
        {
            if (size.Width < 1 || size.Width > LargestVideoSize || size.Height < 1 || size.Height > LargestVideoSize)
            {
                throw new ArgumentOutOfRangeException(parameter, size, $"A size of the video is from 1 to {LargestVideoSize} pixels, and this is {size.Width} by {size.Height}.");
            }
        }
    }
}
