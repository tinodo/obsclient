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
    /// The geometry of scene items (the transform and the blend mode), duplicating a scene item, whether a source is on the air, and the levels of the audio inputs against a real OBS Studio: when the events
    /// come, what they carry, and what the requests refuse with. The model keeps these (or lets the application ask for them), and these check against the real thing what it assumes where the stand-in of
    /// the unit tests only had what was read in the source of obs-websocket. Everything that is made carries the name prefix of <see cref="LiveObs.NamePrefix"/> and is removed again, and studio mode and the
    /// program scene are put back.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveGeometryTests(ITestOutputHelper output)
    {
        private const EventSubscriptions HighVolume = EventSubscriptions.InputVolumeMeters | EventSubscriptions.InputActiveStateChanged | EventSubscriptions.InputShowStateChanged | EventSubscriptions.SceneItemTransformChanged;

        private static string Json(object value) => JsonSerializer.Serialize(value);

        // A file of a tone of 440 Hz (8000 samples a second, 16 bits, one channel), at half the loudness.
        private static string WriteTone(double seconds)
        {
            var path = Path.Combine(Path.GetTempPath(), $"obsclient-live-tone-{Guid.NewGuid():N}.wav");
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
            for (var i = 0; i < samples; i++)
            {
                writer.Write((short)(Math.Sin(2 * Math.PI * 440 * i / 8000) * short.MaxValue / 2));
            }

            return path;
        }

        [LiveUuidChangesFact]
        public async Task Observation_TransformsBlendModesDuplicatesActivityAndLevels()
        {
            await using var session = await LiveSession.ConnectAsync(EventSubscriptions.All | HighVolume, removeLeftovers: true);
            var client = session.Client;
            var version = await client.GetVersion();
            output.WriteLine($"OBS Studio {version.ObsVersion}, obs-websocket {version.ObsWebSocketVersion}.");

            var seen = session.RawEvents().Count;
            var clock = Stopwatch.StartNew();

            // The events that OBS Studio sent since the last call, but for the levels, which are only counted. A change of a transform is told by what is different from the one before it.
            var transforms = new Dictionary<(string Scene, int Item), Dictionary<string, string>>();

            static string Number(JsonElement value) => value.ValueKind == JsonValueKind.Number ? value.GetDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture) : value.GetRawText();

            string Describe(string type, JsonElement? data)
            {
                if (type == "SceneItemTransformChanged" && data is { } changed)
                {
                    var key = (changed.GetProperty("sceneUuid").GetString()!, changed.GetProperty("sceneItemId").GetInt32());
                    var now = changed.GetProperty("sceneItemTransform").EnumerateObject().ToDictionary(property => property.Name, property => Number(property.Value));
                    var text = transforms.TryGetValue(key, out var old)
                        ? string.Join(", ", now.Where(pair => !old.TryGetValue(pair.Key, out var was) || was != pair.Value).Select(pair => $"{pair.Key} {old.GetValueOrDefault(pair.Key) ?? "-"} -> {pair.Value}"))
                        : "first seen: " + string.Join(", ", now.Select(pair => $"{pair.Key} {pair.Value}"));
                    transforms[key] = now;
                    return $"TransformChanged item {key.Item2} [{(text.Length == 0 ? "no difference" : text)}]";
                }

                return data is { } other ? $"{type} {other.GetRawText()}" : type;
            }

            string Events()
            {
                var all = session.RawEvents();
                var fresh = all.Skip(seen).ToList();
                seen = all.Count;
                var levels = fresh.Count(entry => entry.Type == "InputVolumeMeters");
                var text = string.Join("; ", fresh.Where(entry => entry.Type != "InputVolumeMeters").Select(entry => Describe(entry.Type, entry.Data)));
                return levels > 0 ? $"{text} (+{levels} level events)" : text;
            }

            async Task StepAsync(string what, Func<Task>? act = null, int wait = 400)
            {
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
                await Task.Delay(wait);
                output.WriteLine($"{what}: {result} (answered after {answered} ms); events [{Events()}]");
            }

            async Task<string> ActiveAsync(params (string Label, SourceRef Source)[] sources)
            {
                var parts = new List<string>();
                foreach (var (label, source) in sources)
                {
                    try
                    {
                        var state = await client.GetSourceActive(source);
                        parts.Add($"{label}: active {state.VideoActive}, showing {state.VideoShowing}");
                    }
                    catch (ObsResponseException exception)
                    {
                        parts.Add($"{label}: refused {(int)exception.ErrorCode}");
                    }
                }

                return string.Join("; ", parts);
            }

            var originalProgram = await client.GetCurrentProgramScene();
            var originalStudio = await client.GetStudioModeEnabled();
            var sceneAName = LiveObs.UniqueName("ga");
            var sceneBName = LiveObs.UniqueName("gb");
            var colorName = LiveObs.UniqueName("color");
            var inputNames = new List<string>();
            string? tone = null;
            try
            {
                var kinds = await client.GetInputKindList();
                var kind = kinds.Where(candidate => candidate.StartsWith("color_source", StringComparison.Ordinal)).OrderByDescending(candidate => candidate, StringComparer.Ordinal).First();
                var a = SceneRef.FromUuid(await client.CreateScene(sceneAName));
                var b = SceneRef.FromUuid(await client.CreateScene(sceneBName));
                await client.SetCurrentProgramScene(a);
                await Task.Delay(800);
                seen = session.RawEvents().Count;

                await StepAsync("00 creating a color input in A (the events of a new scene item)", async () =>
                {
                    var created = await client.CreateInput(a, colorName, kind, new Dictionary<string, object> { ["color"] = 4278190335L, ["width"] = 640, ["height"] = 360 }, sceneItemEnabled: true);
                    inputNames.Add(colorName);
                    output.WriteLine($"   created: item {created.SceneItemId}, input {created.InputUuid}");
                });
                var items = await client.GetSceneItemList(a);
                var item = items.Single();
                var itemId = item.SceneItemId;
                var input = InputRef.FromUuid(item.SourceUuid!.Value);
                output.WriteLine($"   the list says: transform {Json(item.SceneItemTransform)}, blend mode {item.SceneItemBlendMode}, enabled {item.SceneItemEnabled}, locked {item.SceneItemLocked}");

                // The transform.
                output.WriteLine($"01 the transform of the item: {Json(await client.GetSceneItemTransform(a, itemId))}");
                await StepAsync("02 position X 100", () => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { PositionX = 100f }));
                await StepAsync("03 position X 100 again (nothing changes)", () => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { PositionX = 100f }));
                await StepAsync("04 an update without members", () => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate()));
                await StepAsync("05 scale 2 and rotation 370", () => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { ScaleX = 2f, ScaleY = 2f, Rotation = 370f }));
                output.WriteLine($"   after: {Json(await client.GetSceneItemTransform(a, itemId))}");
                await StepAsync("06 bounds: stretch, 300 by 200", () => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { BoundsType = Bounds.OBS_BOUNDS_STRETCH, BoundsWidth = 300f, BoundsHeight = 200f }));
                output.WriteLine($"   after: {Json(await client.GetSceneItemTransform(a, itemId))}");
                await StepAsync("07 bounds width 0", () => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { BoundsWidth = 0f }));
                await StepAsync("08 crop left -5", () => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { CropLeft = -5 }));
                output.WriteLine($"   after: {Json(await client.GetSceneItemTransform(a, itemId))}");
                await StepAsync("09 crop left 10, top 3, crop to bounds", () => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { CropLeft = 10, CropTop = 3, CropToBounds = true }));
                output.WriteLine($"   after: {Json(await client.GetSceneItemTransform(a, itemId))}");
                await StepAsync("10 alignment 1000", () => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { Alignment = 1000 }));
                output.WriteLine($"   after: {Json(await client.GetSceneItemTransform(a, itemId))}");
                await StepAsync("11 back to alignment 5, no bounds", () => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { Alignment = 5, BoundsType = Bounds.OBS_BOUNDS_NONE }));
                await StepAsync("12 the size of the source changes (200 by 100)", () => client.SetInputSettings(input, new Dictionary<string, object> { ["width"] = 200, ["height"] = 100 }, overlay: true), wait: 1000);
                output.WriteLine($"   after: {Json(await client.GetSceneItemTransform(a, itemId))}");
                await StepAsync("13 the item is of a scene that does not exist", () => client.SetSceneItemTransform(SceneRef.FromName("obsclient-live-not-a-scene"), 1, new SceneItemTransformUpdate { PositionX = 1f }), wait: 100);
                await StepAsync("14 an item that does not exist", () => client.SetSceneItemTransform(a, 9999, new SceneItemTransformUpdate { PositionX = 1f }), wait: 100);
                await StepAsync("15 get the transform of an item that does not exist", async () => await client.GetSceneItemTransform(a, 9999), wait: 100);

                // How many events a burst of changes gives, one after the other and all at once.
                var burst = Stopwatch.StartNew();
                for (var i = 0; i < 50; i++)
                {
                    await client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { PositionX = 1000f + i });
                }

                var sequentialMilliseconds = burst.ElapsedMilliseconds;
                await Task.Delay(800);
                var burstEvents = session.RawEvents().Skip(seen).Where(entry => entry.Type == "SceneItemTransformChanged").Select(entry => entry.Data!.Value.GetProperty("sceneItemTransform").GetProperty("positionX").GetSingle()).ToList();
                seen = session.RawEvents().Count;
                output.WriteLine($"16 50 changes one after the other took {sequentialMilliseconds} ms and gave {burstEvents.Count} transform events; the last positionX is {burstEvents.LastOrDefault()}; in order: {burstEvents.Zip(burstEvents.Skip(1), (x, y) => y >= x).All(ordered => ordered)}");
                burst.Restart();
                await Task.WhenAll(Enumerable.Range(0, 50).Select(i => client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { PositionY = 2000f + i })));
                var parallelMilliseconds = burst.ElapsedMilliseconds;
                await Task.Delay(800);
                var parallelEvents = session.RawEvents().Skip(seen).Where(entry => entry.Type == "SceneItemTransformChanged").Select(entry => entry.Data!.Value.GetProperty("sceneItemTransform").GetProperty("positionY").GetSingle()).ToList();
                seen = session.RawEvents().Count;
                output.WriteLine($"17 50 changes all at once took {parallelMilliseconds} ms and gave {parallelEvents.Count} transform events; the last positionY is {parallelEvents.LastOrDefault()}; OBS Studio holds {(await client.GetSceneItemTransform(a, itemId)).PositionY}");
                output.WriteLine($"   the order in which they came: {string.Join(" ", parallelEvents.Select(value => (value - 2000f).ToString("0")))}");

                // In which order a request is answered and its events come.
                var mark = session.Log.Entries.Count;
                await client.SetSceneItemTransform(a, itemId, new SceneItemTransformUpdate { PositionX = 3000f });
                await Task.Delay(300);
                var sequence = session.RawMessages(mark).Where(message => message.Op is 5 or 7).Select(message => message.Op == 7 ? "response " + message.Data.GetProperty("requestType").GetString() : "event " + message.Data.GetProperty("eventType").GetString()).Where(text => !text.EndsWith("InputVolumeMeters", StringComparison.Ordinal));
                output.WriteLine($"17b the order of an answer and its event: {string.Join(" -> ", sequence)}");
                _ = Events();

                // The blend mode.
                output.WriteLine($"18 the blend mode: {await client.GetSceneItemBlendMode(a, itemId)}");
                foreach (var mode in Enum.GetValues<BlendMode>())
                {
                    await StepAsync($"19 blend mode {mode}", () => client.SetSceneItemBlendMode(a, itemId, mode), wait: 150);
                    output.WriteLine($"   asked: {await client.GetSceneItemBlendMode(a, itemId)}; the list says {(await client.GetSceneItemList(a)).Single().SceneItemBlendMode}");
                }

                await StepAsync("20 blend mode of an item that does not exist", () => client.SetSceneItemBlendMode(a, 9999, BlendMode.OBS_BLEND_NORMAL), wait: 100);

                // Duplicating.
                await StepAsync("21 enabled false and locked true, so that the copy shows what is copied", async () =>
                {
                    await client.SetSceneItemEnabled(a, itemId, false);
                    await client.SetSceneItemLocked(a, itemId, true);
                });
                var copyId = 0;
                await StepAsync("22 duplicate the item into the same scene", async () => copyId = await client.DuplicateSceneItem(a, itemId));
                output.WriteLine($"   the copy is item {copyId}; the list: {Json((await client.GetSceneItemList(a)).Select(entry => new { entry.SceneItemId, entry.SceneItemIndex, entry.SceneItemEnabled, entry.SceneItemLocked, Blend = entry.SceneItemBlendMode.ToString(), entry.SourceName, entry.SceneItemTransform.PositionX, entry.SceneItemTransform.PositionY, entry.SceneItemTransform.ScaleX, entry.SceneItemTransform.CropLeft }))}");
                var otherId = 0;
                await StepAsync("23 duplicate the item into scene B (by its UUID)", async () => otherId = await client.DuplicateSceneItem(a, itemId, b));
                output.WriteLine($"   the copy is item {otherId}; the list of A: {string.Join(", ", (await client.GetSceneItemList(a)).Select(entry => entry.SceneItemId))}; the list of B: {string.Join(", ", (await client.GetSceneItemList(b)).Select(entry => entry.SceneItemId))}");
                var byName = 0;
                await StepAsync("23b duplicate the item into scene B (by its name)", async () => byName = await client.DuplicateSceneItem(a, itemId, SceneRef.FromName(sceneBName)));
                output.WriteLine($"   the copy is item {byName}; the list of A: {string.Join(", ", (await client.GetSceneItemList(a)).Select(entry => entry.SceneItemId))}; the list of B: {Json((await client.GetSceneItemList(b)).Select(entry => new { entry.SceneItemId, entry.SceneItemIndex, entry.SceneItemEnabled, entry.SceneItemLocked, Blend = entry.SceneItemBlendMode.ToString(), entry.SourceName }))}");
                await StepAsync("24 duplicate an item that does not exist", async () => await client.DuplicateSceneItem(a, 9999), wait: 100);
                await StepAsync("25 duplicate into a scene that does not exist", async () => await client.DuplicateSceneItem(a, itemId, SceneRef.FromName("obsclient-live-not-a-scene")), wait: 100);
                await StepAsync("26 enabled true, unlocked again", async () =>
                {
                    await client.SetSceneItemLocked(a, itemId, false);
                    await client.SetSceneItemEnabled(a, itemId, true);
                });

                // Whether a source is on the air.
                output.WriteLine($"27 program is A: {await ActiveAsync(("input", input), ("scene A", a), ("scene B", b))}");
                await StepAsync("28 program to B (a fade of 300 ms)", () => client.SetCurrentProgramScene(b), wait: 1000);
                output.WriteLine($"   {await ActiveAsync(("input", input), ("scene A", a), ("scene B", b))}");
                await StepAsync("29 program to A", () => client.SetCurrentProgramScene(a), wait: 1000);
                await StepAsync("30 the item is hidden", () => client.SetSceneItemEnabled(a, itemId, false), wait: 600);
                output.WriteLine($"   {await ActiveAsync(("input", input))}");
                await StepAsync("31 the item is shown", () => client.SetSceneItemEnabled(a, itemId, true), wait: 600);
                await StepAsync("32 studio mode on", () => client.SetStudioModeEnabled(true), wait: 800);
                await StepAsync("33 the preview scene is B", () => client.SetCurrentPreviewScene(b), wait: 800);
                output.WriteLine($"   {await ActiveAsync(("input", input), ("scene A", a), ("scene B", b))}");
                await StepAsync("34 studio mode off", () => client.SetStudioModeEnabled(false), wait: 800);
                output.WriteLine($"   {await ActiveAsync(("input", input), ("scene A", a), ("scene B", b))}");
                await StepAsync("35 the active state of a source that does not exist", async () => await client.GetSourceActive(SourceRef.FromName("obsclient-live-not-a-source")), wait: 100);

                // The same input in two scenes: it is on the air as long as one of them is.
                await StepAsync("36 the input is added to scene B as well", async () => await client.CreateSceneItem(b, input));
                await StepAsync("37 program to B (the input is in both scenes)", () => client.SetCurrentProgramScene(b), wait: 1000);
                output.WriteLine($"   {await ActiveAsync(("input", input))}");
                await StepAsync("38 program to A", () => client.SetCurrentProgramScene(a), wait: 1000);

                // Removing scene items.
                await StepAsync("39 remove the copy of the item (item 2)", () => client.RemoveSceneItem(a, copyId), wait: 600);
                output.WriteLine($"   the list of A: {string.Join(", ", (await client.GetSceneItemList(a)).Select(entry => $"{entry.SceneItemId}@{entry.SceneItemIndex}"))}");

                // The levels of an input that has sound: a tone that plays again and again.
                var toneFile = WriteTone(2);
                tone = toneFile;
                var toneName = LiveObs.UniqueName("tone");
                inputNames.Add(toneName);
                await StepAsync("40 an input that plays a tone", async () =>
                {
                    await client.CreateInput(a, toneName, "ffmpeg_source", new Dictionary<string, object> { ["local_file"] = toneFile, ["is_local_file"] = true, ["looping"] = true, ["restart_on_activate"] = true, ["close_when_inactive"] = false }, sceneItemEnabled: true);
                }, wait: 1500);
                _ = Events();
                var levelsClock = Stopwatch.StartNew();
                await Task.Delay(3000);
                var levels = session.RawEvents().Skip(seen).Where(entry => entry.Type == "InputVolumeMeters").ToList();
                seen = session.RawEvents().Count;
                output.WriteLine($"41 the levels: {levels.Count} events in {levelsClock.ElapsedMilliseconds} ms ({levels.Count * 1000.0 / levelsClock.ElapsedMilliseconds:N1} a second)");
                foreach (var entry in levels.Take(3).Concat(levels.TakeLast(2)))
                {
                    output.WriteLine($"   {entry.Data!.Value.GetRawText()}");
                }

                await StepAsync("42 program to B (the tone is not in it)", () => client.SetCurrentProgramScene(b), wait: 1000);
                levels = session.RawEvents().Skip(seen).Where(entry => entry.Type == "InputVolumeMeters").ToList();
                seen = session.RawEvents().Count;
                output.WriteLine($"   the levels now: {levels.Count} events; the last {levels.LastOrDefault().Data?.GetRawText()}");
                await client.SetCurrentProgramScene(a);
            }
            finally
            {
                try
                {
                    if (await client.GetStudioModeEnabled() != originalStudio)
                    {
                        await client.SetStudioModeEnabled(originalStudio);
                    }

                    await client.SetCurrentProgramScene(originalProgram.SceneUuid);
                    await Task.Delay(500);
                }
                catch (ObsResponseException exception)
                {
                    output.WriteLine($"Could not put back what the test changed: {exception.Message}");
                }

                await session.RemoveLeftoversAsync();
                if (tone is not null)
                {
                    // OBS Studio lets go of the file a moment after the input is removed.
                    for (var attempt = 0; attempt < 10; attempt++)
                    {
                        try
                        {
                            File.Delete(tone);
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
