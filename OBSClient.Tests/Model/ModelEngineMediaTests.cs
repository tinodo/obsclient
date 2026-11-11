namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Model;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model holds about the playback of media inputs: how it reads the state, what the events of OBS Studio make of it (as they were measured with OBS Studio 33.0.0), what it does with a read
    /// that was sent before an event, with a read that OBS Studio does not answer, and with an OBS Studio that does not say which inputs are media inputs.
    /// </summary>
    public class ModelEngineMediaTests
    {
        private const int MediaEvents = 256;
        private const uint MediaCaps = 8327;

        // A world with a scene "Gaming" that shows the media input "Clip", the input "Mic" (audio, no media) and "Camera" (video only, no media).
        private static EngineHarness Started(out Guid clip, out Guid mic, Action<FakeWorld, Guid>? setup = null, ModelEngineOptions? options = null)
        {
            var h = new EngineHarness(options);
            h.World.AddScene("Scene");
            var gaming = h.World.AddScene("Gaming");
            var clipUuid = clip = h.World.AddInput("Clip", "ffmpeg_source", MediaCaps);
            mic = h.World.AddInput("Mic", "wasapi_input_capture", 2);
            var camera = h.World.AddInput("Camera", "dshow_input", 8);
            h.World.AddItem(gaming, clipUuid, "Clip");
            h.World.AddItem(gaming, camera, "Camera");
            setup?.Invoke(h.World, clipUuid);
            h.Start();
            h.SettleWithTime();
            h.Engine.TakeChanges();
            return h;
        }

        private static EngineInput Input(EngineHarness h, Guid uuid)
        {
            return h.Engine.Snapshot().Inputs.Single(input => input.Uuid == uuid);
        }

        private static ModelChange[] Changes(EngineHarness h, ModelChangeKind kind)
        {
            return h.Engine.TakeChanges().Where(change => change.Kind == kind).ToArray();
        }

        private static ReadRequest[] NextRead(EngineHarness h)
        {
            return h.Take() is { } ticket ? ticket.Requests.ToArray() : [];
        }

        private static void PlaybackStarted(EngineHarness h, Guid input)
        {
            h.Event("MediaInputPlaybackStarted", new { inputName = "Clip", inputUuid = input }, MediaEvents);
        }

        private static void PlaybackEnded(EngineHarness h, Guid input)
        {
            h.Event("MediaInputPlaybackEnded", new { inputName = "Clip", inputUuid = input }, MediaEvents);
        }

        private static void Action(EngineHarness h, Guid input, string action)
        {
            h.Event("MediaInputActionTriggered", new { inputName = "Clip", inputUuid = input, mediaAction = $"OBS_WEBSOCKET_MEDIA_INPUT_ACTION_{action}" }, MediaEvents);
        }

        [Fact]
        public void TheStateOfAMediaInput_IsRead_BeforeTheInputIsAnnounced_AndOfNoOtherInput()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var clip = h.World.AddInput("Clip", "ffmpeg_source", MediaCaps);
            h.World.AddInput("Mic", "wasapi_input_capture", 2);
            h.World.AddInput("Camera", "dshow_input", 8);
            h.Start();

            // The lists are read first: nothing is announced before the rest is known.
            h.Complete(h.Take()!);
            Assert.Empty(h.Engine.Snapshot().Inputs);
            var read = NextRead(h);

            var status = Assert.Single(read, request => request.Kind == ReadKind.MediaStatus);
            Assert.Equal((clip, RequestType.GetMediaInputStatus), (status.Target, status.Type));
        }

        [Fact]
        public void TheStateOfAMediaInput_IsInTheModel_AsOBSStudioSaidIt()
        {
            var h = Started(out var clip, out var mic, (world, clipUuid) => world.MediaStates[clipUuid] = "OBS_MEDIA_STATE_PAUSED");

            Assert.Equal(ObsMediaState.Paused, Input(h, clip).MediaState);
            Assert.Null(Input(h, mic).MediaState);
            Assert.True(h.Engine.IsSettled);
        }

        [Theory]
        [InlineData("OBS_MEDIA_STATE_NONE", ObsMediaState.None)]
        [InlineData("OBS_MEDIA_STATE_PLAYING", ObsMediaState.Playing)]
        [InlineData("OBS_MEDIA_STATE_OPENING", ObsMediaState.Opening)]
        [InlineData("OBS_MEDIA_STATE_BUFFERING", ObsMediaState.Buffering)]
        [InlineData("OBS_MEDIA_STATE_PAUSED", ObsMediaState.Paused)]
        [InlineData("OBS_MEDIA_STATE_STOPPED", ObsMediaState.Stopped)]
        [InlineData("OBS_MEDIA_STATE_ENDED", ObsMediaState.Ended)]
        [InlineData("OBS_MEDIA_STATE_ERROR", ObsMediaState.Error)]
        public void EveryStateThatOBSStudioHas_IsReadAsTheStateOfTheModel(string wire, ObsMediaState expected)
        {
            var h = Started(out var clip, out _, (world, clipUuid) => world.MediaStates[clipUuid] = wire);

            Assert.Equal(expected, Input(h, clip).MediaState);
        }

        [Theory]
        [InlineData("started", null, ObsMediaState.Playing)]
        [InlineData("ended", null, ObsMediaState.Ended)]
        [InlineData("action", "PLAY", ObsMediaState.Playing)]
        [InlineData("action", "RESTART", ObsMediaState.Playing)]
        [InlineData("action", "PAUSE", ObsMediaState.Paused)]
        [InlineData("action", "STOP", ObsMediaState.Stopped)]
        public void AnEventAboutTheMedia_ChangesTheStateAtOnce_WithoutARead_AndTheChangeSaysFromWhatToWhat(string which, string? action, ObsMediaState expected)
        {
            var h = Started(out var clip, out _);

            switch (which)
            {
                case "started":
                    PlaybackStarted(h, clip);
                    break;
                case "ended":
                    PlaybackEnded(h, clip);
                    break;
                default:
                    Action(h, clip, action!);
                    break;
            }

            Assert.Equal(expected, Input(h, clip).MediaState);
            var change = Assert.Single(Changes(h, ModelChangeKind.InputMediaStateChanged));
            Assert.Equal((clip, "Clip", ObsMediaState.None, expected), (change.Subject, change.Text, (ObsMediaState)change.OldValue!, (ObsMediaState)change.NewValue!));
            Assert.Null(h.Take());
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void TheEventsOfAPlay_ThatComeInEitherOrder_LeaveThePlayingState_AndTellItOnce()
        {
            // Measured with OBS Studio 33.0.0: a play says that the playback started, and that play was triggered, in that order; a slow file may say it the other way round.
            var h = Started(out var clip, out _);

            PlaybackStarted(h, clip);
            Action(h, clip, "PLAY");
            Action(h, clip, "PLAY");
            PlaybackStarted(h, clip);

            Assert.Equal(ObsMediaState.Playing, Input(h, clip).MediaState);
            Assert.Single(Changes(h, ModelChangeKind.InputMediaStateChanged));
        }

        [Fact]
        public void ARestartOfAPausedMedia_IsPlaying_WithoutAStartedEvent()
        {
            var h = Started(out var clip, out _, (world, clipUuid) => world.MediaStates[clipUuid] = "OBS_MEDIA_STATE_PAUSED");

            Action(h, clip, "RESTART");

            Assert.Equal(ObsMediaState.Playing, Input(h, clip).MediaState);
        }

        [Theory]
        [InlineData("NEXT")]
        [InlineData("PREVIOUS")]
        public void ANextOrAPreviousItem_ChangesNothingItself_AndMakesTheStateBeRead(string action)
        {
            var h = Started(out var clip, out _);

            Action(h, clip, action);

            Assert.Empty(h.Engine.TakeChanges());
            var read = Assert.Single(NextRead(h));
            Assert.Equal((ReadKind.MediaStatus, clip), (read.Kind, read.Target));
        }

        [Fact]
        public void AMediaEventForAnInputThatIsNoMediaInput_ChangesNothing_AndCausesNoRead()
        {
            var h = Started(out _, out var mic);

            h.Event("MediaInputPlaybackStarted", new { inputName = "Mic", inputUuid = mic }, MediaEvents);
            h.Event("MediaInputActionTriggered", new { inputName = "Mic", inputUuid = mic, mediaAction = "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NEXT" }, MediaEvents);

            Assert.Empty(h.Engine.TakeChanges());
            Assert.Null(h.Take());
        }

        [Fact]
        public void AMediaEventForAnInputThatTheModelDoesNotKnow_MakesTheListOfInputsBeRead()
        {
            var h = Started(out _, out _);

            PlaybackStarted(h, Guid.NewGuid());

            Assert.Empty(h.Engine.TakeChanges());
            Assert.Contains(ReadKind.Inputs, NextRead(h).Select(request => request.Kind));
        }

        [Fact]
        public void AReadThatWasSentBeforeAMediaEvent_DoesNotUndoIt()
        {
            var h = Started(out var clip, out _);
            h.Engine.RequestRefresh(ReadKind.MediaStatus, clip, h.Now);
            var read = h.Take()!;
            Assert.Equal(clip, read.Requests.Single().Target);

            // The event arrives while the read is under way: it is newer than the read, whatever the read says.
            PlaybackStarted(h, clip);
            h.Complete(read);

            Assert.Equal(ObsMediaState.Playing, Input(h, clip).MediaState);
        }

        [Fact]
        public void AReadThatWasSentAfterAMediaEvent_ShowsWhatCameAfterIt()
        {
            var h = Started(out var clip, out _);
            PlaybackStarted(h, clip);

            // OBS Studio opens the file: a state that no event announces, and that only a read shows.
            h.World.MediaStates[clip] = "OBS_MEDIA_STATE_OPENING";
            h.Engine.RequestRefresh(ReadKind.MediaStatus, clip, h.Now);
            h.Settle();

            Assert.Equal(ObsMediaState.Opening, Input(h, clip).MediaState);
            Assert.Equal(new[] { ObsMediaState.Playing, ObsMediaState.Opening }, h.Engine.TakeChanges().Where(change => change.Kind == ModelChangeKind.InputMediaStateChanged).Select(change => (ObsMediaState)change.NewValue!).ToArray());
        }

        [Fact]
        public void ARefreshOfTheMediaState_ReadsItAgain_AndIsDoneWhenTheReadHasBeenApplied()
        {
            var h = Started(out var clip, out _);

            h.Engine.RequestRefresh(ReadKind.MediaStatus, clip, h.Now);

            Assert.False(h.Engine.IsRefreshed(ReadKind.MediaStatus, clip));
            var read = NextRead(h);
            Assert.Equal((ReadKind.MediaStatus, clip), (read.Single().Kind, read.Single().Target));
        }

        [Fact]
        public void ARefreshOfTheMediaState_OfAnInputThatIsNoMediaInput_HasNothingToRead()
        {
            var h = Started(out _, out var mic);

            h.Engine.RequestRefresh(ReadKind.MediaStatus, mic, h.Now);

            Assert.True(h.Engine.IsRefreshed(ReadKind.MediaStatus, mic));
            Assert.Null(h.Take());
        }

        [Fact]
        public void AMediaStateThatOBSStudioRefusesForGood_LeavesTheStateNone_AndTheInputIsAnnounced()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var clip = h.World.AddInput("Clip", "ffmpeg_source", MediaCaps);
            h.Start();

            // A simulation that does not know the request answers 204: there is no state to wait for.
            h.Settle(request => request.Kind == ReadKind.MediaStatus ? (204, null) : h.World.Answer(request));

            Assert.True(h.Engine.IsSettled);
            Assert.Equal(ObsMediaState.None, Input(h, clip).MediaState);
        }

        [Fact]
        public void TheMediaStateOfAnInputThatOBSStudioDoesNotHave_IsReadAgain_AndTheListOfInputsDecides()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var clip = h.World.AddInput("Clip", "ffmpeg_source", MediaCaps);
            h.Start();
            h.Complete(h.Take()!);

            // The read of the state says that the input is not there: the list is read again, and says that it is.
            var status = h.Take()!;
            h.Complete(status, request => request.Kind == ReadKind.MediaStatus ? (600, null) : h.World.Answer(request));
            Assert.Empty(h.Engine.Snapshot().Inputs);

            h.SettleWithTime();

            Assert.Equal(clip, Assert.Single(h.Engine.Snapshot().Inputs).Uuid);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void AMediaStateThatCouldNotBeRead_IsReadAgainAfterAWait()
        {
            var h = new EngineHarness();
            h.World.AddScene("Scene");
            var clip = h.World.AddInput("Clip", "ffmpeg_source", MediaCaps);
            h.World.MediaStates[clip] = "OBS_MEDIA_STATE_STOPPED";
            h.Start();
            h.Complete(h.Take()!);

            // OBS Studio is not ready.
            h.Complete(h.Take()!, request => request.Kind == ReadKind.MediaStatus ? (207, null) : h.World.Answer(request));
            Assert.Empty(h.Engine.Snapshot().Inputs);

            h.SettleWithTime();

            Assert.Equal(ObsMediaState.Stopped, Input(h, clip).MediaState);
        }

        [Fact]
        public void ANewMediaInput_IsAnnouncedWhenItsStateIsKnown()
        {
            var h = Started(out _, out _);
            var created = Guid.NewGuid();
            h.World.Inputs.Add((created, "Intro", "ffmpeg_source", MediaCaps, false, 1.0));
            h.World.MediaStates[created] = "OBS_MEDIA_STATE_STOPPED";

            h.Event("InputCreated", new { inputName = "Intro", inputUuid = created, inputKind = "ffmpeg_source", unversionedInputKind = "ffmpeg_source", inputKindCaps = MediaCaps, inputSettings = new { }, defaultInputSettings = new { } }, 8);
            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == created);
            h.SettleWithTime();

            Assert.Equal(ObsMediaState.Stopped, Input(h, created).MediaState);
            Assert.Single(Changes(h, ModelChangeKind.InputAdded), change => change.Subject == created);
        }

        [Fact]
        public void AnInputThatIsNoMediaInput_IsAnnouncedWithoutAReadOfTheMediaState()
        {
            var h = Started(out _, out _);
            var created = Guid.NewGuid();
            h.World.Inputs.Add((created, "Color", "color_source_v3", 32777, false, 1.0));

            EngineEvents.InputCreated(h, created, "Color");
            var reads = new List<ReadKind>();
            while (h.Take() is { } ticket)
            {
                reads.AddRange(ticket.Requests.Select(request => request.Kind));
                h.Complete(ticket);
            }

            Assert.DoesNotContain(ReadKind.MediaStatus, reads);
            Assert.Null(Input(h, created).MediaState);
        }

        [Fact]
        public void AnOBSStudioThatDoesNotSayWhichInputsAreMediaInputs_HasTwoKindsOfThem()
        {
            var h = new EngineHarness();
            h.World.OmitInputKindCaps = true;
            h.World.AddScene("Scene");
            var clip = h.World.AddInput("Clip", "ffmpeg_source", MediaCaps);
            var playlist = h.World.AddInput("Playlist", "vlc_source", MediaCaps);
            h.World.AddInput("Camera", "dshow_input", 8);
            h.World.MediaStates[playlist] = "OBS_MEDIA_STATE_PLAYING";
            h.Start();
            h.Complete(h.Take()!);
            var read = NextRead(h);

            Assert.Equal(new[] { clip, playlist }.Order(), read.Where(request => request.Kind == ReadKind.MediaStatus).Select(request => request.Target).Order());
        }

        [Fact]
        public void WhenTheConnectionIsMadeAgain_TheMediaStateIsReadAgain_AndWhatChangedIsTold()
        {
            var h = Started(out var clip, out _, (world, clipUuid) => world.MediaStates[clipUuid] = "OBS_MEDIA_STATE_PLAYING");
            h.End();
            h.World.MediaStates[clip] = "OBS_MEDIA_STATE_ENDED";

            h.Start();
            h.SettleWithTime();

            Assert.Equal(ObsMediaState.Ended, Input(h, clip).MediaState);
            var change = Assert.Single(Changes(h, ModelChangeKind.InputMediaStateChanged));
            Assert.Equal((ObsMediaState.Playing, ObsMediaState.Ended), ((ObsMediaState)change.OldValue!, (ObsMediaState)change.NewValue!));
        }

        [Fact]
        public void TheMediaEvents_AreNotActedOnWhileOBSStudioChangesItsSceneCollection()
        {
            var h = Started(out var clip, out _);
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Other" }, 2);

            PlaybackStarted(h, clip);

            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind == ModelChangeKind.InputMediaStateChanged);
        }
    }
}
