namespace OBSStudioClient.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;

    /// <summary>
    /// An output of OBS Studio that is addressed by its name: one that a plugin added (a second stream, a recording to another place), or one of the four of <see cref="ObsOutputs"/> by the name
    /// that OBS Studio gave it. It can be started, stopped and asked how it does, and its settings can be read and set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model keeps no state of such an output: OBS Studio sends no event when a plugin output starts or stops, and the name is all there is to hold on to. So the methods are plain calls
    /// that return when OBS Studio has answered, and <see cref="GetStatusAsync"/> says what the output does at that moment. The four outputs of <see cref="ObsOutputs"/> are the ones that the model
    /// keeps current (<see cref="ObsOutput.Status"/>): use those for them, and this for the others.
    /// </para>
    /// <para>
    /// The object is made by <see cref="ObsOutputs.Named"/> and does not check that the output exists: calls are refused when it does not (<see cref="ObsResponseException.ErrorCode"/> is
    /// <see cref="RequestStatusCode.ResourceNotFound"/>). <see cref="ObsOutputs.GetAllAsync"/> lists the names.
    /// </para>
    /// </remarks>
    public sealed class ObsNamedOutput
    {
        internal ObsNamedOutput(ObsModel model, string name)
        {
            this.Model = model;
            this.Name = name;
        }

        /// <summary>
        /// Gets the model that the output belongs to.
        /// </summary>
        public ObsModel Model { get; }

        /// <summary>
        /// Gets the name of the output, as OBS Studio calls it (see <see cref="ObsOutputs.GetAllAsync"/>).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Starts the output, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered: the output is starting, which can take a moment (see <see cref="GetStatusAsync"/>).</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: there is no output of this name, or it runs already, or it cannot be started.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.</remarks>
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return this.CallAsync<object?>("starting", RequestType.StartOutput, static (name) => new { outputName = name }, null, cancellationToken);
        }

        /// <summary>
        /// Stops the output, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered: the output is stopping, which can take a moment.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: there is no output of this name, or it does not run.</exception>
        /// <remarks>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.</remarks>
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return this.CallAsync<object?>("stopping", RequestType.StopOutput, static (name) => new { outputName = name }, null, cancellationToken);
        }

        /// <summary>
        /// Starts the output when it is not running, and stops it when it is, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes with true when the output was started, and with false when it was stopped.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: there is no output of this name, or it cannot be started.</exception>
        /// <remarks>OBS Studio decides by itself what to do, so two toggles that are asked for one after the other make two toggles. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.</remarks>
        public Task<bool> ToggleAsync(CancellationToken cancellationToken = default)
        {
            return this.CallAsync<bool>("toggling", RequestType.ToggleOutput, static (name) => new { outputName = name }, static responses => ((OutputActiveResponse)responses[0].ResponseData!).OutputActive, cancellationToken);
        }

        /// <summary>
        /// Asks OBS Studio what the output does: whether it runs, and how long, how many bytes it has sent, how many frames it has dropped.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The status at the moment that OBS Studio answered.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: there is no output of this name.</exception>
        /// <remarks>The figures change all the time, and OBS Studio sends no event for them: ask when you need them.</remarks>
        public Task<OutputStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return this.CallAsync<OutputStatusResponse>("asking for the status of", RequestType.GetOutputStatus, static (name) => new { outputName = name }, static responses => (OutputStatusResponse)responses[0].ResponseData!, cancellationToken, readOnly: true);
        }

        /// <summary>
        /// Asks OBS Studio for the settings of the output.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A request that was not sent yet is not sent.</param>
        /// <returns>The settings, as a JSON object: the fields that the kind of output has.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call.</exception>
        /// <exception cref="TimeoutException">OBS Studio did not answer in time (<see cref="ObsModelOptions.WriteTimeout"/>).</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the request: there is no output of this name.</exception>
        /// <remarks>
        /// The settings of the outputs of OBS Studio itself are those of its output mode (simple or advanced); what a field means is up to the output. The value is a snapshot of the moment that
        /// OBS Studio answered.
        /// </remarks>
        public Task<JsonElement> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return this.CallAsync<JsonElement>(
                "asking for the settings of",
                RequestType.GetOutputSettings,
                static (name) => new { outputName = name },
                static responses => JsonSerializer.SerializeToElement(((OutputSettingsResponse)responses[0].ResponseData!).OutputSettings),
                cancellationToken,
                readOnly: true);
        }

        /// <summary>
        /// Sets the settings of the output, and returns when OBS Studio has answered.
        /// </summary>
        /// <param name="settings">The settings to set: an object (the fields and their values), for example <c>new { max_size_mb = 100 }</c>, or a <see cref="JsonElement"/>. They are laid over the settings that the output has.</param>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when OBS Studio has answered.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="settings"/> is not an object, or can not be written as JSON.</exception>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: there is no output of this name.</exception>
        /// <remarks>
        /// OBS Studio does not say what it did with settings that the output does not know. Read them again (<see cref="GetSettingsAsync"/>) to see what the output holds. See
        /// <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes.
        /// </remarks>
        public Task SetSettingsAsync(object settings, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var json = ModelJson.AsObject(settings, nameof(settings), "the settings of an output", "new { max_size_mb = 100 }");
            return this.CallAsync<object?>("setting the settings of", RequestType.SetOutputSettings, name => new { outputName = name, outputSettings = json }, null, cancellationToken);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Output '{this.Name}'";
        }

        private Task<TResult> CallAsync<TResult>(string doing, RequestType request, Func<string, object> data, Func<IReadOnlyList<RequestResponseMessage>, object?>? read, CancellationToken cancellationToken, bool readOnly = false)
        {
            var name = this.Name;
            return this.Model.Writer.ExecuteAsync<TResult>(
                new WriteJob
                {
                    Description = $"{doing} the output '{name}'",
                    ReadOnly = readOnly,
                    BuildRequests = () => [new WriteRequest(request, data(name))],
                    ReadResponses = read ?? (static _ => null),

                    // "Not found" is the name of the output here, and no object that the model holds: it is not a removal.
                    Map = static (_, response) => response.RequestStatus.Code == RequestStatusCode.ResourceNotFound ? new ObsResponseException(response.RequestStatus) : null,
                },
                cancellationToken);
        }
    }
}
