namespace OBSStudioClient.Tests.Live
{
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Text.Json;
    using Xunit.Abstractions;

    /// <summary>
    /// A real OBS Studio, for minutes, over a connection that keeps going wrong. The client connects through a <see cref="ChaosProxy"/> that
    /// cuts the connection, stalls it and refuses it at random while the client asks OBS Studio things all the time (requests, a batch, and
    /// an event that OBS Studio sends back). It has to go on working: every call ends, and ends in a failure that says that the connection
    /// is gone (never in a hang, and never in an exception that says that the client itself went wrong), every connection that ended is
    /// reported, the client is back as soon as the trouble stops, and what it holds on to (memory, threads, and on Linux the file
    /// descriptors) does not grow with the number of connections that it has made.
    /// </summary>
    /// <remarks>
    /// It changes nothing in OBS Studio: it only reads, and broadcasts a custom event, which OBS Studio forwards to the connected clients
    /// and forgets. It only runs when <c>OBS_LIVE_SOAK_SECONDS</c> says for how long (the Live OBS workflow does that every week, and on
    /// request; <c>tools/live/run-live-tests.ps1 -SoakMinutes</c> does it on a developer machine, with a private copy of OBS Studio).
    /// The seed of the chaos is in the output; <c>OBS_LIVE_SOAK_SEED</c> runs the same chaos again.
    /// </remarks>
    [Collection("Live OBS")]
    public class LiveSoakTests(ITestOutputHelper output)
    {
        private const int RequestTimeoutMilliseconds = 5000;

        // What a call can take at most: the request timeout, and room for a machine that is slow to schedule a thread. Longer is a call that
        // was left waiting for a connection that is gone.
        private static readonly TimeSpan LongestCall = TimeSpan.FromMilliseconds(RequestTimeoutMilliseconds) + TimeSpan.FromSeconds(10);

        private static readonly TimeSpan WarmUp = TimeSpan.FromSeconds(5);

        // How long the client is given, once the trouble is over, to find its way back and be answered.
        private static readonly TimeSpan RecoveryTime = TimeSpan.FromSeconds(60);

        // What the client may log at a warning or an error when connections are cut: the read that was cut, and a handshake that a stall held up.
        private static readonly string[] ExpectedLogEntries = ["Handled Exception in ReceiverAsync", "did not complete the authentication handshake"];

        private const long MostMemoryToGrow = 25L * 1024 * 1024;

        private const int MostThreadsToGrow = 25;

        private const int MostDescriptorsToGrow = 25;

        [LiveSoakFact]
        public async Task TheClient_GoesOnWorking_WhileTheConnectionIsCutStalledAndRefusedAgainAndAgain()
        {
            var duration = TimeSpan.FromSeconds(LiveObs.SoakSeconds);
            var seed = int.TryParse(Environment.GetEnvironmentVariable("OBS_LIVE_SOAK_SEED"), out var given) ? given : Random.Shared.Next();
            output.WriteLine($"The soak test runs for {duration}, with the seed {seed} (OBS_LIVE_SOAK_SEED).");

            await using var proxy = new ChaosProxy(LiveObs.Host, LiveObs.Port!.Value);
            proxy.Start();
            var log = new SoakLogger();
            using var client = new ObsClient(log) { RequestTimeout = RequestTimeoutMilliseconds };
            var stats = new Stats();
            var closed = 0;
            var connectedAgain = -1;
            client.ConnectionClosed += (_, _) => Interlocked.Increment(ref closed);
            client.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsClient.ConnectionState) && client.ConnectionState == ConnectionState.Connected)
                {
                    Interlocked.Increment(ref connectedAgain);
                }
            };
            var cameBack = new ConcurrentDictionary<int, byte>();
            client.CustomEvent += (_, e) =>
            {
                if (e.EventData is { ValueKind: JsonValueKind.Object } data && data.TryGetProperty("soak", out var number) && number.TryGetInt32(out var value))
                {
                    cameBack[value] = 0;
                }
            };

            var connected = await client
                .ConnectAsync(autoReconnect: true, password: LiveObs.Password, hostname: "127.0.0.1", port: proxy.Port)
                .WithTimeoutAsync(TimeSpan.FromSeconds(30), "The client did not connect to OBS Studio through the proxy.");
            Assert.True(connected, $"Could not connect to OBS Studio at {LiveObs.Host}:{LiveObs.Port}, through the proxy. Is it running, and is OBS_LIVE_PASSWORD right?");

            using var stopWork = new CancellationTokenSource();
            var work = Task.Run(() => WorkAsync(client, stats, stopWork.Token));
            var failures = new List<string>();
            try
            {
                // What the numbers are compared with: after the client has had a few seconds to settle, without anything going wrong yet.
                await Task.Delay(WarmUp);
                var before = Measure();

                using var stopChaos = new CancellationTokenSource(duration);
                await ChaosAsync(proxy, stats, new Random(seed), stopChaos.Token);

                // The trouble is over: the client has to find its way back (the reconnect timer ticks every 3 seconds), and do what it did.
                var back = await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Connected, RecoveryTime);
                if (!back)
                {
                    failures.Add($"The client was not connected again {RecoveryTime.TotalSeconds:N0} seconds after the trouble ended (ConnectionState {client.ConnectionState}).");
                }
                else
                {
                    // The connection that was cut last may not be known to be gone yet (the state still says Connected): a call that fails
                    // because the connection is gone is made again, for as long as the client is given to find its way back.
                    var version = await AnswerAsync(() => client.GetVersion(), "GetVersion did not answer after the trouble ended.");
                    output.WriteLine($"OBS Studio {version.ObsVersion}, obs-websocket {version.ObsWebSocketVersion}.");
                    await client.BroadcastCustomEvent(JsonSerializer.SerializeToElement(new { soak = -1 }))
                        .WithTimeoutAsync(TimeSpan.FromSeconds(20), "BroadcastCustomEvent did not complete after the trouble ended.");
                    if (!await AsyncTestHelpers.WaitUntilAsync(() => cameBack.ContainsKey(-1), TimeSpan.FromSeconds(10)))
                    {
                        failures.Add("An event that OBS Studio was asked to send back did not arrive after the trouble ended.");
                    }
                }

                await Task.Delay(WarmUp);
                var after = Measure();

                stopWork.Cancel();
                if (await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(30))) != work)
                {
                    failures.Add("The calls of the soak test were still running 30 seconds after they were told to stop.");
                }

                Summarize(seed, duration, proxy, stats, closed, connectedAgain, cameBack, before, after);
                this.Judge(failures, proxy, stats, log, closed, connectedAgain, before, after);
            }
            finally
            {
                stopWork.Cancel();
                client.Disconnect();
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Prepend($"The soak test failed (seed {seed}):")));
        }

        private static async Task WorkAsync(ObsClient client, Stats stats, CancellationToken token)
        {
            var round = 0;
            while (!token.IsCancellationRequested)
            {
                round++;
                var started = Stopwatch.GetTimestamp();
                try
                {
                    switch (round % 6)
                    {
                        case 0:
                            await client.GetVersion();
                            break;
                        case 1:
                            await client.GetStats();
                            break;
                        case 2:
                            await client.GetSceneList();
                            break;
                        case 3:
                            await client.GetInputList();
                            break;
                        case 4:
                            var batch = new RequestBatchMessage();
                            batch.AddGetVersionRequest();
                            batch.AddGetStatsRequest();
                            var results = await client.SendRequestBatchAsync(batch);
                            if (results.Length != 2 || results.Any(result => !result.RequestStatus.Result))
                            {
                                stats.Unexpected($"A batch of two requests came back as {results.Length} results, {results.Count(result => !result.RequestStatus.Result)} of them failed.");
                            }

                            break;
                        default:
                            // OBS Studio sends a custom event to every client that is connected, and the client that sent it as well.
                            await client.BroadcastCustomEvent(JsonSerializer.SerializeToElement(new { soak = round }));
                            break;
                    }

                    stats.Count(nameof(Stats.Succeeded));
                }
                catch (ObsResponseException exception)
                {
                    stats.Unexpected($"OBS Studio refused a request that only reads: {exception.Message}");
                }
                catch (ObsClientException exception) when (IsConnectionLoss(exception))
                {
                    // Not connected, or the connection was lost while the call waited: what a cut is supposed to do. Any other failure of
                    // the client (a response of a type that it did not expect, a request that could not be queued) is not.
                    stats.Count(nameof(Stats.Refused));
                }
                catch (TimeoutException)
                {
                    // The answer did not come in time: what a stall is supposed to do.
                    stats.Count(nameof(Stats.TimedOut));
                }
                catch (Exception exception)
                {
                    stats.Unexpected($"{exception.GetType().Name}: {exception.Message}");
                }

                var took = Stopwatch.GetElapsedTime(started);
                stats.Took(took);
                if (took > LongestCall)
                {
                    stats.Unexpected($"A call (round {round}) took {took.TotalSeconds:N1} seconds: it was left waiting.");
                }

                try
                {
                    await Task.Delay(25, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private static async Task ChaosAsync(ChaosProxy proxy, Stats stats, Random random, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(random.Next(1500, 9000)), token);
                    var roll = random.Next(100);
                    if (roll < 55)
                    {
                        // The process at the other end dies, or the network is cut.
                        if (proxy.Cut() > 0)
                        {
                            stats.Count(nameof(Stats.Cuts));
                        }
                    }
                    else if (roll < 80)
                    {
                        // The machine, or the network, stops answering for a while, and then catches up.
                        proxy.Stall(TimeSpan.FromMilliseconds(random.Next(500, 8000)));
                        stats.Count(nameof(Stats.Stalls));
                    }
                    else if (roll < 92)
                    {
                        // OBS Studio restarts: what is there is cut, and for a few seconds nothing is accepted.
                        proxy.Refuse(true);
                        proxy.Cut();
                        stats.Count(nameof(Stats.Refusals));
                        await Task.Delay(TimeSpan.FromMilliseconds(random.Next(3000, 9000)), token);
                        proxy.Refuse(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // The time is up.
            }
            finally
            {
                // The trouble is over, all of it: a stall that is still under way would hold back the answers that the client waits for.
                proxy.Refuse(false);
                proxy.EndStall();
            }
        }

        // What the client says when the connection is not there: before the call, or while the call waited (see ObsClient.cs).
        private static bool IsConnectionLoss(ObsClientException exception) =>
            exception.Message.StartsWith("Not connected.", StringComparison.Ordinal)
            || exception.Message.StartsWith("The connection to OBS Studio was closed", StringComparison.Ordinal);

        // A call after the trouble: it is made again while it fails because the connection is gone, until the recovery time is up.
        private static async Task<T> AnswerAsync<T>(Func<Task<T>> call, string timeoutMessage)
        {
            var started = Stopwatch.GetTimestamp();
            while (true)
            {
                try
                {
                    return await call().WithTimeoutAsync(TimeSpan.FromSeconds(20), timeoutMessage);
                }
                catch (ObsClientException exception) when (IsConnectionLoss(exception) && Stopwatch.GetElapsedTime(started) < RecoveryTime)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }
            }
        }

        private static Measurement Measure()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            return new Measurement(
                GC.GetTotalMemory(true),
                Process.GetCurrentProcess().Threads.Count,
                Directory.Exists("/proc/self/fd") ? Directory.GetFileSystemEntries("/proc/self/fd").Length : null);
        }

        private void Summarize(int seed, TimeSpan duration, ChaosProxy proxy, Stats stats, int closed, int connectedAgain, ConcurrentDictionary<int, byte> cameBack, Measurement before, Measurement after)
        {
            output.WriteLine($"Seed {seed}, {duration}. The proxy accepted {proxy.Accepted} connections and cut {proxy.ConnectionsCut}; the chaos cut {stats.Cuts}x, stalled {stats.Stalls}x, refused {stats.Refusals}x.");
            output.WriteLine($"The client was connected {connectedAgain + 1}x and raised ConnectionClosed {closed}x.");
            output.WriteLine($"Calls: {stats.Succeeded} succeeded, {stats.Refused} refused (not connected, or lost), {stats.TimedOut} timed out, the longest took {stats.Longest.TotalSeconds:N1} s. {cameBack.Count} events came back.");
            output.WriteLine($"Managed memory {before.Memory / 1024 / 1024} MB -> {after.Memory / 1024 / 1024} MB; threads {before.Threads} -> {after.Threads}; file descriptors {before.Descriptors?.ToString() ?? "n/a"} -> {after.Descriptors?.ToString() ?? "n/a"}.");
        }

        private void Judge(List<string> failures, ChaosProxy proxy, Stats stats, SoakLogger log, int closed, int connectedAgain, Measurement before, Measurement after)
        {
            if (proxy.ConnectionsCut == 0 || connectedAgain < 1)
            {
                failures.Add($"The chaos did not do what it is for: {proxy.ConnectionsCut} connections were cut and the client was connected again {Math.Max(connectedAgain, 0)} times. Is the run long enough?");
            }

            if (stats.Succeeded < 20)
            {
                failures.Add($"Only {stats.Succeeded} calls succeeded in the whole run.");
            }

            // A connection that was up and ended has to be reported: the silent death of the receive loop (issue 17) is what this is for.
            // (One that ended while it was being made may be reported without having been up, hence the more.)
            if (closed < connectedAgain)
            {
                failures.Add($"The client was connected {connectedAgain + 1} times, and raised ConnectionClosed {closed} times: {connectedAgain - closed} connections ended without being reported.");
            }

            failures.AddRange(stats.UnexpectedFailures.Take(10).Select(failure => $"Unexpected: {failure}"));
            var unexpectedLog = log.Entries.Where(entry => !ExpectedLogEntries.Any(expected => entry.Contains(expected, StringComparison.Ordinal))).Take(10).ToList();
            failures.AddRange(unexpectedLog.Select(entry => $"Logged by the client: {entry}"));

            if (after.Memory - before.Memory > MostMemoryToGrow)
            {
                failures.Add($"The managed memory grew by {(after.Memory - before.Memory) / 1024 / 1024} MB (more than {MostMemoryToGrow / 1024 / 1024} MB) over {proxy.Accepted} connections.");
            }

            if (after.Threads - before.Threads > MostThreadsToGrow)
            {
                failures.Add($"The number of threads grew from {before.Threads} to {after.Threads} (more than {MostThreadsToGrow}) over {proxy.Accepted} connections.");
            }

            if (before.Descriptors is { } start && after.Descriptors is { } end && end - start > MostDescriptorsToGrow)
            {
                failures.Add($"The number of open file descriptors grew from {start} to {end} (more than {MostDescriptorsToGrow}) over {proxy.Accepted} connections.");
            }
        }

        private sealed record Measurement(long Memory, int Threads, int? Descriptors);

        /// <summary>What went on, counted by the calls and the chaos that run side by side.</summary>
        private sealed class Stats
        {
            private readonly ConcurrentQueue<string> _unexpected = new();
            private readonly Dictionary<string, long> _counts = [];
            private long _longestTicks;

            public long Succeeded => this.Get(nameof(Succeeded));

            public long Refused => this.Get(nameof(Refused));

            public long TimedOut => this.Get(nameof(TimedOut));

            public long Cuts => this.Get(nameof(Cuts));

            public long Stalls => this.Get(nameof(Stalls));

            public long Refusals => this.Get(nameof(Refusals));

            public TimeSpan Longest => TimeSpan.FromTicks(Interlocked.Read(ref this._longestTicks));

            public IReadOnlyCollection<string> UnexpectedFailures => [.. this._unexpected];

            public void Count(string what)
            {
                lock (this._counts)
                {
                    this._counts[what] = this._counts.GetValueOrDefault(what) + 1;
                }
            }

            public void Unexpected(string what) => this._unexpected.Enqueue(what);

            public void Took(TimeSpan time)
            {
                long seen;
                while (time.Ticks > (seen = Interlocked.Read(ref this._longestTicks)) && Interlocked.CompareExchange(ref this._longestTicks, time.Ticks, seen) != seen)
                {
                }
            }

            private long Get(string what)
            {
                lock (this._counts)
                {
                    return this._counts.GetValueOrDefault(what);
                }
            }
        }

        /// <summary>A logger that keeps the warnings and the errors (the soak test makes far too many messages to keep the others), with their exceptions.</summary>
        private sealed class SoakLogger : ILogger<ObsClient>
        {
            private readonly List<string> _entries = [];

            public IReadOnlyList<string> Entries
            {
                get
                {
                    lock (this._entries)
                    {
                        return [.. this._entries];
                    }
                }
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!this.IsEnabled(logLevel))
                {
                    return;
                }

                var text = $"{logLevel}: {formatter(state, exception)}" + (exception is null ? string.Empty : $" ({exception.GetType().Name}: {exception.Message})");
                lock (this._entries)
                {
                    this._entries.Add(text);
                }
            }
        }
    }
}
