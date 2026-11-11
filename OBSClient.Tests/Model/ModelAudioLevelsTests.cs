namespace OBSStudioClient.Tests.Model
{
    using System.Collections.Concurrent;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The levels of the audio (<see cref="ObsInput.Levels"/> and <see cref="ObsModel.AudioLevels"/>), as the model shows them when it is asked to follow them (<see cref="ObsHighVolume.AudioLevels"/>), and what it does
    /// when it is not: what OBS Studio says becomes the levels of the inputs, an input that is not listed any more has none, and handlers that are slower than OBS Studio get the newest levels and never fill the queue.
    /// </summary>
    public class ModelAudioLevelsTests
    {
        private const int MeterEvents = 1 << 16;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static ObsModelOptions Listening()
        {
            var options = CallFixture.DefaultOptions();
            options.HighVolume = ObsHighVolume.AudioLevels;
            return options;
        }

        private static Task<CallFixture> StartAsync(bool follow = true, ObsModelOptions? options = null)
        {
            return CallFixture.StartAsync(options ?? (follow ? Listening() : null));
        }

        // What OBS Studio sends of an input: its name, its UUID and, for each channel, the magnitude, the peak and the peak of the input.
        private static object Entry(ObsInput input, params float[][] channels)
        {
            return new { inputName = input.Name, inputUuid = input.Uuid, inputLevelsMul = channels };
        }

        private static Task SendAsync(CallFixture test, params object[] entries)
        {
            return test.Rig.Responder.SendEventAsync("InputVolumeMeters", new { inputs = entries }, MeterEvents);
        }

        private static List<ObsAudioLevelsEventArgs> Record(ObsModel model)
        {
            var raised = new List<ObsAudioLevelsEventArgs>();
            model.AudioLevels += (_, e) =>
            {
                lock (raised)
                {
                    raised.Add(e);
                }
            };
            return raised;
        }

        private static int CountOf(List<ObsAudioLevelsEventArgs> raised)
        {
            lock (raised)
            {
                return raised.Count;
            }
        }

        private static ObsAudioLevelsEventArgs[] Raised(List<ObsAudioLevelsEventArgs> raised)
        {
            lock (raised)
            {
                return [.. raised];
            }
        }

        private static List<string> RecordProperties(ObsInput input)
        {
            var names = new List<string>();
            input.PropertyChanged += (_, e) =>
            {
                lock (names)
                {
                    names.Add(e.PropertyName!);
                }
            };
            return names;
        }

        private static int CountOf(List<string> names, string property)
        {
            lock (names)
            {
                return names.Count(name => name == property);
            }
        }

        [Fact]
        public async Task TheLevels_AreNull_AndTheMessagesAreNotActedOn_WhenTheModelWasNotAskedToFollowThem()
        {
            await using var test = await StartAsync(follow: false);
            var raised = Record(test.Model);
            var properties = RecordProperties(test.Mic);

            await SendAsync(test, Entry(test.Mic, [0.5f, 0.6f, 0.7f]));
            await Task.Delay(150);
            await test.Model.WaitUntilSettledAsync().WithTimeoutAsync(Timeout);

            Assert.Null(test.Mic.Levels);
            Assert.Equal(0, CountOf(raised));
            Assert.Equal(0, CountOf(properties, nameof(ObsInput.Levels)));
        }

        [Fact]
        public async Task TheLevelsOfAnInput_AreWhatOBSStudioSays_ForEachChannel()
        {
            await using var test = await StartAsync();
            var raised = Record(test.Model);
            Assert.Null(test.Mic.Levels);

            await SendAsync(test, Entry(test.Mic, [0.25f, 0.5f, 1f], [0.125f, 0.2f, 0.3f]));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 1, "the levels to be told");

            var levels = test.Mic.Levels!;
            Assert.Same(test.Mic, levels.Input);
            Assert.Equal(2, levels.Channels.Count);
            Assert.Equal(0.25f, levels.Channels[0].Magnitude);
            Assert.Equal(0.5f, levels.Channels[0].Peak);
            Assert.Equal(1f, levels.Channels[0].InputPeak);
            Assert.Equal(0.125f, levels.Channels[1].Magnitude);
            Assert.Equal(0.2f, levels.Channels[1].Peak);
            Assert.Equal(0.3f, levels.Channels[1].InputPeak);
            Assert.Equal(-12.04f, levels.Channels[0].MagnitudeDb, 2);
            Assert.Equal(-6.02f, levels.Channels[0].PeakDb, 2);
            Assert.Equal(0f, levels.Channels[0].InputPeakDb, 2);
            var args = Assert.Single(Raised(raised));
            Assert.Same(levels, Assert.Single(args.Levels));
            Assert.Empty(args.Ended);
        }

        [Fact]
        public async Task AnInputWithoutChannels_HasLevelsWithoutChannels_BecauseOBSStudioListsItBeforeItHasAudio()
        {
            await using var test = await StartAsync();
            var raised = Record(test.Model);

            await SendAsync(test, Entry(test.Mic));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 1, "the levels to be told");

            Assert.NotNull(test.Mic.Levels);
            Assert.Empty(test.Mic.Levels!.Channels);
        }

        [Fact]
        public async Task AnInputThatIsNotListedAnyMore_HasNoLevels_AndIsToldAsEnded()
        {
            await using var test = await StartAsync();
            var raised = Record(test.Model);
            var properties = RecordProperties(test.Mic);
            await SendAsync(test, Entry(test.Mic, [0.5f, 0.6f, 0.7f]));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 1, "the levels to be told");

            await SendAsync(test);
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 2, "the end of the levels to be told");

            Assert.Null(test.Mic.Levels);
            var ended = Raised(raised)[1];
            Assert.Empty(ended.Levels);
            Assert.Same(test.Mic, Assert.Single(ended.Ended));
            await ModelRig.EventuallyAsync(() => CountOf(properties, nameof(ObsInput.Levels)) == 2, "PropertyChanged for both changes");
        }

        [Fact]
        public async Task AMessageThatSaysWhatTheLastOneSaid_IsNotTold()
        {
            await using var test = await StartAsync();
            var raised = Record(test.Model);
            var properties = RecordProperties(test.Mic);
            await SendAsync(test, Entry(test.Mic, [0f, 0f, 0.5f]));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 1, "the levels to be told");
            var first = test.Mic.Levels;

            await SendAsync(test, Entry(test.Mic, [0f, 0f, 0.5f]));
            await SendAsync(test, Entry(test.Mic, [0f, 0f, 0.5f]));
            await Task.Delay(150);
            await SendAsync(test, Entry(test.Mic, [0.1f, 0f, 0.5f]));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 2, "the change to be told");

            Assert.Same(first, Raised(raised)[0].Levels[0]);
            Assert.Equal(0.1f, test.Mic.Levels!.Channels[0].Magnitude);
            await ModelRig.EventuallyAsync(() => CountOf(properties, nameof(ObsInput.Levels)) == 2, "PropertyChanged for both changes");
            Assert.Equal(2, CountOf(raised));
        }

        [Fact]
        public async Task AnInputThatTheModelDoesNotHave_IsSkipped_AndAnInputThatIsListedTwiceCountsOnce()
        {
            await using var test = await StartAsync();
            var raised = Record(test.Model);
            var stranger = new { inputName = "Not in the model", inputUuid = Guid.NewGuid(), inputLevelsMul = new[] { new[] { 0.9f, 0.9f, 0.9f } } };

            await SendAsync(test, stranger, Entry(test.Mic, [0.1f, 0.2f, 0.3f]), Entry(test.Mic, [0.9f, 0.9f, 0.9f]), Entry(test.Camera, [0.4f, 0.5f, 0.6f]));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 1, "the levels to be told");

            var args = Assert.Single(Raised(raised));
            Assert.Equal([test.Mic, test.Camera], args.Levels.Select(levels => levels.Input).ToArray());
            Assert.Equal(0.1f, test.Mic.Levels!.Channels[0].Magnitude);
            Assert.Equal(0.4f, test.Camera.Levels!.Channels[0].Magnitude);
            Assert.Null(test.Model.Inputs["Spare"].Levels);
        }

        [Fact]
        public async Task PropertyChanged_IsRaisedOnlyForTheInputsWhoseLevelsChanged()
        {
            await using var test = await StartAsync();
            var raised = Record(test.Model);
            var mic = RecordProperties(test.Mic);
            var camera = RecordProperties(test.Camera);

            await SendAsync(test, Entry(test.Mic, [0.1f, 0.2f, 0.3f]));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 1, "the levels to be told");
            await ModelRig.EventuallyAsync(() => CountOf(mic, nameof(ObsInput.Levels)) == 1, "PropertyChanged of the microphone");
            await SendAsync(test, Entry(test.Mic, [0.1f, 0.2f, 0.3f]), Entry(test.Camera, [0.5f, 0.5f, 0.5f]));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 2, "the second levels to be told");
            await ModelRig.EventuallyAsync(() => CountOf(camera, nameof(ObsInput.Levels)) == 1, "PropertyChanged of the camera");

            Assert.Equal(1, CountOf(mic, nameof(ObsInput.Levels)));
            Assert.Equal(1, CountOf(camera, nameof(ObsInput.Levels)));
            Assert.Equal([test.Mic, test.Camera], Raised(raised)[1].Levels.Select(levels => levels.Input).ToArray());
        }

        [Fact]
        public async Task AHandlerThatIsSlowerThanOBSStudio_GetsTheNewestLevels_AndTheQueueNeverFills()
        {
            var options = Listening();
            options.MaxPendingNotifications = 5;
            await using var test = await StartAsync(options: options);
            var seen = new ConcurrentQueue<float>();
            var resynchronized = 0;
            using var gate = new ManualResetEventSlim();
            var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            test.Model.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);
            test.Model.AudioLevels += (_, e) =>
            {
                seen.Enqueue(e.Levels[0].Channels[0].Magnitude);
                blocked.TrySetResult();
                gate.Wait(Timeout);
            };

            // The first message holds the handler; far more than the queue holds come while it waits.
            await SendAsync(test, Entry(test.Mic, [0.001f, 0f, 0f]));
            await blocked.Task.WaitAsync(Timeout);
            for (var i = 2; i <= 400; i++)
            {
                await SendAsync(test, Entry(test.Mic, [i / 1000f, 0f, 0f]));
            }

            await ModelRig.EventuallyAsync(() => test.Mic.Levels!.Channels[0].Magnitude == 0.4f, "the model to hold the newest levels");
            gate.Set();
            await ModelRig.EventuallyAsync(() => seen.Contains(0.4f), "the newest levels to be raised");

            Assert.Equal(0, Volatile.Read(ref resynchronized));
            Assert.True(seen.Count <= 3, $"The handler was raised {seen.Count} times for 400 messages that came while it waited.");
            Assert.Equal(0.001f, seen.First());
            Assert.Equal(0.4f, seen.Last());
        }

        [Fact]
        public async Task TheLevels_AreTakenFromTheInputs_WhenTheConnectionEnds()
        {
            await using var test = await StartAsync();
            var raised = Record(test.Model);
            var properties = RecordProperties(test.Mic);
            await SendAsync(test, Entry(test.Mic, [0.5f, 0.6f, 0.7f]), Entry(test.Camera, [0.1f, 0.1f, 0.1f]));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 1, "the levels to be told");

            test.Rig.Responder.AbortConnection();
            await ModelRig.EventuallyAsync(() => test.Mic.Levels is null && test.Camera.Levels is null, "the levels to go");

            await ModelRig.EventuallyAsync(() => CountOf(raised) == 2, "the end of the levels to be told");
            var ended = Raised(raised)[1];
            Assert.Empty(ended.Levels);
            Assert.Equal(2, ended.Ended.Count);
            Assert.Contains(test.Mic, ended.Ended);
            Assert.Contains(test.Camera, ended.Ended);
            await ModelRig.EventuallyAsync(() => CountOf(properties, nameof(ObsInput.Levels)) == 2, "PropertyChanged for the end");
        }

        [Fact]
        public async Task TheLevels_FlowAgain_WhenTheModelReconnects()
        {
            var options = Listening();
            options.AutoReconnect = true;
            await using var test = await StartAsync(options: options);
            var raised = Record(test.Model);
            await SendAsync(test, Entry(test.Mic, [0.5f, 0.6f, 0.7f]));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 1, "the levels to be told");

            var handshakes = test.Rig.Responder.Handshakes;
            test.Rig.Responder.AbortConnection();
            await ModelRig.EventuallyAsync(() => test.Mic.Levels is null, "the levels to go");
            await ModelRig.EventuallyAsync(() => test.Rig.Responder.Handshakes > handshakes && test.Model.State == ObsModelState.Synchronized, "the model to be connected again", TimeSpan.FromSeconds(30));
            await SendAsync(test, Entry(test.Mic, [0.25f, 0.5f, 0.75f]));

            await ModelRig.EventuallyAsync(() => test.Mic.Levels is not null, "the levels of the new connection");
            Assert.Equal(0.25f, test.Mic.Levels!.Channels[0].Magnitude);
        }

        [Fact]
        public async Task AnInputThatIsRemoved_LosesItsLevels_WhenOBSStudioStopsListingIt()
        {
            await using var test = await StartAsync();
            var raised = Record(test.Model);
            var spare = test.Model.Inputs["Spare"];
            await SendAsync(test, Entry(spare, [0.5f, 0.6f, 0.7f]));
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 1, "the levels to be told");
            test.Rig.Responder.Change(world => world.Inputs.RemoveAll(input => input.Uuid == spare.Uuid));
            await test.Rig.Responder.SendEventAsync("InputRemoved", new { inputName = spare.Name, inputUuid = spare.Uuid }, 8);
            await ModelRig.EventuallyAsync(() => spare.IsRemoved, "the input to be removed");

            await SendAsync(test);
            await ModelRig.EventuallyAsync(() => CountOf(raised) == 2, "the end of the levels to be told");

            Assert.Null(spare.Levels);
            Assert.Same(spare, Assert.Single(Raised(raised)[1].Ended));
        }
    }
}
