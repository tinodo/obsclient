namespace OBSStudioClient.Tests.Fuzz
{
    using FsCheck;
    using FsCheck.Fluent;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Tests.TestSupport;
    using System.Globalization;
    using System.Reflection;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    /// <summary>
    /// Fuzz tests: the client is fed what a server could send, in shapes that nobody wrote an example for, and has to go on working. The
    /// two properties that are tested are the ones that the client documents: <b>a message that cannot be read, or makes no sense, is
    /// skipped</b> (it never takes the connection down, and never leaves a request waiting), and <b>a response that cannot be read fails
    /// its own call at once</b> (with an <see cref="ObsClientException"/>, and nothing else) and does not touch the next call. After every
    /// case the connection is checked with a request that is answered the right way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cases are made by generators (<see cref="FrameGenerators"/>) from a fixed seed, so a run is the same every time, and a failure
    /// says which case it was, with its seed. To look further, run the tests with more cases or another seed:
    /// <c>OBS_FUZZ_CASES=20000 OBS_FUZZ_SEED=7 dotnet test OBSClient.Tests --filter Category=Fuzz</c>. The tests use the fake server: no
    /// OBS Studio is needed.
    /// </para>
    /// </remarks>
    [Trait("Category", "Fuzz")]
    public class ReceiveFuzzTests(ITestOutputHelper output)
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        // The requests that the response tests answer with data of their own: each has a response with fields that the protocol documents.
        private static readonly (string RequestType, Func<ObsClient, Task> Call)[] Probes =
        [
            ("GetVersion", client => client.GetVersion()),
            ("GetStats", client => client.GetStats()),
            ("GetSceneList", client => client.GetSceneList()),
            ("GetInputList", client => client.GetInputList()),
            ("GetCurrentProgramScene", client => client.GetCurrentProgramScene()),
            ("GetStreamStatus", client => client.GetStreamStatus()),
            ("GetRecordStatus", client => client.GetRecordStatus()),
            ("GetSceneTransitionList", client => client.GetSceneTransitionList()),
        ];

        private static int Cases => (int)ReadNumber("OBS_FUZZ_CASES", 300);

        private static ulong Seed => ReadNumber("OBS_FUZZ_SEED", 20261029);

        [Fact]
        public async Task WhateverTheServerSends_TheClientGoesOnToAnswerTheNextRequest()
        {
            var frames = FrameGenerators.Frame().Where(frame => !FrameGenerators.IsHandshakeMessage(frame)).Sample(Cases, new Rnd(Seed), 8).ToList();
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                var described = () => $"frame {index + 1} of {frames.Count} (seed {Seed}): {Preview(frame)}";

                await server.SendRawAsync(frame);
                await AssertAnswersAsync(client, server, described);
            }
        }

        [Fact]
        public async Task WhateverAResponseHolds_ItsCallEndsAtOnceAndTheClientGoesOn()
        {
            var generator = from probe in Gen.Choose(0, Probes.Length - 1)
                            from data in FrameGenerators.ResponseData(Probes[probe].RequestType)
                            select (Probe: probe, Data: data);
            var cases = generator.Sample(Cases, new Rnd(Seed + 1), 8).ToList();
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);
            var read = 0;

            for (var index = 0; index < cases.Count; index++)
            {
                var (probe, data) = cases[index];
                var described = () => $"case {index + 1} of {cases.Count} (seed {Seed + 1}): {Probes[probe].RequestType} answered with {Preview(data)}";

                var call = Probes[probe].Call(client);
                var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
                await server.SendRawAsync(
                    "{\"op\":7,\"d\":{\"requestType\":" + FrameGenerators.Quote(requestType) + ",\"requestId\":" + FrameGenerators.Quote(requestId)
                    + ",\"requestStatus\":{\"result\":true,\"code\":100},\"responseData\":" + data + "}}");

                if (await EndsAtOnceAsync(call, described))
                {
                    read++;
                }

                await AssertAnswersAsync(client, server, described);
            }

            output.WriteLine($"{cases.Count} responses with data of every shape: {read} were read, {cases.Count - read} were refused (seed {Seed + 1}).");
        }

        [Fact]
        public async Task WhateverAnEventHolds_TheClientGoesOnToAnswerTheNextRequest()
        {
            var generator = from type in Gen.Elements(Enum.GetNames<EventType>())
                            from data in FrameGenerators.EventData(type)
                            select "{\"op\":5,\"d\":{\"eventType\":" + FrameGenerators.Quote(type) + ",\"eventIntent\":1,\"eventData\":" + data + "}}";
            var frames = generator.Sample(Cases, new Rnd(Seed + 2), 8).ToList();
            await using var server = new FakeObsWebSocketServer();
            using var client = await ConnectAsync(server);

            // A handler on every event, so that the events are raised and not dropped for lack of a listener.
            var raised = 0;
            var counter = new EventCounter(() => Interlocked.Increment(ref raised));
            counter.SubscribeToEveryEventOf(client);

            for (var index = 0; index < frames.Count; index++)
            {
                var frame = frames[index];
                var described = () => $"event {index + 1} of {frames.Count} (seed {Seed + 2}): {Preview(frame)}";

                await server.SendRawAsync(frame);
                await AssertAnswersAsync(client, server, described);
            }

            // The events are raised one after the other on a task of their own: give the last ones a moment.
            await Task.Delay(300);
            output.WriteLine($"{frames.Count} events with data of every shape: {Volatile.Read(ref raised)} were raised (seed {Seed + 2}).");
        }

        private static ulong ReadNumber(string name, ulong fallback)
        {
            return ulong.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : fallback;
        }

        private static string Preview(string text)
        {
            return FrameGenerators.Quote(text.Length > 300 ? string.Concat(text.AsSpan(0, 300), "...") : text);
        }

        private static async Task<ObsClient> ConnectAsync(FakeObsWebSocketServer server)
        {
            server.Start();
            var client = new ObsClient { RequestTimeout = 60000 };
            var connect = client.ConnectAsync(hostname: "127.0.0.1", port: server.Port);
            await server.HandshakeAsync().WithTimeoutAsync(Timeout);
            await connect.WithTimeoutAsync(Timeout);
            return client;
        }

        // The check after each case: a request that is answered the right way is answered, and the client is still connected.
        private static async Task AssertAnswersAsync(ObsClient client, FakeObsWebSocketServer server, Func<string> described)
        {
            try
            {
                var call = client.GetSceneList();
                var (requestType, requestId, _) = await server.ReceiveRequestAsync(Timeout);
                await server.SendRequestSuccessAsync(requestType, requestId, new { scenes = Array.Empty<object>() });

                var response = await call.WithTimeoutAsync(Timeout, "The request was not answered.");

                Assert.Empty(response.Scenes);
                Assert.Equal(ConnectionState.Connected, client.ConnectionState);
            }
            catch (Exception exception)
            {
                throw new XunitException($"The client did not go on after {described()}\n{exception.GetType().Name}: {exception.Message}", exception);
            }
        }

        // A call that was answered with a response that cannot be read fails at once, and with the exception of the client, or it has what
        // could be read: waiting for the timeout (a minute here) would be the bug. Tells whether the response was read.
        private static async Task<bool> EndsAtOnceAsync(Task call, Func<string> described)
        {
            var finished = await Task.WhenAny(call, Task.Delay(Timeout));
            if (!ReferenceEquals(call, finished))
            {
                throw new XunitException($"The call was still waiting for its timeout after {described()}");
            }

            try
            {
                await call;
                return true;
            }
            catch (ObsClientException)
            {
                // The response could not be read, and the call says so.
                return false;
            }
            catch (Exception exception)
            {
                throw new XunitException($"The call failed with {exception.GetType().Name} instead of an ObsClientException after {described()}\n{exception}", exception);
            }
        }

        // Counts the events that the client raises: it has an event for every event of the protocol, and this subscribes to each of them.
        private sealed class EventCounter(Action counted)
        {
            public void SubscribeToEveryEventOf(ObsClient client)
            {
                var names = new HashSet<string>(Enum.GetNames<EventType>(), StringComparer.Ordinal);
                foreach (var protocolEvent in typeof(ObsClient).GetEvents().Where(candidate => candidate.EventHandlerType is not null && names.Contains(candidate.Name)))
                {
                    var handlerType = protocolEvent.EventHandlerType!;
                    var handler = handlerType.IsGenericType
                        ? typeof(EventCounter).GetMethod(nameof(Count), BindingFlags.Instance | BindingFlags.NonPublic)!
                            .MakeGenericMethod(handlerType.GetGenericArguments()[0]).CreateDelegate(handlerType, this)
                        : Delegate.CreateDelegate(handlerType, this, typeof(EventCounter).GetMethod(nameof(CountPlain), BindingFlags.Instance | BindingFlags.NonPublic)!);
                    protocolEvent.AddEventHandler(client, handler);
                }
            }

            private void Count<TArgs>(object? sender, TArgs args) => counted();

            private void CountPlain(object? sender, EventArgs args) => counted();
        }
    }
}
