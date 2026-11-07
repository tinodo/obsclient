namespace OBSStudioClient.Tests.Studio
{
    using System.Globalization;
    using System.Text;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.Simulation;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// A random walk of changes by name over the whole stack: an <see cref="ObsStudio"/> on a real <see cref="ObsClient"/>, connected to the simulation of OBS Studio, asks for random
    /// changes (some of them together, so that they wait for each other in the line), and the user of OBS Studio makes random changes of his own in between (so that names are taken,
    /// and objects are removed under the feet of the calls). Whatever the walk does, a call ends with the change, or with an exception that says what OBS Studio refused or what is gone
    /// (never with one that says that the outcome is unknown, or that the model could not show it; the one exception is the rename of a scene that the user removed meanwhile, which a real
    /// OBS Studio never answers, so that the outcome is unknown), and after a batch of calls the model shows what OBS Studio holds, with no wait left in the model. A walk that fails says
    /// its seed: set <c>OBSCLIENT_WALK_FIRST</c> to it and <c>OBSCLIENT_WALK_SEEDS</c> to 1 to run just that walk (the order in which the threads run is not the same twice, so it can take
    /// a few runs), or <c>OBSCLIENT_WALK_SEEDS</c> to a large number, and <c>OBSCLIENT_WALK_STEPS</c> to a longer walk, for a campaign.
    /// </summary>
    public class StudioWriteWalkTests
    {
        private const string Mic = "wasapi_input_capture";
        private const string Colour = "color_source_v3";
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
        private static readonly string[] SceneNames = ["S0", "S1", "S2", "S3", "Gaming", "Chatting"];
        private static readonly string[] InputNames = ["I0", "I1", "I2", "I3", "Webcam", "Mic", "Backdrop"];
        private readonly ITestOutputHelper _output;

        public StudioWriteWalkTests(ITestOutputHelper output)
        {
            this._output = output;
        }

        private static int Seeds => ReadNumber("OBSCLIENT_WALK_SEEDS", 3);

        private static int FirstSeed => ReadNumber("OBSCLIENT_WALK_FIRST", 1);

        private static int Steps => ReadNumber("OBSCLIENT_WALK_STEPS", 20);

        [Fact]
        public async Task AfterARandomWalkOfChangesByName_TheModelShowsWhatOBSStudioHolds()
        {
            var failures = new List<string>();
            for (var seed = FirstSeed; seed < FirstSeed + Seeds; seed++)
            {
                var statistics = new Dictionary<string, int>();
                var failure = await WalkAsync(seed, Steps, statistics);
                this._output.WriteLine($"seed {seed}: {string.Join(", ", statistics.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key} {pair.Value}"))}");
                if (failure is not null)
                {
                    failures.Add($"Seed {seed}: {failure}");
                }
                else if (TooFewChanges(statistics, Steps) is { } nothing)
                {
                    failures.Add($"Seed {seed}: {nothing}");
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine + Environment.NewLine, failures));
        }

        // The seeds that campaigns found, and the walks that they were found with. 273: OBS Studio made a scene item in a scene that the user removed while the call was under way (it sends no event
        // for the scene items of a scene that it removed), and the model did not show it when the call ended (StudioWriter.RefreshDetached). 286 and 1024: the walk renamed a scene that the
        // user removed meanwhile, which a real OBS Studio never answers (RenameSceneAsync).
        [Theory]
        [InlineData(273, 20)]
        [InlineData(286, 18)]
        [InlineData(1024, 16)]
        public async Task AWalkThatACampaignFoundAProblemWith_ShowsWhatOBSStudioHoldsWhenItEnds(int seed, int steps)
        {
            var statistics = new Dictionary<string, int>();
            var failure = await WalkAsync(seed, steps, statistics);

            Assert.True(failure is null, failure);
            Assert.Null(TooFewChanges(statistics, steps));
        }

        // A walk in which (nearly) every call is refused shows nothing: the calls have to change something, as often as one in two steps.
        private static string? TooFewChanges(Dictionary<string, int> statistics, int steps)
        {
            return statistics.GetValueOrDefault("ok") < steps / 2 ? $"only {statistics.GetValueOrDefault("ok")} calls changed anything in {steps} steps: the walk shows nothing." : null;
        }

        private static int ReadNumber(string name, int fallback)
        {
            return int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : fallback;
        }

        private static SimQuirks Quirks(int seed)
        {
            return (seed % 3) switch
            {
                0 => StudioSimRig.DefaultQuirks,
                1 => SimQuirks.Real,
                _ => SimQuirks.Hostile,
            };
        }

        // What a call may end with when the walk is ahead of the model, or the user was quicker: OBS Studio refuses (a name that is taken, the last scene, a loop of scenes), what the
        // call is about is not there any more, or a position that was one when the call was asked for is not one in the scene that the earlier calls left. Nothing else is right: not an
        // unknown outcome, not a change that the model could not show, not a time-out.
        private static bool IsExpected(Exception exception)
        {
            return exception is ObsResponseException or ObsRemovedException or ObsNotReadyException or ArgumentOutOfRangeException;
        }

        private static async Task<string?> WalkAsync(int seed, int steps, Dictionary<string, int> statistics)
        {
            var random = new Random(seed);
            var log = new List<string>();
            var unexpected = new List<string>();
            var rig = new StudioSimRig(Quirks(seed), null, seed);
            try
            {
                await rig.ConnectAsync().WithTimeoutAsync(Patience);
                await rig.ConvergedAsync("the first read", Patience);
                rig.World(world =>
                {
                    var gaming = world.CreateScene("Gaming");
                    world.CreateInput(gaming, "Webcam", Colour);
                    world.CreateInput(gaming, "Mic", Mic);
                    world.CreateInput(gaming, "Backdrop", Colour);
                    world.CreateScene("Chatting");
                });
                await rig.ConvergedAsync("the scenes and the inputs", Patience);

                for (var step = 0; step < steps; step++)
                {
                    log.Add($"-- step {step}");
                    var calls = new List<Task>();
                    var together = random.Next(1, 4);
                    for (var i = 0; i < together; i++)
                    {
                        if (Pick(rig, random, log) is { } call)
                        {
                            calls.Add(Observe(call.Description, call.Run, log, unexpected));
                        }
                    }

                    if (random.Next(3) == 0)
                    {
                        UserChange(rig, random, log);
                    }

                    await Task.WhenAll(calls);
                    if (unexpected.Count > 0)
                    {
                        return Report("A call ended in a way that it must not", unexpected, log);
                    }

                    if (random.Next(2) == 0)
                    {
                        await rig.ConvergedAsync($"step {step}", Patience);
                    }
                }

                await rig.ConvergedAsync("the end of the walk", Patience);
                await StudioRig.EventuallyAsync(() => rig.Studio.Host.ConfirmWaiterCount == 0 && rig.Studio.Host.SettleWaiterCount == 0, "the waits in the model to end", Patience);
                return null;
            }
            catch (Exception ex)
            {
                return Report($"The walk failed: {ex.GetType().Name}: {ex.Message}", unexpected, log);
            }
            finally
            {
                lock (log)
                {
                    foreach (var kind in new[] { "ok", "refused", "user" })
                    {
                        statistics[kind] = log.Count(line => line.StartsWith(kind, StringComparison.Ordinal));
                    }

                    statistics["steps"] = log.Count(line => line.StartsWith("-- step", StringComparison.Ordinal));
                }

                await rig.DisposeAsync();
            }
        }

        private static string Report(string what, List<string> unexpected, List<string> log)
        {
            var text = new StringBuilder(what);
            foreach (var line in unexpected)
            {
                text.AppendLine().Append("  unexpected: ").Append(line);
            }

            text.AppendLine().Append("  the last calls:");
            foreach (var line in log.TakeLast(70))
            {
                text.AppendLine().Append("    ").Append(line);
            }

            return text.ToString();
        }

        private static async Task Observe(string description, Func<Task> run, List<string> log, List<string> unexpected)
        {
            try
            {
                await run().WithTimeoutAsync(Patience);
                lock (log)
                {
                    log.Add($"ok       {description}");
                }
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                lock (log)
                {
                    log.Add($"refused  {description}: {ex.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                lock (unexpected)
                {
                    unexpected.Add($"{description}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static T? One<T>(IReadOnlyList<T> items, Random random)
            where T : class
        {
            return items.Count == 0 ? null : items[random.Next(items.Count)];
        }

        // One call that the application can ask for now, chosen from what the model shows (some choices find nothing to ask for: another is tried).
        private static (string Description, Func<Task> Run)? Pick(StudioSimRig rig, Random random, List<string> log)
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                if (PickOnce(rig, random, log) is { } call)
                {
                    return call;
                }
            }

            return null;
        }

        // A real OBS Studio never answers SetSceneName for a scene that was removed and is kept alive (fact 9). A scene that the user removes while the rename is under way gets no
        // answer, and the call says that the outcome is not known, which is right: it is the walk that raced the user. The world says whether that is what happened.
        private static async Task RenameSceneAsync(StudioSimRig rig, ObsScene scene, string name)
        {
            try
            {
                await scene.RenameAsync(name);
            }
            catch (ObsUnknownOutcomeException) when (rig.Server.Run(sim => sim.Snapshot()).DetachedScenes.Any(detached => detached.Uuid == scene.Uuid))
            {
                throw new ObsRemovedException($"scene '{name}'", ObsObjectState.Detached);
            }
        }

        // One call that the application can ask for now, chosen from what the model shows: what it is about can be gone by the time that it has its turn.
        private static (string Description, Func<Task> Run)? PickOnce(StudioSimRig rig, Random random, List<string> log)
        {
            var studio = rig.Studio;
            var scenes = studio.Scenes.ToArray();
            var inputs = studio.Inputs.ToArray();
            var scene = One(scenes, random);
            var item = scene is null ? null : One(scene.Items, random);
            var input = One(inputs, random);
            var flag = random.Next(2) == 0;
            var sceneName = SceneNames[random.Next(SceneNames.Length)];
            var inputName = InputNames[random.Next(InputNames.Length)];
            (string, Func<Task>)? call = null;
            switch (random.Next(19))
            {
                case 0 when item is not null:
                    call = ($"scene item {item.Id} of '{item.Scene.Name}': enabled {flag}", () => item.SetEnabledAsync(flag));
                    break;
                case 1 when item is not null:
                    call = ($"scene item {item.Id} of '{item.Scene.Name}': toggle", () => item.ToggleEnabledAsync());
                    break;
                case 2 when item is not null:
                    call = ($"scene item {item.Id} of '{item.Scene.Name}': locked {flag}", () => item.SetLockedAsync(flag));
                    break;
                case 3 when item is not null:
                    call = ($"scene item {item.Id} of '{item.Scene.Name}': move", () => item.SetIndexAsync(Math.Max(0, random.Next(Math.Max(1, item.Scene.Items.Count)))));
                    break;
                case 4 when item is not null:
                    call = ($"scene item {item.Id} of '{item.Scene.Name}': remove", () => item.RemoveAsync());
                    break;
                case 5 when input is { HasAudio: true }:
                    call = ($"input '{input.Name}': muted {flag}", () => input.SetMutedAsync(flag));
                    break;
                case 6 when input is { HasAudio: true }:
                    call = ($"input '{input.Name}': toggle mute", () => input.ToggleMuteAsync());
                    break;
                case 7 when input is { HasAudio: true }:
                    var db = Math.Round(-60 + (random.NextDouble() * 60), 1);
                    call = ($"input '{input.Name}': volume {db} dB", () => input.SetVolumeDbAsync(db));
                    break;
                case 8 when scene is not null:
                    call = ($"scene '{scene.Name}': switch to", () => scene.SwitchToAsync());
                    break;
                case 9:
                    call = ($"make scene '{sceneName}'", () => studio.CreateSceneAsync(sceneName));
                    break;
                case 10 when scene is not null:
                    var kind = flag ? Colour : Mic;
                    call = ($"scene '{scene.Name}': make input '{inputName}' ({kind})", () => scene.CreateInputAsync(inputName, kind, enabled: flag));
                    break;
                case 11 when scene is not null:
                    var source = flag ? (ObsSource?)One(scenes, random) : input;
                    if (source is not null && !ReferenceEquals(source, scene))
                    {
                        call = ($"scene '{scene.Name}': add {source.Label}", () => scene.AddAsync(source, flag));
                    }

                    break;
                case 12 when scene is not null:
                    call = ($"scene '{scene.Name}': rename to '{sceneName}'", () => RenameSceneAsync(rig, scene, sceneName));
                    break;
                case 13 when input is not null:
                    call = ($"input '{input.Name}': rename to '{inputName}'", () => input.RenameAsync(inputName));
                    break;
                case 14 when scene is not null:
                    call = ($"scene '{scene.Name}': remove", () => scene.RemoveAsync());
                    break;
                case 15 when input is not null:
                    call = ($"input '{input.Name}': remove", () => input.RemoveAsync());
                    break;
                case 16 when input is not null:
                    call = ($"input '{input.Name}': enabled {flag} everywhere", () => input.SetEnabledInAllScenesAsync(flag));
                    break;
                case 17 when item is { Source: ObsInput shown }:
                    // Through a scene item, so that an input that OBS Studio removed and keeps alive is reached too.
                    call = ($"input '{shown.Name}' ({shown.State}) by a scene item: remove", () => shown.RemoveAsync());
                    break;
                case 18 when item is { Source: ObsScene shownScene }:
                    call = ($"scene '{shownScene.Name}' ({shownScene.State}) by a scene item: remove", () => shownScene.RemoveAsync());
                    break;
                default:
                    break;
            }

            if (call is { } picked)
            {
                lock (log)
                {
                    log.Add($"asks      {picked.Item1}");
                }

                return (picked.Item1, picked.Item2);
            }

            return null;
        }

        // What the user does in OBS Studio meanwhile: the connection gets the events, and the calls that are under way find the world changed. A refusal of OBS Studio is nothing to
        // the walk (the user chose something that is not there any more), so none of it ends the walk.
        private static void UserChange(StudioSimRig rig, Random random, List<string> log)
        {
            var studio = rig.Studio;
            var scene = One(studio.Scenes.ToArray(), random);
            var input = One(studio.Inputs.ToArray(), random);
            var sceneName = SceneNames[random.Next(SceneNames.Length)];
            var inputName = InputNames[random.Next(InputNames.Length)];
            Action<SimExternal>? change = null;
            var what = string.Empty;
            switch (random.Next(9))
            {
                case 0:
                    what = $"the user makes scene '{sceneName}'";
                    change = world => world.CreateScene(sceneName);
                    break;
                case 1 when scene is not null:
                    what = $"the user removes scene '{scene.Name}'";
                    change = world => world.RemoveScene(scene.Uuid);
                    break;
                case 2 when scene is not null:
                    what = $"the user renames scene '{scene.Name}' to '{sceneName}'";
                    change = world => world.RenameScene(scene.Uuid, sceneName);
                    break;
                case 3 when scene is not null:
                    what = $"the user makes input '{inputName}' in scene '{scene.Name}'";
                    change = world => world.CreateInput(scene.Uuid, inputName, random.Next(2) == 0 ? Colour : Mic);
                    break;
                case 4 when input is not null:
                    what = $"the user removes input '{input.Name}'";
                    change = world => world.RemoveInput(input.Uuid);
                    break;
                case 5 when input is not null:
                    what = $"the user renames input '{input.Name}' to '{inputName}'";
                    change = world => world.RenameInput(input.Uuid, inputName);
                    break;
                case 6 when scene is not null && input is not null:
                    what = $"the user puts input '{input.Name}' in scene '{scene.Name}'";
                    change = world => world.AddItem(scene.Uuid, input.Uuid);
                    break;
                case 7 when scene is not null && One(scene.Items, random) is { } victim:
                    what = $"the user removes scene item {victim.Id} of scene '{scene.Name}'";
                    change = world => world.RemoveItem(scene.Uuid, victim.Id);
                    break;
                case 8 when scene is not null && One(scene.Items, random) is { } toggled:
                    what = $"the user hides scene item {toggled.Id} of scene '{scene.Name}'";
                    change = world => world.SetItemEnabled(scene.Uuid, toggled.Id, false);
                    break;
                default:
                    break;
            }

            if (change is null)
            {
                return;
            }

            try
            {
                rig.World(change);
                lock (log)
                {
                    log.Add($"user      {what}");
                }
            }
            catch (Exception ex)
            {
                lock (log)
                {
                    log.Add($"user      {what}: OBS Studio refuses ({ex.GetType().Name})");
                }
            }
        }
    }
}
