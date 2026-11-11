namespace OBSStudioClient.Tests.Live
{
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// Media inputs against a real OBS Studio: what state a media source is in, what the events say, and what the requests refuse with. The model keeps the state of a media input, and these check
    /// against the real thing what it assumes where the stand-in of the unit tests only had what was read in the source of obs-websocket: the observation says what OBS Studio does, and the tests of
    /// the model check that the model follows it and that its calls end as they say. Everything that is made carries the name prefix of <see cref="LiveObs.NamePrefix"/> and is removed again, and the
    /// files that are made for the media sources are deleted.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveMediaTests(ITestOutputHelper output)
    {
        // The media source of OBS Studio: it is on every OBS Studio, and it plays a file without a video.
        private const string MediaKind = "ffmpeg_source";

        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

        private static readonly ObsMediaInputAction Play = ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY;
        private static readonly ObsMediaInputAction Pause = ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE;
        private static readonly ObsMediaInputAction Stop = ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP;
        private static readonly ObsMediaInputAction Restart = ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART;
        private static readonly ObsMediaInputAction Next = ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_NEXT;
        private static readonly ObsMediaInputAction Previous = ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PREVIOUS;

        // A WAVE file of silence (8 kHz, 16 bits, one channel) that is as long as asked: OBS Studio plays it with its media source, and it needs no video.
        private static string WriteWave(double seconds)
        {
            var path = Path.Combine(Path.GetTempPath(), $"obsclient-live-media-{Guid.NewGuid():N}.wav");
            var samples = (int)(seconds * 8000);
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + (samples * 2));
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(8000);
            writer.Write(16000);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8.ToArray());
            writer.Write(samples * 2);
            writer.Write(new byte[samples * 2]);
            return path;
        }

        private static string Describe((string Name, object? Arguments) entry)
        {
            return entry.Arguments switch
            {
                MediaInputActionTriggeredEventArgs action => $"{entry.Name}({action.MediaAction.ToString().Replace("OBS_WEBSOCKET_MEDIA_INPUT_ACTION_", string.Empty, StringComparison.Ordinal)})",
                _ => entry.Name,
            };
        }

        private static async Task TryAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (ObsResponseException)
            {
                // Already gone: what is wanted is that it is not there.
            }
        }

        // The settings of a media source that plays a file once. A source that restarts when it is shown (what OBS Studio does by default) is not opened until its scene is on the program output; one that does
        // not is opened, and plays, as soon as it is made.
        private static object MediaSettings(string file, bool restartOnActivate = true)
        {
            return new { local_file = file, is_local_file = true, looping = false, restart_on_activate = restartOnActivate, close_when_inactive = false };
        }

        // OBS Studio holds a media file open until the input is gone, and for a moment after that.
        private static async Task DeleteAsync(string path)
        {
            for (var attempt = 0; attempt < 40 && File.Exists(path); attempt++)
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    await Task.Delay(250);
                }
            }
        }

        // What OBS Studio says about the media, asked again until it says what is wanted: the position of a media that was moved, and the length of one that was just opened, come a moment later.
        private static async Task<ObsMediaStatus> WaitForStatusAsync(ObsMedia media, string what, Func<ObsMediaStatus, bool> reached)
        {
            var deadline = DateTime.UtcNow + Wait;
            var status = await media.GetStatusAsync();
            while (!reached(status) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
                status = await media.GetStatusAsync();
            }

            Assert.True(reached(status), $"The media of {media.Input.Label} did not {what} in time: OBS Studio says {status}.");
            return status;
        }

        // The changes of the state of a media, as the model announces them.
        private sealed class StateLog
        {
            private readonly List<ObsMediaState> _states = [];

            public StateLog(ObsModel model, ObsMedia media)
            {
                model.Changed += (_, e) =>
                {
                    if (ReferenceEquals(e.Entity, media) && e.PropertyName == nameof(ObsMedia.State))
                    {
                        lock (this._states)
                        {
                            this._states.Add((ObsMediaState)e.NewValue!);
                        }
                    }
                };
            }

            // The changes are raised on a thread of their own, a moment after the model shows them.
            public bool Is(params ObsMediaState[] expected)
            {
                lock (this._states)
                {
                    return this._states.SequenceEqual(expected);
                }
            }

            public override string ToString()
            {
                lock (this._states)
                {
                    return string.Join(", ", this._states);
                }
            }
        }

        [LiveUuidChangesFact]
        public async Task Observation_TheStatesAndTheEventsOfAMediaInput()
        {
            await using var session = await LiveSession.ConnectAsync(EventSubscriptions.All, removeLeftovers: true);
            var client = session.Client;
            var version = await client.GetVersion();
            output.WriteLine($"OBS Studio {version.ObsVersion}, obs-websocket {version.ObsWebSocketVersion}.");

            var longFile = WriteWave(12);
            var shortFile = WriteWave(1.5);
            var sceneName = LiveObs.UniqueName("media");
            var names = new List<string>();
            var seen = 0;

            // What the model needs to know: after an action, what the events say and what the status says.
            async Task StepAsync(string what, InputRef input, Func<Task>? act = null, int wait = 700)
            {
                string result = "ok";
                try
                {
                    if (act is not null)
                    {
                        await act();
                    }
                }
                catch (ObsResponseException exception)
                {
                    result = $"refused {(int)exception.ErrorCode} {exception.ErrorCode}: {exception.ErrorMessage}";
                }

                await Task.Delay(wait);
                var events = session.Events.Skip(seen).Where(entry => entry.Name.StartsWith("MediaInput", StringComparison.Ordinal)).Select(Describe).ToList();
                seen = session.Events.Count;
                var status = await client.GetMediaInputStatus(input);
                output.WriteLine($"{what}: {result}; events [{string.Join(", ", events)}]; state {status.MediaState}, duration {status.MediaDuration?.ToString() ?? "null"}, cursor {status.MediaCursor?.ToString() ?? "null"}");
            }

            var original = await client.GetCurrentProgramScene();
            try
            {
                var sceneUuid = await client.CreateScene(sceneName);
                var scene = SceneRef.FromUuid(sceneUuid);
                seen = session.Events.Count;

                // A media input that is not in the program scene, with a file of 12 seconds.
                var clipName = LiveObs.UniqueName("clip");
                names.Add(clipName);
                var created = await client.CreateInput(
                    scene,
                    clipName,
                    MediaKind,
                    new Dictionary<string, object> { ["local_file"] = longFile, ["is_local_file"] = true, ["looping"] = false, ["restart_on_activate"] = true, ["close_when_inactive"] = false },
                    sceneItemEnabled: true);
                var clip = InputRef.FromUuid(created.InputUuid);
                var listed = (await client.GetInputList()).First(candidate => candidate.InputName == clipName);
                output.WriteLine($"capabilities of {MediaKind}: {listed.InputKindCaps} (audio bit {((listed.InputKindCaps ?? 0) & 2) != 0}, controllable media bit {((listed.InputKindCaps ?? 0) & 8192) != 0})");

                await StepAsync("0 right after the creation (not in the program scene)", clip, wait: 900);
                await StepAsync("1 pause", clip, () => client.TriggerMediaInputAction(clip, Pause));
                await StepAsync("2 play (from paused)", clip, () => client.TriggerMediaInputAction(clip, Play));
                await StepAsync("3 stop", clip, () => client.TriggerMediaInputAction(clip, Stop));
                await StepAsync("4 play (from stopped)", clip, () => client.TriggerMediaInputAction(clip, Play));
                await StepAsync("5 restart", clip, () => client.TriggerMediaInputAction(clip, Restart));
                await StepAsync("6 set the cursor to 4000 (playing)", clip, () => client.SetMediaInputCursor(clip, 4000));
                await StepAsync("7 offset the cursor by -1000", clip, () => client.OffsetMediaInputCursor(clip, -1000));
                await StepAsync("8 offset the cursor by +500", clip, () => client.OffsetMediaInputCursor(clip, 500));
                await StepAsync("9 offset the cursor by -60000 (before the start)", clip, () => client.OffsetMediaInputCursor(clip, -60000));
                await StepAsync("10 pause", clip, () => client.TriggerMediaInputAction(clip, Pause));
                await StepAsync("11 set the cursor to 3000 (paused)", clip, () => client.SetMediaInputCursor(clip, 3000));
                await StepAsync("12 set the cursor to 999999 (beyond the end, paused)", clip, () => client.SetMediaInputCursor(clip, 999999));
                await StepAsync("13 stop", clip, () => client.TriggerMediaInputAction(clip, Stop));
                await StepAsync("14 set the cursor to 1000 (stopped)", clip, () => client.SetMediaInputCursor(clip, 1000));
                await StepAsync("15 offset the cursor (stopped)", clip, () => client.OffsetMediaInputCursor(clip, 100));
                await StepAsync("16 next", clip, () => client.TriggerMediaInputAction(clip, Next));
                await StepAsync("17 previous", clip, () => client.TriggerMediaInputAction(clip, Previous));
                await StepAsync("18 pause (from stopped)", clip, () => client.TriggerMediaInputAction(clip, Pause));
                await StepAsync("19 play (from stopped, after a pause)", clip, () => client.TriggerMediaInputAction(clip, Play));
                await StepAsync("20 play (while playing)", clip, () => client.TriggerMediaInputAction(clip, Play));
                await StepAsync("21 pause", clip, () => client.TriggerMediaInputAction(clip, Pause));
                await StepAsync("22 pause (while paused)", clip, () => client.TriggerMediaInputAction(clip, Pause));
                await StepAsync("23 restart (from paused)", clip, () => client.TriggerMediaInputAction(clip, Restart));
                await StepAsync("24 stop", clip, () => client.TriggerMediaInputAction(clip, Stop));
                await StepAsync("25 stop (while stopped)", clip, () => client.TriggerMediaInputAction(clip, Stop));

                // The program scene: the source becomes active.
                await StepAsync("26 the scene becomes the program scene", clip, () => client.SetCurrentProgramScene(scene), wait: 1200);
                await StepAsync("27 play (in the program scene)", clip, () => client.TriggerMediaInputAction(clip, Play));
                await StepAsync("28 the original program scene comes back", clip, () => client.SetCurrentProgramScene(SceneRef.FromUuid(original.SceneUuid)), wait: 1200);
                await StepAsync("29 stop", clip, () => client.TriggerMediaInputAction(clip, Stop));

                // A file that ends: 1.5 seconds, not looping.
                var shortName = LiveObs.UniqueName("short");
                names.Add(shortName);
                var shortCreated = await client.CreateInput(
                    scene,
                    shortName,
                    MediaKind,
                    new Dictionary<string, object> { ["local_file"] = shortFile, ["is_local_file"] = true, ["looping"] = false, ["restart_on_activate"] = true, ["close_when_inactive"] = false },
                    sceneItemEnabled: true);
                var shortClip = InputRef.FromUuid(shortCreated.InputUuid);
                await StepAsync("30 a file of 1.5 s, right after the creation", shortClip, wait: 600);
                await StepAsync("31 the same, after 2.5 more seconds", shortClip, wait: 2500);
                await StepAsync("32 play (after it ended)", shortClip, () => client.TriggerMediaInputAction(shortClip, Play), wait: 600);
                await StepAsync("33 the same, after 2.5 more seconds", shortClip, wait: 2500);
                await StepAsync("34 restart (after it ended)", shortClip, () => client.TriggerMediaInputAction(shortClip, Restart), wait: 600);

                // A file that does not exist.
                var missingName = LiveObs.UniqueName("missing");
                names.Add(missingName);
                var missingCreated = await client.CreateInput(
                    scene,
                    missingName,
                    MediaKind,
                    new Dictionary<string, object> { ["local_file"] = Path.Combine(Path.GetTempPath(), "obsclient-live-does-not-exist.wav"), ["is_local_file"] = true, ["looping"] = false },
                    sceneItemEnabled: true);
                var missing = InputRef.FromUuid(missingCreated.InputUuid);
                await StepAsync("35 a file that does not exist, right after the creation", missing, wait: 900);
                await StepAsync("36 play", missing, () => client.TriggerMediaInputAction(missing, Play), wait: 900);

                // An input that is not a media input.
                var colorKind = (await client.GetInputKindList()).Where(kind => kind.StartsWith("color_source", StringComparison.Ordinal)).OrderByDescending(kind => kind, StringComparer.Ordinal).First();
                var colorName = LiveObs.UniqueName("color");
                names.Add(colorName);
                var colorCreated = await client.CreateInput(scene, colorName, colorKind, new Dictionary<string, object> { ["color"] = 4278190335L, ["width"] = 64, ["height"] = 64 }, sceneItemEnabled: true);
                var color = InputRef.FromUuid(colorCreated.InputUuid);
                var colorListed = (await client.GetInputList()).First(candidate => candidate.InputName == colorName);
                output.WriteLine($"capabilities of {colorKind}: {colorListed.InputKindCaps} (controllable media bit {((colorListed.InputKindCaps ?? 0) & 8192) != 0})");
                await StepAsync("37 a color source: its status", color, wait: 200);
                await StepAsync("38 a color source: play", color, () => client.TriggerMediaInputAction(color, Play), wait: 300);
                await StepAsync("39 a color source: set the cursor", color, () => client.SetMediaInputCursor(color, 100), wait: 100);

                // With the scene on the program output the source is active: it moves, and a change of its position takes effect.
                await StepAsync("40 the scene is the program scene again", clip, () => client.SetCurrentProgramScene(scene), wait: 1200);
                await StepAsync("41 set the cursor to 5000 (playing, shown)", clip, () => client.SetMediaInputCursor(clip, 5000), wait: 300);
                await StepAsync("42 pause", clip, () => client.TriggerMediaInputAction(clip, Pause), wait: 300);
                await StepAsync("43 the same, a second later", clip, wait: 1000);
                await StepAsync("44 set the cursor to 3000 (paused, shown)", clip, () => client.SetMediaInputCursor(clip, 3000), wait: 300);
                await StepAsync("45 offset the cursor by -1000 (paused, shown)", clip, () => client.OffsetMediaInputCursor(clip, -1000), wait: 300);
                await StepAsync("46 offset the cursor by -60000 (paused, shown)", clip, () => client.OffsetMediaInputCursor(clip, -60000), wait: 300);
                await StepAsync("47 play", clip, () => client.TriggerMediaInputAction(clip, Play), wait: 300);
                await StepAsync("48 offset the cursor by +2000 (playing, shown)", clip, () => client.OffsetMediaInputCursor(clip, 2000), wait: 300);
                await StepAsync("49 set the cursor to 999999 (playing, shown, beyond the end)", clip, () => client.SetMediaInputCursor(clip, 999999), wait: 1500);
                await StepAsync("50 the same, a second later", clip, wait: 1000);
            }
            finally
            {
                foreach (var name in names)
                {
                    await TryAsync(() => client.RemoveInput(InputRef.FromName(name)));
                }

                await TryAsync(() => client.SetCurrentProgramScene(SceneRef.FromUuid(original.SceneUuid)));
                await TryAsync(() => client.RemoveScene(SceneRef.FromName(sceneName)));
                await DeleteAsync(longFile);
                await DeleteAsync(shortFile);
            }
        }

        [LiveUuidChangesFact]
        public async Task TheMedia_IsPlayedPausedStoppedRestartedAndMoved_AsOBSStudioDoesIt_AndTheModelShowsEveryStep()
        {
            var file = WriteWave(30);
            try
            {
                await using var live = await LiveModel.ConnectAsync(output);
                var model = live.Model;
                output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
                var scene = await model.CreateSceneAsync(LiveObs.UniqueName("media"));
                var color = await scene.CreateInputAsync(LiveObs.UniqueName("color"), await live.ColorKindAsync(), new { width = 64, height = 64 });
                var input = await scene.CreateInputAsync(LiveObs.UniqueName("clip"), MediaKind, MediaSettings(file));

                // Only a media input has a media. The scene is not on the program output, so OBS Studio does not open the file.
                Assert.Null(color.Media);
                var media = input.Media;
                Assert.NotNull(media);
                Assert.Same(input, media.Input);
                Assert.Equal(ObsMediaState.None, media.State);
                await live.AssertModelEqualsObsAsync("after the media input was made");
                var log = new StateLog(model, media);

                // A pause opens a media that was not opened, and holds it at the beginning.
                await media.PauseAsync();
                Assert.Equal(ObsMediaState.Paused, media.State);
                var status = await WaitForStatusAsync(media, "say its length", candidate => candidate.State == ObsMediaState.Paused && candidate.Duration is not null);
                output.WriteLine($"after the pause: {status}");
                Assert.InRange(status.Duration!.Value.TotalSeconds, 29.5, 30.5);
                await live.AssertModelEqualsObsAsync("after the media was paused");

                await media.PlayAsync();
                Assert.Equal(ObsMediaState.Playing, media.State);
                Assert.Equal(ObsMediaState.Paused, await media.TogglePlayPauseAsync());
                Assert.Equal(ObsMediaState.Paused, media.State);
                Assert.Equal(ObsMediaState.Playing, await media.TogglePlayPauseAsync());
                Assert.Equal(ObsMediaState.Playing, media.State);
                await live.AssertModelEqualsObsAsync("after the media was played, paused and played again");

                // A media input that is not on the program output takes a change of its position and does not carry it out: its position stays where it was. So the scene is shown first, and OBS Studio plays the
                // media from the start (which the model shows as before: it played).
                await scene.SwitchToAsync();
                status = await WaitForStatusAsync(media, "play in the program scene", candidate => candidate.State == ObsMediaState.Playing && candidate.Position is { } position && position > TimeSpan.FromMilliseconds(300));
                output.WriteLine($"when the scene was shown: {status}");

                // A media that plays and is shown moves when it is told to, and goes on from there. OBS Studio stops at the beginning.
                await media.SeekAsync(TimeSpan.FromSeconds(5));
                status = await WaitForStatusAsync(media, "move to five seconds", candidate => candidate.Position is { } position && position >= TimeSpan.FromSeconds(4.9) && position < TimeSpan.FromSeconds(8));
                output.WriteLine($"after the seek to 5 s: {status}");
                var before = status.Position!.Value;
                await media.SkipAsync(TimeSpan.FromSeconds(2));
                status = await WaitForStatusAsync(media, "move forwards by two seconds", candidate => candidate.Position is { } position && position - before >= TimeSpan.FromSeconds(1.9) && position - before < TimeSpan.FromSeconds(5));
                output.WriteLine($"after the skip of 2 s: {status}");
                await media.SkipAsync(TimeSpan.FromMinutes(-10));
                status = await WaitForStatusAsync(media, "stop at the beginning", candidate => candidate.Position is { } position && position < TimeSpan.FromSeconds(1.5));
                output.WriteLine($"after the skip of -10 minutes: {status}");
                Assert.Equal(ObsMediaState.Playing, media.State);

                // A single file has nothing next and nothing before: OBS Studio says that it did it, and nothing changes.
                await media.NextAsync();
                await media.PreviousAsync();
                Assert.Equal(ObsMediaState.Playing, media.State);

                // A position beyond the end ends the media (OBS Studio says so with the event that it sends when a media ends), and a media that ended plays from the start when it is told to.
                await media.SeekAsync(TimeSpan.FromMinutes(10));
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => media.State == ObsMediaState.Ended, Wait), $"The media did not end: it is {media.State}.");
                status = await WaitForStatusAsync(media, "say that it ended", candidate => candidate.State == ObsMediaState.Ended);
                Assert.Null(status.Duration);
                Assert.Null(status.Position);
                await media.PlayAsync();
                Assert.Equal(ObsMediaState.Playing, media.State);
                status = await WaitForStatusAsync(media, "play from the start", candidate => candidate.State == ObsMediaState.Playing && candidate.Position is { } position && position < TimeSpan.FromSeconds(2));
                output.WriteLine($"after the play of the media that ended: {status}");
                await live.AssertModelEqualsObsAsync("after the media ended and was played");

                // OBS Studio takes a change of the position of a media that is paused, and plays it from the start when it plays again: there is no position to ask for, so only the calls are checked.
                await media.PauseAsync();
                Assert.Equal(ObsMediaState.Paused, media.State);
                await media.SeekAsync(TimeSpan.FromSeconds(3));
                await media.SkipAsync(TimeSpan.FromSeconds(-1));
                Assert.Equal(ObsMediaState.Paused, media.State);
                await media.PlayAsync();
                Assert.Equal(ObsMediaState.Playing, media.State);

                // A stopped media is closed: it has no length and no position, and the position cannot be changed.
                await media.StopAsync();
                Assert.Equal(ObsMediaState.Stopped, media.State);
                status = await WaitForStatusAsync(media, "say that it is stopped", candidate => candidate.State == ObsMediaState.Stopped);
                output.WriteLine($"after the stop: {status}");
                Assert.Null(status.Duration);
                Assert.Null(status.Position);
                var refusedSeek = await Assert.ThrowsAsync<ObsResponseException>(() => media.SeekAsync(TimeSpan.FromSeconds(1)));
                Assert.Equal(RequestStatusCode.InvalidResourceState, refusedSeek.ErrorCode);
                var refusedSkip = await Assert.ThrowsAsync<ObsResponseException>(() => media.SkipAsync(TimeSpan.FromMilliseconds(100)));
                Assert.Equal(RequestStatusCode.InvalidResourceState, refusedSkip.ErrorCode);
                Assert.Equal(ObsMediaState.Stopped, media.State);
                await live.AssertModelEqualsObsAsync("after the media was stopped");

                await media.RestartAsync();
                Assert.Equal(ObsMediaState.Playing, media.State);
                await media.StopAsync();
                await media.StopAsync();
                Assert.Equal(ObsMediaState.Stopped, media.State);
                await live.AssertModelEqualsObsAsync("after the media was restarted and stopped");

                // Every change was announced once, in order: showing the scene made the media play from the start, which it did, and a stop of a media that was stopped is no change.
                Assert.True(
                    await AsyncTestHelpers.WaitUntilAsync(
                        () => log.Is(
                            ObsMediaState.Paused,
                            ObsMediaState.Playing,
                            ObsMediaState.Paused,
                            ObsMediaState.Playing,
                            ObsMediaState.Ended,
                            ObsMediaState.Playing,
                            ObsMediaState.Paused,
                            ObsMediaState.Playing,
                            ObsMediaState.Stopped,
                            ObsMediaState.Playing,
                            ObsMediaState.Stopped),
                        Wait),
                    $"The model announced these changes of the state: {log}.");

                // An input that was removed has no calls.
                await input.RemoveAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => input.State == ObsObjectState.Gone, Wait), $"The input was not destroyed (it is {input.State}).");
                await Assert.ThrowsAsync<ObsRemovedException>(() => media.PlayAsync());
            }
            finally
            {
                await DeleteAsync(file);
            }
        }

        [LiveUuidChangesFact]
        public async Task TheMediaOfASceneThatBecomesTheProgramScene_PlaysAndEnds_AsOBSStudioDoesIt_AndTheModelFollows()
        {
            var file = WriteWave(30);
            var shortFile = WriteWave(1.5);
            try
            {
                await using var live = await LiveModel.ConnectAsync(output);
                var model = live.Model;
                output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
                var original = model.ProgramScene;
                Assert.NotNull(original);
                var scene = await model.CreateSceneAsync(LiveObs.UniqueName("shown"));
                var clip = (await scene.CreateInputAsync(LiveObs.UniqueName("clip"), MediaKind, MediaSettings(file, restartOnActivate: true))).Media!;
                var brief = (await scene.CreateInputAsync(LiveObs.UniqueName("brief"), MediaKind, MediaSettings(shortFile, restartOnActivate: true))).Media!;
                Assert.Equal(ObsMediaState.None, clip.State);
                Assert.Equal(ObsMediaState.None, brief.State);
                var clipLog = new StateLog(model, clip);
                var briefLog = new StateLog(model, brief);

                // The scene is shown: both media play from the start, and the short one ends by itself.
                await scene.SwitchToAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => clipLog.Is(ObsMediaState.Playing), Wait), $"The long media did not start to play: it announced {clipLog}.");
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => briefLog.Is(ObsMediaState.Playing, ObsMediaState.Ended), Wait), $"The short media did not play and end: it announced {briefLog}.");
                await live.AssertModelEqualsObsAsync("while the scene is the program scene");

                // The scene is no longer shown: the media that was playing ends, and the one that ended stays as it is.
                await original!.SwitchToAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => clipLog.Is(ObsMediaState.Playing, ObsMediaState.Ended), Wait), $"The long media did not end: it announced {clipLog}.");
                await live.AssertModelEqualsObsAsync("after the scene is no longer the program scene");
                Assert.True(briefLog.Is(ObsMediaState.Playing, ObsMediaState.Ended), $"The short media announced {briefLog}.");

                // A media that ended plays from the start when it is told to.
                await clip.PlayAsync();
                Assert.Equal(ObsMediaState.Playing, clip.State);
                await live.AssertModelEqualsObsAsync("after the media that ended was played");
                await clip.StopAsync();
                Assert.Equal(ObsMediaState.Stopped, clip.State);
                await live.AssertModelEqualsObsAsync("after the media was stopped");
                output.WriteLine($"the long media announced: {clipLog}; the short one: {briefLog}.");
            }
            finally
            {
                await DeleteAsync(file);
                await DeleteAsync(shortFile);
            }
        }

        [LiveUuidChangesFact]
        public async Task WhatTheUserDoesToAMediaInputInObsStudio_IsFollowedByTheModel_AndTheModelEqualsWhatObsStudioHolds()
        {
            var file = WriteWave(30);
            try
            {
                await using var live = await LiveModel.ConnectAsync(output);
                var model = live.Model;
                var scene = await model.CreateSceneAsync(LiveObs.UniqueName("user"));
                var name = LiveObs.UniqueName("clip");

                // The user makes the input in OBS Studio (the plain client plays the user): the model has it, with its media, when OBS Studio has announced it.
                var created = await live.Client.CreateInput(
                    SceneRef.FromUuid(scene.Uuid),
                    name,
                    MediaKind,
                    new Dictionary<string, object> { ["local_file"] = file, ["is_local_file"] = true, ["looping"] = false, ["restart_on_activate"] = true, ["close_when_inactive"] = false },
                    sceneItemEnabled: true);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => model.Inputs.Contains(name) && model.Inputs[name].Media is not null, Wait), "The model did not get the media input that was made in OBS Studio.");
                var media = model.Inputs[name].Media!;
                var log = new StateLog(model, media);
                var reference = InputRef.FromUuid(created.InputUuid);
                await live.AssertModelEqualsObsAsync("after the user made the media input");

                var steps = new[]
                {
                    (Action: Pause, Shows: ObsMediaState.Paused),
                    (Action: Play, Shows: ObsMediaState.Playing),
                    (Action: Stop, Shows: ObsMediaState.Stopped),
                    (Action: Restart, Shows: ObsMediaState.Playing),
                    (Action: Pause, Shows: ObsMediaState.Paused),
                    (Action: Stop, Shows: ObsMediaState.Stopped),
                };
                foreach (var (action, shows) in steps)
                {
                    await live.Client.TriggerMediaInputAction(reference, action);
                    Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => media.State == shows, Wait), $"The model shows {media.State} after the user did {action}, and not {shows}.");
                    await live.AssertModelEqualsObsAsync($"after the user did {action}");
                }

                Assert.True(
                    await AsyncTestHelpers.WaitUntilAsync(
                        () => log.Is(ObsMediaState.Paused, ObsMediaState.Playing, ObsMediaState.Stopped, ObsMediaState.Playing, ObsMediaState.Paused, ObsMediaState.Stopped),
                        Wait),
                    $"The model announced these changes of the state: {log}.");
            }
            finally
            {
                await DeleteAsync(file);
            }
        }

        [LiveUuidChangesFact]
        public async Task AMediaInputThatDoesNotWaitToBeShown_PlaysAtOnce_AndTheModelFollowsItFromItsCreation()
        {
            var file = WriteWave(30);
            try
            {
                await using var live = await LiveModel.ConnectAsync(output);
                var model = live.Model;
                var scene = await model.CreateSceneAsync(LiveObs.UniqueName("now"));

                // OBS Studio opens the file and plays it while the model reads the new input: whatever the model read first, what it shows has to become what OBS Studio holds.
                var input = await scene.CreateInputAsync(LiveObs.UniqueName("clip"), MediaKind, MediaSettings(file, restartOnActivate: false));
                var media = input.Media!;
                output.WriteLine($"right after the input was made, the model shows {media.State}.");
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => media.State == ObsMediaState.Playing, Wait), $"The media did not show that it plays: it is {media.State}.");
                await live.AssertModelEqualsObsAsync("after the media input that plays at once was made");

                await media.PauseAsync();
                Assert.Equal(ObsMediaState.Paused, media.State);
                await live.AssertModelEqualsObsAsync("after the media that played at once was paused");
                await media.StopAsync();
                Assert.Equal(ObsMediaState.Stopped, media.State);
                await live.AssertModelEqualsObsAsync("after the media that played at once was stopped");
            }
            finally
            {
                await DeleteAsync(file);
            }
        }

        [LiveUuidChangesFact]
        public async Task AMediaInputWithAFileThatDoesNotExist_ShowsWhatObsStudioHolds_AndIsPlayedAllTheSame()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var scene = await model.CreateSceneAsync(LiveObs.UniqueName("missing"));
            var missing = Path.Combine(Path.GetTempPath(), "obsclient-live-does-not-exist.wav");
            Assert.False(File.Exists(missing));
            var input = await scene.CreateInputAsync(LiveObs.UniqueName("clip"), MediaKind, MediaSettings(missing));
            var media = input.Media!;
            output.WriteLine($"right after the input was made, the model shows {media.State}.");
            await live.AssertModelEqualsObsAsync("after the media input of a file that does not exist was made");
            output.WriteLine($"OBS Studio and the model agree on {media.State}.");

            // OBS Studio answers that it plays a media that it cannot open.
            await media.PlayAsync();
            Assert.Equal(ObsMediaState.Playing, media.State);
            output.WriteLine($"after the play: {await media.GetStatusAsync()}.");
            await live.AssertModelEqualsObsAsync("after the play of a file that does not exist");
        }
    }
}
