namespace OBSStudioClient.Tests.Live
{
    using System.Diagnostics;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// Studio mode, the preview scene and the transitions against a real OBS Studio: when the events come, what the requests refuse with, and what a transition that takes a while does to the program
    /// scene. The model keeps these, and these check against the real thing what it assumes where the stand-in of the unit tests only had what was read in the source of obs-websocket: the observations say
    /// what OBS Studio does, and the tests of the model check that the model follows it and that its calls end as they say. Everything that is made carries the name prefix of
    /// <see cref="LiveObs.NamePrefix"/> and is removed again, and studio mode, the transition and its duration are put back.
    /// </summary>
    /// <remarks>
    /// Nothing here removes the program scene while studio mode is on: OBS Studio 33.0.0 then stops answering the requests that need its window for seconds (up to half a minute was seen), and everything
    /// that waits for it fails for it.
    /// </remarks>
    [Collection("Live OBS")]
    public class LiveTransitionTests(ITestOutputHelper output)
    {
        private const string CutKind = "cut_transition";
        private const string FadeKind = "fade_transition";

        // OBS Studio can stop answering the requests that need its window for seconds (23 were seen on a fresh copy), so the waits of these tests are long.
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

        private static async Task TryAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (ObsResponseException)
            {
                // Already gone, or not possible any more: what is wanted is that it is as it was.
            }
        }

        // What the tests of the model change and put back.
        private sealed record Original(bool StudioMode, ObsTransition? Transition, TimeSpan? Duration);

        private static Original Remember(ObsModel model)
        {
            return new Original(model.IsStudioModeEnabled, model.Transitions.Current, model.Transitions.Duration);
        }

        // Puts back studio mode, the current transition and its duration, once no transition runs any more (the scenes that the transition goes to are removed after this).
        private async Task RestoreAsync(ObsModel model, Original original)
        {
            try
            {
                await AsyncTestHelpers.WaitUntilAsync(() => !model.Transitions.IsRunning, Wait);
                if (model.IsStudioModeEnabled != original.StudioMode)
                {
                    await model.SetStudioModeEnabledAsync(original.StudioMode);
                }

                if (original.Transition is { } transition && model.Transitions.Current?.Uuid != transition.Uuid)
                {
                    await model.Transitions.SetCurrentAsync(transition.Name);
                }

                if (original.Duration is { } duration && model.Transitions.Duration != duration)
                {
                    await model.Transitions.SetDurationAsync(duration);
                }
            }
            catch (ObsClientException exception)
            {
                output.WriteLine($"Could not put back what the test changed: {exception.Message}");
            }
        }

        // The transitions of a fresh OBS Studio: the cut (which takes no time) and the fade, found by their kinds because their names are in the language of OBS Studio.
        private static async Task<(ObsTransition Cut, ObsTransition Fade)> TheTwoTransitionsAsync(ObsModel model)
        {
            var all = await model.Transitions.GetAllAsync();
            return (all.First(transition => transition.Kind == CutKind), all.First(transition => transition.Kind == FadeKind));
        }

        // What the model says about the transitions, with the time (since the start of the test) at which it said so.
        private sealed class Heard
        {
            private readonly Stopwatch _clock = Stopwatch.StartNew();
            private readonly List<(string What, string Name, TimeSpan At)> _entries = [];

            public Heard(ObsTransitions transitions)
            {
                transitions.Started += (_, e) => this.Add("Started", e.TransitionName);
                transitions.VideoEnded += (_, e) => this.Add("VideoEnded", e.TransitionName);
                transitions.Ended += (_, e) => this.Add("Ended", e.TransitionName);
            }

            public (string What, string Name, TimeSpan At)[] All
            {
                get
                {
                    lock (this._entries)
                    {
                        return this._entries.ToArray();
                    }
                }
            }

            public string[] Whats => this.All.Select(entry => entry.What).ToArray();

            public TimeSpan? When(string what)
            {
                return this.All.Where(entry => entry.What == what).Select(entry => (TimeSpan?)entry.At).FirstOrDefault();
            }

            public void Clear()
            {
                lock (this._entries)
                {
                    this._entries.Clear();
                }
            }

            private void Add(string what, string name)
            {
                lock (this._entries)
                {
                    this._entries.Add((what, name, this._clock.Elapsed));
                }
            }
        }

        [LiveUuidChangesFact]
        public async Task Observation_StudioModeThePreviewSceneAndTheTransitions()
        {
            await using var session = await LiveSession.ConnectAsync(EventSubscriptions.All, removeLeftovers: true);
            var client = session.Client;
            var version = await client.GetVersion();
            output.WriteLine($"OBS Studio {version.ObsVersion}, obs-websocket {version.ObsWebSocketVersion}.");

            var seen = session.RawEvents().Count;

            string Events()
            {
                var all = session.RawEvents();
                var text = string.Join("; ", all.Skip(seen).Select(entry => entry.Data is { } data ? $"{entry.Type} {data.GetRawText()}" : entry.Type));
                seen = all.Count;
                return text;
            }

            async Task<string> ShortStateAsync()
            {
                var program = await client.GetCurrentProgramScene();
                string preview;
                try
                {
                    preview = $"'{(await client.GetCurrentPreviewScene()).SceneName}'";
                }
                catch (ObsResponseException exception)
                {
                    preview = $"({(int)exception.ErrorCode})";
                }

                var cursor = await client.GetCurrentSceneTransitionCursor();
                return $"program '{program.SceneName}', preview {preview}, cursor {cursor}";
            }

            async Task<string> TransitionAsync()
            {
                var current = await client.GetCurrentSceneTransition();
                var settings = current.TransitionSettings is null ? "null" : "{" + string.Join(", ", current.TransitionSettings.Select(pair => $"{pair.Key}={pair.Value}")) + "}";
                return $"'{current.TransitionName}' kind {current.TransitionKind} uuid {current.TransitionUuid} duration {(current.TransitionDuration?.ToString() ?? "null")} fixed {current.TransitionFixed} configurable {current.TransitionConfigurable} settings {settings}";
            }

            async Task StepAsync(string what, Func<Task>? act = null, int wait = 500)
            {
                var result = "ok";
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
                output.WriteLine($"{what}: {result}; events [{Events()}]");
            }

            // What happens over time: the call, then the state and the events every moment, for as long as asked.
            async Task TimelineAsync(string what, Func<Task> act, int totalMilliseconds, int pollMilliseconds = 100)
            {
                var clock = Stopwatch.StartNew();
                var result = "ok";
                try
                {
                    await act();
                }
                catch (ObsResponseException exception)
                {
                    result = $"refused {(int)exception.ErrorCode} {exception.ErrorCode}: {exception.ErrorMessage}";
                }

                output.WriteLine($"{what}: the call returned after {clock.ElapsedMilliseconds} ms: {result}");
                var last = string.Empty;
                while (clock.ElapsedMilliseconds < totalMilliseconds)
                {
                    var events = Events();
                    var state = await ShortStateAsync();
                    if (events.Length > 0 || state != last)
                    {
                        output.WriteLine($"    t={clock.ElapsedMilliseconds,5} ms  {state}; events [{events}]");
                        last = state;
                    }

                    await Task.Delay(pollMilliseconds);
                }
            }

            var originalProgram = await client.GetCurrentProgramScene();
            var originalStudio = await client.GetStudioModeEnabled();
            var originalTransition = await client.GetCurrentSceneTransition();
            var sceneA = LiveObs.UniqueName("tra");
            var sceneB = LiveObs.UniqueName("trb");
            try
            {
                output.WriteLine($"at the start: studio mode {originalStudio}, program '{originalProgram.SceneName}', transition {await TransitionAsync()}");
                output.WriteLine($"transition kinds: {string.Join(", ", await client.GetTransitionKindList())}");
                var list = await client.GetSceneTransitionList();
                output.WriteLine($"the transitions: current '{list.CurrentSceneTransitionName}' ({list.CurrentSceneTransitionKind}); {string.Join("; ", list.Transitions.Select(item => $"'{item.TransitionName}' {item.TransitionKind} fixed {item.TransitionFixed} configurable {item.TransitionConfigurable}"))}");

                var a = SceneRef.FromUuid(await client.CreateScene(sceneA));
                var b = SceneRef.FromUuid(await client.CreateScene(sceneB));
                await client.SetCurrentProgramScene(a);
                await Task.Delay(800);
                seen = session.RawEvents().Count;
                output.WriteLine($"program is A now: {await ShortStateAsync()}");

                // Studio mode is off: what the requests that need it say.
                await StepAsync("01 get the preview scene (studio mode off)", async () => output.WriteLine($"   preview: {(await client.GetCurrentPreviewScene()).SceneName}"), wait: 100);
                await StepAsync("02 set the preview scene (studio mode off)", () => client.SetCurrentPreviewScene(b), wait: 200);
                await StepAsync("03 trigger the studio mode transition (studio mode off)", () => client.TriggerStudioModeTransition(), wait: 200);
                await StepAsync("04 set the T-bar (studio mode off)", () => client.SetTBarPosition(0.5f), wait: 200);

                // Studio mode on.
                await StepAsync("05 studio mode on", () => client.SetStudioModeEnabled(true), wait: 800);
                output.WriteLine($"   state: {await ShortStateAsync()}");
                await StepAsync("06 studio mode on again", () => client.SetStudioModeEnabled(true), wait: 500);
                await StepAsync("07 the preview scene is B", () => client.SetCurrentPreviewScene(b), wait: 500);
                output.WriteLine($"   state: {await ShortStateAsync()}");
                await StepAsync("08 the preview scene is B again", () => client.SetCurrentPreviewScene(b), wait: 500);
                await StepAsync("09 the preview scene is one that does not exist", () => client.SetCurrentPreviewScene(SceneRef.FromName("obsclient-live-not-a-scene")), wait: 200);

                // The default transition (300 ms): preview to program.
                output.WriteLine($"   transition: {await TransitionAsync()}");
                await TimelineAsync("10 trigger the transition (fade, default duration)", () => client.TriggerStudioModeTransition(), 1500, 50);

                // A transition that takes a while.
                await StepAsync("11 duration 3000 ms", () => client.SetCurrentSceneTransitionDuration(3000), wait: 500);
                output.WriteLine($"   transition: {await TransitionAsync()}");
                await StepAsync("12 the preview scene is A", () => client.SetCurrentPreviewScene(a), wait: 500);
                output.WriteLine($"   state: {await ShortStateAsync()}");
                await TimelineAsync("13 trigger the transition (3 s)", () => client.TriggerStudioModeTransition(), 4800, 150);
                await StepAsync("14 the preview scene is B", () => client.SetCurrentPreviewScene(b), wait: 300);
                await TimelineAsync("15 trigger the transition (3 s), and trigger it again while it runs", () => client.TriggerStudioModeTransition(), 800, 100);
                await StepAsync("16 trigger it again while it runs", () => client.TriggerStudioModeTransition(), wait: 300);
                await TimelineAsync("17 set the program scene while it runs (to A)", () => client.SetCurrentProgramScene(a), 4500, 150);

                // The T-bar.
                await StepAsync("18 the preview scene is B", () => client.SetCurrentPreviewScene(b), wait: 300);
                await TimelineAsync("19 T-bar to 0.5, not released", () => client.SetTBarPosition(0.5f, false), 800, 200);
                await TimelineAsync("20 T-bar to 1.0, released", () => client.SetTBarPosition(1.0f, true), 2000, 200);
                await StepAsync("21 the preview scene is A", () => client.SetCurrentPreviewScene(a), wait: 300);
                await TimelineAsync("22 T-bar to 0.3, released at once", () => client.SetTBarPosition(0.3f), 1500, 200);
                output.WriteLine($"   state: {await ShortStateAsync()}");
                await StepAsync("23 T-bar to 1.5", () => client.SetTBarPosition(1.5f), wait: 200);
                await StepAsync("24 T-bar to 0", () => client.SetTBarPosition(0f), wait: 800);

                // Studio mode off, and the program scene changes with a transition that takes a while.
                await StepAsync("25 studio mode off", () => client.SetStudioModeEnabled(false), wait: 800);
                output.WriteLine($"   state: {await ShortStateAsync()}");
                await TimelineAsync("26 program scene to B (3 s fade, studio mode off)", () => client.SetCurrentProgramScene(b), 4500, 150);
                await TimelineAsync("27 program scene to A (3 s fade) and A again", () => client.SetCurrentProgramScene(a), 4500, 150);

                // The transition itself.
                await StepAsync("28 transition Cut", () => client.SetCurrentSceneTransition("Cut"), wait: 500);
                output.WriteLine($"   transition: {await TransitionAsync()}");
                await StepAsync("29 duration of a fixed transition (500)", () => client.SetCurrentSceneTransitionDuration(500), wait: 300);
                await StepAsync("30 settings of a fixed transition", () => client.SetCurrentSceneTransitionSettings(new Dictionary<string, object> { ["x"] = 1 }), wait: 300);
                await TimelineAsync("31 program scene to B (cut)", () => client.SetCurrentProgramScene(b), 800, 100);
                await StepAsync("32 transition Fade", () => client.SetCurrentSceneTransition("Fade"), wait: 500);
                output.WriteLine($"   transition: {await TransitionAsync()}");
                await StepAsync("33 transition Fade again", () => client.SetCurrentSceneTransition("Fade"), wait: 400);
                await StepAsync("34 transition that does not exist", () => client.SetCurrentSceneTransition("obsclient-live-no-transition"), wait: 200);
                await StepAsync("35 duration 49", () => client.SetCurrentSceneTransitionDuration(49), wait: 200);
                await StepAsync("36 duration 20001", () => client.SetCurrentSceneTransitionDuration(20001), wait: 200);
                await StepAsync("37 duration 50", () => client.SetCurrentSceneTransitionDuration(50), wait: 400);
                await StepAsync("38 duration 123.7", () => client.SetCurrentSceneTransitionDuration(123.7f), wait: 400);
                output.WriteLine($"   transition: {await TransitionAsync()}");
                await StepAsync("39 duration 300", () => client.SetCurrentSceneTransitionDuration(300), wait: 400);
                await StepAsync("40 settings of Fade (overlay)", () => client.SetCurrentSceneTransitionSettings(new Dictionary<string, object> { ["x"] = 1 }, true), wait: 400);
                output.WriteLine($"   transition: {await TransitionAsync()}");
                await StepAsync("41 settings of Fade (replace)", () => client.SetCurrentSceneTransitionSettings(new Dictionary<string, object>(), false), wait: 400);
                output.WriteLine($"   transition: {await TransitionAsync()}");
                output.WriteLine($"   cursor when idle: {await client.GetCurrentSceneTransitionCursor()}");

                // The transition of a scene.
                var none = await client.GetSceneSceneTransitionOverride(a);
                output.WriteLine($"42 the override of A: '{none.TransitionName}' / {none.TransitionDuration?.ToString() ?? "null"}");
                await StepAsync("43 override of A: Cut", () => client.SetSceneSceneTransitionOverride(a, "Cut", null), wait: 400);
                var cut = await client.GetSceneSceneTransitionOverride(a);
                output.WriteLine($"   the override of A: '{cut.TransitionName}' / {cut.TransitionDuration?.ToString() ?? "null"}");
                await StepAsync("44 override of A: Fade 1000", () => client.SetSceneSceneTransitionOverride(a, "Fade", 1000), wait: 400);
                var fade = await client.GetSceneSceneTransitionOverride(a);
                output.WriteLine($"   the override of A: '{fade.TransitionName}' / {fade.TransitionDuration?.ToString() ?? "null"}");
                await StepAsync("45 override of A: duration only", () => client.SetSceneSceneTransitionOverride(a, null, 700), wait: 400);
                var durationOnly = await client.GetSceneSceneTransitionOverride(a);
                output.WriteLine($"   the override of A: '{durationOnly.TransitionName}' / {durationOnly.TransitionDuration?.ToString() ?? "null"}");
                await StepAsync("46 override of A: a transition that does not exist", () => client.SetSceneSceneTransitionOverride(a, "obsclient-live-no-transition", null), wait: 300);
                await StepAsync("47 override of A: duration 10", () => client.SetSceneSceneTransitionOverride(a, "Cut", 10), wait: 300);
                await StepAsync("48 override of a scene that does not exist", () => client.SetSceneSceneTransitionOverride(SceneRef.FromName("obsclient-live-not-a-scene"), "Cut", null), wait: 300);
                await StepAsync("49 get the override of a scene that does not exist", async () => await client.GetSceneSceneTransitionOverride(SceneRef.FromName("obsclient-live-not-a-scene")), wait: 300);
                await StepAsync("50 override of A: Cut, 400", () => client.SetSceneSceneTransitionOverride(a, "Cut", 400), wait: 300);
                await StepAsync("51 override of B: Fade, 2000", () => client.SetSceneSceneTransitionOverride(b, "Fade", 2000), wait: 300);
                output.WriteLine($"   transition now: {await TransitionAsync()}");
                await TimelineAsync("52 program scene to A (the override of A is Cut, 400)", () => client.SetCurrentProgramScene(a), 1500, 100);
                output.WriteLine($"   transition after: {await TransitionAsync()}");
                await TimelineAsync("53 program scene to B (the override of B is Fade, 2000)", () => client.SetCurrentProgramScene(b), 3200, 100);
                output.WriteLine($"   transition after: {await TransitionAsync()}");
                await StepAsync("54 remove the overrides", async () =>
                {
                    await client.SetSceneSceneTransitionOverride(a, null, null);
                    await client.SetSceneSceneTransitionOverride(b, null, null);
                }, wait: 300);
                var removed = await client.GetSceneSceneTransitionOverride(a);
                output.WriteLine($"   the override of A: '{removed.TransitionName}' / {removed.TransitionDuration?.ToString() ?? "null"}");
            }
            finally
            {
                await TryAsync(() => client.SetStudioModeEnabled(originalStudio));
                await TryAsync(() => client.SetCurrentSceneTransition(originalTransition.TransitionName));
                if (originalTransition.TransitionDuration is { } duration)
                {
                    await TryAsync(() => client.SetCurrentSceneTransitionDuration(duration));
                }

                await TryAsync(() => client.SetCurrentProgramScene(SceneRef.FromUuid(originalProgram.SceneUuid)));
                await Task.Delay(500);
                await TryAsync(() => client.RemoveScene(SceneRef.FromName(sceneA)));
                await TryAsync(() => client.RemoveScene(SceneRef.FromName(sceneB)));
            }
        }

        [LiveUuidChangesFact]
        public async Task Observation_TheProgramAndThePreviewSceneInStudioMode_WhenScenesAreSwitchedRenamedAndRemoved()
        {
            await using var session = await LiveSession.ConnectAsync(EventSubscriptions.All, removeLeftovers: true);
            var client = session.Client;
            var seen = session.RawEvents().Count;

            string Events()
            {
                var all = session.RawEvents();
                var text = string.Join("; ", all.Skip(seen).Select(entry => entry.Data is { } data ? $"{entry.Type} {data.GetRawText()}" : entry.Type));
                seen = all.Count;
                return text;
            }

            async Task<string> StateAsync()
            {
                async Task<string> AskAsync(Func<Task<string>> ask)
                {
                    try
                    {
                        return await ask();
                    }
                    catch (ObsResponseException exception)
                    {
                        return $"({(int)exception.ErrorCode})";
                    }
                    catch (TimeoutException)
                    {
                        return "(no answer)";
                    }
                }

                var studio = await AskAsync(async () => (await client.GetStudioModeEnabled()).ToString());
                var program = await AskAsync(async () => $"'{(await client.GetCurrentProgramScene()).SceneName}'");
                var preview = await AskAsync(async () => $"'{(await client.GetCurrentPreviewScene()).SceneName}'");
                var scenes = await AskAsync(async () => string.Join(",", (await client.GetSceneList()).Scenes.Select(scene => scene.SceneName[^3..])));
                return $"studio {studio}, program {program}, preview {preview}, scenes [{scenes}]";
            }

            async Task StepAsync(string what, Func<Task>? act = null, int wait = 500)
            {
                var result = "ok";
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
                catch (TimeoutException)
                {
                    result = "no answer";
                }

                await Task.Delay(wait);
                var events = Events();
                output.WriteLine($"{what}: {result}; events [{events}]; {await StateAsync()}");
            }

            var originalProgram = await client.GetCurrentProgramScene();
            var originalStudio = await client.GetStudioModeEnabled();
            var originalTransition = await client.GetCurrentSceneTransition();
            var names = new[] { LiveObs.UniqueName("pa"), LiveObs.UniqueName("pb"), LiveObs.UniqueName("pc"), LiveObs.UniqueName("pd") };
            var refs = new List<SceneRef>();
            try
            {
                foreach (var name in names)
                {
                    refs.Add(SceneRef.FromUuid(await client.CreateScene(name)));
                }

                // A short transition keeps this fast.
                await client.SetCurrentSceneTransitionDuration(100);
                await client.SetCurrentProgramScene(refs[0]);
                await Task.Delay(600);
                seen = session.RawEvents().Count;
                output.WriteLine($"names: A {names[0]}, B {names[1]}, C {names[2]}, D {names[3]}");
                output.WriteLine($"start: {await StateAsync()}");

                await StepAsync("01 the program scene is A again (studio mode off)", () => client.SetCurrentProgramScene(refs[0]), wait: 700);
                await StepAsync("02 the same duration again (100)", () => client.SetCurrentSceneTransitionDuration(100), wait: 300);

                var before = session.Log.Entries.Count;
                await client.GetSceneSceneTransitionOverride(refs[0]);
                var raw = session.RawResponse("GetSceneSceneTransitionOverride", before);
                output.WriteLine($"03 the raw answer for a scene without an override: {raw?.GetRawText()}");

                await StepAsync("04 studio mode on", () => client.SetStudioModeEnabled(true), wait: 500);
                await StepAsync("05 the program scene is B (studio mode on, nothing running)", () => client.SetCurrentProgramScene(refs[1]), wait: 900);
                await StepAsync("06 the preview scene is C", () => client.SetCurrentPreviewScene(refs[2]), wait: 300);
                await StepAsync("07 the preview scene is the program scene (B), and trigger", async () =>
                {
                    await client.SetCurrentPreviewScene(refs[1]);
                    await client.TriggerStudioModeTransition();
                }, wait: 900);
                await StepAsync("08 the preview scene is C, and trigger", async () =>
                {
                    await client.SetCurrentPreviewScene(refs[2]);
                    await client.TriggerStudioModeTransition();
                }, wait: 900);
                await StepAsync("09 the preview scene is D, and it is renamed", async () =>
                {
                    await client.SetCurrentPreviewScene(refs[3]);
                    await client.SetSceneName(refs[3], names[3] + "-r");
                    names[3] += "-r";
                }, wait: 500);
                await StepAsync("10 the preview scene (D) is removed", () => client.RemoveScene(refs[3]), wait: 1500);
                await StepAsync("11 the preview scene is A", () => client.SetCurrentPreviewScene(refs[0]), wait: 300);

                // Not done here: removing the program scene (C) while studio mode is on. OBS Studio 33.0.0 then stops answering the requests that need its window for seconds (up to half a minute was seen).
                await StepAsync("12 the program scene is changed to B while the preview scene is A", () => client.SetCurrentProgramScene(refs[1]), wait: 900);
                await StepAsync("13 studio mode off", () => client.SetStudioModeEnabled(false), wait: 600);
                await StepAsync("14 studio mode on again", () => client.SetStudioModeEnabled(true), wait: 600);
                await StepAsync("15 studio mode off", () => client.SetStudioModeEnabled(false), wait: 600);
            }
            finally
            {
                await TryAsync(() => client.SetStudioModeEnabled(originalStudio));
                await TryAsync(() => client.SetCurrentSceneTransitionDuration((int)(originalTransition.TransitionDuration ?? 300)));
                await TryAsync(() => client.SetCurrentProgramScene(SceneRef.FromUuid(originalProgram.SceneUuid)));
                await Task.Delay(500);
                foreach (var name in names)
                {
                    await TryAsync(() => client.RemoveScene(SceneRef.FromName(name)));
                }
            }
        }

        [LiveUuidChangesFact]
        public async Task StudioMode_IsSwitchedOnAndOff_AndThePreviewSceneIsMadeAndFollowed_AsOBSStudioDoesIt()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            output.WriteLine($"OBS Studio {live.ObsVersionText}, obs-websocket {live.WebSocketVersionText}.");
            var original = Remember(model);
            try
            {
                if (model.IsStudioModeEnabled)
                {
                    await model.SetStudioModeEnabledAsync(false);
                }

                var a = await model.CreateSceneAsync(LiveObs.UniqueName("sa"));
                var b = await model.CreateSceneAsync(LiveObs.UniqueName("sb"));

                // OBS Studio refuses what needs studio mode while it is off, and the model has no preview scene.
                var preview = await Assert.ThrowsAsync<ObsResponseException>(() => b.SwitchToPreviewAsync());
                Assert.Equal(RequestStatusCode.StudioModeNotActive, preview.ErrorCode);
                var trigger = await Assert.ThrowsAsync<ObsResponseException>(() => model.Transitions.TriggerAsync());
                Assert.Equal(RequestStatusCode.StudioModeNotActive, trigger.ErrorCode);
                Assert.Null(model.PreviewScene);
                Assert.False(b.IsPreview);
                await live.AssertModelEqualsObsAsync("with studio mode off");

                // Studio mode on: OBS Studio makes the program scene the preview scene, and sends no event for it: the model reads it.
                await model.SetStudioModeEnabledAsync(true);
                Assert.True(model.IsStudioModeEnabled);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => model.PreviewScene is not null, Wait), "The model did not get the preview scene when studio mode was switched on.");
                Assert.Same(model.ProgramScene, model.PreviewScene);
                await live.AssertModelEqualsObsAsync("after studio mode was switched on");

                await b.SwitchToPreviewAsync();
                Assert.True(b.IsPreview);
                Assert.Same(b, model.PreviewScene);
                Assert.NotSame(b, model.ProgramScene);
                await live.AssertModelEqualsObsAsync("after the preview scene was made");

                // The same again: OBS Studio says nothing, and the model shows it already.
                await b.SwitchToPreviewAsync();
                await model.SetStudioModeEnabledAsync(true);
                await live.AssertModelEqualsObsAsync("after the same preview scene and the same studio mode were asked for again");

                // A scene that is the preview scene and is removed: OBS Studio puts another scene in its place (the one that is first in the list, as far as was measured), and the model reads it.
                await b.RemoveAsync();
                var got = await AsyncTestHelpers.WaitUntilAsync(() => model.PreviewScene is not null && !ReferenceEquals(model.PreviewScene, b), Wait);
                if (!got)
                {
                    output.WriteLine($"the model shows preview '{model.PreviewScene?.Name}' and program '{model.ProgramScene?.Name}'; OBS Studio has preview '{(await live.Client.GetCurrentPreviewScene()).SceneName}' and program '{(await live.Client.GetCurrentProgramScene()).SceneName}'.");
                }

                Assert.True(got, "The model did not get another scene as the preview scene after the preview scene was removed.");
                Assert.True(b.IsRemoved);
                output.WriteLine($"after the preview scene was removed OBS Studio has '{model.PreviewScene!.Name}' as the preview scene, and the program scene is '{model.ProgramScene?.Name}'.");
                await live.AssertModelEqualsObsAsync("after the preview scene was removed");

                await a.SwitchToPreviewAsync();
                Assert.True(a.IsPreview);
                await live.AssertModelEqualsObsAsync("after another preview scene was made");

                await model.SetStudioModeEnabledAsync(false);
                Assert.False(model.IsStudioModeEnabled);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => model.PreviewScene is null && !a.IsPreview, Wait), "The model kept a preview scene after studio mode was switched off.");
                await live.AssertModelEqualsObsAsync("after studio mode was switched off");
            }
            finally
            {
                await this.RestoreAsync(model, original);
            }
        }

        [LiveUuidChangesFact]
        public async Task TheTransitionOfStudioMode_IsTriggered_AndTheModelFollowsItFromItsStartToItsEnd()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var original = Remember(model);
            var heard = new Heard(model.Transitions);
            try
            {
                var (_, fade) = await TheTwoTransitionsAsync(model);
                await model.Transitions.SetCurrentAsync(fade.Name);
                await model.Transitions.SetDurationAsync(TimeSpan.FromSeconds(3));
                var a = await model.CreateSceneAsync(LiveObs.UniqueName("ta"));
                var b = await model.CreateSceneAsync(LiveObs.UniqueName("tb"));
                if (model.IsStudioModeEnabled)
                {
                    await model.SetStudioModeEnabledAsync(false);
                }

                await a.SwitchToAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => !model.Transitions.IsRunning, Wait), "The transition to the first scene did not end.");
                await model.SetStudioModeEnabledAsync(true);
                await b.SwitchToPreviewAsync();
                await live.AssertModelEqualsObsAsync("before the transition of studio mode was triggered");
                heard.Clear();

                // The call returns when the transition has started: the scene that is switched to is the program scene at once for OBS Studio, and so for the model, while the transition is running.
                await model.Transitions.TriggerAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => heard.Whats.Contains("Started"), Wait), "The model did not say that the transition started.");
                var seenRunning = false;
                Assert.True(
                    await AsyncTestHelpers.WaitUntilAsync(
                        () =>
                        {
                            seenRunning = model.Transitions.IsRunning;
                            return ReferenceEquals(model.ProgramScene, b);
                        },
                        TimeSpan.FromSeconds(2.5)),
                    "The model did not show the scene that was switched to as the program scene while the transition ran.");
                Assert.True(seenRunning, "The transition was over when the model showed the scene that was switched to: the model learned it from the end of the transition.");
                Assert.Same(b, model.PreviewScene);

                // A second trigger while it runs: OBS Studio does nothing, and the call returns at once. (It does not do nothing in the first moment of a transition, while its position is still 0: it
                // blocks a new transition only while the position is between 0 and 1.)
                await Task.Delay(600);
                await model.Transitions.TriggerAsync();

                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => !model.Transitions.IsRunning && a.IsPreview, Wait), "The transition did not end, or the preview scene did not become the scene that was on the air.");
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => heard.Whats.Length == 3, Wait), $"The model said {string.Join(", ", heard.Whats)}.");
                Assert.Equal(new[] { "Started", "VideoEnded", "Ended" }, heard.Whats);
                Assert.All(heard.All, entry => Assert.Equal(fade.Name, entry.Name));
                var length = heard.When("VideoEnded")!.Value - heard.When("Started")!.Value;
                output.WriteLine($"the transition ran {length.TotalMilliseconds:0} ms (3000 ms were asked for).");
                Assert.True(length > TimeSpan.FromSeconds(2.5), $"The transition ran {length.TotalMilliseconds:0} ms.");
                Assert.Same(b, model.ProgramScene);
                Assert.Same(a, model.PreviewScene);
                Assert.True(await model.Transitions.GetProgressAsync() >= 0.99);
                await live.AssertModelEqualsObsAsync("after the transition of studio mode ended");
            }
            finally
            {
                await this.RestoreAsync(model, original);
            }
        }

        [LiveUuidChangesFact]
        public async Task ASwitchOfTheProgramScene_ThatTakesAWhile_IsShownByTheModelFromItsStart_WhoeverMadeIt()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var original = Remember(model);
            try
            {
                var (_, fade) = await TheTwoTransitionsAsync(model);
                await model.Transitions.SetCurrentAsync(fade.Name);
                await model.Transitions.SetDurationAsync(TimeSpan.FromSeconds(4));
                if (model.IsStudioModeEnabled)
                {
                    await model.SetStudioModeEnabledAsync(false);
                }

                var a = await model.CreateSceneAsync(LiveObs.UniqueName("pa"));
                var b = await model.CreateSceneAsync(LiveObs.UniqueName("pb"));
                await a.SwitchToAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => !model.Transitions.IsRunning, Wait), "The first transition did not end.");

                // The user switches: OBS Studio answers a question about the program scene with the new scene at once, and says that it is the program scene when the transition has ended, four seconds later.
                await live.Client.SetCurrentProgramScene(SceneRef.FromUuid(b.Uuid));
                var seenRunning = false;
                var clock = Stopwatch.StartNew();
                Assert.True(
                    await AsyncTestHelpers.WaitUntilAsync(
                        () =>
                        {
                            seenRunning = model.Transitions.IsRunning;
                            return ReferenceEquals(model.ProgramScene, b);
                        },
                        TimeSpan.FromSeconds(3)),
                    "The model did not show the scene that was switched to while the transition ran.");
                output.WriteLine($"the model showed the new program scene {clock.ElapsedMilliseconds} ms after the user switched, while the transition {(seenRunning ? "ran" : "was over")}.");
                Assert.True(seenRunning);
                Assert.True(b.IsProgram);
                Assert.False(a.IsProgram);

                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => !model.Transitions.IsRunning, Wait), "The transition did not end.");
                await live.AssertModelEqualsObsAsync("after the transition of the user ended");

                // The model asks: the call returns when the model shows it, while the transition runs.
                await a.SwitchToAsync();
                Assert.True(a.IsProgram);
                Assert.True(model.Transitions.IsRunning, "The call returned after the transition ended.");
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => !model.Transitions.IsRunning, Wait), "The transition did not end.");
                await live.AssertModelEqualsObsAsync("after the transition of the model ended");
            }
            finally
            {
                await this.RestoreAsync(model, original);
            }
        }

        [LiveUuidChangesFact]
        public async Task TheCurrentTransitionAndTheirDuration_AreSetAndShownByTheModel_AsOBSStudioDoesIt()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var original = Remember(model);
            try
            {
                var (cut, fade) = await TheTwoTransitionsAsync(model);
                output.WriteLine($"the transitions: '{cut.Name}' ({cut.Kind}, fixed {cut.IsFixed}) and '{fade.Name}' ({fade.Kind}, fixed {fade.IsFixed}).");
                Assert.True(cut.IsFixed);
                Assert.False(fade.IsFixed);
                Assert.Contains(CutKind, await model.Transitions.GetKindsAsync());
                Assert.Contains(FadeKind, await model.Transitions.GetKindsAsync());

                await model.Transitions.SetCurrentAsync(cut.Name);
                Assert.Equal(cut.Uuid, model.Transitions.Current!.Uuid);
                Assert.True(model.Transitions.Current.IsFixed);
                await live.AssertModelEqualsObsAsync("after the cut was made the current transition");

                // OBS Studio sends the event again when it is the current transition already.
                await model.Transitions.SetCurrentAsync(cut.Name);

                // The duration belongs to the transitions: it is set while the cut is the current one, and the fade has it.
                await model.Transitions.SetDurationAsync(TimeSpan.FromMilliseconds(1234));
                Assert.Equal(TimeSpan.FromMilliseconds(1234), model.Transitions.Duration);
                await model.Transitions.SetCurrentAsync(fade.Name);
                Assert.Equal(fade.Uuid, model.Transitions.Current!.Uuid);
                Assert.Equal(TimeSpan.FromMilliseconds(1234), model.Transitions.Duration);
                await live.AssertModelEqualsObsAsync("after the fade was made the current transition");

                // The same duration again: OBS Studio says nothing, and the model shows it already.
                await model.Transitions.SetDurationAsync(TimeSpan.FromMilliseconds(1234));
                await model.Transitions.SetDurationAsync(TimeSpan.FromMilliseconds(50));
                Assert.Equal(TimeSpan.FromMilliseconds(50), model.Transitions.Duration);
                await model.Transitions.SetDurationAsync(TimeSpan.FromSeconds(20));
                Assert.Equal(TimeSpan.FromSeconds(20), model.Transitions.Duration);
                await model.Transitions.SetDurationAsync(TimeSpan.FromMilliseconds(300));
                await live.AssertModelEqualsObsAsync("after the duration was set");

                var unknown = await Assert.ThrowsAsync<ObsResponseException>(() => model.Transitions.SetCurrentAsync(LiveObs.UniqueName("no-transition")));
                Assert.Equal(RequestStatusCode.ResourceNotFound, unknown.ErrorCode);
                Assert.Throws<ArgumentOutOfRangeException>(() => { _ = model.Transitions.SetDurationAsync(TimeSpan.FromMilliseconds(49)); });
                Assert.Throws<ArgumentOutOfRangeException>(() => { _ = model.Transitions.SetDurationAsync(TimeSpan.FromMilliseconds(20001)); });
                Assert.Equal(fade.Uuid, model.Transitions.Current.Uuid);

                // Neither of the two has settings.
                var settings = await model.Transitions.GetSettingsAsync();
                Assert.Empty(settings.EnumerateObject());
                var refusal = await Assert.ThrowsAsync<ObsResponseException>(() => model.Transitions.SetSettingsAsync(new { direction = "left" }));
                Assert.Equal(RequestStatusCode.ResourceNotConfigurable, refusal.ErrorCode);

                // No transition runs: the position is the end.
                Assert.True(await model.Transitions.GetProgressAsync() >= 0.99);
                Assert.False(model.Transitions.IsRunning);
                await live.AssertModelEqualsObsAsync("after the transitions were asked about");
            }
            finally
            {
                await this.RestoreAsync(model, original);
            }
        }

        [LiveUuidChangesFact]
        public async Task TheTransitionOfAScene_IsSetAskedAndTakenAway_AndOBSStudioUsesItInsteadOfTheCurrentOne()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var original = Remember(model);
            var heard = new Heard(model.Transitions);
            try
            {
                var (cut, fade) = await TheTwoTransitionsAsync(model);
                if (model.IsStudioModeEnabled)
                {
                    await model.SetStudioModeEnabledAsync(false);
                }

                await model.Transitions.SetCurrentAsync(cut.Name);
                var a = await model.CreateSceneAsync(LiveObs.UniqueName("oa"));
                var other = await model.CreateSceneAsync(LiveObs.UniqueName("ob"));

                Assert.False((await a.GetTransitionOverrideAsync()).IsSet);

                await a.SetTransitionOverrideAsync(fade.Name, TimeSpan.FromMilliseconds(1500));
                var set = await a.GetTransitionOverrideAsync();
                Assert.True(set.IsSet);
                Assert.Equal((fade.Name, TimeSpan.FromMilliseconds(1500)), (set.Name, set.Duration));

                // Switched to, the scene is shown with its own transition for as long as it says, and the current transition is not changed for it.
                await other.SwitchToAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => !model.Transitions.IsRunning, Wait), "The first transition did not end.");
                heard.Clear();
                await a.SwitchToAsync();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => heard.Whats.Contains("VideoEnded"), Wait), $"The model said {string.Join(", ", heard.Whats)}.");
                var length = heard.When("VideoEnded")!.Value - heard.When("Started")!.Value;
                output.WriteLine($"the scene with a transition of its own was shown with '{heard.All[0].Name}' for {length.TotalMilliseconds:0} ms (the current transition is the cut).");
                Assert.Equal(fade.Name, heard.All[0].Name);
                Assert.True(length > TimeSpan.FromSeconds(1.2), $"The transition ran {length.TotalMilliseconds:0} ms.");
                Assert.Equal(cut.Uuid, model.Transitions.Current!.Uuid);
                await live.AssertModelEqualsObsAsync("after the scene was shown with its own transition");

                // The transition and the duration are independent: only a duration, and then nothing.
                await a.SetTransitionOverrideAsync(null, TimeSpan.FromMilliseconds(700));
                var onlyDuration = await a.GetTransitionOverrideAsync();
                Assert.Equal((true, (string?)null, (TimeSpan?)TimeSpan.FromMilliseconds(700)), (onlyDuration.IsSet, onlyDuration.Name, onlyDuration.Duration));
                await a.ClearTransitionOverrideAsync();
                var none = await a.GetTransitionOverrideAsync();
                Assert.Equal((false, (string?)null, (TimeSpan?)null), (none.IsSet, none.Name, none.Duration));

                var unknown = await Assert.ThrowsAsync<ObsResponseException>(() => a.SetTransitionOverrideAsync(LiveObs.UniqueName("no-transition")));
                Assert.Equal(RequestStatusCode.ResourceNotFound, unknown.ErrorCode);
                Assert.Throws<ArgumentOutOfRangeException>(() => { _ = a.SetTransitionOverrideAsync(fade.Name, TimeSpan.FromMilliseconds(10)); });
            }
            finally
            {
                await this.RestoreAsync(model, original);
            }
        }

        [LiveUuidChangesFact]
        public async Task WhatTheUserDoesWithStudioModeAndTheTransitions_IsFollowedByTheModel_AndTheModelEqualsWhatObsStudioHolds()
        {
            await using var live = await LiveModel.ConnectAsync(output);
            var model = live.Model;
            var original = Remember(model);
            try
            {
                var (cut, fade) = await TheTwoTransitionsAsync(model);
                if (model.IsStudioModeEnabled)
                {
                    await model.SetStudioModeEnabledAsync(false);
                }

                var a = await model.CreateSceneAsync(LiveObs.UniqueName("ua"));
                var b = await model.CreateSceneAsync(LiveObs.UniqueName("ub"));
                await live.Client.SetCurrentSceneTransition(fade.Name);
                await live.Client.SetCurrentSceneTransitionDuration(400);
                await live.Client.SetCurrentProgramScene(SceneRef.FromUuid(a.Uuid));
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => a.IsProgram && !model.Transitions.IsRunning && model.Transitions.Duration == TimeSpan.FromMilliseconds(400), Wait), "The model did not show what the user did.");
                await live.AssertModelEqualsObsAsync("after the user chose the fade and the scene");

                // The user (the plain client) switches studio mode on and chooses the preview scene.
                await live.Client.SetStudioModeEnabled(true);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => model.IsStudioModeEnabled && model.PreviewScene is not null, Wait), "The model did not show that the user switched studio mode on.");
                await live.AssertModelEqualsObsAsync("after the user switched studio mode on");
                await live.Client.SetCurrentPreviewScene(SceneRef.FromUuid(b.Uuid));
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => b.IsPreview, Wait), "The model did not show the preview scene that the user chose.");
                await live.AssertModelEqualsObsAsync("after the user chose the preview scene");

                // The cut, and a duration while the cut is the current transition.
                await live.Client.SetCurrentSceneTransition(cut.Name);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => model.Transitions.Current?.Uuid == cut.Uuid, Wait), "The model did not show the transition that the user chose.");
                await live.Client.SetCurrentSceneTransitionDuration(2222);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => model.Transitions.Duration == TimeSpan.FromMilliseconds(2222), Wait), "The model did not show the duration that the user set.");
                await live.AssertModelEqualsObsAsync("after the user chose the cut and a duration");

                // The user triggers the transition: with the cut it is over at once, and the scenes have changed places.
                await live.Client.TriggerStudioModeTransition();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => b.IsProgram && a.IsPreview && !model.Transitions.IsRunning, Wait), "The model did not show the transition that the user triggered.");
                await live.AssertModelEqualsObsAsync("after the user triggered the transition of studio mode");

                await live.Client.SetCurrentSceneTransition(fade.Name);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => model.Transitions.Current?.Uuid == fade.Uuid && model.Transitions.Duration == TimeSpan.FromMilliseconds(2222), Wait));
                await live.Client.TriggerStudioModeTransition();
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => model.Transitions.IsRunning, Wait), "The model did not show that a transition runs.");
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => !model.Transitions.IsRunning && a.IsProgram && b.IsPreview, Wait), "The model did not show the end of the transition.");
                await live.AssertModelEqualsObsAsync("after the user triggered the fade");

                await live.Client.SetStudioModeEnabled(false);
                Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => !model.IsStudioModeEnabled && model.PreviewScene is null, Wait), "The model did not show that the user switched studio mode off.");
                await live.AssertModelEqualsObsAsync("after the user switched studio mode off");
            }
            finally
            {
                await this.RestoreAsync(model, original);
            }
        }
    }
}
