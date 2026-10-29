namespace OBSStudioClient.Tests.Live
{
    using System.Text.Json;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Tests.Conformance;
    using OBSStudioClient.Tests.TestSupport;
    using OBSStudioClient.TestApp.Analysis;
    using OBSStudioClient.TestApp.Catalog;
    using OBSStudioClient.TestApp.Services;
    using Xunit;

    /// <summary>
    /// Where the real OBS Studio of the live tests is, and whether they may change it. It all comes from the environment, so that
    /// the live tests are skipped wherever there is no OBS Studio (which is almost everywhere), and never touch one by accident.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>OBS_LIVE_PORT</c>: the port of the WebSocket server of OBS Studio. Without it every live test is skipped.</item>
    /// <item><c>OBS_LIVE_HOST</c>: its host (127.0.0.1).</item>
    /// <item><c>OBS_LIVE_PASSWORD</c>: its password; leave it out when the server asks for none.</item>
    /// <item><c>OBS_LIVE_ALLOW_CHANGES=1</c> and <c>OBS_LIVE_COLLECTION</c>: let the tests that create and remove scenes and inputs run, and
    /// only against an OBS Studio whose current scene collection has exactly that name. <c>tools/live/run-live-tests.ps1</c> starts an OBS
    /// Studio of its own, with a scene collection that it names after the run, and sets both: an OBS Studio of somebody else (another
    /// version that is running on the same machine, with a port that is not the one of this run) is refused, and nothing in it is changed.</item>
    /// <item><c>OBS_LIVE_SOAK_SECONDS</c>: how long the soak test runs (<see cref="LiveSoakTests"/>). Without it that test is skipped.</item>
    /// <item><c>OBS_LIVE_UUIDS=0</c>: this OBS Studio has no UUIDs for its scenes and inputs (obs-websocket has them from 5.4.0, OBS Studio from
    /// 30.1.0): the tests that follow them by UUID are skipped. The live workflow sets it for the OBS Studio of Ubuntu 24.04 (30.0.2).</item>
    /// </list>
    /// </remarks>
    internal static class LiveObs
    {
        public const string NotConfigured = "A live test needs a real OBS Studio: set OBS_LIVE_PORT (and OBS_LIVE_PASSWORD). See CONTRIBUTING.md, Live tests.";

        public const string ChangesNotAllowed = "This live test creates and removes scenes and inputs: set OBS_LIVE_ALLOW_CHANGES=1 and OBS_LIVE_COLLECTION (the current scene collection of an OBS Studio that exists for the tests; tools/live/run-live-tests.ps1 sets both). See CONTRIBUTING.md, Live tests.";

        public const string NoPassword = "This live test needs an OBS Studio that asks for a password: set OBS_LIVE_PASSWORD. See CONTRIBUTING.md, Live tests.";

        /// <summary>The start of the name of everything that the live tests create, which is how they recognise what a crashed run left.</summary>
        public const string NamePrefix = "obsclient-live-";

        public static string Host => Environment.GetEnvironmentVariable("OBS_LIVE_HOST") is { Length: > 0 } host ? host : "127.0.0.1";

        public static int? Port => int.TryParse(Environment.GetEnvironmentVariable("OBS_LIVE_PORT"), out var port) ? port : null;

        public static string Password => Environment.GetEnvironmentVariable("OBS_LIVE_PASSWORD") ?? string.Empty;

        /// <summary>Gets the name of the scene collection that the OBS Studio that may be changed has to have as its current one.</summary>
        public static string Collection => Environment.GetEnvironmentVariable("OBS_LIVE_COLLECTION") ?? string.Empty;

        public static bool IsConfigured => Port is not null;

        /// <summary>Gets a value that indicates whether the tests may change the OBS Studio: they are allowed to, and they know which scene collection to expect in it.</summary>
        public static bool MayChange => Environment.GetEnvironmentVariable("OBS_LIVE_ALLOW_CHANGES") == "1" && Collection.Length > 0;

        /// <summary>Gets how long the soak test runs, in seconds (<c>OBS_LIVE_SOAK_SECONDS</c>): 0 when it is off.</summary>
        public static int SoakSeconds => int.TryParse(Environment.GetEnvironmentVariable("OBS_LIVE_SOAK_SECONDS"), out var seconds) && seconds > 0 ? seconds : 0;

        /// <summary>Gets a value that indicates whether the OBS Studio has UUIDs for its scenes and inputs: it does, unless <c>OBS_LIVE_UUIDS</c> is 0 (obs-websocket has them from 5.4.0, OBS Studio from 30.1.0).</summary>
        public static bool HasUuids => Environment.GetEnvironmentVariable("OBS_LIVE_UUIDS") != "0";

        /// <summary>A name for a scene or an input that no other run uses, and that <see cref="LiveSession.RemoveLeftoversAsync"/> recognises.</summary>
        /// <param name="what">What it is called for.</param>
        /// <returns>The name.</returns>
        public static string UniqueName(string what) => $"{NamePrefix}{Guid.NewGuid():N}"[..(NamePrefix.Length + 8)] + "-" + what;
    }

    /// <summary>A test against a real OBS Studio that only reads from it: it is skipped without <c>OBS_LIVE_PORT</c>.</summary>
    internal sealed class LiveFactAttribute : FactAttribute
    {
        public LiveFactAttribute()
        {
            if (!LiveObs.IsConfigured)
            {
                this.Skip = LiveObs.NotConfigured;
            }
        }
    }

    /// <summary>A test against a real OBS Studio that creates and removes scenes and inputs: it also needs <c>OBS_LIVE_ALLOW_CHANGES=1</c>.</summary>
    internal sealed class LiveChangesFactAttribute : FactAttribute
    {
        public LiveChangesFactAttribute()
        {
            if (!LiveObs.IsConfigured)
            {
                this.Skip = LiveObs.NotConfigured;
            }
            else if (!LiveObs.MayChange)
            {
                this.Skip = LiveObs.ChangesNotAllowed;
            }
        }
    }

    /// <summary>A test against a real OBS Studio that asks for a password: it also needs <c>OBS_LIVE_PASSWORD</c>.</summary>
    internal sealed class LivePasswordFactAttribute : FactAttribute
    {
        public LivePasswordFactAttribute()
        {
            if (!LiveObs.IsConfigured)
            {
                this.Skip = LiveObs.NotConfigured;
            }
            else if (LiveObs.Password.Length == 0)
            {
                this.Skip = LiveObs.NoPassword;
            }
        }
    }

    /// <summary>
    /// A test that creates and removes scenes and inputs and follows them by UUID, as <see cref="LiveChangesFactAttribute"/>: it is skipped as well
    /// when <c>OBS_LIVE_UUIDS</c> is 0, for an OBS Studio before 30.1.0 (obs-websocket before 5.4.0), which has no UUIDs.
    /// </summary>
    internal sealed class LiveUuidChangesFactAttribute : FactAttribute
    {
        public const string NoUuids = "This live test follows scenes and inputs by UUID, which obs-websocket has from 5.4.0 (OBS Studio 30.1.0): OBS_LIVE_UUIDS=0 says that this OBS Studio has none.";

        public LiveUuidChangesFactAttribute()
        {
            if (!LiveObs.IsConfigured)
            {
                this.Skip = LiveObs.NotConfigured;
            }
            else if (!LiveObs.MayChange)
            {
                this.Skip = LiveObs.ChangesNotAllowed;
            }
            else if (!LiveObs.HasUuids)
            {
                this.Skip = NoUuids;
            }
        }
    }

    /// <summary>The soak test: a test against a real OBS Studio that runs for minutes. It is skipped without <c>OBS_LIVE_PORT</c>, and without <c>OBS_LIVE_SOAK_SECONDS</c>.</summary>
    internal sealed class LiveSoakFactAttribute : FactAttribute
    {
        public const string NotWanted = "The soak test runs for as long as OBS_LIVE_SOAK_SECONDS says (the launcher has -SoakMinutes), and is off without it. See CONTRIBUTING.md, Live tests.";

        public LiveSoakFactAttribute()
        {
            if (!LiveObs.IsConfigured)
            {
                this.Skip = LiveObs.NotConfigured;
            }
            else if (LiveObs.SoakSeconds == 0)
            {
                this.Skip = NotWanted;
            }
        }
    }

    /// <summary>The live tests share one OBS Studio, so they take turns: one test at a time changes what the others look at.</summary>
    [CollectionDefinition("Live OBS", DisableParallelization = true)]
    public sealed class LiveCollection
    {
    }

    /// <summary>
    /// A connection to the real OBS Studio of the live tests, with everything that came of it: the events that the client raised, and
    /// the messages that went over the wire as the client logged them (the answers of OBS Studio as OBS Studio sent them).
    /// </summary>
    internal sealed class LiveSession : IAsyncDisposable
    {
        // OBS Studio answers the requests that need its user interface thread when that thread is free, and on a busy or slow machine
        // that thread can be away for seconds (nine were seen on a development machine; the answers that do not need it keep coming).
        private const int RequestTimeoutMilliseconds = 30000;

        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20);

        private static readonly TimeSpan EventsQuietPeriod = TimeSpan.FromMilliseconds(300);

        private readonly List<(string Name, object? Arguments)> _events = [];

        private LiveSession(ObsClient client, ListLogger log)
        {
            this.Client = client;
            this.Log = log;
        }

        /// <summary>Gets the client.</summary>
        public ObsClient Client { get; }

        /// <summary>Gets what the client logged, including the messages that it sent and received.</summary>
        public ListLogger Log { get; }

        /// <summary>Gets the events that the client has raised so far, with their arguments, in the order in which they were raised.</summary>
        public IReadOnlyList<(string Name, object? Arguments)> Events
        {
            get
            {
                lock (this._events)
                {
                    return [.. this._events];
                }
            }
        }

        /// <summary>Connects to the OBS Studio of the environment.</summary>
        /// <param name="subscriptions">The events to subscribe to.</param>
        /// <param name="password">The password; the one of the environment when null.</param>
        /// <param name="removeLeftovers">Whether to remove the scenes and inputs that an earlier run that did not finish left behind.</param>
        /// <param name="expectedCollection">The scene collection that the OBS Studio has to have as its current one for the connection to be kept when the tests may change it; <c>OBS_LIVE_COLLECTION</c> when null.</param>
        /// <returns>The connection.</returns>
        public static async Task<LiveSession> ConnectAsync(EventSubscriptions subscriptions = EventSubscriptions.All, string? password = null, bool removeLeftovers = false, string? expectedCollection = null)
        {
            var log = new ListLogger();
            var client = new ObsClient(log) { RequestTimeout = RequestTimeoutMilliseconds };
            var session = new LiveSession(client, log);

            // The caller only gets the session when this succeeds: what fails on the way must not leave a client that stays connected.
            try
            {
                RequestCatalog.Subscribe(client, session.OnEvent);

                var connected = await client
                    .ConnectAsync(false, password ?? LiveObs.Password, LiveObs.Host, LiveObs.Port!.Value, subscriptions)
                    .WithTimeoutAsync(ConnectTimeout, $"OBS Studio at {LiveObs.Host}:{LiveObs.Port} did not complete the connection within {ConnectTimeout.TotalSeconds:N0} seconds.");
                if (!connected)
                {
                    Assert.Fail($"Could not connect to OBS Studio at {LiveObs.Host}:{LiveObs.Port}. Is it running, with the WebSocket server on, and is OBS_LIVE_PASSWORD right?");
                }

                // Whatever the port and the password are, an OBS Studio is only changed when it is the one that was made for these tests:
                // another one that happens to listen there (a version that is running for somebody else) is left exactly as it is.
                if (LiveObs.MayChange)
                {
                    var expected = expectedCollection ?? LiveObs.Collection;
                    var collection = (await client.GetSceneCollectionList()).CurrentSceneCollectionName;
                    if (!string.Equals(collection, expected, StringComparison.Ordinal))
                    {
                        Assert.Fail($"Refusing to change the OBS Studio at {LiveObs.Host}:{LiveObs.Port}: its current scene collection is '{collection}', not the '{expected}' that the OBS Studio of this run has (OBS_LIVE_COLLECTION), so it is not the OBS Studio that tools/live/run-live-tests.ps1 started. Nothing was changed.");
                    }
                }

                if (removeLeftovers)
                {
                    await session.RemoveLeftoversAsync();
                }

                return session;
            }
            catch
            {
                await session.DisposeAsync();
                throw;
            }
        }

        /// <summary>Removes the scenes and the inputs that were made by an earlier run of the live tests that did not finish: the ones that carry <see cref="LiveObs.NamePrefix"/>.</summary>
        /// <returns>A task that completes when they are removed.</returns>
        /// <remarks>
        /// A scene that OBS Studio is already removing (it let go of it only later, which it does when something still holds it) is
        /// still in the list, and refuses to be removed again: it is not an error here.
        /// </remarks>
        public async Task RemoveLeftoversAsync()
        {
            var inputs = await this.Client.GetInputList();
            foreach (var input in inputs.Where(input => input.InputName.StartsWith(LiveObs.NamePrefix, StringComparison.Ordinal)))
            {
                await this.TryRemoveAsync(() => this.Client.RemoveInput(InputRef.FromName(input.InputName)));
            }

            var scenes = await this.Client.GetSceneList();
            foreach (var scene in scenes.Scenes.Where(scene => scene.SceneName.StartsWith(LiveObs.NamePrefix, StringComparison.Ordinal)))
            {
                // An OBS Studio before 30.1.0 has no UUIDs (they are empty): the scene is removed by its name.
                var reference = scene.SceneUuid == Guid.Empty ? SceneRef.FromName(scene.SceneName) : SceneRef.FromUuid(scene.SceneUuid);
                await this.TryRemoveAsync(() => this.Client.RemoveScene(reference));
            }
        }

        private async Task TryRemoveAsync(Func<Task> remove)
        {
            try
            {
                await remove();
            }
            catch (ObsResponseException exception) when (exception.ErrorCode is RequestStatusCode.ResourceActionFailed or RequestStatusCode.ResourceNotFound)
            {
                // Already being removed, or gone: what is wanted is that it is not there any more.
            }
        }

        /// <summary>
        /// Waits for an event of a type of arguments, and returns the first one that is accepted. An event that was raised
        /// before the call counts: what is waited for may already have happened.
        /// </summary>
        /// <typeparam name="T">The type of the arguments of the event.</typeparam>
        /// <param name="name">The name of the event.</param>
        /// <param name="accept">Tells whether an event is the one that is waited for; every event of that name when null.</param>
        /// <param name="timeout">How long to wait (10 seconds).</param>
        /// <returns>The arguments of the event.</returns>
        public async Task<T> WaitForEventAsync<T>(string name, Func<T, bool>? accept = null, TimeSpan? timeout = null)
            where T : class
        {
            T? found = null;
            var arrived = await AsyncTestHelpers.WaitUntilAsync(
                () =>
                {
                    found = this.Events.Where(entry => entry.Name == name).Select(entry => entry.Arguments).OfType<T>().FirstOrDefault(arguments => accept is null || accept(arguments));
                    return found is not null;
                },
                timeout ?? TimeSpan.FromSeconds(10));

            Assert.True(arrived, $"The event {name} that was waited for did not arrive. {this.DescribeEvents()}");
            return found!;
        }

        private static string Summarize(IEnumerable<string> names) =>
            string.Join(", ", names.GroupBy(name => name, StringComparer.Ordinal).Select(group => group.Count() == 1 ? group.Key : $"{group.Key} x{group.Count()}"));

        /// <summary>Describes the events that OBS Studio sent and the events that the client raised, by name and number.</summary>
        /// <returns>The description.</returns>
        public string DescribeEvents() =>
            $"OBS Studio sent: {Summarize(this.RawEvents().Select(entry => entry.Type))}. The client raised: {Summarize(this.Events.Select(entry => entry.Name))}.";

        /// <summary>The raw answer that OBS Studio gave to a request: its <c>d</c> member. The first one after a position of the log.</summary>
        /// <param name="requestType">The type of the request, as OBS Studio names it.</param>
        /// <param name="fromEntry">The number of entries that the log had before the request was sent.</param>
        /// <returns>The answer, or null when none was logged.</returns>
        public JsonElement? RawResponse(string requestType, int fromEntry) =>
            this.RawMessages(fromEntry)
                .Where(message => message.Op == 7 && message.Data.TryGetProperty("requestType", out var type) && type.GetString() == requestType)
                .Select(message => (JsonElement?)message.Data)
                .FirstOrDefault();

        /// <summary>How many messages with an operation code OBS Studio has sent so far (2 is Identified, 5 an event, 7 a response).</summary>
        /// <param name="operation">The operation code.</param>
        /// <returns>The number of messages.</returns>
        public int RawMessageCount(int operation) => this.RawMessages(0).Count(message => message.Op == operation);

        /// <summary>The data of every event that OBS Studio sent, as it sent them, in order, with the name of its type.</summary>
        /// <returns>The events.</returns>
        public IReadOnlyList<(string Type, JsonElement? Data)> RawEvents() =>
            [.. this.RawMessages(0)
                .Where(message => message.Op == 5 && message.Data.TryGetProperty("eventType", out _))
                .Select(message => (
                    message.Data.GetProperty("eventType").GetString()!,
                    message.Data.TryGetProperty("eventData", out var data) && data.ValueKind == JsonValueKind.Object ? (JsonElement?)data : null))];

        /// <summary>
        /// Waits until the events have settled (the client has raised as many events of every type as OBS Studio sent, and no more came for a
        /// moment), and compares each one with what OBS Studio sent: an event that the client did not raise, or raised without anything that
        /// OBS Studio sent (or with another value), is a problem, and so is an event of a type that OBS Studio never sent.
        /// </summary>
        /// <returns>What is wrong; nothing when the client raised every event as OBS Studio sent it.</returns>
        public async Task<IReadOnlyList<string>> CompareEventsWithWhatObsStudioSentAsync()
        {
            await this.WaitUntilEventsHaveSettledAsync();

            var problems = new List<string>();
            var sentByType = this.RawEvents().GroupBy(entry => entry.Type, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            var raisedByType = this.Events.GroupBy(entry => entry.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            // The types of both sides: an event that the client raised and OBS Studio never sent is as wrong as one that got lost.
            foreach (var type in sentByType.Keys.Union(raisedByType.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
            {
                var fromObs = sentByType.GetValueOrDefault(type) ?? [];
                var heard = raisedByType.GetValueOrDefault(type) ?? [];
                if (heard.Count != fromObs.Count)
                {
                    problems.Add($"{type}: OBS Studio sent {fromObs.Count}, the client raised {heard.Count}.");
                }

                for (var index = 0; index < Math.Min(heard.Count, fromObs.Count); index++)
                {
                    if (fromObs[index].Data is not { } data)
                    {
                        continue;
                    }

                    var arguments = heard[index].Arguments;
                    if (arguments is null)
                    {
                        problems.Add($"{type}: OBS Studio sent event data, the client gave no arguments.");
                        continue;
                    }

                    using var back = JsonTools.ToDocument(arguments)!;
                    var comparison = JsonShape.Compare(data, back.RootElement);
                    var argumentsType = arguments.GetType();
                    problems.AddRange(comparison.OnlyInObs
                        .Where(path => !ObsStudioCaptureTests.IsObsoleteAlias(argumentsType, path))
                        .Select(path => $"{type}: OBS Studio sent {path}, the client does not give it back"));
                    problems.AddRange(comparison.TypeDifferences.Select(difference => $"{type}: {difference}"));
                    problems.AddRange(JsonValues.Differences(data, back.RootElement).Select(difference => $"{type}: {difference}"));
                }
            }

            return problems;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            this.Client.Disconnect();
            await AsyncTestHelpers.WaitUntilAsync(() => this.Client.ConnectionState == ConnectionState.Disconnected, TimeSpan.FromSeconds(5));
            this.Client.Dispose();
        }

        // More events can follow the last one that a test waited for (OBS Studio sends some of them twice, and the client raises the events off
        // the thread that receives them). What OBS Studio sent and what the client raised is therefore only compared when both have the same
        // number of events of every type, and that has not changed for a moment, so that both are copied from one and the same state. When it
        // does not settle, the comparison reports the difference.
        private async Task WaitUntilEventsHaveSettledAsync()
        {
            var previous = string.Empty;
            var changedAt = DateTime.UtcNow;
            await AsyncTestHelpers.WaitUntilAsync(
                () =>
                {
                    var sent = this.RawEvents().Select(entry => entry.Type).ToList();
                    var raised = this.Events.Select(entry => entry.Name).ToList();
                    var state = $"{Summarize(sent)}|{Summarize(raised)}";
                    if (!string.Equals(state, previous, StringComparison.Ordinal))
                    {
                        previous = state;
                        changedAt = DateTime.UtcNow;
                    }

                    var sameNumbers = sent.Count == raised.Count
                        && sent.GroupBy(name => name, StringComparer.Ordinal).All(group => raised.Count(name => string.Equals(name, group.Key, StringComparison.Ordinal)) == group.Count());
                    return sameNumbers && DateTime.UtcNow - changedAt >= EventsQuietPeriod;
                },
                TimeSpan.FromSeconds(10));
        }

        /// <summary>The messages that OBS Studio sent, as the client logged them: the operation code and the data (<c>d</c>) of each, from a position of the log.</summary>
        /// <param name="fromEntry">The number of entries of the log to skip.</param>
        /// <returns>The messages, in the order in which they were received.</returns>
        internal IEnumerable<(int Op, JsonElement Data)> RawMessages(int fromEntry)
        {
            const string Prefix = "Received: ";
            foreach (var (_, message) in this.Log.Entries.Skip(fromEntry))
            {
                if (!message.StartsWith(Prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(message[Prefix.Length..]);
                var root = document.RootElement;
                if (root.TryGetProperty("op", out var op) && root.TryGetProperty("d", out var data))
                {
                    yield return (op.GetInt32(), data.Clone());
                }
            }
        }

        private void OnEvent(string name, object? arguments)
        {
            lock (this._events)
            {
                this._events.Add((name, arguments));
            }
        }
    }
}
