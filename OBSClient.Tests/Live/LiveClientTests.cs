namespace OBSStudioClient.Tests.Live
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Security.Cryptography;
    using System.Text;
    using OBSStudioClient.Classes;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Events;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Responses;
    using OBSStudioClient.Tests.Conformance;
    using OBSStudioClient.Tests.TestSupport;
    using Xunit.Abstractions;

    /// <summary>
    /// How the client behaves against a real OBS Studio, rather than against one that the tests play themselves: the handshake and
    /// the password, the order in which events are raised, a request that is cancelled, a batch that fails, and a connection
    /// that is made again.
    /// </summary>
    [Collection("Live OBS")]
    public class LiveClientTests(ITestOutputHelper output)
    {
        private const int LeastDistinctivePasswordLength = 8;
        [LivePasswordFact]
        public async Task AWrongPassword_IsRefused_WithTheCloseCodeOfObsStudio()
        {
            var log = new ListLogger();
            using var client = new ObsClient(log) { RequestTimeout = 5000 };
            var closed = new TaskCompletionSource<ConnectionClosedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.ConnectionClosed += (_, e) => closed.TrySetResult(e);

            var connected = await client
                .ConnectAsync(false, LiveObs.Password + "-not-the-password", LiveObs.Host, LiveObs.Port!.Value)
                .WithTimeoutAsync(TimeSpan.FromSeconds(20));

            Assert.False(connected);
            var reason = await closed.Task.WithTimeoutAsync(TimeSpan.FromSeconds(5), "The client did not report that OBS Studio closed the connection.");
            Assert.Equal(WebSocketCloseCode.AuthenticationFailed, reason.WebSocketCloseCode);
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, TimeSpan.FromSeconds(5)));
        }

        [LivePasswordFact]
        public async Task TheRightPassword_IsAccepted_AndNeitherItNorItsHashIsLogged()
        {
            await using var session = await LiveSession.ConnectAsync();

            // The connection is made with the password of the environment, which may be one with characters that are not ASCII.
            Assert.Equal(ConnectionState.Connected, session.Client.ConnectionState);
            var entries = session.Log.Entries.Select(entry => entry.Message).ToList();

            // What the client works out of the password with the salt and the challenge of OBS Studio (as obs-websocket 5 defines it) are long
            // strings that cannot be in the log by chance. The password itself can be a word that the log is made of (a person chose it), so it
            // is only looked for in what the client wrote, and only when it is long enough to be told apart from the words of the protocol.
            Assert.True(
                session.RawMessages(0).First(message => message.Op == 0).Data.TryGetProperty("authentication", out var authentication),
                "OBS Studio did not ask for a password. This test needs an OBS Studio that does (OBS_LIVE_PASSWORD).");
            var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(LiveObs.Password + authentication.GetProperty("salt").GetString())));
            var proof = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + authentication.GetProperty("challenge").GetString())));
            Assert.DoesNotContain(entries, message => message.Contains(secret, StringComparison.Ordinal) || message.Contains(proof, StringComparison.Ordinal));
            if (LiveObs.Password.Length >= LeastDistinctivePasswordLength)
            {
                Assert.DoesNotContain(
                    entries.Where(message => !message.StartsWith("Received: ", StringComparison.Ordinal)),
                    message => message.Contains(LiveObs.Password, StringComparison.Ordinal));
            }

            var identify = entries.Single(message => message.StartsWith("Sending: ", StringComparison.Ordinal) && message.Contains("\"authentication\"", StringComparison.Ordinal));
            Assert.Contains("\"authentication\":\"***\"", identify, StringComparison.Ordinal);
        }

        [LiveFact]
        public async Task AClient_CanDisconnect_AndConnectToTheSameObsStudioAgain()
        {
            await using var session = await LiveSession.ConnectAsync();
            var client = session.Client;

            client.Disconnect();
            Assert.True(await AsyncTestHelpers.WaitUntilAsync(() => client.ConnectionState == ConnectionState.Disconnected, TimeSpan.FromSeconds(5)));
            await Assert.ThrowsAsync<ObsClientException>(() => client.GetVersion());

            var connected = await client
                .ConnectAsync(false, LiveObs.Password, LiveObs.Host, LiveObs.Port!.Value)
                .WithTimeoutAsync(TimeSpan.FromSeconds(20));
            Assert.True(connected);
            Assert.Equal(1, (await client.GetVersion()).RpcVersion);
        }

        [LiveFact]
        public async Task ABatchWithAFailingRequest_StopsOrGoesOn_AsItWasAsked()
        {
            await using var session = await LiveSession.ConnectAsync();
            var missing = SceneRef.FromName(LiveObs.UniqueName("no-such-scene"));

            RequestBatchMessage Batch(bool haltOnFailure)
            {
                var batch = new RequestBatchMessage(haltOnFailure, RequestBatchExecutionType.SerialRealtime);
                batch.AddGetVersionRequest();
                batch.AddSetCurrentProgramSceneRequest(missing);
                batch.AddGetStatsRequest();
                return batch;
            }

            var halted = await session.Client.SendRequestBatchAsync(Batch(haltOnFailure: true));
            Assert.Equal(2, halted.Length);
            Assert.True(halted[0].RequestStatus.Result);
            Assert.IsType<VersionResponse>(halted[0].ResponseData);
            Assert.False(halted[1].RequestStatus.Result);
            Assert.Equal(RequestStatusCode.ResourceNotFound, halted[1].RequestStatus.Code);

            var continued = await session.Client.SendRequestBatchAsync(Batch(haltOnFailure: false));
            Assert.Equal(3, continued.Length);
            Assert.False(continued[1].RequestStatus.Result);
            Assert.Equal(RequestStatusCode.ResourceNotFound, continued[1].RequestStatus.Code);
            Assert.True(continued[2].RequestStatus.Result);
            Assert.IsType<StatsResponse>(continued[2].ResponseData);
        }

        [LiveFact]
        public async Task ACancelledBatch_EndsPromptly_AndTheConnectionStaysUsable()
        {
            await using var session = await LiveSession.ConnectAsync();
            var client = session.Client;
            client.RequestTimeout = 15000;

            var batch = new RequestBatchMessage(false, RequestBatchExecutionType.SerialRealtime);
            batch.AddSleepRequest(3000, null);

            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            var stopwatch = Stopwatch.StartNew();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendRequestBatchAsync(batch, 20000, cancellation.Token));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"The batch was cancelled after 300 ms, and the call ended after {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);

            // OBS Studio carries on with the sleep and answers when it is done: the client lets the late answer go by.
            Assert.Equal(1, (await client.GetVersion()).RpcVersion);
            await Task.Delay(TimeSpan.FromSeconds(3.5));
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
            Assert.Equal(1, (await client.GetVersion()).RpcVersion);
        }

        [LiveChangesFact]
        public async Task AnObsStudioWithAnotherSceneCollection_IsRefused_BeforeAnythingIsChanged()
        {
            // The tests that change things only do so in the OBS Studio that was made for them: another one, which may well listen on the
            // port that was meant for this run (several copies of OBS Studio can run on one machine), is disconnected and left alone.
            var refused = await Assert.ThrowsAnyAsync<Exception>(
                () => LiveSession.ConnectAsync(removeLeftovers: true, expectedCollection: "the-scene-collection-of-another-obs-studio"));

            Assert.Contains("Refusing to change", refused.Message, StringComparison.Ordinal);
        }

        [LiveChangesFact]
        public async Task AHandlerThatWaitsForAnotherRequest_DoesNotStallTheClient()
        {
            await using var session = await LiveSession.ConnectAsync(removeLeftovers: true);
            var client = session.Client;
            const int Count = 12;
            var prefix = LiveObs.UniqueName("burst");
            var names = Enumerable.Range(0, Count).Select(index => $"{prefix}-{index:00}").ToList();
            var handled = 0;
            var failures = new ConcurrentQueue<string>();

            client.SceneCreated += (_, e) =>
            {
                if (!names.Contains(e.SceneName))
                {
                    return;
                }

                try
                {
                    // What a handler of a desktop application may well do: it waits for the answer on the thread that raised the event.
                    // The answer has to be read by the client in the meantime, so the thread that reads must not be this one. (Whether
                    // OBS Studio lists the scene yet is not asked: it announces a scene a moment before its list has it.)
                    client.GetSceneList().GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    failures.Enqueue($"{e.SceneName}: {exception.GetType().Name}: {exception.Message}");
                }
                finally
                {
                    Interlocked.Increment(ref handled);
                }
            };

            try
            {
                var batch = new RequestBatchMessage(false, RequestBatchExecutionType.SerialRealtime);
                foreach (var name in names)
                {
                    batch.AddCreateSceneRequest(name);
                }

                var stopwatch = Stopwatch.StartNew();
                var responses = await client.SendRequestBatchAsync(batch, 20000);
                Assert.Equal(Count, responses.Length);
                Assert.All(responses, response => Assert.True(response.RequestStatus.Result, response.RequestStatus.Comment));

                // Every event that OBS Studio sent has been raised, and every one of those for these scenes has been handled to the end.
                var problems = await session.CompareEventsWithWhatObsStudioSentAsync();
                var announced = session.Events.Count(entry => entry.Name == "SceneCreated" && entry.Arguments is SceneModifiedEventArgs created && names.Contains(created.SceneName));
                var allHandled = await AsyncTestHelpers.WaitUntilAsync(() => Volatile.Read(ref handled) >= announced, TimeSpan.FromSeconds(20));
                output.WriteLine($"OBS Studio announced {announced} scenes for the {Count} that were created; {Volatile.Read(ref handled)} handlers ran, after {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
                foreach (var raw in session.RawEvents().Where(entry => entry.Type == "SceneCreated" && entry.Data is { } data && names.Contains(data.GetProperty("sceneName").GetString()!)).Take(3))
                {
                    output.WriteLine($"SceneCreated as OBS Studio sent it: {raw.Data!.Value.GetRawText()}");
                }

                // At least Count, not exactly Count: obs-websocket 5.7 sends SceneCreated twice (https://github.com/obsproject/obs-websocket/issues/1364),
                // and a later one may send it once.
                Assert.True(announced >= Count, $"OBS Studio announced {announced} of the {Count} scenes that were created.");
                Assert.True(allHandled, $"Only {Volatile.Read(ref handled)} of {announced} handlers ran: {string.Join("; ", failures)}");
                Assert.Equal(announced, Volatile.Read(ref handled));
                Assert.Empty(failures);
                Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"The handlers took {stopwatch.Elapsed.TotalSeconds:N1} seconds: each of them waited for a request that the client could not read the answer of.");
                ConformanceAssert.NoProblems(problems, "The client does not raise the events of OBS Studio as OBS Studio sent them:");
            }
            finally
            {
                var cleanup = new RequestBatchMessage(false, RequestBatchExecutionType.SerialRealtime);
                foreach (var name in names)
                {
                    cleanup.AddRemoveSceneRequest(SceneRef.FromName(name));
                }

                try
                {
                    await client.SendRequestBatchAsync(cleanup, 20000);
                }
                catch (Exception exception) when (exception is ObsClientException or TimeoutException)
                {
                    // The next run removes what is left (LiveSession.RemoveLeftoversAsync).
                }
            }
        }

        [LiveChangesFact]
        public async Task Reidentify_ChangesWhichEventsOBSStudioSends()
        {
            await using var session = await LiveSession.ConnectAsync(EventSubscriptions.None, removeLeftovers: true);
            var client = session.Client;
            var silent = LiveObs.UniqueName("silent");
            var heard = LiveObs.UniqueName("heard");
            try
            {
                await client.CreateScene(silent);
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.DoesNotContain(session.Events, entry => entry.Name == "SceneCreated");

                // OBS Studio answers a reidentify with Identified: that is the sign that the new subscription is in place.
                var identified = session.RawMessageCount(2);
                await client.ReidentifyAsync(EventSubscriptions.All);
                Assert.True(
                    await AsyncTestHelpers.WaitUntilAsync(() => session.RawMessageCount(2) > identified, TimeSpan.FromSeconds(5)),
                    "OBS Studio did not answer the reidentify.");

                await client.CreateScene(heard);
                var created = await session.WaitForEventAsync<SceneModifiedEventArgs>("SceneCreated", e => e.SceneName == heard);
                Assert.Equal(heard, created.SceneName);
                Assert.DoesNotContain(session.Events, entry => entry.Name == "SceneCreated" && entry.Arguments is SceneModifiedEventArgs { SceneName: var name } && name == silent);
            }
            finally
            {
                foreach (var name in new[] { silent, heard })
                {
                    try
                    {
                        await client.RemoveScene(SceneRef.FromName(name));
                    }
                    catch (Exception exception) when (exception is ObsClientException or TimeoutException)
                    {
                        // The scene was not created, or it is gone: there is nothing to remove.
                    }
                }
            }
        }
    }
}
