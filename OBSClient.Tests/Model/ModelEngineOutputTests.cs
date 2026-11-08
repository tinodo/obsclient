namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model holds about the outputs of OBS Studio (the stream, the recording, the virtual camera and the replay buffer): how it reads them, what their events make
    /// of them, and what it does when OBS Studio cannot answer, changes its scene collection, or the connection ends.
    /// </summary>
    public class ModelEngineOutputTests
    {
        private const int OutputEvents = 64;

        private static EngineHarness Started(Action<FakeWorld>? setup = null)
        {
            var harness = new EngineHarness();
            harness.World.AddScene("Scene");
            setup?.Invoke(harness.World);
            harness.Start();
            harness.SettleWithTime();
            return harness;
        }

        private static ObsOutputStatus StatusOf(EngineHarness h, ObsOutputKind kind)
        {
            return h.Engine.Output(kind).Status.Value;
        }

        // The changes of the outputs since the last call.
        private static ModelChange[] OutputChanges(EngineHarness h)
        {
            return h.Engine.TakeChanges().Where(change => change.Kind == ModelChangeKind.OutputChanged).ToArray();
        }

        // What OBS Studio sends when an output gets into a state.
        private static void Send(EngineHarness h, ObsOutputKind kind, ObsOutputState state)
        {
            var active = state is not (ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING or ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPED);
            var name = state.ToString();
            switch (kind)
            {
                case ObsOutputKind.Stream:
                    h.Event("StreamStateChanged", new { outputActive = active, outputState = name }, OutputEvents);
                    break;
                case ObsOutputKind.Record:
                    h.Event("RecordStateChanged", new { outputActive = active, outputState = name, outputPath = "C:\\Videos\\a.mkv" }, OutputEvents);
                    break;
                case ObsOutputKind.VirtualCamera:
                    h.Event("VirtualcamStateChanged", new { outputActive = active, outputState = name }, OutputEvents);
                    break;
                default:
                    h.Event("ReplayBufferStateChanged", new { outputActive = active, outputState = name }, OutputEvents);
                    break;
            }
        }

        [Fact]
        public void TheModel_IsNotSettledBeforeEveryOutputWasRead_AndReadsThemAll()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();
            Assert.False(h.Engine.IsSettled);

            var types = new List<RequestType>();
            while (h.Take() is { } ticket)
            {
                types.AddRange(ticket.Requests.Select(request => request.Type));
                h.Complete(ticket);
            }

            Assert.Contains(RequestType.GetStreamStatus, types);
            Assert.Contains(RequestType.GetRecordStatus, types);
            Assert.Contains(RequestType.GetVirtualCamStatus, types);
            Assert.Contains(RequestType.GetReplayBufferStatus, types);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void WhatTheReadsSay_IsWhatTheOutputsAre()
        {
            var h = Started(world =>
            {
                world.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Reconnecting;
                world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Paused;
                world.Outputs[ObsOutputKind.VirtualCamera] = ObsOutputStatus.Started;
            });

            Assert.Equal(ObsOutputStatus.Reconnecting, StatusOf(h, ObsOutputKind.Stream));
            Assert.Equal(ObsOutputStatus.Paused, StatusOf(h, ObsOutputKind.Record));
            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.VirtualCamera));
            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.ReplayBuffer));

            // An output that is what it was before (not running) is no change.
            var changed = OutputChanges(h).Select(change => change.Text).Order().ToArray();
            Assert.Equal(new[] { "Record", "Stream", "VirtualCamera" }, changed);
        }

        [Theory]
        [InlineData(ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING, ObsOutputStatus.Starting)]
        [InlineData(ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED, ObsOutputStatus.Started)]
        [InlineData(ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING, ObsOutputStatus.Stopping)]
        [InlineData(ObsOutputState.OBS_WEBSOCKET_OUTPUT_RECONNECTING, ObsOutputStatus.Reconnecting)]
        [InlineData(ObsOutputState.OBS_WEBSOCKET_OUTPUT_RECONNECTED, ObsOutputStatus.Started)]
        [InlineData(ObsOutputState.OBS_WEBSOCKET_OUTPUT_PAUSED, ObsOutputStatus.Paused)]
        [InlineData(ObsOutputState.OBS_WEBSOCKET_OUTPUT_RESUMED, ObsOutputStatus.Started)]
        public void TheEventOfAnOutput_SaysWhatItIsDoing(ObsOutputState state, ObsOutputStatus expected)
        {
            foreach (var kind in Enum.GetValues<ObsOutputKind>())
            {
                var h = Started();
                OutputChanges(h);

                Send(h, kind, state);

                Assert.Equal(expected, StatusOf(h, kind));
                var change = Assert.Single(OutputChanges(h));
                Assert.Equal(kind.ToString(), change.Text);
                Assert.Equal(ObsOutputStatus.Stopped, change.OldValue);
                Assert.Equal(expected, change.NewValue);
            }
        }

        [Fact]
        public void TheEventThatSaysThatAnOutputStopped_MakesItStopped_AndOnlyThatOutput()
        {
            var h = Started(world =>
            {
                world.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Started;
                world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started;
            });
            OutputChanges(h);

            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPED);

            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.Record));
            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Stream));
            Assert.Equal("Record", Assert.Single(OutputChanges(h)).Text);
        }

        [Fact]
        public void AnEventThatSaysWhatTheModelHolds_ChangesNothing()
        {
            var h = Started();
            OutputChanges(h);

            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED);
            Assert.Single(OutputChanges(h));
            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED);

            Assert.Empty(OutputChanges(h));
        }

        [Fact]
        public void TheEvents_CountTheStartsAndTheStops_ThatAChangeIsConfirmedBy()
        {
            var h = Started();
            var output = h.Engine.Output(ObsOutputKind.Record);
            var steps = new (ObsOutputState State, int Starts, int Stops)[]
            {
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING, 1, 0),
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED, 2, 0),
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_PAUSED, 2, 0),
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_RESUMED, 2, 0),
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING, 2, 1),
                (ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPED, 2, 2),
            };

            foreach (var (state, starts, stops) in steps)
            {
                Send(h, ObsOutputKind.Record, state);

                Assert.True(output.StartsSeen == starts && output.StopsSeen == stops, $"After {state}: {output.StartsSeen} starts and {output.StopsSeen} stops, not {starts} and {stops}.");
            }
        }

        [Fact]
        public void AStateThatTheModelDoesNotKnow_ChangesNothing_AndMakesTheOutputBeReadAgain()
        {
            var h = Started(world => world.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Started);
            OutputChanges(h);

            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_UNKNOWN);

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Stream));
            Assert.Empty(OutputChanges(h));
            var ticket = h.Take();
            Assert.NotNull(ticket);
            Assert.Equal(ReadKind.Stream, Assert.Single(ticket!.Requests).Kind);
        }

        [Fact]
        public void ARead_ThatWasSentBeforeAnEvent_DoesNotUndoIt()
        {
            var h = Started();
            h.Engine.RequestRefresh(ReadKind.Stream, Guid.Empty, h.Now);
            var ticket = h.Take();
            Assert.NotNull(ticket);

            // The read went out when the stream was not running, and the event that says that it is comes before the answer.
            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED);
            h.Complete(ticket!);

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Stream));
        }

        [Fact]
        public void ARead_ThatSaysNotRunning_LeavesAnOutputThatIsStarting_AsItIs()
        {
            var h = Started();
            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);

            // A read cannot tell an output that is on its way up from one that is not running: the stream that connects is not running yet.
            h.Engine.RequestRefresh(ReadKind.Stream, Guid.Empty, h.Now);
            h.Settle();

            Assert.Equal(ObsOutputStatus.Starting, StatusOf(h, ObsOutputKind.Stream));
        }

        [Fact]
        public void ARead_ThatSaysRunning_LeavesAnOutputThatIsStopping_AsItIs()
        {
            var h = Started(world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started);
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING);

            // A recording that is closed is still running until it is done.
            h.Engine.RequestRefresh(ReadKind.Record, Guid.Empty, h.Now);
            h.Settle();

            Assert.Equal(ObsOutputStatus.Stopping, StatusOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void ARead_ThatSaysRunning_MakesAnOutputThatIsStarting_Started()
        {
            var h = Started();
            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);

            // The event that said that it started was lost, or the model was not told.
            h.World.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Started;
            h.Engine.RequestRefresh(ReadKind.Stream, Guid.Empty, h.Now);
            h.Settle();

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Stream));
        }

        [Fact]
        public void ARead_ThatSaysRunning_DoesNotUndoAnEventThatSaidStopped()
        {
            var h = Started(world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started);
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING);
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPED);

            // A real OBS Studio 33.0.0 does this: a moment after the event that says that a recording stopped, it still says that it runs.
            h.Engine.RequestRefresh(ReadKind.Record, Guid.Empty, h.Now);
            h.Settle();

            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void ARead_ThatSaysNotRunning_DoesNotUndoAnEventThatSaidStarted()
        {
            var h = Started();
            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED);

            h.Engine.RequestRefresh(ReadKind.Stream, Guid.Empty, h.Now);
            h.Settle();

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Stream));
        }

        [Fact]
        public void ARead_IsApplied_WhenNoEventOfThisConnectionHasToldWhatTheOutputDoes()
        {
            var h = Started();

            // OBS Studio started it, and the model was not told: the read of a change that the application made is what shows it.
            h.World.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Started;
            h.Engine.RequestRefresh(ReadKind.Stream, Guid.Empty, h.Now);
            h.Settle();

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Stream));
        }

        [Fact]
        public void AStateThatTheModelDoesNotKnow_IsReplacedByWhatTheReadSays_EvenAfterEventsThatTold()
        {
            var h = Started();
            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED);
            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Stream));

            h.World.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Stopped;
            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_UNKNOWN);
            h.Settle();

            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.Stream));
        }

        [Fact]
        public void TheOutputs_AreReadAgain_WhenTheConnectionIsRestored()
        {
            var h = Started();
            OutputChanges(h);
            h.End();

            // The recording was started while there was no connection to hear it.
            h.World.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started;
            h.Start();
            Assert.False(h.Engine.IsSettled);
            h.SettleWithTime();

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Record));
            Assert.Equal("Record", Assert.Single(OutputChanges(h)).Text);
        }

        [Fact]
        public void AStepThatAConnectionBeforeSaw_IsNotKept_WhenTheReadOfTheNextOneSaysNotRunning()
        {
            var h = Started(world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started);
            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING);
            h.End();

            // The events that end the steps were not heard: the output that was starting stopped, and the one that was stopping is stopped. No event will say so.
            h.World.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Stopped;
            h.World.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Stopped;
            h.Start();
            h.SettleWithTime();

            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.Stream));
            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void AStepThatAConnectionBeforeSaw_IsNotKept_WhenTheReadOfTheNextOneSaysRunning()
        {
            var h = Started(world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started);
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING);
            h.End();

            // The recording was stopped while the connection was down, and started again before it was restored: it runs.
            h.Start();
            h.SettleWithTime();

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void AStepThatTheNextConnectionSees_IsKept_ByItsReads()
        {
            var h = Started();
            h.End();
            h.Start();

            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);
            h.Settle();

            Assert.Equal(ObsOutputStatus.Starting, StatusOf(h, ObsOutputKind.Stream));
        }

        [Theory]
        [InlineData(204)]
        [InlineData(500)]
        [InlineData(600)]
        [InlineData(702)]
        public void AnOutputThatOBSStudioRefusesToReadForGood_IsNotReadAgain_AndTheModelSettles(int code)
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.Start();

            h.SettleWithTime(request => request.Kind == ReadKind.VirtualCamera ? (code, null) : h.World.Answer(request));

            Assert.True(h.Engine.IsSettled);
            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.VirtualCamera));
            Assert.Null(h.Take());
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void AnOutputThatOBSStudioIsNotReadyToAnswerFor_IsReadAgainAfterAWait()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.World.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started;
            h.Start();
            var start = h.Now;
            var refused = 2;

            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.Record && refused > 0)
                {
                    refused--;
                    return (207, null);
                }

                return h.World.Answer(request);
            });

            Assert.Equal(0, refused);
            Assert.True(h.Now > start, "The read was repeated without waiting.");
            Assert.True(h.Engine.IsSettled);
            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void AnOutputThatWasNotAnswered_IsReadAgain_AndTheModelIsNotSettledUntilItIs()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            h.World.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Started;
            h.Start();
            var ticket = h.Take();
            Assert.NotNull(ticket);

            // The read did not come back in time.
            h.Engine.OnReadFailed(ticket!, h.Now);
            Assert.False(h.Engine.IsSettled);
            h.SettleWithTime();

            Assert.True(h.Engine.IsSettled);
            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Stream));
        }

        [Fact]
        public void AnEventOfAnOutput_IsTaken_WhileOBSStudioChangesItsSceneCollection()
        {
            var h = Started();
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            Assert.True(h.Engine.IsSwitching);

            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED);

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Stream));
        }

        [Fact]
        public void TheOutputs_AreTheSame_AfterASwitchOfTheSceneCollection()
        {
            var h = Started(world => world.Outputs[ObsOutputKind.Record] = ObsOutputStatus.Started);
            OutputChanges(h);

            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            h.Event("CurrentSceneCollectionChanged", new { sceneCollectionName = "Other" }, 2);
            h.SettleWithTime();

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Record));
            Assert.Empty(OutputChanges(h));
        }

        [Fact]
        public void AStartThatOBSStudioNeverEnds_IsLookedAtAfterTheTimeout_AndIsOverWhenTheOutputDoesNotRun()
        {
            var h = Started();
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);
            OutputChanges(h);

            // As a real OBS Studio does when the encoder of a recording does not start: STARTING, an error in its own window, and nothing more.
            Assert.Equal(ObsOutputStatus.Starting, StatusOf(h, ObsOutputKind.Record));
            Assert.True(h.Engine.IsSettled);
            Assert.Equal(h.Now + TimeSpan.FromSeconds(30), h.Engine.NextWake);
            Assert.Null(h.Take());
            h.Advance(TimeSpan.FromSeconds(29));
            Assert.Null(h.Take());

            h.Advance(TimeSpan.FromSeconds(1));
            var ticket = h.Take();
            Assert.NotNull(ticket);
            Assert.Equal(ReadKind.Record, Assert.Single(ticket!.Requests).Kind);
            Assert.False(h.Engine.IsSettled);
            h.Complete(ticket);

            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.Record));
            var change = Assert.Single(OutputChanges(h));
            Assert.Equal(ObsOutputStatus.Starting, change.OldValue);
            Assert.Equal(ObsOutputStatus.Stopped, change.NewValue);
            Assert.Null(h.Engine.NextWake);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AStepThatAnEventEnds_IsNotLookedAt()
        {
            var h = Started();
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED);

            Assert.Null(h.Engine.NextWake);
            h.Advance(TimeSpan.FromMinutes(5));
            Assert.Null(h.Take());
            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void AStartThatTheReadShowsRunning_AfterTheTimeout_IsStarted()
        {
            var h = Started();
            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);

            // The event that said that it started was lost.
            h.World.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Started;
            h.Advance(TimeSpan.FromSeconds(30));
            h.Settle();

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Stream));
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void AStopThatTakesLongerThanTheTimeout_IsLookedAtAgainAndAgain_WhileTheOutputRuns_AndIsOverWhenItDoesNot()
        {
            var h = Started(world => world.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Started);
            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STOPPING);

            // A stream that sends what it has buffered runs until it is done: it is closing, it is not stopped.
            h.Advance(TimeSpan.FromSeconds(30));
            h.Settle();
            Assert.Equal(ObsOutputStatus.Stopping, StatusOf(h, ObsOutputKind.Stream));
            Assert.Equal(h.Now + TimeSpan.FromSeconds(30), h.Engine.NextWake);

            // And the event that says that it stopped was lost.
            h.World.Outputs[ObsOutputKind.Stream] = ObsOutputStatus.Stopped;
            h.Advance(TimeSpan.FromSeconds(30));
            h.Settle();

            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.Stream));
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void ARead_ThatIsMadeBeforeTheTimeout_LeavesTheStepAndItsTimeAsTheyAre()
        {
            var h = Started();
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);
            var due = h.Engine.NextWake;
            Assert.NotNull(due);

            h.Advance(TimeSpan.FromSeconds(5));
            h.Engine.RequestRefresh(ReadKind.Record, Guid.Empty, h.Now);
            h.Settle();

            Assert.Equal(ObsOutputStatus.Starting, StatusOf(h, ObsOutputKind.Record));
            Assert.Equal(due, h.Engine.NextWake);
        }

        [Fact]
        public void TheCheckOfAStep_IsRepeated_WhenOBSStudioIsNotReadyToAnswer()
        {
            var h = Started();
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);
            h.Advance(TimeSpan.FromSeconds(30));
            var refused = 2;

            h.SettleWithTime(request =>
            {
                if (request.Kind == ReadKind.Record && refused > 0)
                {
                    refused--;
                    return (207, null);
                }

                return h.World.Answer(request);
            });

            Assert.Equal(0, refused);
            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.Record));
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void AnEvent_ThatComesWhileTheStepIsChecked_IsNotUndoneByTheCheck()
        {
            var h = Started();
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);
            h.Advance(TimeSpan.FromSeconds(30));
            var ticket = h.Take();
            Assert.NotNull(ticket);

            // It started after all, as the read goes out: the answer says that it does not run, for it was taken before.
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED);
            h.Complete(ticket!);

            Assert.Equal(ObsOutputStatus.Started, StatusOf(h, ObsOutputKind.Record));
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void AStepThatAConnectionBeforeSaw_HasNoCheck_OnTheNextConnection()
        {
            var h = Started();
            Send(h, ObsOutputKind.Record, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);
            h.End();
            h.Start();
            h.SettleWithTime();

            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.Record));
            Assert.Null(h.Engine.NextWake);
        }

        [Fact]
        public void TheTimeoutOfTheSteps_IsTheOneOfTheOptions()
        {
            var h = new EngineHarness(new ModelEngineOptions { OutputStepTimeout = TimeSpan.FromSeconds(3) });
            h.World.AddScene("Scene");
            h.Start();
            h.SettleWithTime();

            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTING);

            Assert.Equal(h.Now + TimeSpan.FromSeconds(3), h.Engine.NextWake);
        }

        [Fact]
        public void AnEventOfAnOutput_ThatComesWithoutAConnection_IsNotTaken()
        {
            var h = Started();
            h.End();

            Send(h, ObsOutputKind.Stream, ObsOutputState.OBS_WEBSOCKET_OUTPUT_STARTED);
            h.Start();
            h.SettleWithTime();

            Assert.Equal(ObsOutputStatus.Stopped, StatusOf(h, ObsOutputKind.Stream));
        }
    }
}
