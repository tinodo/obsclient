namespace OBSStudioClient.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Responses;
    using OBSStudioClient.Studio.Engine;

    public partial class ObsOutput
    {
        /// <summary>
        /// Starts the output, and returns when OBS Studio has begun to start it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when the model has seen the output start (<see cref="Status"/> is <see cref="ObsOutputStatus.Starting"/> or a later step), or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the output is running already, or it cannot be started (a stream without a streaming service, a replay buffer that is not enabled).</exception>
        /// <remarks>
        /// <para>
        /// Starting an output takes time: a stream connects to the streaming service, which can take seconds, and can fail. The call returns when OBS Studio has <b>begun</b> (the output is
        /// <see cref="ObsOutputStatus.Starting"/>), not when it runs. Watch <see cref="Status"/> (or <see cref="PropertyChanged"/>, or <see cref="ObsStudio.Changed"/>) for
        /// <see cref="ObsOutputStatus.Started"/>, and for <see cref="ObsOutputStatus.Stopped"/> again when it could not start.
        /// </para>
        /// <para>See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.</para>
        /// </remarks>
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            var starts = 0;
            return this.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"starting the {this.Name}",
                    BuildRequests = () =>
                    {
                        starts = this.StartsSeen;
                        return [new WriteRequest(this.StartRequest, null)];
                    },
                    IsConfirmed = _ => this.HasStartedSince(starts),
                    Refresh = [new RefreshTarget(OutputNode.ReadKindOf(this.Kind))],
                },
                cancellationToken);
        }

        /// <summary>
        /// Stops the output, and returns when OBS Studio has begun to stop it.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes when the model has seen the output stop (<see cref="Status"/> is <see cref="ObsOutputStatus.Stopping"/> or <see cref="ObsOutputStatus.Stopped"/>), or when the model has been read again after the events did not show it.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change: the output is not running.</exception>
        /// <remarks>
        /// Stopping an output can take a moment too (a stream sends what is buffered): the call returns when OBS Studio has begun, and <see cref="Status"/> is
        /// <see cref="ObsOutputStatus.Stopped"/> when it is done. See <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            var stops = 0;
            return this.Studio.Writer.ExecuteAsync<object?>(
                new WriteJob
                {
                    Description = $"stopping the {this.Name}",
                    BuildRequests = () =>
                    {
                        stops = this.StopsSeen;
                        return [new WriteRequest(this.StopRequest, null)];
                    },
                    IsConfirmed = _ => this.HasStoppedSince(stops),
                    Refresh = [new RefreshTarget(OutputNode.ReadKindOf(this.Kind))],
                },
                cancellationToken);
        }

        /// <summary>
        /// Starts the output when it is not running, and stops it when it is, and returns when OBS Studio has begun.
        /// </summary>
        /// <param name="cancellationToken">A token that ends the wait. A change that was not sent yet is not sent.</param>
        /// <returns>A task that completes with true when the output was started, and with false when it was stopped.</returns>
        /// <exception cref="ObsNotConnectedException">There is no connection to OBS Studio. Nothing was changed.</exception>
        /// <exception cref="ObsNotReadyException">OBS Studio is changing its scene collection, or did so after the call. Nothing was changed.</exception>
        /// <exception cref="ObsUnknownOutcomeException">OBS Studio did not answer in time, or the connection ended: it is not known whether it made the change.</exception>
        /// <exception cref="ObsNotConfirmedException">OBS Studio made the change, and the model does not show it. Do not send the change again.</exception>
        /// <exception cref="ObsResponseException">OBS Studio refused the change.</exception>
        /// <remarks>
        /// OBS Studio decides by itself what to do, so two toggles that are asked for one after the other make two toggles. See <see cref="StartAsync"/> and <see cref="StopAsync"/> for what
        /// "begun" means, and <see cref="ObsSceneItem.SetEnabledAsync"/> for the order of changes and the wait.
        /// </remarks>
        public Task<bool> ToggleAsync(CancellationToken cancellationToken = default)
        {
            var starts = 0;
            var stops = 0;
            return this.Studio.Writer.ExecuteAsync<bool>(
                new WriteJob
                {
                    Description = $"toggling the {this.Name}",
                    BuildRequests = () =>
                    {
                        starts = this.StartsSeen;
                        stops = this.StopsSeen;
                        return [new WriteRequest(this.ToggleRequest, null)];
                    },
                    ReadResponses = static responses => ((OutputActiveResponse)responses[0].ResponseData!).OutputActive,
                    IsConfirmed = result => (bool)result! ? this.HasStartedSince(starts) : this.HasStoppedSince(stops),
                    Refresh = [new RefreshTarget(OutputNode.ReadKindOf(this.Kind))],
                },
                cancellationToken);
        }

        // The model shows the start when it has seen an event of a start since the change was written, or the output is on its way or running (the events can have been missed, and a read says that it
        // is running); a read cannot say that it is on its way, so the events are what tells a start that came and went in a single step.
        private bool HasStartedSince(int starts)
        {
            return this.StartsSeen != starts || this.Status is not (ObsOutputStatus.Stopped or ObsOutputStatus.Stopping);
        }

        private bool HasStoppedSince(int stops)
        {
            return this.StopsSeen != stops || this.Status is ObsOutputStatus.Stopped or ObsOutputStatus.Stopping;
        }

        private RequestType StartRequest => this.Kind switch
        {
            ObsOutputKind.Stream => RequestType.StartStream,
            ObsOutputKind.Record => RequestType.StartRecord,
            ObsOutputKind.VirtualCamera => RequestType.StartVirtualCam,
            _ => RequestType.StartReplayBuffer,
        };

        private RequestType StopRequest => this.Kind switch
        {
            ObsOutputKind.Stream => RequestType.StopStream,
            ObsOutputKind.Record => RequestType.StopRecord,
            ObsOutputKind.VirtualCamera => RequestType.StopVirtualCam,
            _ => RequestType.StopReplayBuffer,
        };

        private RequestType ToggleRequest => this.Kind switch
        {
            ObsOutputKind.Stream => RequestType.ToggleStream,
            ObsOutputKind.Record => RequestType.ToggleRecord,
            ObsOutputKind.VirtualCamera => RequestType.ToggleVirtualCam,
            _ => RequestType.ToggleReplayBuffer,
        };
    }
}
