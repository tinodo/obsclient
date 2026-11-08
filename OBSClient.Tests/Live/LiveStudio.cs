namespace OBSStudioClient.Tests.Live
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using Xunit.Abstractions;

    /// <summary>
    /// The studio layer (<see cref="ObsStudio"/>) connected to the real OBS Studio of the live tests, next to a connection of the plain client that guards it, cleans up after it, and is
    /// what the model is compared with: after each step of a test the model has to equal what OBS Studio holds.
    /// </summary>
    /// <remarks>
    /// The plain connection is made first, with the checks of <see cref="LiveSession.ConnectAsync"/>: an OBS Studio that is not the one that was made for the run is refused and left as it is.
    /// Everything that a test makes carries the name prefix of <see cref="LiveObs.NamePrefix"/>, and is removed again when this is disposed of; the program scene is put back.
    /// </remarks>
    internal sealed class LiveStudio : IAsyncDisposable
    {
        // How long the model may take to equal what OBS Studio holds: the list of scenes of OBS Studio lags behind its events by up to about two seconds, and a busy machine adds to that.
        private static readonly TimeSpan EqualTimeout = TimeSpan.FromSeconds(20);

        private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(60);

        private static readonly SemaphoreSlim WarmUpTurn = new(1, 1);

        private static bool _warm;

        private readonly Guid _originalProgramScene;

        // The inputs that the model announced as removed. OBS Studio goes on listing some of them (it keeps an input alive that was removed, also when no scene item shows it any more: seen with
        // the plain client alone too, about once in a few dozen rapid removals), and the model does not show an input that was removed: that is not a difference.
        private readonly HashSet<Guid> _removedInputs = [];

        private LiveStudio(LiveSession raw, ObsStudio studio, string obsVersion, string webSocketVersion, Guid originalProgramScene)
        {
            this.Raw = raw;
            this.Studio = studio;
            this.ObsVersionText = obsVersion;
            this.WebSocketVersionText = webSocketVersion;
            this.ObsVersion = ParseVersion(obsVersion);
            this.WebSocketVersion = ParseVersion(webSocketVersion);
            this._originalProgramScene = originalProgramScene;
            studio.Changed += this.OnChanged;
        }

        /// <summary>Gets the names of the inputs that OBS Studio still listed at the last comparison, and that the model does not show because it announced them as removed.</summary>
        public IReadOnlyList<string> StillListedRemovedInputs { get; private set; } = [];

        /// <summary>Gets the connection of the plain client: the guard, the clean-up, and the truth.</summary>
        public LiveSession Raw { get; }

        /// <summary>Gets the plain client of <see cref="Raw"/>.</summary>
        public ObsClient Client => this.Raw.Client;

        /// <summary>Gets the studio layer, which has a connection of its own.</summary>
        public ObsStudio Studio { get; }

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
                for (var attempt = 0; attempt < 20 && fast < 3; attempt++)
                {
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

                    fast = clock.Elapsed < TimeSpan.FromMilliseconds(500) ? fast + 1 : 0;
                }

                _warm = true;
            }
            finally
            {
                WarmUpTurn.Release();
            }
        }

        /// <summary>Connects the plain client (with the checks of the live tests) and the studio layer, and waits until the model is read.</summary>
        /// <returns>The connections.</returns>
        public static async Task<LiveStudio> ConnectAsync(ITestOutputHelper? output = null)
        {
            var raw = await LiveSession.ConnectAsync(EventSubscriptions.None, removeLeftovers: true);
            await WarmUpAsync(raw.Client);
            ObsStudio? studio = null;
            try
            {
                var version = await raw.Client.GetVersion();
                var program = await raw.Client.GetCurrentProgramScene();

                // The answers of OBS Studio can take seconds when its user interface thread is busy (see LiveSession), and the defaults are for a machine that is not.
                studio = new ObsStudio(
                    new ObsStudioOptions
                    {
                        ReadTimeout = TimeSpan.FromSeconds(30),
                        WriteTimeout = TimeSpan.FromSeconds(30),
                        SynchronizationTimeout = TimeSpan.FromSeconds(60),
                    },
                    output is null ? null : new StampedLoggerFactory(output));
                studio.Client.RequestTimeout = 30000;
                await studio.ConnectAsync(LiveObs.Host, LiveObs.Port!.Value, LiveObs.Password);
                await studio.WaitUntilSettledAsync(SettleTimeout);
                return new LiveStudio(raw, studio, version.ObsVersion, version.ObsWebSocketVersion, program.SceneUuid);
            }
            catch
            {
                if (studio is not null)
                {
                    await studio.DisposeAsync();
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
            var studio = this.Studio;
            if (studio.State != ObsStudioState.Synchronized)
            {
                problems.Add($"the model is {studio.State}, not Synchronized");
            }

            var program = await this.Client.GetCurrentProgramScene();
            var sceneList = await this.Client.GetSceneList();
            var inputs = await this.Client.GetInputList();

            // The scenes, in the order of OBS Studio.
            var modelScenes = studio.Scenes.ToArray();
            Compare(
                problems,
                "the scenes",
                sceneList.Scenes.OrderBy(scene => scene.SceneIndex ?? 0).Select(scene => $"{scene.SceneUuid:D} '{scene.SceneName}'"),
                modelScenes.Select(scene => $"{scene.Uuid:D} '{scene.Name}'"));
            if (studio.ProgramScene?.Uuid != program.SceneUuid)
            {
                problems.Add($"the program scene is '{program.SceneName}' in OBS Studio and '{studio.ProgramScene?.Name}' in the model");
            }

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

                Compare(
                    problems,
                    $"the scene items of '{scene.Name}'",
                    obsItems.OrderBy(item => item.SceneItemId).Select(item => $"{item.SceneItemId}: '{item.SourceName}' {item.SourceUuid:D} index {item.SceneItemIndex}, enabled {item.SceneItemEnabled}, locked {item.SceneItemLocked}"),
                    scene.Items.OrderBy(item => item.Id).Select(item => $"{item.Id}: '{item.Source.Name}' {item.Source.Uuid:D} index {item.Index}, enabled {item.IsEnabled}, locked {item.IsLocked}"));
            }

            // The inputs: OBS Studio lists the ones that it keeps alive after they were removed too, and the model shows those through the scene items that keep them alive.
            var stillListed = new List<string>();
            foreach (var input in inputs)
            {
                var uuid = input.InputUuid ?? Guid.Empty;
                if (studio.Inputs.TryGet(uuid, out var shown))
                {
                    if (!string.Equals(shown.Name, input.InputName, StringComparison.Ordinal) || !string.Equals(shown.Kind, input.InputKind, StringComparison.Ordinal))
                    {
                        problems.Add($"the input {uuid:D} is '{input.InputName}' ({input.InputKind}) in OBS Studio and '{shown.Name}' ({shown.Kind}) in the model");
                    }
                }
                else if (IsKeptAliveInTheModel(modelScenes, uuid))
                {
                    continue;
                }
                else if (this.WasAnnouncedRemoved(uuid))
                {
                    stillListed.Add(input.InputName);
                }
                else
                {
                    problems.Add($"OBS Studio lists the input '{input.InputName}' ({uuid:D}), and the model shows none");
                }
            }

            this.StillListedRemovedInputs = stillListed;

            foreach (var shown in studio.Inputs)
            {
                if (!inputs.Any(input => input.InputUuid == shown.Uuid))
                {
                    problems.Add($"the model shows the input '{shown.Name}' ({shown.Uuid:D}), and OBS Studio does not list it");
                }
            }

            return problems;
        }

        /// <summary>Waits until the model equals what OBS Studio holds, and fails with the differences when it does not in time.</summary>
        /// <param name="when">Where the test is, for the message: "after the input was made".</param>
        /// <param name="timeout">How long to wait; <see cref="EqualTimeout"/> when null.</param>
        /// <returns>A task that completes when they are equal.</returns>
        public async Task AssertModelEqualsObsAsync(string when, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? EqualTimeout);
            IReadOnlyList<string> problems;
            while (true)
            {
                problems = await this.DifferencesAsync();
                if (problems.Count == 0 || DateTime.UtcNow >= deadline)
                {
                    break;
                }

                await Task.Delay(250);
            }

            Assert.True(problems.Count == 0, $"The model does not equal what OBS Studio holds {when}: {string.Join("; ", problems)}");
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
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
                await this.Studio.DisposeAsync();
                await this.Raw.DisposeAsync();
            }
        }

        private static bool IsKeptAliveInTheModel(IEnumerable<ObsScene> scenes, Guid uuid)
        {
            return scenes.SelectMany(scene => scene.Items).Any(item => item.Source is ObsInput { State: ObsObjectState.Detached } source && source.Uuid == uuid);
        }

        private void OnChanged(object? sender, ObsChangedEventArgs e)
        {
            if (e.Kind == ObsChangeKind.Removed && e.Entity is ObsInput input)
            {
                lock (this._removedInputs)
                {
                    this._removedInputs.Add(input.Uuid);
                }
            }
        }

        private bool WasAnnouncedRemoved(Guid uuid)
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

        private static Version ParseVersion(string text)
        {
            var end = text.IndexOfAny(['-', '+', ' ']);
            return Version.TryParse(end < 0 ? text : text[..end], out var version) ? version : new Version(0, 0);
        }

        // What the studio layer and its client log, with the time in milliseconds since the connection was made, in the output of the test. With OBS_LIVE_WIRE_LOG=1 that includes the messages that were
        // sent and received (the debug level), which is what a slow OBS Studio looks like from the outside; without it the log is only what the studio layer says about itself (information and up),
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
