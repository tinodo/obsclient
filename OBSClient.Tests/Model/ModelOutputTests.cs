namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The outputs of OBS Studio as the application sees them (<see cref="ObsModel.Outputs"/>): what they show, what they tell when it changes, and the changes that start,
    /// stop, pause and save them, against a stand-in for OBS Studio that answers and sends what a test tells it to.
    /// </summary>
    public class ModelOutputTests
    {
        private const int OutputEvents = 64;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static ObsModelOptions Options()
        {
            return new ObsModelOptions
            {
                ConfirmationTimeout = TimeSpan.FromMilliseconds(300),
                WriteTimeout = TimeSpan.FromMilliseconds(600),
            };
        }

        // What OBS Studio sends when an output gets into a state.
        private static Task SendAsync(ModelRig rig, ObsOutputKind kind, ObsOutputState state)
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
        private static void SendLater(ModelRig rig, ObsOutputKind kind, params ObsOutputState[] states)
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

        private static int Count(ModelRig rig, string type)
        {
            return rig.Responder.RequestLog.Count(requested => requested == type);
        }

        [Fact]
        public void TheOutputs_AreThere_BeforeAnythingIsConnected_AndAreStopped()
        {
            using var model = new ObsModel();

            Assert.Equal(4, model.Outputs.Count);
            Assert.Equal(new[] { ObsOutputKind.Stream, ObsOutputKind.Record, ObsOutputKind.VirtualCamera, ObsOutputKind.ReplayBuffer }, model.Outputs.Select(output => output.Kind).ToArray());
            Assert.Equal(new[] { "stream", "recording", "virtual camera", "replay buffer" }, model.Outputs.Select(output => output.Name).ToArray());
            Assert.All(model.Outputs, output => Assert.Equal(ObsOutputStatus.Stopped, output.Status));
            Assert.All(model.Outputs, output => Assert.False(output.IsActive));
            Assert.All(model.Outputs, output => Assert.Same(model, output.Model));
        }

        [Fact]
        public void TheOutputs_CanBeHadByTheirKind_OrByNameOfTheProperty_AndAreTheSameObjects()
        {
            using var model = new ObsModel();
            var outputs = model.Outputs;

            Assert.Same(outputs.Stream, outputs[ObsOutputKind.Stream]);
            Assert.Same(outputs.Record, outputs[ObsOutputKind.Record]);
            Assert.Same(outputs.VirtualCamera, outputs[ObsOutputKind.VirtualCamera]);
            Assert.Same(outputs.ReplayBuffer, outputs[ObsOutputKind.ReplayBuffer]);
            Assert.Same(outputs.Record, outputs[1]);
            Assert.Same(outputs, model.Outputs);
            Assert.Throws<ArgumentOutOfRangeException>(() => outputs[(ObsOutputKind)9]);
        }

        [Fact]
        public async Task TheOutputs_ShowWhatOBSStudioDoes_WhenTheModelIsConnected()
        {
            await using var rig = await ModelRig.ConnectedAsync(
                world =>
                {
                    world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Paused;
                    world.Outputs[ObsOutputKind.VirtualCamera] = ObsOutputStatus.Started;
                },
                Options());
            var outputs = rig.Model.Outputs;

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
        public async Task TheModel_SubscribesToTheEventsOfTheOutputs_WhateverTheApplicationAsksFor()
        {
            await using var rig = new ModelSimRig(options: new ObsModelOptions { AdditionalEvents = EventSubscriptions.None });

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);

            Assert.NotNull(rig.Server.EventSubscriptions);
            Assert.Equal(EventSubscriptions.Outputs, (EventSubscriptions)rig.Server.EventSubscriptions!.Value & EventSubscriptions.Outputs);
        }

        [Fact]
        public async Task ARecording_TellsItsSteps_InPropertyChanged_AndInChanged()
        {
            await using var rig = await ModelRig.ConnectedAsync(options: Options());
            var record = rig.Model.Outputs.Record;
            var properties = new List<string>();
            record.PropertyChanged += (_, e) =>
            {
                // The empty name says that everything changed: it comes once when the model is synchronized.
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
                await ModelRig.EventuallyAsync(() => record.Status == status, $"the recording to be {status}");
                expected.AddRange(told);
                await ModelRig.EventuallyAsync(
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

            await ModelRig.EventuallyAsync(() => rig.Changes.Count(change => ReferenceEquals(change.Entity, record)) == steps.Length, "the records of the changes");
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
            await using var rig = await ModelRig.ConnectedAsync(options: new ObsModelOptions { AutoReconnect = true, ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromMilliseconds(600) });
            var stream = rig.Model.Outputs.Stream;
            Assert.False(stream.IsActive);

            // The stream starts while nobody is listening, and the model reconnects by itself.
            rig.Responder.Change(world => world.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Started);
            rig.Responder.AbortConnection();

            await ModelRig.EventuallyAsync(() => stream.IsActive, "the stream to show what OBS Studio does", Timeout);
            Assert.True(rig.Responder.Handshakes >= 2);
        }

        [Fact]
        public async Task StartingAnOutput_ReturnsWhenOBSStudioHasBegun_AndNotWhenItRuns()
        {
            await using var rig = await ModelRig.ConnectedAsync(options: Options());
            var stream = rig.Model.Outputs.Stream;
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
            await ModelRig.EventuallyAsync(() => stream.IsActive, "the stream to run");
        }

        [Fact]
        public async Task AStartThatCameAndWentInASingleStep_IsDone_AndTheOutputIsStopped()
        {
            await using var rig = await ModelRig.ConnectedAsync(options: Options());
            var stream = rig.Model.Outputs.Stream;
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

            await ModelRig.EventuallyAsync(() => stream.Status == ObsOutputStatus.Stopped, "the stream to be stopped again");
            Assert.False(stream.IsActive);
            Assert.Equal(1, Count(rig, "StartStream"));
        }

        [Fact]
        public async Task AStartThatNoEventShows_IsShownByARead()
        {
            await using var rig = await ModelRig.ConnectedAsync(options: Options());
            var record = rig.Model.Outputs.Record;
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
            await using var rig = await ModelRig.ConnectedAsync(options: Options());
            var record = rig.Model.Outputs.Record;
            rig.Responder.OnRequest = (type, data) => type == "StartRecord" ? (100, null) : null;

            await record.StartAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsOutputStatus.Stopped, record.Status);
        }

        [Fact]
        public async Task StoppingAnOutput_ReturnsWhenOBSStudioHasBegun_AndNotWhenItIsDone()
        {
            await using var rig = await ModelRig.ConnectedAsync(world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started, Options());
            var record = rig.Model.Outputs.Record;
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
            await ModelRig.EventuallyAsync(() => record.Status == ObsOutputStatus.Stopped, "the recording to be stopped");
        }

        [Fact]
        public async Task ToggleAsync_SaysWhatOBSStudioDid_BothWays()
        {
            await using var rig = await ModelRig.ConnectedAsync(options: Options());
            var camera = rig.Model.Outputs.VirtualCamera;
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
            await using var rig = await ModelRig.ConnectedAsync(options: Options());
            rig.Responder.OnRequest = (type, data) => type == request ? (code, null) : null;
            var output = request switch
            {
                "StartStream" => rig.Model.Outputs.Stream,
                "StartRecord" => rig.Model.Outputs.Record,
                _ => (ObsOutput)rig.Model.Outputs.ReplayBuffer,
            };

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => output.StartAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(expected, exception.ErrorCode);
            Assert.Equal(ObsOutputStatus.Stopped, output.Status);
        }

        [Fact]
        public async Task AStopThatOBSStudioRefuses_IsAResponseException_AndTheOutputIsAsItWas()
        {
            await using var rig = await ModelRig.ConnectedAsync(options: Options());
            var stream = rig.Model.Outputs.Stream;
            rig.Responder.OnRequest = (type, data) => type == "StopStream" ? (501, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => stream.StopAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.OutputNotRunning, exception.ErrorCode);
            Assert.Equal(ObsOutputStatus.Stopped, stream.Status);
        }

        [Fact]
        public async Task ARecording_CanBePausedAndResumed_AndTheModelShowsIt()
        {
            await using var rig = await ModelRig.ConnectedAsync(world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started, Options());
            var record = rig.Model.Outputs.Record;
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
            await using var rig = await ModelRig.ConnectedAsync(options: Options());
            var record = rig.Model.Outputs.Record;
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
            await using var rig = await ModelRig.ConnectedAsync(world => world.Outputs[ObsOutputKind.ReplayBuffer] = ObsOutputStatus.Started, Options());
            var buffer = rig.Model.Outputs.ReplayBuffer;
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
            await using var rig = await ModelRig.ConnectedAsync(options: Options());
            rig.Responder.OnRequest = (type, data) => type == "SaveReplayBuffer" ? (501, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => rig.Model.Outputs.ReplayBuffer.SaveAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.OutputNotRunning, exception.ErrorCode);
        }

        [Fact]
        public async Task AStartThatOBSStudioCannotMake_IsStoppedAgain_AfterTheOutputStepTimeout()
        {
            await using var rig = await ModelRig.ConnectedAsync(
                options: new ObsModelOptions
                {
                    OutputStepTimeout = TimeSpan.FromMilliseconds(400),
                    ConfirmationTimeout = TimeSpan.FromMilliseconds(300),
                    WriteTimeout = TimeSpan.FromMilliseconds(600),
                });
            var record = rig.Model.Outputs.Record;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "StartRecord")
                {
                    return null;
                }

                // As a real OBS Studio does when the encoder of a recording does not start: it answers, sends that it is starting, shows an error in its own window, and sends nothing more.
                SendLater(rig, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);
                return (100, null);
            };

            await record.StartAsync().WithTimeoutAsync(Timeout);
            Assert.Equal(ObsOutputStatus.Starting, record.Status);

            await ModelRig.EventuallyAsync(() => record.Status == ObsOutputStatus.Stopped, "the recording to be stopped again", Timeout);
            Assert.True(Count(rig, "GetRecordStatus") >= 2);
        }

        [Fact]
        public async Task AStopThatTakesLong_IsNotTakenForDone_WhileOBSStudioRunsTheOutput()
        {
            await using var rig = await ModelRig.ConnectedAsync(
                world => world.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Started,
                new ObsModelOptions { OutputStepTimeout = TimeSpan.FromMilliseconds(300), ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromMilliseconds(600) });
            var stream = rig.Model.Outputs.Stream;
            rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "StopStream")
                {
                    return null;
                }

                SendLater(rig, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING);
                return (100, null);
            };

            await stream.StopAsync().WithTimeoutAsync(Timeout);
            Assert.Equal(ObsOutputStatus.Stopping, stream.Status);

            // The stream still runs (it sends what it has buffered) when the model looks: it is stopping still. The events that end the stop come later.
            var reads = Count(rig, "GetStreamStatus");
            await ModelRig.EventuallyAsync(() => Count(rig, "GetStreamStatus") > reads, "the model to look at the stream", Timeout);
            Assert.Equal(ObsOutputStatus.Stopping, stream.Status);

            rig.Responder.Change(world => world.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Stopped);
            await ModelRig.EventuallyAsync(() => stream.Status == ObsOutputStatus.Stopped, "the stream to be stopped", Timeout);
        }

        [Fact]
        public void TheOutputStepTimeout_IsCheckedLikeTheOtherTimes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsModel(new ObsModelOptions { OutputStepTimeout = TimeSpan.Zero }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsModel(new ObsModelOptions { OutputStepTimeout = TimeSpan.FromDays(25) }));
            using var model = new ObsModel(new ObsModelOptions { OutputStepTimeout = TimeSpan.FromMilliseconds(1) });
            Assert.NotNull(model);
        }

        [Fact]
        public async Task AChangeOfAnOutput_WithoutAConnection_IsANotConnectedException_AndNothingIsSent()
        {
            await using var rig = ModelRig.Create();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Model.Outputs.Stream.StartAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Model.Outputs.Record.PauseAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Model.Outputs.ReplayBuffer.SaveAsync().WithTimeoutAsync(Timeout));

            Assert.Empty(rig.Responder.RequestLog);
        }
    }
}
