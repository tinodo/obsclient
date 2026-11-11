namespace OBSStudioClient.Tests.Live
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// The model (<see cref="ObsModel"/>) connected to the real OBS Studio of the live tests, next to a connection of the plain client that guards it, cleans up after it, and is
    /// what the model is compared with: after each step of a test the model has to equal what OBS Studio holds.
    /// </summary>
    /// <remarks>
    /// The plain connection is made first, with the checks of <see cref="LiveSession.ConnectAsync"/>: an OBS Studio that is not the one that was made for the run is refused and left as it is.
    /// Everything that a test makes carries the name prefix of <see cref="LiveObs.NamePrefix"/>, and is removed again when this is disposed of; the program scene is put back.
    /// </remarks>
    internal sealed class LiveModel : IAsyncDisposable
    {
        // A fresh OBS Studio is warm when it made and removed a scene within this time, so many times in a row (see WarmUpAsync), in at most so many attempts.
        private const int WarmUpAttempts = 20;

        private const int WarmUpFastAnswers = 3;

        // How long the model may take to equal what OBS Studio holds: the list of scenes of OBS Studio lags behind its events by up to about two seconds, and a busy machine adds to that.
        private static readonly TimeSpan EqualTimeout = TimeSpan.FromSeconds(20);

        private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(60);

        private static readonly TimeSpan WarmUpFastAnswer = TimeSpan.FromMilliseconds(500);

        private static readonly SemaphoreSlim WarmUpTurn = new(1, 1);

        private static bool _warm;

        private readonly Guid _originalProgramScene;

        // The inputs that OBS Studio announced as removed on the plain connection, which does not depend on the model that is compared. OBS Studio goes on listing some of them (it keeps an input alive that was
        // removed, also when no scene item shows it any more: seen with the plain client alone too, about once in a few dozen rapid removals), and the model does not show an input that was removed: that is not a
        // difference. An input that the model does not show while OBS Studio did not say that it was removed is one.
        private readonly HashSet<Guid> _removedInputs = [];

        private LiveModel(LiveSession raw, ObsModel model, string obsVersion, string webSocketVersion, Guid originalProgramScene)
        {
            this.Raw = raw;
            this.Model = model;
            this.ObsVersionText = obsVersion;
            this.WebSocketVersionText = webSocketVersion;
            this.ObsVersion = ParseVersion(obsVersion);
            this.WebSocketVersion = ParseVersion(webSocketVersion);
            this._originalProgramScene = originalProgramScene;
            raw.Client.InputRemoved += this.OnInputRemoved;
        }

        /// <summary>Gets the names of the inputs that OBS Studio still listed at the last comparison, and that the model does not show because OBS Studio announced them as removed.</summary>
        public IReadOnlyList<string> StillListedRemovedInputs { get; private set; } = [];

        /// <summary>Gets the connection of the plain client: the guard, the clean-up, and the truth.</summary>
        public LiveSession Raw { get; }

        /// <summary>Gets the plain client of <see cref="Raw"/>.</summary>
        public ObsClient Client => this.Raw.Client;

        /// <summary>Gets the model, which has a connection of its own.</summary>
        public ObsModel Model { get; }

        /// <summary>Gets the version of OBS Studio as it reports it.</summary>
        public string ObsVersionText { get; }

        /// <summary>Gets the version of obs-websocket as it reports it.</summary>
        public string WebSocketVersionText { get; }

        /// <summary>Gets the version of OBS Studio (a suffix such as -rc1 is not part of the number).</summary>
        public Version ObsVersion { get; }

        /// <summary>Gets the version of obs-websocket (a suffix is not part of the number).</summary>
        public Version WebSocketVersion { get; }

        // A fresh OBS Studio is slow for its first changes: its user interface thread is away for seconds (8, 13 and 23 seconds were seen on a copy that had just been started, and nothing like
        // it after that), and its list of scenes is made from the user interface, so the list is late for as long. The first connection makes and removes scenes until OBS Studio answers
        // at once, so that no test pays for it (what the model does when the list is late is pinned by the tests of the engine, which can say how late).
        private static async Task WarmUpAsync(ObsClient client)
        {
            await WarmUpTurn.WaitAsync();
            try
            {
                if (_warm)
                {
                    return;
                }

                var fast = 0;
                var attempts = 0;
                while (attempts < WarmUpAttempts && fast < WarmUpFastAnswers)
                {
                    attempts++;
                    var clock = Stopwatch.StartNew();
                    var name = LiveObs.UniqueName("warm");
                    var uuid = await client.CreateScene(name);
                    try
                    {
                        await client.RemoveScene(uuid == Guid.Empty ? SceneRef.FromName(name) : SceneRef.FromUuid(uuid));
                    }
                    catch (ObsResponseException)
                    {
                        // Being removed already: the leftovers are removed when the connection is made.
                    }

                    fast = clock.Elapsed < WarmUpFastAnswer ? fast + 1 : 0;
                }

                // A warm-up that ran out of attempts is not a warm-up: say so now, and let the next connection try again, rather than have every test fail later on a synchronization that was too slow.
                if (fast < WarmUpFastAnswers)
                {
                    throw new InvalidOperationException(
                        $"OBS Studio did not make and remove a scene within {WarmUpFastAnswer.TotalMilliseconds:0} ms, {WarmUpFastAnswers} times in a row, in {attempts} attempts: it is too busy (or stuck) for the live tests of the model. "
                        + "A fresh OBS Studio is slow for its first changes for seconds, not for this long.");
                }

                _warm = true;
            }
            finally
            {
                WarmUpTurn.Release();
            }
        }

        /// <summary>Connects the plain client (with the checks of the live tests) and the model, and waits until the model is read.</summary>
        /// <param name="output">Where the log of the model goes, or null.</param>
        /// <param name="highVolume">What the model follows of what OBS Studio sends often.</param>
        /// <returns>The connections.</returns>
        public static async Task<LiveModel> ConnectAsync(ITestOutputHelper? output = null, ObsHighVolume highVolume = ObsHighVolume.None)
        {
            var raw = await LiveSession.ConnectAsync(EventSubscriptions.Inputs, removeLeftovers: true);
            ObsModel? model = null;
            try
            {
                await WarmUpAsync(raw.Client);
                var version = await raw.Client.GetVersion();
                var program = await raw.Client.GetCurrentProgramScene();

                // The answers of OBS Studio can take seconds when its user interface thread is busy (see LiveSession), and the defaults are for a machine that is not.
                model = new ObsModel(
                    new ObsModelOptions
                    {
                        ReadTimeout = TimeSpan.FromSeconds(30),
                        WriteTimeout = TimeSpan.FromSeconds(30),
                        SynchronizationTimeout = TimeSpan.FromSeconds(60),
                        HighVolume = highVolume,
                    },
                    output is null ? null : new StampedLoggerFactory(output));
                model.Client.RequestTimeout = 30000;
                await model.ConnectAsync(LiveObs.Host, LiveObs.Port!.Value, LiveObs.Password);
                await model.WaitUntilSettledAsync(SettleTimeout);
                return new LiveModel(raw, model, version.ObsVersion, version.ObsWebSocketVersion, program.SceneUuid);
            }
            catch
            {
                if (model is not null)
                {
                    await model.DisposeAsync();
                }

                await raw.DisposeAsync();
                throw;
            }
        }

        /// <summary>Finds a kind of input that has no source of its own and no audio: the color source, in the newest version that OBS Studio offers.</summary>
        /// <returns>The kind.</returns>
        public async Task<string> ColorKindAsync()
        {
            var kind = (await this.Client.GetInputKindList())
                .Where(candidate => candidate.StartsWith("color_source", StringComparison.Ordinal))
                .OrderByDescending(candidate => candidate, StringComparer.Ordinal)
                .FirstOrDefault();
            Assert.True(kind is not null, "OBS Studio offers no color source input.");
            return kind!;
        }

        /// <summary>Reads what OBS Studio holds with the plain client, and says in what the model differs from it.</summary>
        /// <returns>The differences; none when the model equals what OBS Studio holds.</returns>
        public async Task<IReadOnlyList<string>> DifferencesAsync()
        {
            var problems = new List<string>();
            var model = this.Model;
            if (model.State != ObsModelState.Synchronized)
            {
                problems.Add($"the model is {model.State}, not Synchronized");
            }

            var program = await this.Client.GetCurrentProgramScene();
            var sceneList = await this.Client.GetSceneList();
            var inputs = await this.Client.GetInputList();

            // The scenes, in the order of OBS Studio.
            var modelScenes = model.Scenes.ToArray();
            Compare(
                problems,
                "the scenes",
                sceneList.Scenes.OrderBy(scene => scene.SceneIndex ?? 0).Select(scene => $"{scene.SceneUuid:D} '{scene.SceneName}'"),
                modelScenes.Select(scene => $"{scene.Uuid:D} '{scene.Name}'"));
            if (model.ProgramScene?.Uuid != program.SceneUuid)
            {
                problems.Add($"the program scene is '{program.SceneName}' in OBS Studio and '{model.ProgramScene?.Name}' in the model");
            }

            await this.CompareStudioModeAndTransitionsAsync(problems);

            // The scene items of every scene, with what they show.
            foreach (var scene in modelScenes)
            {
                SceneItem[] obsItems;
                try
                {
                    obsItems = [.. await this.Client.GetSceneItemList(SceneRef.FromUuid(scene.Uuid))];
                }
                catch (ObsResponseException exception)
                {
                    problems.Add($"the scene items of '{scene.Name}' could not be read: {exception.Message}");
                    continue;
                }

                var follows = model.HighVolume.HasFlag(ObsHighVolume.Transforms);
                Compare(
                    problems,
                    $"the scene items of '{scene.Name}'",
                    obsItems.OrderBy(item => item.SceneItemId).Select(item => $"{item.SceneItemId}: '{item.SourceName}' {item.SourceUuid:D} index {item.SceneItemIndex}, enabled {item.SceneItemEnabled}, locked {item.SceneItemLocked}{(follows ? ", " + (item.SceneItemTransform is { } obsTransform ? TransformTexts.Of(obsTransform) : "(no transform)") : string.Empty)}"),
                    scene.Items.OrderBy(item => item.Id).Select(item => $"{item.Id}: '{item.Source.Name}' {item.Source.Uuid:D} index {item.Index}, enabled {item.IsEnabled}, locked {item.IsLocked}{(follows ? ", " + (item.Transform is { } held ? TransformTexts.Of(held) : "(no transform)") : string.Empty)}"));
            }

            // The filters of every scene and input.
            foreach (var source in modelScenes.Cast<ObsSource>().Concat(model.Inputs))
            {
                Filter[] obsFilters;
                try
                {
                    obsFilters = await this.Client.GetSourceFilterList(SourceRef.FromUuid(source.Uuid));
                }
                catch (ObsResponseException exception)
                {
                    problems.Add($"the filters of '{source.Name}' could not be read: {exception.Message}");
                    continue;
                }

                Compare(
                    problems,
                    $"the filters of '{source.Name}'",
                    obsFilters.OrderBy(filter => filter.FilterIndex).Select(filter => $"{filter.FilterIndex}: '{filter.FilterName}' {filter.FilterKind}, enabled {filter.FilterEnabled}"),
                    source.Filters.Select(filter => $"{filter.Index}: '{filter.Name}' {filter.Kind}, enabled {filter.IsEnabled}"));
            }

            // The inputs: OBS Studio lists the ones that it keeps alive after they were removed too, and the model shows those through the scene items that keep them alive. What the model does not show has
            // to be what OBS Studio announced as removed: the model is not its own witness.
            var stillListed = new List<string>();
            foreach (var input in inputs)
            {
                var uuid = input.InputUuid ?? Guid.Empty;
                if (model.Inputs.TryGet(uuid, out var shown))
                {
                    if (!string.Equals(shown.Name, input.InputName, StringComparison.Ordinal) || !string.Equals(shown.Kind, input.InputKind, StringComparison.Ordinal))
                    {
                        problems.Add($"the input {uuid:D} is '{input.InputName}' ({input.InputKind}) in OBS Studio and '{shown.Name}' ({shown.Kind}) in the model");
                    }
                }
                else if (!this.ObsAnnouncedRemoved(uuid))
                {
                    problems.Add($"OBS Studio lists the input '{input.InputName}' ({uuid:D}), and the model shows none, and OBS Studio did not announce that it was removed");
                }
                else if (!IsKeptAliveInTheModel(modelScenes, uuid))
                {
                    stillListed.Add(input.InputName);
                }
            }

            this.StillListedRemovedInputs = stillListed;

            // The settings of every input, and the details of the audio of the ones that have audio.
            foreach (var shown in model.Inputs)
            {
                var reference = InputRef.FromUuid(shown.Uuid);
                try
                {
                    var settings = await this.Client.GetInputSettings(reference);
                    Compare(problems, $"the settings of '{shown.Name}'", [Canonical(settings.InputSettings)], [Canonical(shown.Settings)]);
                    if (shown.HasAudio)
                    {
                        var balance = await this.Client.GetInputAudioBalance(reference);
                        var offset = await this.Client.GetInputAudioSyncOffset(reference);
                        var monitor = await this.Client.GetInputAudioMonitorType(reference);
                        var tracks = await this.Client.GetInputAudioTracks(reference);
                        Compare(
                            problems,
                            $"the audio details of '{shown.Name}'",
                            [$"balance {balance:R}, sync offset {offset} ms, monitor {ModelEnums.ToModel(monitor)}, tracks {(ObsAudioTracks)ModelEnums.ToMask(tracks)}"],
                            [$"balance {shown.AudioBalance:R}, sync offset {(int)shown.SyncOffset.TotalMilliseconds} ms, monitor {shown.MonitorType}, tracks {shown.AudioTracks}"]);
                    }

                    if (shown.Media is { } media)
                    {
                        var status = await this.Client.GetMediaInputStatus(reference);
                        Compare(problems, $"the media of '{shown.Name}'", [ModelEnums.ToModel(status.MediaState).ToString()], [media.State.ToString()]);
                    }

                    if (model.HighVolume.HasFlag(ObsHighVolume.Activity))
                    {
                        var activity = await this.Client.GetSourceActive(SourceRef.FromUuid(shown.Uuid));
                        Compare(problems, $"the activity of '{shown.Name}'", [$"on the air {activity.VideoActive}, shown {activity.VideoShowing}"], [$"on the air {shown.IsActive}, shown {shown.IsShowing}"]);
                    }
                }
                catch (ObsResponseException exception)
                {
                    problems.Add($"the settings, the audio or the media of '{shown.Name}' could not be read: {exception.Message}");
                }
            }

            foreach (var shown in model.Inputs)
            {
                if (!inputs.Any(input => input.InputUuid == shown.Uuid))
                {
                    problems.Add($"the model shows the input '{shown.Name}' ({shown.Uuid:D}), and OBS Studio does not list it");
                }
            }

            return problems;
        }

        // Studio mode, the preview scene and the current transition: what OBS Studio says with the plain client, and what the model shows. A transition runs while its position (the cursor) is not 1.
        private async Task CompareStudioModeAndTransitionsAsync(List<string> problems)
        {
            var model = this.Model;
            var studio = await this.Client.GetStudioModeEnabled();
            if (model.IsStudioModeEnabled != studio)
            {
                problems.Add($"studio mode is {(studio ? "on" : "off")} in OBS Studio and {(model.IsStudioModeEnabled ? "on" : "off")} in the model");
            }

            (Guid Uuid, string Name)? preview = null;
            if (studio)
            {
                try
                {
                    var asked = await this.Client.GetCurrentPreviewScene();
                    preview = (asked.SceneUuid, asked.SceneName);
                }
                catch (ObsResponseException exception) when (exception.ErrorCode == RequestStatusCode.StudioModeNotActive)
                {
                    // Switched off since the question before: the next comparison says.
                }
            }

            if (model.PreviewScene?.Uuid != preview?.Uuid)
            {
                problems.Add($"the preview scene is '{preview?.Name}' in OBS Studio and '{model.PreviewScene?.Name}' in the model");
            }

            var transition = await this.Client.GetCurrentSceneTransition();
            var current = model.Transitions.Current;
            Compare(
                problems,
                "the current scene transition",
                [$"{transition.TransitionUuid:D} '{transition.TransitionName}' {transition.TransitionKind}, fixed {transition.TransitionFixed}, configurable {transition.TransitionConfigurable}"],
                current is null ? [] : [$"{current.Uuid:D} '{current.Name}' {current.Kind}, fixed {current.IsFixed}, configurable {current.IsConfigurable}"]);
            if (transition.TransitionDuration is { } milliseconds && model.Transitions.Duration != TimeSpan.FromMilliseconds(Math.Round(milliseconds)))
            {
                problems.Add($"the duration of the scene transitions is {milliseconds} ms in OBS Studio and {model.Transitions.Duration?.TotalMilliseconds.ToString() ?? "unknown"} ms in the model");
            }

            var running = await this.Client.GetCurrentSceneTransitionCursor() < 1.0;
            if (model.Transitions.IsRunning != running)
            {
                problems.Add($"a scene transition {(running ? "runs" : "does not run")} in OBS Studio and {(model.Transitions.IsRunning ? "runs" : "does not run")} in the model");
            }
        }

        /// <summary>Waits until the model equals what OBS Studio holds and OBS Studio is idle, and fails with the differences when it does not in time.</summary>
        /// <param name="when">Where the test is, for the message: "after the input was made".</param>
        /// <param name="timeout">How long to wait; <see cref="EqualTimeout"/> when null.</param>
        /// <returns>A task that completes when they are equal.</returns>
        /// <remarks>
        /// The model shows the scene that is switched to from the start of a transition (it reads it then), so it equals OBS Studio while the transition still runs. A transition keeps the scenes that
        /// it goes from and to rendered, and OBS Studio takes the scene items of what is removed out of them on its next frame: what a test does next to those scenes races with that. So the wait is
        /// for the end of the transition too.
        /// </remarks>
        public async Task AssertModelEqualsObsAsync(string when, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? EqualTimeout);
            IReadOnlyList<string> problems;
            var idle = false;
            while (true)
            {
                problems = await this.DifferencesAsync();
                idle = problems.Count == 0 && await this.Client.GetCurrentSceneTransitionCursor() >= 1.0;
                if (idle || DateTime.UtcNow >= deadline)
                {
                    break;
                }

                await Task.Delay(250);
            }

            // A transition that still runs when the time is up is a failure as well: what the method promises is an OBS Studio that is idle.
            if (problems.Count == 0 && !idle)
            {
                problems = ["a transition of OBS Studio is still running"];
            }

            Assert.True(problems.Count == 0, $"The model does not equal what OBS Studio holds {when}: {string.Join("; ", problems)}");
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
                // A test that switched studio mode on puts it back; one that failed halfway must not leave it on for the next, because OBS Studio stops answering for seconds when the program scene is removed
                // in studio mode, which is what the clean-up below does.
                try
                {
                    if (await this.Client.GetStudioModeEnabled())
                    {
                        await this.Client.SetStudioModeEnabled(false);
                        await Task.Delay(300);
                    }
                }
                catch (ObsClientException)
                {
                    // Nothing more can be done for it here.
                }

                // What a test made is removed with the plain client (the names carry the prefix), and the scene that was on the program output is put back.
                await this.Raw.RemoveLeftoversAsync();
                try
                {
                    await this.Client.SetCurrentProgramScene(SceneRef.FromUuid(this._originalProgramScene));
                }
                catch (ObsResponseException)
                {
                    // The scene is gone: there is nothing to put back.
                }
            }
            finally
            {
                await this.Model.DisposeAsync();
                await this.Raw.DisposeAsync();
            }
        }

        private static bool IsKeptAliveInTheModel(IEnumerable<ObsScene> scenes, Guid uuid)
        {
            return scenes.SelectMany(scene => scene.Items).Any(item => item.Source is ObsInput { State: ObsObjectState.Detached } source && source.Uuid == uuid);
        }

        private void OnInputRemoved(object? sender, InputNameEventArgs e)
        {
            lock (this._removedInputs)
            {
                this._removedInputs.Add(e.InputUuid);
            }
        }

        private bool ObsAnnouncedRemoved(Guid uuid)
        {
            lock (this._removedInputs)
            {
                return this._removedInputs.Contains(uuid);
            }
        }

        private static void Compare(List<string> problems, string what, IEnumerable<string> inObs, IEnumerable<string> inModel)
        {
            var expected = inObs.ToArray();
            var actual = inModel.ToArray();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                problems.Add($"{what}: OBS Studio has [{string.Join(" | ", expected)}], the model has [{string.Join(" | ", actual)}]");
            }
        }

        /// <summary>Writes the settings that OBS Studio said (the fields of the object) with the fields in order: what is compared is what the fields are, and not how they are written.</summary>
        /// <param name="settings">The settings.</param>
        /// <returns>The JSON.</returns>
        internal static string Canonical(IReadOnlyDictionary<string, object>? settings)
        {
            return Canonical(OBSStudioClient.Model.Engine.SettingsValue.From(settings).Element);
        }

        /// <summary>Writes the settings that the model shows with the fields in order.</summary>
        /// <param name="settings">The settings.</param>
        /// <returns>The JSON.</returns>
        internal static string Canonical(System.Text.Json.JsonElement settings)
        {
            return Simulation.SimJson.Serialize(Simulation.SimJson.Sorted(System.Text.Json.Nodes.JsonNode.Parse(settings.GetRawText()))!);
        }

        private static Version ParseVersion(string text)
        {
            var end = text.IndexOfAny(['-', '+', ' ']);
            return Version.TryParse(end < 0 ? text : text[..end], out var version) ? version : new Version(0, 0);
        }

        // What the model and its client log, with the time in milliseconds since the connection was made, in the output of the test. With OBS_LIVE_WIRE_LOG=1 that includes the messages that were
        // sent and received (the debug level), which is what a slow OBS Studio looks like from the outside; without it the log is only what the model says about itself (information and up),
        // because the output of a test is part of the log of CI.
        private sealed class StampedLoggerFactory(ITestOutputHelper output) : ILoggerFactory
        {
            private readonly Stopwatch _clock = Stopwatch.StartNew();

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public ILogger CreateLogger(string categoryName) => new StampedLogger(output, this._clock, categoryName);

            public void Dispose()
            {
            }
        }

        private sealed class StampedLogger(ITestOutputHelper output, Stopwatch clock, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= (LiveObs.WireLog ? LogLevel.Debug : LogLevel.Information);

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // The extension methods of ILogger (LogDebug and the others) do not ask IsEnabled: the logger has to, or the messages of the wire would be written whatever OBS_LIVE_WIRE_LOG says.
                if (!this.IsEnabled(logLevel))
                {
                    return;
                }

                try
                {
                    output.WriteLine($"[{clock.ElapsedMilliseconds,6}] {logLevel} {category}: {formatter(state, exception)}");
                }
                catch (InvalidOperationException)
                {
                    // The test is over: there is no output to write to.
                }
            }
        }
    }
}
