namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The playback of media inputs: what the model shows, what its calls send, and how they end. OBS Studio is made to do what it was measured to do with OBS Studio 33.0.0 (see LiveMediaTests):
    /// a play says that the playback started and that play was triggered, a pause says that pause was triggered, and so on.
    /// </summary>
    public class ModelMediaTests
    {
        private const int MediaEvents = 256;
        private const uint MediaCaps = 8327;
        private const string PlayAction = "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY";
        private const string PauseAction = "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE";
        private const string StopAction = "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP";
        private const string RestartAction = "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static Task<CallFixture> StartAsync(string? state = null)
        {
            return CallFixture.StartAsync(setup: world =>
            {
                var clip = world.AddInput("Clip", "ffmpeg_source", MediaCaps);
                if (state is not null)
                {
                    world.MediaStates[clip] = state;
                }
            });
        }

        private static List<string> Watch(ObsMedia media)
        {
            var raised = new List<string>();
            media.PropertyChanged += (_, e) =>
            {
                lock (raised)
                {
                    raised.Add(e.PropertyName!);
                }
            };
            return raised;
        }

        private static int Told<T>(List<T> list)
        {
            lock (list)
            {
                return list.Count;
            }
        }

        private static async Task SendAsync(CallFixture test, string type, object data)
        {
            await test.Rig.Responder.SendEventAsync(type, data, MediaEvents);
        }

        // What OBS Studio 33.0.0 does when it is told to do something with a media input: the world holds the new state, and the events follow the answer.
        private static void ActLikeObsStudio(CallFixture test, ObsInput clip)
        {
            test.Answer("TriggerMediaInputAction", data =>
            {
                var action = data!.Value.GetProperty("mediaAction").GetString()!;
                var (state, events) = action switch
                {
                    PlayAction => ("OBS_MEDIA_STATE_PLAYING", new[] { "MediaInputPlaybackStarted", PlayAction }),
                    PauseAction => ("OBS_MEDIA_STATE_PAUSED", new[] { PauseAction }),
                    StopAction => ("OBS_MEDIA_STATE_STOPPED", new[] { StopAction }),
                    RestartAction => ("OBS_MEDIA_STATE_PLAYING", new[] { RestartAction }),
                    _ => ((string?)null, Array.Empty<string>()),
                };
                if (state is not null)
                {
                    test.Rig.Responder.Change(world => world.MediaStates[clip.Uuid] = state);
                }

                _ = Task.Run(async () =>
                {
                    foreach (var announced in events)
                    {
                        if (announced == "MediaInputPlaybackStarted")
                        {
                            await SendAsync(test, announced, new { inputName = "Clip", inputUuid = clip.Uuid });
                        }
                        else
                        {
                            await SendAsync(test, "MediaInputActionTriggered", new { inputName = "Clip", inputUuid = clip.Uuid, mediaAction = announced });
                        }
                    }
                });
                return (100, null);
            });
        }

        [Fact]
        public async Task AMediaInput_HasAMedia_ThatShowsItsState_AndAnInputThatIsNoMediaInputHasNone()
        {
            await using var test = await StartAsync("OBS_MEDIA_STATE_PAUSED");
            var clip = test.Model.Inputs["Clip"];

            Assert.NotNull(clip.Media);
            Assert.Equal(ObsMediaState.Paused, clip.Media.State);
            Assert.Same(clip, clip.Media.Input);
            Assert.Same(clip.Media, clip.Media);
            Assert.Null(test.Camera.Media);
            Assert.Null(test.Mic.Media);
            Assert.Contains("Clip", clip.Media.ToString());
        }

        [Fact]
        public async Task TheEventsAboutThePlayback_ChangeTheState_AndAreToldAsPropertyChangesAndRecords()
        {
            await using var test = await StartAsync();
            var clip = test.Model.Inputs["Clip"];
            var media = clip.Media!;
            var raised = Watch(media);
            var uuid = clip.Uuid;

            await SendAsync(test, "MediaInputPlaybackStarted", new { inputName = "Clip", inputUuid = uuid });
            await SendAsync(test, "MediaInputActionTriggered", new { inputName = "Clip", inputUuid = uuid, mediaAction = PauseAction });
            await SendAsync(test, "MediaInputActionTriggered", new { inputName = "Clip", inputUuid = uuid, mediaAction = StopAction });
            await SendAsync(test, "MediaInputPlaybackEnded", new { inputName = "Clip", inputUuid = uuid });
            await ModelRig.EventuallyAsync(() => Told(raised) == 4, "the four changes to be told");

            Assert.Equal(new[] { "State", "State", "State", "State" }, raised.ToArray());
            Assert.Equal(ObsMediaState.Ended, media.State);
            var records = test.Rig.Changes.Where(change => ReferenceEquals(change.Entity, media)).ToArray();
            Assert.Equal(
                new[] { (ObsMediaState.None, ObsMediaState.Playing), (ObsMediaState.Playing, ObsMediaState.Paused), (ObsMediaState.Paused, ObsMediaState.Stopped), (ObsMediaState.Stopped, ObsMediaState.Ended) },
                records.Select(change => ((ObsMediaState)change.OldValue!, (ObsMediaState)change.NewValue!)).ToArray());
            Assert.All(records, change => Assert.Equal("State", change.PropertyName));
            Assert.Contains("the media of input 'Clip'", records[0].ToString());
        }

        [Fact]
        public async Task TheSameStateAgain_IsNoChange()
        {
            await using var test = await StartAsync();
            var media = test.Model.Inputs["Clip"].Media!;
            var raised = Watch(media);

            await SendAsync(test, "MediaInputPlaybackStarted", new { inputName = "Clip", inputUuid = test.Model.Inputs["Clip"].Uuid });
            await SendAsync(test, "MediaInputActionTriggered", new { inputName = "Clip", inputUuid = test.Model.Inputs["Clip"].Uuid, mediaAction = PlayAction });
            await SendAsync(test, "MediaInputActionTriggered", new { inputName = "Clip", inputUuid = test.Model.Inputs["Clip"].Uuid, mediaAction = RestartAction });
            await SendAsync(test, "MediaInputActionTriggered", new { inputName = "Clip", inputUuid = test.Model.Inputs["Clip"].Uuid, mediaAction = PauseAction });
            await ModelRig.EventuallyAsync(() => Told(raised) == 2, "the two changes to be told");

            Assert.Equal(new[] { "State", "State" }, raised.ToArray());
        }

        [Theory]
        [InlineData("Play", PlayAction, ObsMediaState.Playing)]
        [InlineData("Pause", PauseAction, ObsMediaState.Paused)]
        [InlineData("Stop", StopAction, ObsMediaState.Stopped)]
        [InlineData("Restart", RestartAction, ObsMediaState.Playing)]
        public async Task ThePlaybackCalls_SendTheAction_AndReturnWhenTheEventsShowTheState(string call, string action, ObsMediaState expected)
        {
            await using var test = await StartAsync();
            var clip = test.Model.Inputs["Clip"];
            ActLikeObsStudio(test, clip);
            var before = test.Count("GetMediaInputStatus");

            await (call switch
            {
                "Play" => clip.Media!.PlayAsync(),
                "Pause" => clip.Media!.PauseAsync(),
                "Stop" => clip.Media!.StopAsync(),
                _ => clip.Media!.RestartAsync(),
            }).WithTimeoutAsync(Timeout);

            Assert.Equal(expected, clip.Media!.State);
            var sent = Assert.Single(test.AllSent("TriggerMediaInputAction"));
            Assert.Equal((clip.Uuid, action), (sent.GetProperty("inputUuid").GetGuid(), sent.GetProperty("mediaAction").GetString()));
            Assert.Equal(before, test.Count("GetMediaInputStatus"));
        }

        [Fact]
        public async Task APlay_OfAMediaThatPlaysAlready_ReturnsAtOnce()
        {
            await using var test = await StartAsync("OBS_MEDIA_STATE_PLAYING");
            var clip = test.Model.Inputs["Clip"];
            test.Answer("TriggerMediaInputAction");

            await clip.Media!.PlayAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsMediaState.Playing, clip.Media.State);
            Assert.Equal(1, test.Count("TriggerMediaInputAction"));
        }

        [Fact]
        public async Task APlay_ThatNoEventShows_IsShownByARead()
        {
            await using var test = await StartAsync();
            var clip = test.Model.Inputs["Clip"];
            test.Answer("TriggerMediaInputAction", _ =>
            {
                test.Rig.Responder.Change(world => world.MediaStates[clip.Uuid] = "OBS_MEDIA_STATE_PLAYING");
                return (100, null);
            });
            var before = test.Count("GetMediaInputStatus");

            await clip.Media!.PlayAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsMediaState.Playing, clip.Media.State);
            Assert.True(test.Count("GetMediaInputStatus") > before);
        }

        [Fact]
        public async Task APlay_ThatNoEventShowsAndThatTheReadDoesNotShowEither_ReturnsAfterTheRead_WithTheStateThatOBSStudioHolds()
        {
            await using var test = await StartAsync();
            var clip = test.Model.Inputs["Clip"];
            test.Answer("TriggerMediaInputAction");
            var before = test.Count("GetMediaInputStatus");

            // OBS Studio answered, and nothing shows that it plays: the model is read again, and the call ends with what OBS Studio holds (as it does for every change that comes out differently).
            await clip.Media!.PlayAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsMediaState.None, clip.Media.State);
            Assert.True(test.Count("GetMediaInputStatus") > before);
        }

        [Theory]
        [InlineData("OBS_MEDIA_STATE_PLAYING", PauseAction, ObsMediaState.Paused)]
        [InlineData("OBS_MEDIA_STATE_OPENING", PauseAction, ObsMediaState.Paused)]
        [InlineData("OBS_MEDIA_STATE_BUFFERING", PauseAction, ObsMediaState.Paused)]
        [InlineData("OBS_MEDIA_STATE_PAUSED", PlayAction, ObsMediaState.Playing)]
        [InlineData("OBS_MEDIA_STATE_STOPPED", PlayAction, ObsMediaState.Playing)]
        [InlineData("OBS_MEDIA_STATE_ENDED", PlayAction, ObsMediaState.Playing)]
        [InlineData("OBS_MEDIA_STATE_NONE", PlayAction, ObsMediaState.Playing)]
        [InlineData("OBS_MEDIA_STATE_ERROR", PlayAction, ObsMediaState.Playing)]
        public async Task TheToggle_AsksOBSStudioWhatTheMediaDoes_AndPausesWhatPlays_AndPlaysTheRest(string held, string expectedAction, ObsMediaState expected)
        {
            await using var test = await StartAsync();
            var clip = test.Model.Inputs["Clip"];
            ActLikeObsStudio(test, clip);

            // The model says nothing of what OBS Studio holds: the world does, and the toggle asks.
            test.Rig.Responder.Change(world => world.MediaStates[clip.Uuid] = held);
            var before = test.Count("GetMediaInputStatus");

            var asked = await clip.Media!.TogglePlayPauseAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(expected, asked);
            Assert.Equal(expected, clip.Media.State);
            Assert.Equal(expectedAction, test.AllSent("TriggerMediaInputAction").Single().GetProperty("mediaAction").GetString());
            Assert.Equal(before + 1, test.Count("GetMediaInputStatus"));
        }

        [Fact]
        public async Task TwoToggles_OneAfterTheOther_MakeTwoToggles_WhateverTheModelShowsMeanwhile()
        {
            await using var test = await StartAsync("OBS_MEDIA_STATE_PLAYING");
            var clip = test.Model.Inputs["Clip"];
            ActLikeObsStudio(test, clip);

            var first = clip.Media!.TogglePlayPauseAsync();
            var second = clip.Media.TogglePlayPauseAsync();

            Assert.Equal(ObsMediaState.Paused, await first.WithTimeoutAsync(Timeout));
            Assert.Equal(ObsMediaState.Playing, await second.WithTimeoutAsync(Timeout));
            Assert.Equal(new[] { PauseAction, PlayAction }, test.AllSent("TriggerMediaInputAction").Select(sent => sent.GetProperty("mediaAction").GetString()).ToArray());
        }

        [Fact]
        public async Task NextAndPrevious_SendTheirActions_AndReturnWhenOBSStudioHasAnswered()
        {
            await using var test = await StartAsync("OBS_MEDIA_STATE_PLAYING");
            var clip = test.Model.Inputs["Clip"];
            test.Answer("TriggerMediaInputAction");

            await clip.Media!.NextAsync().WithTimeoutAsync(Timeout);
            await clip.Media.PreviousAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(
                new[] { "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NEXT", "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PREVIOUS" },
                test.AllSent("TriggerMediaInputAction").Select(sent => sent.GetProperty("mediaAction").GetString()).ToArray());
            Assert.Equal(ObsMediaState.Playing, clip.Media.State);
        }

        [Fact]
        public async Task ASeekAndASkip_SendWholeMilliseconds_AndReturnWhenOBSStudioHasAnswered()
        {
            await using var test = await StartAsync("OBS_MEDIA_STATE_PLAYING");
            var clip = test.Model.Inputs["Clip"];
            test.Answer("SetMediaInputCursor");
            test.Answer("OffsetMediaInputCursor");

            await clip.Media!.SeekAsync(TimeSpan.FromSeconds(30.0004)).WithTimeoutAsync(Timeout);
            await clip.Media.SkipAsync(TimeSpan.FromMilliseconds(-1500)).WithTimeoutAsync(Timeout);

            var seek = Assert.Single(test.AllSent("SetMediaInputCursor"));
            Assert.Equal((clip.Uuid, 30000), (seek.GetProperty("inputUuid").GetGuid(), seek.GetProperty("mediaCursor").GetInt32()));
            var skip = Assert.Single(test.AllSent("OffsetMediaInputCursor"));
            Assert.Equal((clip.Uuid, -1500), (skip.GetProperty("inputUuid").GetGuid(), skip.GetProperty("mediaCursorOffset").GetInt32()));
        }

        [Fact]
        public async Task ASeekOrASkip_ThatOBSStudioRefuses_IsAnObsResponseException_WithWhatItSaid()
        {
            await using var test = await StartAsync("OBS_MEDIA_STATE_STOPPED");
            var clip = test.Model.Inputs["Clip"];
            test.Answer("SetMediaInputCursor", 604, "The media input must be playing or paused in order to set the cursor position.");
            test.Answer("OffsetMediaInputCursor", 604, "The media input must be playing or paused in order to set the cursor position.");

            var seek = await Assert.ThrowsAsync<ObsResponseException>(() => clip.Media!.SeekAsync(TimeSpan.FromSeconds(1)).WithTimeoutAsync(Timeout));
            var skip = await Assert.ThrowsAsync<ObsResponseException>(() => clip.Media!.SkipAsync(TimeSpan.FromSeconds(1)).WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.InvalidResourceState, seek.ErrorCode);
            Assert.Equal(RequestStatusCode.InvalidResourceState, skip.ErrorCode);
            Assert.Contains("must be playing or paused", seek.Message);
        }

        [Fact]
        public async Task APositionOrAnOffsetThatOBSStudioCanNotTake_IsAnArgumentOutOfRangeException_AndNothingIsSent()
        {
            await using var test = await StartAsync("OBS_MEDIA_STATE_PLAYING");
            var media = test.Model.Inputs["Clip"].Media!;

            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = media.SeekAsync(TimeSpan.FromSeconds(-1)); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = media.SeekAsync(TimeSpan.FromDays(30)); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = media.SkipAsync(TimeSpan.FromDays(-30)); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = media.SkipAsync(TimeSpan.FromDays(30)); });

            Assert.Equal(0, test.Count("SetMediaInputCursor") + test.Count("OffsetMediaInputCursor"));
        }

        [Fact]
        public async Task TheStatus_IsWhatOBSStudioSays_WithTheLengthAndThePositionWhenItKnowsThem()
        {
            await using var test = await StartAsync("OBS_MEDIA_STATE_PLAYING");
            var clip = test.Model.Inputs["Clip"];
            test.Rig.Responder.Change(world => world.MediaTimes[clip.Uuid] = (12000, 4500));

            var status = await clip.Media!.GetStatusAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsMediaState.Playing, status.State);
            Assert.Equal(TimeSpan.FromSeconds(12), status.Duration);
            Assert.Equal(TimeSpan.FromSeconds(4.5), status.Position);
            Assert.Contains("0:00:04.5", status.ToString());
        }

        [Fact]
        public async Task TheStatusOfAMediaThatDoesNotPlay_HasNoLengthAndNoPosition()
        {
            await using var test = await StartAsync("OBS_MEDIA_STATE_STOPPED");

            var status = await test.Model.Inputs["Clip"].Media!.GetStatusAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsMediaState.Stopped, status.State);
            Assert.Null(status.Duration);
            Assert.Null(status.Position);
            Assert.Equal("Stopped", status.ToString());
        }

        [Fact]
        public async Task TheStatus_ShowsWhatTheEventsDoNotAnnounce_AndLeavesTheStateOfTheModelAlone()
        {
            await using var test = await StartAsync();
            var clip = test.Model.Inputs["Clip"];
            test.Rig.Responder.Change(world => world.MediaStates[clip.Uuid] = "OBS_MEDIA_STATE_OPENING");
            var before = test.Count("GetMediaInputStatus");

            var status = await clip.Media!.GetStatusAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsMediaState.Opening, status.State);
            Assert.Equal(ObsMediaState.None, clip.Media.State);
            Assert.Equal(before + 1, test.Count("GetMediaInputStatus"));
        }

        [Fact]
        public async Task TheStatus_IsAskedInItsTurn_AfterTheChangesThatWereAskedForBeforeIt()
        {
            await using var test = await StartAsync();
            var clip = test.Model.Inputs["Clip"];
            ActLikeObsStudio(test, clip);

            var play = clip.Media!.PlayAsync();
            var status = clip.Media.GetStatusAsync();

            Assert.Equal(ObsMediaState.Playing, (await status.WithTimeoutAsync(Timeout)).State);
            await play.WithTimeoutAsync(Timeout);
        }

        [Fact]
        public async Task TheCalls_OfAnInputThatWasRemoved_AreAnObsRemovedException_AndNothingIsSent()
        {
            await using var test = await StartAsync();
            var clip = test.Model.Inputs["Clip"];
            var media = clip.Media!;
            test.Rig.Responder.Change(world => world.Inputs.RemoveAll(input => input.Uuid == clip.Uuid));
            await test.Rig.Responder.SendEventAsync("InputRemoved", new { inputName = "Clip", inputUuid = clip.Uuid }, 8);
            await ModelRig.EventuallyAsync(() => clip.IsRemoved, "the input to be removed");

            await Assert.ThrowsAsync<ObsRemovedException>(() => media.PlayAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => media.PauseAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => media.TogglePlayPauseAsync().WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => media.SeekAsync(TimeSpan.Zero).WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsRemovedException>(() => media.NextAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(0, test.Count("TriggerMediaInputAction") + test.Count("SetMediaInputCursor"));
        }
    }
}
