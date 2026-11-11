namespace OBSStudioClient.Tests.Live
{
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// What the model does with the levels of the audio (<see cref="ObsHighVolume.AudioLevels"/>) against a real OBS Studio: the inputs that are on the air and have audio get levels (a tone of a known loudness in a file, mono
    /// and stereo), a muted input has levels of zero, the volume scales them, a paused input has zeros and keeps its levels, an input that goes off the air loses them, and a handler that is slower than OBS Studio
    /// gets the newest levels and does not hold up the model. Everything that is made carries the name prefix of <see cref="LiveObs.NamePrefix"/> and is removed again, and so are the files of the tones.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveLevelsModelTests(ITestOutputHelper output)
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

        private static object ToneSettings(string file) => new { local_file = file, is_local_file = true, looping = true, restart_on_activate = true, close_when_inactive = false };

        // The levels are measured over short chunks of audio and the first message after an input comes on the air can be far off (see ObsChannelLevel.Peak): the middle of a few is what a tone has.
        private static async Task<float[]> MedianAsync(Func<ObsChannelLevel?> channel, int samples = 7)
        {
            var magnitudes = new List<float>();
            var peaks = new List<float>();
            var inputPeaks = new List<float>();
            for (var sample = 0; sample < samples; sample++)
            {
                if (channel() is { } level)
                {
                    magnitudes.Add(level.Magnitude);
                    peaks.Add(level.Peak);
                    inputPeaks.Add(level.InputPeak);
                }

                await Task.Delay(120);
            }

            float Middle(List<float> values) => values.Count == 0 ? float.NaN : values.Order().ElementAt(values.Count / 2);
            return [Middle(magnitudes), Middle(peaks), Middle(inputPeaks)];
        }

        private static async Task DeleteAsync(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                // OBS Studio lets go of a file a moment after the input that plays it is removed.
                for (var attempt = 0; attempt < 10; attempt++)
                {
                    try
                    {
                        File.Delete(file);
                        break;
                    }
                    catch (IOException)
                    {
                        await Task.Delay(300);
                    }
                }
            }
        }

        [LiveUuidChangesFact]
        public async Task TheLevelsOfTheInputsOnTheAir_AreFollowedByTheModel_AsOBSStudioSendsThem()
        {
            var files = new List<string>();
            try
            {
                await using var live = await LiveModel.ConnectAsync(output, ObsHighVolume.AudioLevels | ObsHighVolume.Activity);
                var model = live.Model;
                var kind = await live.ColorKindAsync();
                output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
                var mono = LiveLevelsTests.WriteWave(3, (440, 0.5));
                var stereo = LiveLevelsTests.WriteWave(3, (440, 0.5), (880, 0.25));
                files.Add(mono);
                files.Add(stereo);

                var raised = new List<ObsAudioLevelsEventArgs>();
                var resynchronized = 0;
                model.AudioLevels += (_, e) =>
                {
                    lock (raised)
                    {
                        raised.Add(e);
                    }
                };
                model.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);

                var a = await model.CreateSceneAsync(LiveObs.UniqueName("lev-a"));
                var b = await model.CreateSceneAsync(LiveObs.UniqueName("lev-b"));
                var ta = await a.CreateInputAsync(LiveObs.UniqueName("lev-ta"), "ffmpeg_source", ToneSettings(mono));
                var tb = await a.CreateInputAsync(LiveObs.UniqueName("lev-tb"), "ffmpeg_source", ToneSettings(stereo));
                var ce = await a.CreateInputAsync(LiveObs.UniqueName("lev-ce"), kind, new { color = 4278190335L, width = 320, height = 180 });
                var td = await b.CreateInputAsync(LiveObs.UniqueName("lev-td"), "ffmpeg_source", ToneSettings(mono));

                // Nothing that is not on the air has levels: the program scene is neither A nor B.
                await Task.Delay(500);
                Assert.All(new[] { ta, tb, ce, td }, input => Assert.Null(input.Levels));

                // The inputs of A go on the air: the tones have levels, and the color source (no audio) has none.
                await a.SwitchToAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => ta.Levels?.Channels.Count == 2 && tb.Levels?.Channels.Count == 2, Wait), "The levels of the tones did not arrive.");
                Assert.Null(ce.Levels);
                Assert.Null(td.Levels);
                var monoLevel = await MedianAsync(() => ta.Levels is { Channels.Count: 2 } levels ? levels.Channels[0] : null);
                var left = await MedianAsync(() => tb.Levels is { Channels.Count: 2 } levels ? levels.Channels[0] : null);
                var right = await MedianAsync(() => tb.Levels is { Channels.Count: 2 } levels ? levels.Channels[1] : null);
                output.WriteLine($"mono: magnitude {monoLevel[0]:0.000}, peak {monoLevel[1]:0.000}, input peak {monoLevel[2]:0.000}; stereo left {left[0]:0.000}/{left[1]:0.000}/{left[2]:0.000}, right {right[0]:0.000}/{right[1]:0.000}/{right[2]:0.000}");

                // A tone of half the full scale: a magnitude of half of it over the root of two, and a peak of half. The left channel is twice as loud as the right.
                Assert.InRange(monoLevel[0], 0.30f, 0.40f);
                Assert.InRange(monoLevel[1], 0.45f, 0.55f);
                Assert.InRange(left[1], 0.45f, 0.55f);
                Assert.InRange(right[1], 0.20f, 0.30f);
                Assert.InRange(right[0], 0.15f, 0.20f);

                // The levels come about twenty times a second, and the model tells of them when they change.
                int before;
                lock (raised)
                {
                    before = raised.Count;
                }

                await Task.Delay(2000);
                int told;
                lock (raised)
                {
                    told = raised.Count - before;
                    Assert.All(raised.Skip(before), args => Assert.Contains(args.Levels, levels => levels.Input == ta));
                }

                output.WriteLine($"{told} notifications of the levels in 2 seconds.");
                Assert.InRange(told, 10, 45);

                // A muted input is listed, with levels of zero and the peak of the input as it is.
                await ta.SetMutedAsync(true);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => ta.Levels is { Channels.Count: 2 } levels && levels.Channels[0].Magnitude == 0f && levels.Channels[0].Peak == 0f && levels.Channels[0].InputPeak > 0.4f, Wait), "The levels of the muted input were not zero.");
                await ta.SetMutedAsync(false);

                // The volume scales the magnitude and the peak, and not the peak of the input.
                await ta.SetVolumeDbAsync(-6);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => ta.Levels is { Channels.Count: 2 } levels && levels.Channels[0].Magnitude is > 0.1f and < 0.25f, Wait), "The volume did not scale the levels.");
                var scaled = await MedianAsync(() => ta.Levels is { Channels.Count: 2 } levels ? levels.Channels[0] : null);
                output.WriteLine($"at -6 dB: magnitude {scaled[0]:0.000}, peak {scaled[1]:0.000}, input peak {scaled[2]:0.000}");
                Assert.InRange(scaled[0], 0.15f, 0.21f);
                Assert.InRange(scaled[2], 0.45f, 0.55f);
                await ta.SetVolumeDbAsync(0);

                // An input that plays nothing is still listed: its levels are zero.
                await ta.Media!.PauseAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => ta.Levels is { Channels.Count: 2 } levels && levels.Channels[0].Magnitude == 0f && levels.Channels[0].Peak == 0f, Wait), "The levels of the paused input did not go to zero.");
                Assert.NotNull(ta.Levels);
                await ta.Media!.PlayAsync();

                // The levels agree with whether the input is on the air.
                await live.AssertModelEqualsObsAsync("with the levels on");
                foreach (var input in new[] { ta, tb, ce, td })
                {
                    Assert.Equal(input.HasAudio && input.IsActive == true, input.Levels is not null);
                }

                // The program scene changes: the inputs of A lose their levels (and are told as ended), and the input of B gets some.
                await b.SwitchToAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => ta.Levels is null && tb.Levels is null && td.Levels is { Channels.Count: 2 }, Wait), "The levels did not follow the program scene.");
                lock (raised)
                {
                    Assert.Contains(raised, args => args.Ended.Contains(ta));
                    Assert.Contains(raised, args => args.Ended.Contains(tb));
                }

                await live.AssertModelEqualsObsAsync("after the program scene changed");
                Assert.Equal(0, Volatile.Read(ref resynchronized));
            }
            finally
            {
                await DeleteAsync(files);
            }
        }

        [LiveUuidChangesFact]
        public async Task AHandlerThatIsSlowerThanOBSStudio_GetsTheNewestLevels_AndDoesNotHoldUpTheModelForLong()
        {
            var files = new List<string>();
            try
            {
                await using var live = await LiveModel.ConnectAsync(output, ObsHighVolume.AudioLevels);
                var model = live.Model;
                var mono = LiveLevelsTests.WriteWave(3, (440, 0.5));
                files.Add(mono);
                var a = await model.CreateSceneAsync(LiveObs.UniqueName("lev-a"));
                var tone = await a.CreateInputAsync(LiveObs.UniqueName("lev-tone"), "ffmpeg_source", ToneSettings(mono));
                var resynchronized = 0;
                model.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);
                var calls = 0;
                using var gate = new ManualResetEventSlim();
                model.AudioLevels += (_, _) =>
                {
                    // The first notification holds the handler for two seconds: forty messages of OBS Studio come meanwhile.
                    if (Interlocked.Increment(ref calls) == 1)
                    {
                        gate.Wait(TimeSpan.FromSeconds(2));
                    }
                };

                await a.SwitchToAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => Volatile.Read(ref calls) >= 1, Wait), "The handler was not called.");
                await Task.Delay(2500);

                // The handler was called again after it had held up the lane, and not forty times: what came meanwhile was told once, with the newest levels.
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => Volatile.Read(ref calls) >= 2, Wait), "The handler was not called again.");
                output.WriteLine($"The handler was called {Volatile.Read(ref calls)} times while it was held for 2 seconds and 2.5 seconds followed.");
                Assert.True(Volatile.Read(ref calls) < 60, "The handler was called for every message.");
                Assert.Equal(0, Volatile.Read(ref resynchronized));
                Assert.NotNull(tone.Levels);
                await live.AssertModelEqualsObsAsync("after the slow handler");
            }
            finally
            {
                await DeleteAsync(files);
            }
        }

        [LiveUuidChangesFact]
        public async Task TheLevels_AreNotAskedFor_WhenTheModelIsNotAskedToFollowThem()
        {
            var files = new List<string>();
            try
            {
                await using var live = await LiveModel.ConnectAsync(output);
                var model = live.Model;
                Assert.Equal(ObsHighVolume.None, model.HighVolume);
                var mono = LiveLevelsTests.WriteWave(3, (440, 0.5));
                files.Add(mono);
                var a = await model.CreateSceneAsync(LiveObs.UniqueName("lev-a"));
                var tone = await a.CreateInputAsync(LiveObs.UniqueName("lev-tone"), "ffmpeg_source", ToneSettings(mono));
                var raised = 0;
                model.AudioLevels += (_, _) => Interlocked.Increment(ref raised);

                await a.SwitchToAsync();
                await Task.Delay(1500);

                Assert.Null(tone.Levels);
                Assert.Equal(0, Volatile.Read(ref raised));
                await live.AssertModelEqualsObsAsync("after the tone was put on the air, and not followed");
            }
            finally
            {
                await DeleteAsync(files);
            }
        }
    }
}
