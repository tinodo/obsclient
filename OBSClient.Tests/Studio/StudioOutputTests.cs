namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The outputs of OBS Studio as the application sees them (<see cref="ObsStudio.Outputs"/>): what they show, what they tell when it changes, and the changes that start,
    /// stop, pause and save them, against a stand-in for OBS Studio that answers and sends what a test tells it to.
    /// </summary>
    public class StudioOutputTests
    {
        private const int OutputEvents = 64;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static ObsStudioOptions Options()
        {
            return new ObsStudioOptions
            {
                ConfirmationTimeout = TimeSpan.FromMilliseconds(300),
                WriteTimeout = TimeSpan.FromMilliseconds(600),
            };
        }

        // What OBS Studio sends when an output gets into a state.
        private static Task SendAsync(StudioRig rig, ObsOutputKind kind, ObsOutputState state)
        {
            var active = state is ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED or ObsOutputState.OBS_WEBSOCKET_OUTPUT_RECONNECTED or ObsOutputState.OBS_WEBSOCKET_OUTPUT_RESUMED;
            var name = state.ToString();
            return kind switch
            {
                ObsOutputKind.Stream => rig.Responder.SendEventAsync("StreamStateChanged", new { outputActive = active, outputState = name }, OutputEvents),
                ObsOutputKind.Record => rig.Responder.SendEventAsync("RecordStateChanged", new { outputActive = active, outputState = name, outputPath = "C:\\Videos\\a.mkv" }, OutputEvents),
                ObsOutputKind.VirtualCamera => rig.Responder.SendEventAsync("VirtualcamStateChanged", new { outputActive = active, outputState = name }, OutputEvents),
                _ => rig.Responder.SendEventAsync("ReplayBufferStateChanged", new { outputActive = active, outputState = name }, OutputEvents),
            };
        }

        // OBS Studio answers first, and what it does shows a moment after, as the events come: they are sent from the answer, after a short wait.
        private static void SendLater(StudioRig rig, ObsOutputKind kind, params ObsOutputState[] states)
        {
            _ = Task.Run(async () =>
            {
                foreach (var state in states)
                {
                    await Task.Delay(50);
                    await SendAsync(rig, kind, state);
                }
            });
        }

        private static int Count(StudioRig rig, string type)
        {
            return rig.Responder.RequestLog.Count(requested => requested == type);
        }

        [Fact]
        public void TheOutputs_AreThere_BeforeAnythingIsConnected_AndAreStopped()
        {
            using var studio = new ObsStudio();

            Assert.Equal(4, studio.Outputs.Count);
            Assert.Equal(new[] { ObsOutputKind.Stream, ObsOutputKind.Record, ObsOutputKind.VirtualCamera, ObsOutputKind.ReplayBuffer }, studio.Outputs.Select(output => output.Kind).ToArray());
            Assert.Equal(new[] { "stream", "recording", "virtual camera", "replay buffer" }, studio.Outputs.Select(output => output.Name).ToArray());
            Assert.All(studio.Outputs, output => Assert.Equal(ObsOutputStatus.Stopped, output.Status));
            Assert.All(studio.Outputs, output => Assert.False(output.IsActive));
            Assert.All(studio.Outputs, output => Assert.Same(studio, output.Studio));
        }

        [Fact]
        public void TheOutputs_CanBeHadByTheirKind_OrByNameOfTheProperty_AndAreTheSameObjects()
        {
            using var studio = new ObsStudio();
            var outputs = studio.Outputs;

            Assert.Same(outputs.Stream, outputs[ObsOutputKind.Stream]);
            Assert.Same(outputs.Record, outputs[ObsOutputKind.Record]);
            Assert.Same(outputs.VirtualCamera, outputs[ObsOutputKind.VirtualCamera]);
            Assert.Same(outputs.ReplayBuffer, outputs[ObsOutputKind.ReplayBuffer]);
            Assert.Same(outputs.Record, outputs[1]);
            Assert.Same(outputs, studio.Outputs);
            Assert.Throws<ArgumentOutOfRangeException>(() => outputs[(ObsOutputKind)9]);
        }

        [Fact]
        public async Task TheOutputs_ShowWhatOBSStudioDoes_WhenTheStudioIsConnected()
        {
            await using var rig = await StudioRig.ConnectedAsync(
                world =>
                {
                    world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Paused;
                    world.Outputs[ObsOutputKind.VirtualCamera] = ObsOutputStatus.Started;
                },
                Options());
            var outputs = rig.Studio.Outputs;

            Assert.Equal(ObsOutputStatus.Stopped, outputs.Stream.Status);
            Assert.False(outputs.Stream.IsActive);
            Assert.Equal(ObsOutputStatus.Paused, outputs.Record.Status);
            Assert.True(outputs.Record.IsActive);
            Assert.True(outputs.Record.IsPaused);
            Assert.Equal(ObsOutputStatus.Started, outputs.VirtualCamera.Status);
            Assert.True(outputs.VirtualCamera.IsActive);
            Assert.Equal(ObsOutputStatus.Stopped, outputs.ReplayBuffer.Status);
            Assert.Equal("The recording: Paused", outputs.Record.ToString());
        }

        [Fact]
        public async Task TheStudio_SubscribesToTheEventsOfTheOutputs_WhateverTheApplicationAsksFor()
        {
            await using var rig = new StudioSimRig(options: new ObsStudioOptions { AdditionalEvents = EventSubscriptions.None });

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);

            Assert.NotNull(rig.Server.EventSubscriptions);
            Assert.Equal(EventSubscriptions.Outputs, (EventSubscriptions)rig.Server.EventSubscriptions!.Value & EventSubscriptions.Outputs);
        }

        [Fact]
        public async Task ARecording_TellsItsSteps_InPropertyChanged_AndInChanged()
        {
            await using var rig = await StudioRig.ConnectedAsync(options: Options());
            var record = rig.Studio.Outputs.Record;
            var properties = new List<string>();
            record.PropertyChanged += (_, e) =>
            {
                // The empty name says that everything changed: it comes once when the studio is synchronized.
                if (!string.IsNullOrEmpty(e.PropertyName))
                {
                    lock (properties)
                    {
                        properties.Add(e.PropertyName);
                    }
                }
            };
            var steps = new (ObsOutputState State, ObsOutputStatus Status, string[] Told)[]
            {
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING, ObsOutputStatus.Starting, ["Status"]),
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED, ObsOutputStatus.Started, ["Status", "IsActive"]),
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_PAUSED, ObsOutputStatus.Paused, ["Status", "IsPaused"]),
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_RESUMED, ObsOutputStatus.Started, ["Status", "IsPaused"]),
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING, ObsOutputStatus.Stopping, ["Status", "IsActive"]),
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPED, ObsOutputStatus.Stopped, ["Status"]),
            };
            var expected = new List<string>();

            foreach (var (state, status, told) in steps)
            {
                await SendAsync(rig, ObsOutputKind.Record, state);
                await StudioRig.EventuallyAsync(() => record.Status == status, $"the recording to be {status}");
                expected.AddRange(told);
                await StudioRig.EventuallyAsync(
                    () =>
                    {
                        lock (properties)
                        {
                            return properties.Count >= expected.Count;
                        }
                    },
                    "the notifications of the step");
            }

            lock (properties)
            {
                Assert.Equal(expected, properties);
            }

            await StudioRig.EventuallyAsync(() => rig.Changes.Count(change => ReferenceEquals(change.Entity, record)) == steps.Length, "the records of the changes");
            var changes = rig.Changes.Where(change => ReferenceEquals(change.Entity, record)).ToArray();
            Assert.All(changes, change => Assert.Equal(ObsChangeKind.PropertyChanged, change.Kind));
            Assert.All(changes, change => Assert.Equal("Status", change.PropertyName));
            Assert.Equal(
                new[] { ObsOutputStatus.Stopped, ObsOutputStatus.Starting, ObsOutputStatus.Started, ObsOutputStatus.Paused, ObsOutputStatus.Started, ObsOutputStatus.Stopping },
                changes.Select(change => (ObsOutputStatus)change.OldValue!).ToArray());
            Assert.Equal(steps.Select(step => step.Status).ToArray(), changes.Select(change => (ObsOutputStatus)change.NewValue!).ToArray());
            Assert.Equal("the recording: Status Stopped -> Starting", changes[0].ToString());
        }

        [Fact]
        public async Task AnOutputThatChangesWhileTheConnectionIsLost_IsToldWhenItIsRestored()
        {
            await using var rig = await StudioRig.ConnectedAsync(options: new ObsStudioOptions { AutoReconnect = true, ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromMilliseconds(600) });
            var stream = rig.Studio.Outputs.Stream;
            Assert.False(stream.IsActive);

            // The stream starts while nobody is listening, and the studio reconnects by itself.
            rig.Responder.Change(world => world.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Started);
            rig.Responder.AbortConnection();

            await StudioRig.EventuallyAsync(() => stream.IsActive, "the stream to show what OBS Studio does", Timeout);
            Assert.True(rig.Responder.Handshakes >= 2);
        }

        [Fact]
        public async Task StartingAnOutput_ReturnsWhenOBSStudioHasBegun_AndNotWhenItRuns()
        {
            await using var rig = await StudioRig.ConnectedAsync(options: Options());
            var stream = rig.Studio.Outputs.Stream;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "StartStream")
                {
                    return null;
                }

                SendLater(rig, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);
                return (100, null);
            };
            var reads = Count(rig, "GetStreamStatus");

            await stream.StartAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsOutputStatus.Starting, stream.Status);
            Assert.False(stream.IsActive);
            Assert.Equal(1, Count(rig, "StartStream"));
            Assert.Equal(reads, Count(rig, "GetStreamStatus"));

            await SendAsync(rig, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED);
            await StudioRig.EventuallyAsync(() => stream.IsActive, "the stream to run");
        }

        [Fact]
        public async Task AStartThatCameAndWentInASingleStep_IsDone_AndTheOutputIsStopped()
        {
            await using var rig = await StudioRig.ConnectedAsync(options: Options());
            var stream = rig.Studio.Outputs.Stream;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "StartStream")
                {
                    return null;
                }

                // A streaming service that cannot be reached: OBS Studio begins, and ends at once, so the model can see the two events in one step.
                SendLater(rig, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPED);
                return (100, null);
            };

            await stream.StartAsync().WithTimeoutAsync(Timeout);

            await StudioRig.EventuallyAsync(() => stream.Status == ObsOutputStatus.Stopped, "the stream to be stopped again");
            Assert.False(stream.IsActive);
            Assert.Equal(1, Count(rig, "StartStream"));
        }

        [Fact]
        public async Task AStartThatNoEventShows_IsShownByARead()
        {
            await using var rig = await StudioRig.ConnectedAsync(options: Options());
            var record = rig.Studio.Outputs.Record;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "StartRecord")
                {
                    return null;
                }

                rig.Responder.Change(world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started);
                return (100, null);
            };
            var reads = Count(rig, "GetRecordStatus");

            await record.StartAsync().WithTimeoutAsync(Timeout);

            Assert.True(record.IsActive);
            Assert.True(Count(rig, "GetRecordStatus") > reads);
        }

        [Fact]
        public async Task AStartThatOBSStudioAnsweredAndDidNotMake_EndsWithWhatOBSStudioHolds()
        {
            await using var rig = await StudioRig.ConnectedAsync(options: Options());
            var record = rig.Studio.Outputs.Record;
            rig.Responder.OnRequest = (type, data) => type == "StartRecord" ? (100, null) : null;

            await record.StartAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsOutputStatus.Stopped, record.Status);
        }

        [Fact]
        public async Task StoppingAnOutput_ReturnsWhenOBSStudioHasBegun_AndNotWhenItIsDone()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started, Options());
            var record = rig.Studio.Outputs.Record;
            Assert.True(record.IsActive);
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "StopRecord")
                {
                    return null;
                }

                SendLater(rig, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING);
                return (100, new { outputPath = "C:\\Videos\\a.mkv" });
            };

            await record.StopAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsOutputStatus.Stopping, record.Status);
            Assert.False(record.IsActive);
            Assert.Equal(1, Count(rig, "StopRecord"));

            await SendAsync(rig, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPED);
            await StudioRig.EventuallyAsync(() => record.Status == ObsOutputStatus.Stopped, "the recording to be stopped");
        }

        [Fact]
        public async Task ToggleAsync_SaysWhatOBSStudioDid_BothWays()
        {
            await using var rig = await StudioRig.ConnectedAsync(options: Options());
            var camera = rig.Studio.Outputs.VirtualCamera;
            var toggles = 0;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "ToggleVirtualCam")
                {
                    return null;
                }

                // The virtual camera has no step in between: it started or it stopped.
                var started = Interlocked.Increment(ref toggles) % 2 == 1;
                SendLater(rig, ObsOutputKind.VirtualCamera, started ? ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED : ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPED);
                return (100, new { outputActive = started });
            };

            var first = await camera.ToggleAsync().WithTimeoutAsync(Timeout);
            Assert.True(first);
            Assert.True(camera.IsActive);

            var second = await camera.ToggleAsync().WithTimeoutAsync(Timeout);
            Assert.False(second);
            Assert.False(camera.IsActive);
            Assert.Equal(2, Count(rig, "ToggleVirtualCam"));
        }

        [Theory]
        [InlineData("StartStream", 500, RequestStatusCode.OutputRunning)]
        [InlineData("StartRecord", 500, RequestStatusCode.OutputRunning)]
        [InlineData("StartReplayBuffer", 504, RequestStatusCode.OutputDisabled)]
        public async Task AStartThatOBSStudioRefuses_IsAResponseException_WithItsCode(string request, int code, RequestStatusCode expected)
        {
            await using var rig = await StudioRig.ConnectedAsync(options: Options());
            rig.Responder.OnRequest = (type, data) => type == request ? (code, null) : null;
            var output = request switch
            {
                "StartStream" => rig.Studio.Outputs.Stream,
                "StartRecord" => rig.Studio.Outputs.Record,
                _ => (ObsOutput)rig.Studio.Outputs.ReplayBuffer,
            };

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => output.StartAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(expected, exception.ErrorCode);
            Assert.Equal(ObsOutputStatus.Stopped, output.Status);
        }

        [Fact]
        public async Task AStopThatOBSStudioRefuses_IsAResponseException_AndTheOutputIsAsItWas()
        {
            await using var rig = await StudioRig.ConnectedAsync(options: Options());
            var stream = rig.Studio.Outputs.Stream;
            rig.Responder.OnRequest = (type, data) => type == "StopStream" ? (501, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => stream.StopAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.OutputNotRunning, exception.ErrorCode);
            Assert.Equal(ObsOutputStatus.Stopped, stream.Status);
        }

        [Fact]
        public async Task ARecording_CanBePausedAndResumed_AndTheModelShowsIt()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started, Options());
            var record = rig.Studio.Outputs.Record;
            rig.Responder.OnRequest = (type, data) =>
            {
                switch (type)
                {
                    case "PauseRecord":
                        SendLater(rig, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_PAUSED);
                        return (100, null);
                    case "ResumeRecord":
                        SendLater(rig, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_RESUMED);
                        return (100, null);
                    default:
                        return null;
                }
            };

            await record.PauseAsync().WithTimeoutAsync(Timeout);
            Assert.True(record.IsPaused);
            Assert.True(record.IsActive);
            Assert.Equal(ObsOutputStatus.Paused, record.Status);

            await record.ResumeAsync().WithTimeoutAsync(Timeout);
            Assert.False(record.IsPaused);
            Assert.Equal(ObsOutputStatus.Started, record.Status);
        }

        [Fact]
        public async Task ARecordingThatIsNotRunning_CannotBePaused_AndOneThatIsNotPaused_CannotBeResumed()
        {
            await using var rig = await StudioRig.ConnectedAsync(options: Options());
            var record = rig.Studio.Outputs.Record;
            rig.Responder.OnRequest = (type, data) => type switch
            {
                "PauseRecord" => (501, null),
                "ResumeRecord" => (503, null),
                _ => null,
            };

            var pause = await Assert.ThrowsAsync<ObsResponseException>(() => record.PauseAsync().WithTimeoutAsync(Timeout));
            var resume = await Assert.ThrowsAsync<ObsResponseException>(() => record.ResumeAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.OutputNotRunning, pause.ErrorCode);
            Assert.Equal(RequestStatusCode.OutputNotPaused, resume.ErrorCode);
        }

        [Fact]
        public async Task TheReplayBuffer_CanBeSaved_AndTheModelIsNotChangedByIt()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.Outputs[ObsOutputKind.ReplayBuffer] = ObsOutputStatus.Started, Options());
            var buffer = rig.Studio.Outputs.ReplayBuffer;
            rig.Responder.OnRequest = (type, data) => type == "SaveReplayBuffer" ? (100, null) : null;
            var reads = Count(rig, "GetReplayBufferStatus");

            await buffer.SaveAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(1, Count(rig, "SaveReplayBuffer"));
            Assert.Equal(ObsOutputStatus.Started, buffer.Status);
            Assert.Equal(reads, Count(rig, "GetReplayBufferStatus"));
        }

        [Fact]
        public async Task ASaveThatOBSStudioRefuses_IsAResponseException()
        {
            await using var rig = await StudioRig.ConnectedAsync(options: Options());
            rig.Responder.OnRequest = (type, data) => type == "SaveReplayBuffer" ? (501, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => rig.Studio.Outputs.ReplayBuffer.SaveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.OutputNotRunning, exception.ErrorCode);
        }

        [Fact]
        public async Task AChangeOfAnOutput_WithoutAConnection_IsANotConnectedException_AndNothingIsSent()
        {
            await using var rig = StudioRig.Create();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Studio.Outputs.Stream.StartAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Studio.Outputs.Record.PauseAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Studio.Outputs.ReplayBuffer.SaveAsync().WithTimeoutAsync(Timeout));

            Assert.Empty(rig.Responder.RequestLog);
        }
    }
}
