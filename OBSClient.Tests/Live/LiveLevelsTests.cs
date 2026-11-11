namespace OBSStudioClient.Tests.Live
{
    using System.Diagnostics;
    using System.Text.Json;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// The levels of the audio (the event <c>InputVolumeMeters</c>) against a real OBS Studio: how often it comes, which inputs it lists (only the ones that are on the air, and have audio?), what it says of a muted input, an
    /// input that plays nothing, an input that was just made or removed, and how many channels and which numbers it gives for a mono and a stereo file. It prints what it sees, and checks nothing: what the simulation does is
    /// what this printed. Everything that is made carries the name prefix of <see cref="LiveObs.NamePrefix"/> and is removed again, and studio mode, the transition and the program scene are put back.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveLevelsTests(ITestOutputHelper output)
    {
        private const EventSubscriptions Subscriptions = EventSubscriptions.All | EventSubscriptions.InputVolumeMeters | EventSubscriptions.InputActiveStateChanged | EventSubscriptions.InputShowStateChanged;

        // A file of tones (8000 samples a second, 16 bits), a channel of each: a frequency and the part of the full scale that it takes.
        internal static string WriteWave(double seconds, params (double Frequency, double Amplitude)[] channels)
        {
            var path = Path.Combine(Path.GetTempPath(), $"obsclient-live-levels-{Guid.NewGuid():N}.wav");
            var samples = (int)(seconds * 8000);
            var blockAlign = (short)(2 * channels.Length);
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write("RIFF"u8.ToArray());
            writer.Write(36 + (samples * blockAlign));
            writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels.Length);
            writer.Write(8000);
            writer.Write(8000 * blockAlign);
            writer.Write(blockAlign);
            writer.Write((short)16);
            writer.Write("data"u8.ToArray());
            writer.Write(samples * blockAlign);
            for (var i = 0; i < samples; i++)
            {
                foreach (var (frequency, amplitude) in channels)
                {
                    writer.Write((short)(Math.Sin(2 * Math.PI * frequency * i / 8000) * short.MaxValue * amplitude));
                }
            }

            return path;
        }

        [LiveUuidChangesFact]
        public async Task Observation_TheLevelsOfTheAudio_InEveryWayThatTheyChange()
        {
            await using var session = await LiveSession.ConnectAsync(Subscriptions, removeLeftovers: true);
            var client = session.Client;
            var version = await client.GetVersion();
            output.WriteLine($"OBS Studio {version.ObsVersion}, obs-websocket {version.ObsWebSocketVersion}.");

            var clock = Stopwatch.StartNew();
            var names = new Dictionary<string, string>();
            var files = new List<string>();

            string LabelOf(JsonElement entry)
            {
                var uuid = entry.TryGetProperty("inputUuid", out var found) ? found.GetString() ?? string.Empty : string.Empty;
                return names.TryGetValue(uuid, out var known) ? known : (entry.TryGetProperty("inputName", out var name) ? name.GetString() ?? "?" : "?");
            }

            // The inputs of one message: the label, and the levels as OBS Studio sent them (a list of three numbers for each channel).
            List<(string Label, float[][] Levels)> InputsOf(JsonElement data) =>
                [.. data.GetProperty("inputs").EnumerateArray().Select(entry => (LabelOf(entry), entry.GetProperty("inputLevelsMul").EnumerateArray().Select(channel => channel.EnumerateArray().Select(number => number.GetSingle()).ToArray()).ToArray()))];

            string Describe(string type, JsonElement? data)
            {
                if (data is not { } value)
                {
                    return type;
                }

                var label = value.TryGetProperty("inputUuid", out _) ? LabelOf(value) : (value.TryGetProperty("sceneUuid", out var scene) && names.TryGetValue(scene.GetString() ?? string.Empty, out var sceneLabel) ? sceneLabel : string.Empty);
                return type switch
                {
                    "InputActiveStateChanged" => $"Active {label}={value.GetProperty("videoActive").GetBoolean()}",
                    "InputShowStateChanged" => $"Show {label}={value.GetProperty("videoShowing").GetBoolean()}",
                    _ => $"{type} {label}".TrimEnd(),
                };
            }

            void Summarize(int from, long elapsed, string? showFirstOf)
            {
                var all = session.RawEvents();
                var messages = all.Skip(from).Where(entry => entry.Type == "InputVolumeMeters" && entry.Data is not null).Select(entry => entry.Data!.Value).ToList();
                var parsed = messages.Select(InputsOf).ToList();
                output.WriteLine(FormattableString.Invariant($"      levels: {messages.Count} messages in {elapsed} ms ({messages.Count * 1000.0 / Math.Max(1, elapsed):N1} a second)"));
                foreach (var label in parsed.SelectMany(message => message.Select(entry => entry.Label)).Distinct().Order(StringComparer.Ordinal))
                {
                    var listed = parsed.Where(message => message.Any(entry => entry.Label == label)).ToList();
                    var entries = listed.SelectMany(message => message.Where(entry => entry.Label == label)).ToList();
                    var doubles = listed.Count(message => message.Count(entry => entry.Label == label) > 1);
                    var counts = string.Join("/", entries.Select(entry => entry.Levels.Length).Distinct().Order());
                    var parts = new List<string>();
                    for (var channel = 0; channel < entries.Max(entry => entry.Levels.Length); channel++)
                    {
                        var values = entries.Where(entry => entry.Levels.Length > channel && entry.Levels[channel].Length >= 3).Select(entry => entry.Levels[channel]).ToList();
                        if (values.Count > 0)
                        {
                            parts.Add(FormattableString.Invariant($"ch{channel} magnitude {values.Min(value => value[0]):G3}..{values.Max(value => value[0]):G3}, peak {values.Min(value => value[1]):G3}..{values.Max(value => value[1]):G3}, input peak {values.Min(value => value[2]):G3}..{values.Max(value => value[2]):G3}"));
                        }
                    }

                    output.WriteLine($"      {label}: listed in {listed.Count} of {parsed.Count}; channels {counts}{(doubles > 0 ? $"; LISTED TWICE in {doubles}" : string.Empty)}; {string.Join("; ", parts)}");
                }

                if (parsed.Count > 0)
                {
                    output.WriteLine($"      order in the first message: [{string.Join(", ", parsed[0].Select(entry => entry.Label))}]; in the last: [{string.Join(", ", parsed[^1].Select(entry => entry.Label))}]");
                }

                if (showFirstOf is not null)
                {
                    var first = messages.Select((message, index) => (Index: index, Inputs: InputsOf(message))).Where(pair => pair.Inputs.Any(entry => entry.Label == showFirstOf)).Take(3).ToList();
                    output.WriteLine($"      the first messages that list {showFirstOf} are number {string.Join(", ", first.Select(pair => pair.Index))} of the window:");
                    foreach (var pair in first)
                    {
                        output.WriteLine($"         {JsonSerializer.Serialize(pair.Inputs.Where(entry => entry.Label == showFirstOf).Select(entry => entry.Levels))}");
                    }
                }
            }

            async Task StepAsync(string what, Func<Task>? act = null, int window = 1500, string? showFirstOf = null)
            {
                var from = session.RawEvents().Count;
                var result = "ok";
                var started = clock.ElapsedMilliseconds;
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

                var answered = clock.ElapsedMilliseconds - started;
                await Task.Delay(window);
                var elapsed = clock.ElapsedMilliseconds - started;
                var others = session.RawEvents().Skip(from).Where(entry => entry.Type != "InputVolumeMeters").Select(entry => Describe(entry.Type, entry.Data));
                output.WriteLine($"{what}: {result} (answered after {answered} ms); events [{string.Join("; ", others)}]");
                Summarize(from, elapsed, showFirstOf);
            }

            var originalProgram = await client.GetCurrentProgramScene();
            var originalStudio = await client.GetStudioModeEnabled();
            var originalTransition = await client.GetCurrentSceneTransition();
            var originalDuration = originalTransition.TransitionDuration;
            try
            {
                output.WriteLine($"The inputs that exist: {string.Join(", ", (await client.GetInputList()).Select(input => $"{input.InputName} ({input.InputKind})"))}");

                var kinds = await client.GetInputKindList();
                var colorKind = kinds.Where(candidate => candidate.StartsWith("color_source", StringComparison.Ordinal)).OrderByDescending(candidate => candidate, StringComparer.Ordinal).First();
                var mono = WriteWave(3, (440, 0.5));
                var stereo = WriteWave(3, (440, 0.5), (880, 0.25));
                files.Add(mono);
                files.Add(stereo);

                async Task<SceneRef> SceneAsync(string label)
                {
                    var uuid = await client.CreateScene(LiveObs.UniqueName(label));
                    names[uuid.ToString("D")] = label.ToUpperInvariant();
                    return SceneRef.FromUuid(uuid);
                }

                async Task<(InputRef Input, Guid Uuid, int Item)> ToneAsync(SceneRef scene, string label, string file, bool enabled = true)
                {
                    var settings = new Dictionary<string, object> { ["local_file"] = file, ["is_local_file"] = true, ["looping"] = true, ["restart_on_activate"] = true, ["close_when_inactive"] = false };
                    var created = await client.CreateInput(scene, LiveObs.UniqueName(label), "ffmpeg_source", settings, sceneItemEnabled: enabled);
                    names[created.InputUuid.ToString("D")] = label;
                    return (InputRef.FromUuid(created.InputUuid), created.InputUuid, created.SceneItemId);
                }

                var a = await SceneAsync("a");
                var b = await SceneAsync("b");
                await client.SetCurrentSceneTransition("Cut");
                await Task.Delay(300);

                (InputRef Input, Guid Uuid, int Item) ta = default, tb = default, tc = default, tf = default;
                await StepAsync(
                    "00 the inputs: in A a mono tone (ta), a stereo tone (tb: 440 Hz and 880 Hz at half of it), a mono tone (tc), a mono tone with its scene item hidden (tf) and a color source without audio (ce); in B a mono tone (td); the program scene is neither",
                    async () =>
                    {
                        ta = await ToneAsync(a, "ta", mono);
                        tb = await ToneAsync(a, "tb", stereo);
                        tc = await ToneAsync(a, "tc", mono);
                        tf = await ToneAsync(a, "tf", mono, enabled: false);
                        var created = await client.CreateInput(a, LiveObs.UniqueName("ce"), colorKind, new Dictionary<string, object> { ["color"] = 4278190335L, ["width"] = 320, ["height"] = 180 });
                        names[created.InputUuid.ToString("D")] = "ce";
                        await ToneAsync(b, "td", mono);
                    });
                await StepAsync("01 program to A (a cut): the inputs of A go on the air (the first messages that list ta)", () => client.SetCurrentProgramScene(a), showFirstOf: "ta");
                await StepAsync("02 tc is muted", () => client.SetInputMute(tc.Input, true));
                await StepAsync("03 tc is not muted any more, and the volume of ta is -6 dB", async () =>
                {
                    await client.SetInputMute(tc.Input, false);
                    await client.SetInputVolume(ta.Input, inputVolumeDb: -6f);
                });
                await StepAsync("04 the volume of ta is 0 (minus 100 dB), and the volume of tb is 6 dB", async () =>
                {
                    await client.SetInputVolume(ta.Input, inputVolumeDb: -100f);
                    await client.SetInputVolume(tb.Input, inputVolumeDb: 6f);
                });
                await StepAsync("05 the volume of ta and tb is 0 dB again", async () =>
                {
                    await client.SetInputVolume(ta.Input, inputVolumeDb: 0f);
                    await client.SetInputVolume(tb.Input, inputVolumeDb: 0f);
                });
                await StepAsync("06 ta is paused", () => client.TriggerMediaInputAction(ta.Input, ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PAUSE), window: 2000);
                await StepAsync("07 ta plays again, and tb is stopped", async () =>
                {
                    await client.TriggerMediaInputAction(ta.Input, ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_PLAY);
                    await client.TriggerMediaInputAction(tb.Input, ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_STOP);
                }, window: 2000);
                await StepAsync("08 tb plays again (restart)", () => client.TriggerMediaInputAction(tb.Input, ObsMediaInputAction.OBS_WEBSOCKET_MEDIA_INPUT_ACTION_RESTART));
                await StepAsync("09 program to B (a cut): the inputs of A go off the air, td goes on it", () => client.SetCurrentProgramScene(b));
                await StepAsync("10 studio mode on, and the preview scene is A (it shows the inputs of A, which are not on the air)", async () =>
                {
                    await client.SetStudioModeEnabled(true);
                    await Task.Delay(500);
                    await client.SetCurrentPreviewScene(a);
                });
                await StepAsync("11 studio mode off", () => client.SetStudioModeEnabled(false));
                await StepAsync("12 program to A (a cut)", () => client.SetCurrentProgramScene(a));
                await StepAsync("13 the scene item of tb in A is hidden", () => client.SetSceneItemEnabled(a, tb.Item, false));
                await StepAsync("14 the scene item of tb in A is shown", () => client.SetSceneItemEnabled(a, tb.Item, true));
                await StepAsync("15 tc is removed", () => client.RemoveInput(tc.Input));
                names[tc.Uuid.ToString("D")] = "(tc, removed)";
                await StepAsync("16 a new tone tg is made in A, which is on the air (the first messages that list tg)", async () => await ToneAsync(a, "tg", mono), showFirstOf: "tg");
                await StepAsync("17 the scene item of tf in A is shown (it was hidden)", () => client.SetSceneItemEnabled(a, tf.Item, true), showFirstOf: "tf");
                await client.SetCurrentSceneTransition("Fade");
                await client.SetCurrentSceneTransitionDuration(600f);
                await Task.Delay(300);
                await StepAsync("18 program to B (a fade of 600 ms): both scenes are on the air while it runs", () => client.SetCurrentProgramScene(b), window: 2000);
                await StepAsync("19 program to A (a fade of 600 ms)", () => client.SetCurrentProgramScene(a), window: 2000);
            }
            finally
            {
                try
                {
                    if (await client.GetStudioModeEnabled() != originalStudio)
                    {
                        await client.SetStudioModeEnabled(originalStudio);
                    }

                    await client.SetCurrentSceneTransition(originalTransition.TransitionName);
                    if (originalDuration is { } duration)
                    {
                        await client.SetCurrentSceneTransitionDuration((float)duration);
                    }

                    await client.SetCurrentProgramScene(originalProgram.SceneUuid);
                    await Task.Delay(500);
                }
                catch (ObsResponseException exception)
                {
                    output.WriteLine($"Could not put back what the test changed: {exception.Message}");
                }

                await session.RemoveLeftoversAsync();
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
        }
    }
}
