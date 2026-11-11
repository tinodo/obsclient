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
    /// Whether a source is on the air or shown, and the events that say so (<c>InputActiveStateChanged</c> and <c>InputShowStateChanged</c>), against a real OBS Studio: when they come and in which order, for which
    /// sources (inputs only, or scenes too), and what <c>GetSourceActive</c> says, for every way in which it changes: the program scene, a scene in a scene, a scene item that is hidden or removed, studio mode and
    /// the preview scene, a transition that runs, an input or a scene that is removed. It prints what it sees, and checks nothing: what the simulation does is what this printed. Everything that is made carries the
    /// name prefix of <see cref="LiveObs.NamePrefix"/> and is removed again, and studio mode, the transition and the program scene are put back.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveActivityTests(ITestOutputHelper output)
    {
        private const EventSubscriptions HighVolume = EventSubscriptions.InputVolumeMeters | EventSubscriptions.InputActiveStateChanged | EventSubscriptions.InputShowStateChanged | EventSubscriptions.SceneItemTransformChanged;

        [LiveUuidChangesFact]
        public async Task Observation_TheActivityOfSources_InEveryWayThatItChanges()
        {
            await using var session = await LiveSession.ConnectAsync(EventSubscriptions.All | HighVolume, removeLeftovers: true);
            var client = session.Client;
            var version = await client.GetVersion();
            output.WriteLine($"OBS Studio {version.ObsVersion}, obs-websocket {version.ObsWebSocketVersion}.");

            var seen = session.RawEvents().Count;
            var clock = Stopwatch.StartNew();
            var names = new Dictionary<string, string>();

            // What happened since the last call, as the names of the test say it: the activity of the inputs, and what else is about the sources and the scenes that it follows.
            string Events()
            {
                var all = session.RawEvents();
                var fresh = all.Skip(seen).Where(entry => entry.Type != "InputVolumeMeters" && entry.Type != "SceneItemTransformChanged").ToList();
                seen = all.Count;
                return string.Join("; ", fresh.Select(entry => Describe(entry.Type, entry.Data)));
            }

            string Describe(string type, JsonElement? data)
            {
                if (data is not { } value)
                {
                    return type;
                }

                string Name(string property) => value.TryGetProperty(property, out var uuid) && names.TryGetValue(uuid.GetString() ?? string.Empty, out var name) ? name : (value.TryGetProperty(property.Replace("Uuid", "Name", StringComparison.Ordinal), out var raw) ? raw.GetString() ?? "?" : "?");
                return type switch
                {
                    "InputShowStateChanged" => $"ShowState {Name("inputUuid")} showing={value.GetProperty("videoShowing").GetBoolean()}",
                    "InputActiveStateChanged" => $"ActiveState {Name("inputUuid")} active={value.GetProperty("videoActive").GetBoolean()}",
                    "CurrentProgramSceneChanged" or "CurrentPreviewSceneChanged" => $"{type} {Name("sceneUuid")}",
                    "SceneItemEnableStateChanged" => $"ItemEnabled {Name("sceneUuid")}#{value.GetProperty("sceneItemId").GetInt32()}={value.GetProperty("sceneItemEnabled").GetBoolean()}",
                    "SceneItemCreated" or "SceneItemRemoved" => $"{type} {Name("sceneUuid")}#{value.GetProperty("sceneItemId").GetInt32()} source {Name("sourceUuid")}",
                    "InputCreated" or "InputRemoved" => $"{type} {Name("inputUuid")}",
                    "SceneCreated" or "SceneRemoved" => $"{type} {Name("sceneUuid")}",
                    "SceneTransitionStarted" or "SceneTransitionEnded" or "SceneTransitionVideoEnded" => type,
                    _ => type,
                };
            }

            async Task<string> StateAsync(params string[] labels)
            {
                var parts = new List<string>();
                foreach (var label in labels)
                {
                    var uuid = names.First(pair => pair.Value == label).Key;
                    try
                    {
                        var state = await client.GetSourceActive(SourceRef.FromUuid(Guid.Parse(uuid)));
                        parts.Add($"{label} {(state.VideoActive ? "ACTIVE" : "-")}/{(state.VideoShowing ? "SHOWING" : "-")}");
                    }
                    catch (ObsResponseException exception)
                    {
                        parts.Add($"{label} refused {(int)exception.ErrorCode}");
                    }
                }

                return string.Join(", ", parts);
            }

            async Task StepAsync(string what, Func<Task>? act = null, int wait = 600)
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
                output.WriteLine($"      state: {await StateAsync([.. names.Values.Where(name => !name.StartsWith("(", StringComparison.Ordinal))])}");
            }

            var originalProgram = await client.GetCurrentProgramScene();
            var originalStudio = await client.GetStudioModeEnabled();
            var originalTransition = await client.GetCurrentSceneTransition();
            var originalDuration = originalTransition.TransitionDuration;
            try
            {
                var kinds = await client.GetInputKindList();
                var kind = kinds.Where(candidate => candidate.StartsWith("color_source", StringComparison.Ordinal)).OrderByDescending(candidate => candidate, StringComparer.Ordinal).First();
                var settings = new Dictionary<string, object> { ["color"] = 4278190335L, ["width"] = 320, ["height"] = 180 };

                async Task<(SceneRef Scene, Guid Uuid)> SceneAsync(string label)
                {
                    var uuid = await client.CreateScene(LiveObs.UniqueName(label));
                    names[uuid.ToString("D")] = label.ToUpperInvariant();
                    return (SceneRef.FromUuid(uuid), uuid);
                }

                async Task<(InputRef Input, Guid Uuid, int Item)> InputAsync(SceneRef scene, string label, bool enabled = true)
                {
                    var created = await client.CreateInput(scene, LiveObs.UniqueName(label), kind, settings, sceneItemEnabled: enabled);
                    names[created.InputUuid.ToString("D")] = label;
                    return (InputRef.FromUuid(created.InputUuid), created.InputUuid, created.SceneItemId);
                }

                // The scenes: A shows ia, B shows ib, C shows ic and the scene A. The program scene is a scene of the user, and a cut puts a scene on the air at once.
                var (a, aUuid) = await SceneAsync("a");
                var (b, bUuid) = await SceneAsync("b");
                var (c, cUuid) = await SceneAsync("c");
                await client.SetCurrentSceneTransition("Cut");
                await Task.Delay(300);
                seen = session.RawEvents().Count;

                (InputRef Input, Guid Uuid, int Item) ia = default, ib = default, ic = default;
                int nested = 0;
                await StepAsync("00 inputs in the scenes (the program scene is none of them)", async () =>
                {
                    ia = await InputAsync(a, "ia");
                    ib = await InputAsync(b, "ib");
                    ic = await InputAsync(c, "ic");
                    nested = await client.CreateSceneItem(c, SourceRef.FromUuid(aUuid));
                });
                await StepAsync("01 program to A (a cut)", () => client.SetCurrentProgramScene(a));
                await StepAsync("02 program to B (a cut)", () => client.SetCurrentProgramScene(b));
                await StepAsync("03 program to C (a cut): it shows ic and the scene A, which shows ia", () => client.SetCurrentProgramScene(c));
                await StepAsync("04 the scene item of A in C is hidden", () => client.SetSceneItemEnabled(c, nested, false));
                await StepAsync("05 the scene item of A in C is shown", () => client.SetSceneItemEnabled(c, nested, true));
                await StepAsync("06 program to A (a cut)", () => client.SetCurrentProgramScene(a));
                await StepAsync("07 the scene item of ia in A is hidden", () => client.SetSceneItemEnabled(a, ia.Item, false));
                await StepAsync("08 the scene item of ia in A is shown", () => client.SetSceneItemEnabled(a, ia.Item, true));

                // Inputs and scene items that come and go in the scene that is on the air.
                (InputRef Input, Guid Uuid, int Item) id = default;
                int ibInA = 0;
                await StepAsync("09 a new input id is made in the scene that is on the air (A)", async () => id = await InputAsync(a, "id"));
                await StepAsync("10 ib (alive in B) is added to the scene that is on the air (A)", async () => ibInA = await client.CreateSceneItem(a, SourceRef.FromUuid(ib.Uuid)));
                await StepAsync("11 the scene item of ib in A is removed (ib stays alive in B)", () => client.RemoveSceneItem(a, ibInA));
                await StepAsync("12 ib is added to A again", async () => ibInA = await client.CreateSceneItem(a, SourceRef.FromUuid(ib.Uuid)));

                // Studio mode: the program scene A shows ia, id and ib; B shows ib; C shows ic and the scene A.
                await StepAsync("13 studio mode on (the program scene A is the preview scene as well)", () => client.SetStudioModeEnabled(true), wait: 800);
                await StepAsync("14 the preview scene is B (it shows ib, which the program scene shows too)", () => client.SetCurrentPreviewScene(b), wait: 800);
                await StepAsync("15 the preview scene is C (it shows ic and the scene A)", () => client.SetCurrentPreviewScene(c), wait: 800);
                await StepAsync("16 the scene item of ia in the program scene A is hidden", () => client.SetSceneItemEnabled(a, ia.Item, false), wait: 800);
                await StepAsync("17 the scene item of ia in the program scene A is shown", () => client.SetSceneItemEnabled(a, ia.Item, true), wait: 800);
                await StepAsync("18 the preview scene is B again, and the transition button (a cut): B goes on the air, A is the preview scene", async () =>
                {
                    await client.SetCurrentPreviewScene(b);
                    await Task.Delay(300);
                    await client.TriggerStudioModeTransition();
                }, wait: 1000);
                await StepAsync("19 the preview scene is C", () => client.SetCurrentPreviewScene(c), wait: 800);
                await StepAsync("20 studio mode off", () => client.SetStudioModeEnabled(false), wait: 800);

                // Studio mode with the program scene as the preview scene as well: OBS Studio puts a copy of the program scene on the air (what the simulation assumes), so what is done to the scene itself is not done to
                // what is on the air, and the preview shows the scene itself.
                (InputRef Input, Guid Uuid, int Item) iy = default, iz = default;
                await StepAsync("20-0 program to A (a cut)", () => client.SetCurrentProgramScene(a));
                await StepAsync("20a a new input iy is made in A, hidden (the program scene; studio mode is off)", async () => iy = await InputAsync(a, "iy", enabled: false));
                await StepAsync("20b studio mode on (A is the program scene and the preview scene)", () => client.SetStudioModeEnabled(true), wait: 800);
                await StepAsync("20c the scene item of ia in A is hidden (a copy of A is on the air)", () => client.SetSceneItemEnabled(a, ia.Item, false), wait: 800);
                await StepAsync("20d the scene item of iy in A is shown (it was hidden when the copy was made)", () => client.SetSceneItemEnabled(a, iy.Item, true), wait: 800);
                await StepAsync("20e a new input iz is made in A (the copy does not have it)", async () => iz = await InputAsync(a, "iz"), wait: 800);
                await StepAsync("20f the scene item of ia in A is shown again", () => client.SetSceneItemEnabled(a, ia.Item, true), wait: 800);
                await StepAsync("20g the preview scene is B, and the scene item of iy in A is hidden again", async () =>
                {
                    await client.SetCurrentPreviewScene(b);
                    await Task.Delay(300);
                    await client.SetSceneItemEnabled(a, iy.Item, false);
                }, wait: 800);
                await StepAsync("20h studio mode off (A is on the air itself again)", () => client.SetStudioModeEnabled(false), wait: 800);
                await StepAsync("20i the scene item of iy in A is shown, and the scene item of iz in A is removed", async () =>
                {
                    await client.SetSceneItemEnabled(a, iy.Item, true);
                    await client.RemoveSceneItem(a, iz.Item);
                }, wait: 800);
                await StepAsync("20j program to B (a cut)", () => client.SetCurrentProgramScene(b));

                // A fade: both scenes are on the air while it runs.
                await client.SetCurrentSceneTransition("Fade");
                await client.SetCurrentSceneTransitionDuration(600f);
                await Task.Delay(300);
                seen = session.RawEvents().Count;
                await StepAsync("21 program to A (a fade of 600 ms), looked at while it runs", async () =>
                {
                    await client.SetCurrentProgramScene(a);
                    await Task.Delay(250);
                    output.WriteLine($"      while the fade runs: {await StateAsync("ia", "id", "ib", "A", "B")}; events so far [{Events()}]");
                }, wait: 1200);
                await StepAsync("22 program to C (a fade of 600 ms)", () => client.SetCurrentProgramScene(c), wait: 1500);

                // Removal.
                await client.SetCurrentSceneTransition("Cut");
                await Task.Delay(300);
                await StepAsync("23 program to A (a cut) before the removals", () => client.SetCurrentProgramScene(a));
                await StepAsync("24 the scene item of ib in A is removed while it is on the air", () => client.RemoveSceneItem(a, ibInA));
                await StepAsync("25 ia is removed (it is on the air in A, and in no other scene)", () => client.RemoveInput(ia.Input), wait: 800);
                names[ia.Uuid.ToString("D")] = "(ia, removed)";
                await StepAsync("26 the scene C is removed while it is not on the air (it shows A and ic)", () => client.RemoveScene(c), wait: 800);
                names[cUuid.ToString("D")] = "(C, removed)";
                names[ic.Uuid.ToString("D")] = "(ic, removed)";
                await StepAsync("27 the scene A is removed while it is the program scene", () => client.RemoveScene(a), wait: 1200);
                names[aUuid.ToString("D")] = "(A, removed)";
                names[id.Uuid.ToString("D")] = "(id, removed)";
                output.WriteLine($"      the program scene is now {(await client.GetCurrentProgramScene()).SceneName}");
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
            }
        }
    }
}
