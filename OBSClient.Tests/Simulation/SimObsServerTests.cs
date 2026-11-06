namespace OBSStudioClient.Tests.Simulation
{
    using System.Diagnostics;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The WebSocket front-end of the simulation (<see cref="SimObsServer"/>), with the real <see cref="ObsClient"/> on the loopback
    /// address: the handshake with and without a password, requests, the events and their subscription, Reidentify, a connection that is
    /// reset, a client that comes after another, request batches, and the codes with which obs-websocket closes a connection. The tests
    /// use quirks without any lag, so that they are exact in real time, and wait for what happens, and never for a time to pass.
    /// </summary>
    public class SimObsServerTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        // The quirks of the perfect OBS Studio, with the one that a client of the real one has to live with, and that is about the events.
        private static readonly SimQuirks Quirks = SimQuirks.Ideal with { DuplicateSceneCreated = true };

        private static SimObsServer StartServer(SimQuirks? quirks = null, string? password = null)
        {
            var server = new SimObsServer(new SimObs(quirks ?? Quirks, seed: 1), password);
            server.Start();
            return server;
        }

        private static async Task<ObsClient> ConnectAsync(SimObsServer server, string password = "", EventSubscriptions subscriptions = EventSubscriptions.All, bool autoReconnect = false)
        {
            var client = new ObsClient { RequestTimeout = 5000 };
            var connected = await client.ConnectAsync(autoReconnect, password, "127.0.0.1", server.Port, subscriptions).WithTimeoutAsync(Timeout);
            Assert.True(connected, "The client could not connect to the simulation.");
            return client;
        }

        private static EventLog Listen(ObsClient client)
        {
            var log = new EventLog();
            client.SceneCreated += (_, _) => log.Add("SceneCreated");
            client.SceneRemoved += (_, _) => log.Add("SceneRemoved");
            client.SceneNameChanged += (_, _) => log.Add("SceneNameChanged");
            client.SceneListChanged += (_, _) => log.Add("SceneListChanged");
            client.CurrentProgramSceneChanged += (_, _) => log.Add("CurrentProgramSceneChanged");
            client.InputCreated += (_, _) => log.Add("InputCreated");
            client.InputRemoved += (_, _) => log.Add("InputRemoved");
            client.SceneItemCreated += (_, _) => log.Add("SceneItemCreated");
            client.SceneItemSelected += (_, _) => log.Add("SceneItemSelected");
            return log;
        }

        [Fact]
        public async Task AClientConnectsWithoutAPassword_AndTheServerHasIdentifiedIt()
        {
            await using var server = StartServer();
            using var client = await ConnectAsync(server);

            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
            Assert.True(server.IsIdentified);
            Assert.Equal(1, server.IdentifiedCount);
            Assert.Equal(1, server.ConnectionCount);
            Assert.Equal((long)EventSubscriptions.All, server.EventSubscriptions);
            client.Disconnect();
        }

        [Fact]
        public async Task AClientWithThePasswordIsIdentified_AndAClientWithAnotherPasswordIsNot()
        {
            await using var server = StartServer(password: "P@ssw0rd!");

            using var wrong = new ObsClient();
            var refused = await wrong.ConnectAsync(password: "not the password", hostname: "127.0.0.1", port: server.Port).WithTimeoutAsync(Timeout);
            Assert.False(refused);
            Assert.Equal(0, server.IdentifiedCount);

            using var right = await ConnectAsync(server, "P@ssw0rd!");
            Assert.Equal(ConnectionState.Connected, right.ConnectionState);
            Assert.Equal(1, server.IdentifiedCount);
            right.Disconnect();
        }

        [Fact]
        public async Task TheClientReadsTheScenesOfTheSimulation_AndCreatesOne()
        {
            await using var server = StartServer();
            using var client = await ConnectAsync(server);

            var before = await client.GetSceneList();
            var created = await client.CreateScene("Second");
            var after = await client.GetSceneList();

            Assert.Equal(["Scene"], before.Scenes.Select(scene => scene.SceneName).ToArray());
            Assert.Equal("Scene", before.CurrentProgramSceneName);
            Assert.Equal(["Second", "Scene"], after.Scenes.Select(scene => scene.SceneName).ToArray());
            Assert.Equal(created, after.Scenes[0].SceneUuid);
            Assert.Equal(created, server.Run(sim => sim.Snapshot().Scenes[0].Uuid));
            client.Disconnect();
        }

        [Fact]
        public async Task TheSimulationSaysWhichObsStudioItIs()
        {
            await using var server = StartServer();
            using var client = await ConnectAsync(server);

            var version = await client.GetVersion();

            Assert.Equal("33.0.0", version.ObsVersion);
            Assert.Equal("5.7.6", version.ObsWebSocketVersion);
            Assert.Equal(1, version.RpcVersion);
            Assert.Equal(SimRequests.Implemented.ToArray(), version.AvailableRequests.Order(StringComparer.Ordinal).ToArray());
            client.Disconnect();
        }

        [Fact]
        public async Task Fact10_CreatingAScene_IsAnnouncedTwice_WithSceneListChangedInBetween()
        {
            await using var server = StartServer();
            using var client = await ConnectAsync(server);
            var log = Listen(client);

            await client.CreateScene("Second");
            await log.WaitAsync(names => names.Count >= 3);

            Assert.Equal(["SceneCreated", "SceneListChanged", "SceneCreated"], log.Names);
            client.Disconnect();
        }

        [Fact]
        public async Task ARequestThatFails_IsAnsweredWithTheStatusAndTheCommentOfObsWebsocket()
        {
            await using var server = StartServer();
            using var client = await ConnectAsync(server);
            await client.CreateScene("Second");

            var failure = await Assert.ThrowsAsync<OBSStudioClient.Exceptions.ObsResponseException>(() => client.CreateScene("Second"));

            Assert.Equal(RequestStatusCode.ResourceAlreadyExists, failure.ErrorCode);
            Assert.Equal("A source already exists by that scene name.", failure.Message);
            client.Disconnect();
        }

        [Fact]
        public async Task OnlyTheEventsOfTheCategoriesThatWereSubscribedToAreSent()
        {
            await using var server = StartServer();
            using var client = await ConnectAsync(server, subscriptions: EventSubscriptions.Inputs);
            var log = Listen(client);

            // The scene, and the scene item of each input, are events of categories that nobody asked for. They would come between
            // the two events of the inputs, in the order in which OBS Studio makes them.
            await client.CreateScene("Second");
            await client.CreateInput("Second", "One", "color_source_v3");
            await client.CreateInput("Second", "Two", "color_source_v3");
            await log.WaitAsync(names => names.Count >= 2);
            await client.GetVersion();

            Assert.Equal(["InputCreated", "InputCreated"], log.Names);
            Assert.Equal((long)EventSubscriptions.Inputs, server.EventSubscriptions);
            client.Disconnect();
        }

        [Fact]
        public async Task Reidentify_ChangesTheEventsThatAreSent()
        {
            await using var server = StartServer();
            using var client = await ConnectAsync(server, subscriptions: EventSubscriptions.None);
            var log = Listen(client);

            await client.CreateScene("Quiet");
            await client.ReidentifyAsync(EventSubscriptions.Scenes);

            // The request is read after the Reidentify that was sent before it, so the events of its change are sent to the new subscription.
            await client.CreateScene("Loud");
            await log.WaitAsync(names => names.Count >= 3);

            Assert.Equal(["SceneCreated", "SceneListChanged", "SceneCreated"], log.Names);
            Assert.Equal((long)EventSubscriptions.Scenes, server.EventSubscriptions);
            client.Disconnect();
        }

        [Fact]
        public async Task ChangesOfSomeoneElse_AreAnnouncedToTheClient()
        {
            await using var server = StartServer();
            using var client = await ConnectAsync(server);
            var log = Listen(client);

            server.Run(sim => sim.External.CreateScene("FromTheUserInterface"));
            await log.WaitAsync(names => names.Count >= 3);

            Assert.Equal(["SceneCreated", "SceneListChanged", "SceneCreated"], log.Names);
            Assert.Contains((await client.GetSceneList()).Scenes, scene => scene.SceneName == "FromTheUserInterface");
            client.Disconnect();
        }

        [Fact]
        public async Task ARequestBatchIsRunByTheSimulation_AndAHaltedBatchStopsAtTheFailure()
        {
            await using var server = StartServer();
            using var client = await ConnectAsync(server);

            var batch = new RequestBatchMessage();
            batch.AddCreateSceneRequest("A");
            batch.AddCreateSceneRequest("A");
            batch.AddCreateSceneRequest("B");
            batch.AddGetSceneListRequest();
            var all = await client.SendRequestBatchAsync(batch);

            var halting = new RequestBatchMessage(haltOnFailure: true);
            halting.AddCreateSceneRequest("C");
            halting.AddCreateSceneRequest("C");
            halting.AddCreateSceneRequest("D");
            var halted = await client.SendRequestBatchAsync(halting);

            Assert.Equal([100, 601, 100, 100], all.Select(result => (int)result.RequestStatus.Code).ToArray());
            Assert.Equal([100, 601], halted.Select(result => (int)result.RequestStatus.Code).ToArray());
            Assert.Equal(["C", "B", "A", "Scene"], (await client.GetSceneList()).Scenes.Select(scene => scene.SceneName).ToArray());
            client.Disconnect();
        }

        [Fact]
        public async Task AParallelBatchComesBackWithTheResultsOfAllItsRequests()
        {
            await using var server = StartServer(Quirks with { ParallelBatchMisorders = true });
            using var client = await ConnectAsync(server);

            var batch = new RequestBatchMessage(requestBatchExecutionType: RequestBatchExecutionType.Parallel);
            batch.AddGetStudioModeEnabledRequest();
            batch.AddGetSceneListRequest();
            batch.AddGetVersionRequest();
            var results = await client.SendRequestBatchAsync(batch);

            Assert.Equal(3, results.Length);
            Assert.All(results, result => Assert.True(result.RequestStatus.Result));
            client.Disconnect();
        }

        [Fact]
        public async Task WhenTheConnectionIsReset_AClientWithAutoReconnectComesBack_AndGetsTheEventsAgain()
        {
            await using var server = StartServer();
            using var client = await ConnectAsync(server, autoReconnect: true);
            var log = Listen(client);

            server.AbortConnection();

            // The client tries again on a timer of its own, which takes a few seconds.
            await server.WaitForIdentifiedAsync(2, TimeSpan.FromSeconds(20));
            server.Run(sim => sim.External.CreateScene("AfterTheReset"));
            await log.WaitAsync(names => names.Contains("SceneCreated"));

            Assert.Equal(ConnectionState.Connected, client.ConnectionState);
            Assert.Equal(2, server.ConnectionCount);
            Assert.Contains((await client.GetSceneList()).Scenes, scene => scene.SceneName == "AfterTheReset");
            client.Disconnect();
        }

        [Fact]
        public async Task ANewConnectionReplacesTheOneBeforeIt_AndGetsAFreshHandshake()
        {
            await using var server = StartServer();
            using var first = await ConnectAsync(server);
            var closed = new EventAwaiter<ConnectionClosedEventArgs>();
            first.ConnectionClosed += closed.Handler;

            using var second = await ConnectAsync(server);
            await closed.Task.WithTimeoutAsync(Timeout, "The first connection was not ended.");

            Assert.Equal(2, server.ConnectionCount);
            Assert.Equal(2, server.IdentifiedCount);
            Assert.Equal(["Scene"], (await second.GetSceneList()).Scenes.Select(scene => scene.SceneName).ToArray());
            second.Disconnect();
        }

        [Fact]
        public async Task TimeOfTheSimulationFollowsTheClockOfTheMachine()
        {
            await using var server = StartServer(Quirks with { ResponseLatency = SimRange.Exactly(60) });
            using var client = await ConnectAsync(server);

            var watch = Stopwatch.StartNew();
            await client.GetVersion();
            watch.Stop();

            Assert.InRange(watch.ElapsedMilliseconds, 50, 5000);
            client.Disconnect();
        }

        [Fact]
        public async Task WhatWasDueForAConnectionThatIsGone_IsNotSentToTheNextOne()
        {
            await using var server = StartServer(Quirks with { ResponseLatency = SimRange.Exactly(60) });
            using var gone = await RawClient.ConnectAsync(server.Port);
            await gone.IdentifyAsync();
            await gone.SendAsync("""{"op":6,"d":{"requestType":"GetVersion","requestId":"orphan"}}""");
            await server.WaitForRequestsAsync(1, Timeout);
            gone.Abort();

            using var next = await RawClient.ConnectAsync(server.Port);
            await next.IdentifyAsync();
            await next.SendAsync("""{"op":6,"d":{"requestType":"GetVersion","requestId":"mine"}}""");

            // Both responses were due at about the same time, the orphan's first. Everything that is sent to the second connection comes in order.
            var requestIds = new List<string>();
            while (true)
            {
                using var message = await next.ReceiveAsync();
                Assert.NotNull(message);
                var data = message.RootElement.GetProperty("d");
                if (message.RootElement.GetProperty("op").GetInt32() == SimOp.RequestResponse)
                {
                    requestIds.Add(data.GetProperty("requestId").GetString()!);
                    break;
                }
            }

            Assert.Equal(["mine"], requestIds);
        }

        [Fact]
        public async Task TheResponseToARequestOfAConnectionThatIsGone_IsNotGivenToTheNextOne_EvenWhenItUsesTheSameRequestId()
        {
            await using var server = StartServer(Quirks with { ResponseLatency = SimRange.Exactly(60) });
            using var gone = await RawClient.ConnectAsync(server.Port);
            await gone.IdentifyAsync();
            await gone.SendAsync("""{"op":6,"d":{"requestType":"GetVersion","requestId":"same"}}""");
            await server.WaitForRequestsAsync(1, Timeout);
            gone.Abort();

            using var next = await RawClient.ConnectAsync(server.Port);
            await next.IdentifyAsync();
            await next.SendAsync("""{"op":6,"d":{"requestType":"GetStudioModeEnabled","requestId":"same"}}""");

            var types = new List<string>();
            while (true)
            {
                using var message = await next.ReceiveAsync();
                Assert.NotNull(message);
                if (message.RootElement.GetProperty("op").GetInt32() == SimOp.RequestResponse)
                {
                    types.Add(message.RootElement.GetProperty("d").GetProperty("requestType").GetString()!);
                    break;
                }
            }

            Assert.Equal(["GetStudioModeEnabled"], types);
        }

        // obs-websocket writes every object with its members in alphabetical order, the one that a client sent as the ID of a request too, so the sorted
        // object is what comes back; the shape check of the simulation is the one that says so.
        [Theory]
        [InlineData("7", "7")]
        [InlineData("null", "null")]
        [InlineData("\"text\"", "\"text\"")]
        [InlineData("""{"b":1,"a":{"d":1,"c":[{"z":1,"y":2}]}}""", """{"a":{"c":[{"y":2,"z":1}],"d":1},"b":1}""")]
        [InlineData("""[{"b":1,"a":2},3]""", """[{"a":2,"b":1},3]""")]
        public async Task ARequestIdOfAnyKind_IsAnsweredWithTheSameId_WithTheMembersOfObjectsInOrder(string id, string expected)
        {
            await using var server = StartServer();
            using var raw = await RawClient.ConnectAsync(server.Port);
            await raw.IdentifyAsync();

            await raw.SendAsync($$$"""{"op":6,"d":{"requestType":"GetVersion","requestId":{{{id}}}}}""");
            await raw.SendAsync($$$"""{"op":8,"d":{"requestId":{{{id}}},"requests":[{"requestType":"GetVersion"}]}}""");

            var echoed = new Dictionary<int, string>();
            var problems = new List<string>();
            while (echoed.Count < 2)
            {
                using var message = await raw.ReceiveAsync();
                Assert.NotNull(message);
                var op = message.RootElement.GetProperty("op").GetInt32();
                if (op is SimOp.RequestResponse or SimOp.RequestBatchResponse)
                {
                    echoed[op] = message.RootElement.GetProperty("d").GetProperty("requestId").GetRawText();
                    problems.AddRange(SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, op, message.RootElement.GetRawText())));
                }
            }

            Assert.Equal(expected, echoed[SimOp.RequestResponse]);
            Assert.Equal(expected, echoed[SimOp.RequestBatchResponse]);
            Assert.Empty(problems);
        }

        [Fact]
        public async Task AClientThatClosesTheConnectionWhileEventsAreSent_GetsTheAcknowledgement()
        {
            await using var server = StartServer();
            using var raw = await RawClient.ConnectAsync(server.Port);
            await raw.IdentifyAsync();

            var changes = Task.Run(() =>
            {
                for (var index = 0; index < 200; index++)
                {
                    var name = "S" + index;
                    server.Run(sim => sim.External.CreateScene(name));
                }
            });
            await raw.CloseAsync();
            await changes;

            Assert.Equal(WebSocketCloseStatus.NormalClosure, raw.CloseStatus);
        }

        [Fact]
        public async Task NoEventIsSentBeforeTheIdentifiedMessage()
        {
            await using var server = StartServer();
            for (var round = 0; round < 100; round++)
            {
                using var raw = await RawClient.ConnectAsync(server.Port);
                using var hello = await raw.ReceiveAsync();

                // A change is made while the client identifies: its events are due at once.
                var name = "S" + round;
                var change = Task.Run(() => server.Run(sim => sim.External.CreateScene(name)));
                await raw.SendAsync("""{"op":1,"d":{"rpcVersion":1}}""");
                using var first = await raw.ReceiveAsync();
                await change;

                Assert.Equal(SimOp.Identified, first!.RootElement.GetProperty("op").GetInt32());
            }
        }

        [Fact]
        public async Task TheCountOfIdentifiedConnections_IsNeverSeenBeforeTheConnectionIsIdentified()
        {
            // Whoever waits for the count (WaitForIdentifiedAsync) goes on with a change of the simulation, and Dispatch only sends a change to a connection that
            // is identified: when the count is seen before the connection is, the change is lost without a trace. A thread that does nothing but watch the count
            // is as quick to see it as anyone can be, and a round ends before the next connection replaces this one. The watcher runs at a low priority, because it
            // spins: the tests that run next to this one (on a machine with few cores, as a build agent is) must not wait for it.
            await using var server = StartServer();
            for (var round = 1; round <= 100; round++)
            {
                var expected = round;
                var seen = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously);
                var watcher = new Thread(() =>
                {
                    var clock = Stopwatch.StartNew();
                    while (server.IdentifiedCount < expected)
                    {
                        if (clock.Elapsed > Timeout)
                        {
                            seen.SetResult(null);
                            return;
                        }
                    }

                    seen.SetResult(server.IsIdentified);
                })
                {
                    IsBackground = true,
                    Priority = ThreadPriority.BelowNormal,
                };
                watcher.Start();

                using var raw = await RawClient.ConnectAsync(server.Port);
                await raw.IdentifyAsync();

                var identified = await seen.Task;
                Assert.NotNull(identified);
                Assert.True(identified, $"In round {round} the count of identified connections was seen before the connection was identified.");
            }
        }

        [Fact]
        public async Task AnEventSubscriptionThatIsAnUnsigned64BitNumber_IsAccepted_AndEveryEventIsSent()
        {
            // obs-websocket reads the mask as a 64 bit unsigned number, so the largest value is a valid mask: all bits set, every category subscribed.
            await using var server = StartServer();
            using var raw = await RawClient.ConnectAsync(server.Port);
            using var hello = await raw.ReceiveAsync();
            await raw.SendAsync("""{"op":1,"d":{"rpcVersion":1,"eventSubscriptions":18446744073709551615}}""");
            using var identified = await raw.ReceiveAsync();

            Assert.Equal(SimOp.Identified, identified!.RootElement.GetProperty("op").GetInt32());
            Assert.Equal(-1L, server.EventSubscriptions);
            server.Run(sim => sim.External.CreateScene("S"));
            using var first = await raw.ReceiveAsync();
            Assert.Equal(SimOp.Event, first!.RootElement.GetProperty("op").GetInt32());
        }

        [Fact]
        public async Task WhenTheConnectionIsGone_NoEventSubscriptionIsReported()
        {
            await using var server = StartServer();
            using var raw = await RawClient.ConnectAsync(server.Port);
            await raw.IdentifyAsync();
            Assert.Equal((long)EventSubscriptions.All, server.EventSubscriptions);

            raw.Abort();
            var deadline = Stopwatch.StartNew();
            while (server.EventSubscriptions is not null && deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(10);
            }

            Assert.False(server.IsIdentified);
            Assert.Null(server.EventSubscriptions);
        }

        [Fact]
        public async Task TheHandshakeIsTheOneOfObsWebsocket()
        {
            await using var server = StartServer();
            using var raw = await RawClient.ConnectAsync(server.Port);

            using var hello = await raw.ReceiveAsync();
            await raw.SendAsync("""{"op":1,"d":{"rpcVersion":1,"eventSubscriptions":4}}""");
            using var identified = await raw.ReceiveAsync();

            Assert.Equal("""{"d":{"obsStudioVersion":"33.0.0","obsWebSocketVersion":"5.7.6","rpcVersion":1},"op":0}""", hello!.RootElement.GetRawText());
            Assert.Equal("""{"d":{"negotiatedRpcVersion":1},"op":2}""", identified!.RootElement.GetRawText());
            Assert.Equal(4L, server.EventSubscriptions);
        }

        [Fact]
        public async Task WithAPassword_TheHelloHasAChallengeAndASalt_AndTheCorrectAnswerIsAccepted()
        {
            await using var server = StartServer(password: "secret");
            using var raw = await RawClient.ConnectAsync(server.Port);

            using var hello = await raw.ReceiveAsync();
            var authentication = hello!.RootElement.GetProperty("d").GetProperty("authentication");
            var salt = authentication.GetProperty("salt").GetString()!;
            var challenge = authentication.GetProperty("challenge").GetString()!;
            var secret = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("secret" + salt)));
            var answer = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
            await raw.SendAsync("{\"op\":1,\"d\":{\"rpcVersion\":1,\"authentication\":\"" + answer + "\"}}");
            using var identified = await raw.ReceiveAsync();

            Assert.Equal(SimOp.Identified, identified!.RootElement.GetProperty("op").GetInt32());
        }

        [Theory]
        [InlineData("not json", true, SimCloseCode.MessageDecodeError)]
        [InlineData("""{"op":42,"d":{}}""", true, SimCloseCode.UnknownOpCode)]
        [InlineData("""{"d":{}}""", true, SimCloseCode.UnknownOpCode)]
        [InlineData("""{"op":"6","d":{}}""", true, SimCloseCode.UnknownOpCode)]
        [InlineData("""{"op":6,"d":{"requestType":"GetVersion"}}""", true, SimCloseCode.MissingDataField)]
        [InlineData("""{"op":6,"d":{"requestId":"1","requestType":5}}""", true, SimCloseCode.InvalidDataFieldType)]
        [InlineData("""{"op":8,"d":{"requestId":"1","requests":[],"executionType":9}}""", true, SimCloseCode.InvalidDataFieldValue)]
        [InlineData("""{"op":1,"d":{"rpcVersion":1}}""", true, SimCloseCode.AlreadyIdentified)]
        [InlineData("""{"op":6,"d":{"requestType":"GetVersion","requestId":"1"}}""", false, SimCloseCode.NotIdentified)]
        [InlineData("""{"op":1,"d":{}}""", false, SimCloseCode.MissingDataField)]
        [InlineData("""{"op":1,"d":{"rpcVersion":2}}""", false, SimCloseCode.UnsupportedRpcVersion)]
        [InlineData("""{"op":1,"d":{"rpcVersion":"1"}}""", false, SimCloseCode.InvalidDataFieldType)]
        [InlineData("""{"op":1,"d":{"rpcVersion":1,"eventSubscriptions":-1}}""", false, SimCloseCode.InvalidDataFieldType)]
        [InlineData("""{"op":1,"d":{"rpcVersion":1,"eventSubscriptions":1.5}}""", false, SimCloseCode.InvalidDataFieldType)]
        [InlineData("""{"op":1,"d":{"rpcVersion":1,"eventSubscriptions":18446744073709551616}}""", false, SimCloseCode.InvalidDataFieldType)]
        public async Task AMessageThatObsWebsocketDoesNotAccept_ClosesTheConnectionWithItsCloseCode(string message, bool identifyFirst, int closeCode)
        {
            await using var server = StartServer();
            using var raw = await RawClient.ConnectAsync(server.Port);
            if (identifyFirst)
            {
                await raw.IdentifyAsync();
            }
            else
            {
                using var hello = await raw.ReceiveAsync();
            }

            await raw.SendAsync(message);
            using var next = await raw.ReceiveAsync();

            Assert.Null(next);
            Assert.Equal(closeCode, (int)raw.CloseStatus!.Value);
        }

        [Fact]
        public async Task AnAnswerThatIsWrong_ClosesTheConnectionWithAuthenticationFailed()
        {
            await using var server = StartServer(password: "secret");
            using var raw = await RawClient.ConnectAsync(server.Port);

            using var hello = await raw.ReceiveAsync();
            await raw.SendAsync("""{"op":1,"d":{"rpcVersion":1,"authentication":"wrong"}}""");
            using var next = await raw.ReceiveAsync();

            Assert.Null(next);
            Assert.Equal(SimCloseCode.AuthenticationFailed, (int)raw.CloseStatus!.Value);
            Assert.Equal(0, server.IdentifiedCount);
        }

        [Fact]
        public async Task ABinaryMessage_ClosesTheConnection_BecauseTheSessionIsJson()
        {
            await using var server = StartServer();
            using var raw = await RawClient.ConnectAsync(server.Port);
            await raw.IdentifyAsync();

            await raw.SendBinaryAsync([1, 2, 3]);
            using var next = await raw.ReceiveAsync();

            Assert.Null(next);
            Assert.Equal(SimCloseCode.MessageDecodeError, (int)raw.CloseStatus!.Value);
        }

        [Fact]
        public async Task ARequestAnsweredOnTheRawConnection_HasTheShapeOfObsWebsocket()
        {
            await using var server = StartServer();
            using var raw = await RawClient.ConnectAsync(server.Port);
            await raw.IdentifyAsync();

            await raw.SendAsync("""{"op":6,"d":{"requestType":"CreateScene","requestId":"x","requestData":{"sceneName":"A"}}}""");
            string responseJson;
            while (true)
            {
                using var message = await raw.ReceiveAsync();
                if (message!.RootElement.GetProperty("op").GetInt32() != SimOp.RequestResponse)
                {
                    continue;
                }

                responseJson = message.RootElement.GetRawText();
                var data = message.RootElement.GetProperty("d");
                Assert.Equal("x", data.GetProperty("requestId").GetString());
                Assert.Equal("CreateScene", data.GetProperty("requestType").GetString());
                Assert.True(data.GetProperty("requestStatus").GetProperty("result").GetBoolean());
                Assert.True(Guid.TryParse(data.GetProperty("responseData").GetProperty("sceneUuid").GetString(), out _));
                break;
            }

            Assert.Empty(SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, responseJson)));
        }

        private sealed class EventLog
        {
            private readonly List<string> _names = [];
            private readonly List<(Func<IReadOnlyList<string>, bool> Condition, TaskCompletionSource Done)> _waiters = [];

            public string[] Names
            {
                get
                {
                    lock (_names)
                    {
                        return _names.ToArray();
                    }
                }
            }

            public void Add(string name)
            {
                lock (_names)
                {
                    _names.Add(name);
                    foreach (var waiter in _waiters.Where(waiter => waiter.Condition(_names)).ToArray())
                    {
                        _waiters.Remove(waiter);
                        waiter.Done.TrySetResult();
                    }
                }
            }

            public Task WaitAsync(Func<IReadOnlyList<string>, bool> condition)
            {
                lock (_names)
                {
                    if (condition(_names))
                    {
                        return Task.CompletedTask;
                    }

                    var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _waiters.Add((condition, done));
                    return done.Task.WaitAsync(TimeSpan.FromSeconds(20));
                }
            }
        }

        // A client that speaks the protocol by hand, to see what the server sends and how it closes a connection.
        private sealed class RawClient : IDisposable
        {
            private readonly ClientWebSocket _socket = new();

            public WebSocketCloseStatus? CloseStatus => _socket.CloseStatus;

            public static async Task<RawClient> ConnectAsync(int port)
            {
                var client = new RawClient();
                client._socket.Options.AddSubProtocol("obswebsocket.json");
                using var cancellation = new CancellationTokenSource(Timeout);
                await client._socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"), cancellation.Token);
                return client;
            }

            // Reads the Hello, answers with an Identify (for a server without a password) and reads the Identified.
            public async Task IdentifyAsync()
            {
                using var hello = await ReceiveAsync();
                await SendAsync("""{"op":1,"d":{"rpcVersion":1}}""");
                using var identified = await ReceiveAsync();
                Assert.Equal(SimOp.Identified, identified!.RootElement.GetProperty("op").GetInt32());
            }

            public async Task SendAsync(string json)
            {
                using var cancellation = new CancellationTokenSource(Timeout);
                await _socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, cancellation.Token);
            }

            public async Task SendBinaryAsync(byte[] bytes)
            {
                using var cancellation = new CancellationTokenSource(Timeout);
                await _socket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellation.Token);
            }

            // Closes the connection with the handshake of WebSocket, and waits for the acknowledgement of the server.
            public async Task CloseAsync()
            {
                using var cancellation = new CancellationTokenSource(Timeout);
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cancellation.Token);
            }

            // Reads a message, or returns null when the server closed the connection.
            public async Task<JsonDocument?> ReceiveAsync()
            {
                var buffer = new byte[16 * 1024];
                using var stream = new MemoryStream();
                using var cancellation = new CancellationTokenSource(Timeout);
                while (true)
                {
                    var result = await _socket.ReceiveAsync(buffer, cancellation.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return null;
                    }

                    stream.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        return JsonDocument.Parse(stream.ToArray());
                    }
                }
            }

            public void Abort()
            {
                _socket.Abort();
            }

            public void Dispose()
            {
                _socket.Dispose();
            }
        }
    }
}
