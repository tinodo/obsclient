namespace OBSStudioClient.Tests.Studio
{
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// <see cref="ObsStudio"/> against a stand-in for OBS Studio (see <see cref="WorldResponder"/>): the connection, the first read, what the events
    /// do to the model, the notifications, and the ways in which a connection fails.
    /// </summary>
    public class ObsStudioTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Fact]
        public async Task ConnectAsync_ReadsTheModel_BeforeItReturns()
        {
            Guid gaming = Guid.Empty;
            Guid mic = Guid.Empty;
            await using var rig = await StudioRig.ConnectedAsync(world =>
            {
                gaming = world.AddScene("Gaming");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(gaming, mic, "Mic");
            });
            var obs = rig.Studio;

            Assert.Equal(ObsStudioState.Synchronized, obs.State);
            Assert.False(obs.IsStale);
            Assert.Equal(new[] { "Gaming", "Scene" }, obs.Scenes.Select(scene => scene.Name).ToArray());
            var item = obs.Scenes["Gaming"].Items["Mic"];
            Assert.Same(obs.Inputs["Mic"], item.Source);
            Assert.Equal(new[] { item }, obs.Inputs["Mic"].GetSceneItems());
            Assert.Equal("Scene", obs.ProgramScene!.Name);
            Assert.True(obs.Scenes["Scene"].IsProgram);
            Assert.False(obs.Scenes["Gaming"].IsProgram);
            Assert.False(obs.IsStudioModeEnabled);
            Assert.Same(rig.Studio.Client, obs.Client);
        }

        [Fact]
        public async Task ASourceKnowsTheSceneItemsThatShowIt_AsTheyAreWhenYouAsk()
        {
            Guid gaming = Guid.Empty;
            Guid chatting = Guid.Empty;
            Guid mic = Guid.Empty;
            await using var rig = await StudioRig.ConnectedAsync(world =>
            {
                chatting = world.AddScene("Chatting");
                gaming = world.AddScene("Gaming");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(gaming, mic, "Mic");
                world.AddItem(gaming, chatting, "Chatting", isScene: true);
                world.AddItem(chatting, mic, "Mic");
            });
            var obs = rig.Studio;

            // The list of scenes is Gaming, Chatting, Scene: the scene items come in that order.
            var shown = obs.Inputs["Mic"].GetSceneItems();
            Assert.Equal(new[] { "Gaming", "Chatting" }, shown.Select(item => item.Scene.Name).ToArray());
            Assert.Equal(new[] { obs.Scenes["Gaming"].Items["Chatting"] }, obs.Scenes["Chatting"].GetSceneItems());
            Assert.Empty(obs.Scenes["Scene"].GetSceneItems());

            // A list that was taken is not changed, and the next answer is the new one.
            await rig.Responder.SendEventAsync("SceneItemRemoved", new { sceneName = "Gaming", sceneUuid = gaming, sourceName = "Mic", sourceUuid = mic, sceneItemId = 1 }, 128);
            await StudioRig.EventuallyAsync(() => obs.Inputs["Mic"].GetSceneItems().Count == 1, "the answer to follow the model");
            Assert.Equal(2, shown.Count);
            Assert.Equal("Chatting", obs.Inputs["Mic"].GetSceneItems().Single().Scene.Name);
        }

        [Fact]
        public async Task TheFirstRead_IsAnnouncedOnce_ByResynchronized_AndNotObjectByObject()
        {
            var resynchronized = 0;
            await using var rig = StudioRig.Create(world => world.AddScene("Gaming"));
            rig.Studio.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await StudioRig.EventuallyAsync(() => rig.Changes.Count(change => change.PropertyName == "State") >= 3, "the three changes of the state");

            // It is raised after the last change of the state, by the same notifier.
            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 1, "Resynchronized");
            await Task.Delay(100);
            Assert.Equal(1, Volatile.Read(ref resynchronized));
            Assert.Equal(
                new object?[] { ObsStudioState.Connecting, ObsStudioState.Synchronizing, ObsStudioState.Synchronized },
                rig.Changes.Where(change => change.PropertyName == "State").Select(change => change.NewValue).ToArray());
            Assert.DoesNotContain(rig.Changes, change => change.Kind == ObsChangeKind.Added);
        }

        [Fact]
        public async Task ARenameThatOBSAnnounces_ChangesTheObject_AndIsReported()
        {
            Guid gaming = Guid.Empty;
            await using var rig = await StudioRig.ConnectedAsync(world => gaming = world.AddScene("Gaming"));
            var scene = rig.Studio.Scenes["Gaming"];
            var raised = new ConcurrentQueue<string>();
            scene.PropertyChanged += (_, e) => raised.Enqueue(e.PropertyName!);

            rig.Responder.Change(world => world.Scenes[world.Scenes.FindIndex(s => s.Uuid == gaming)] = (gaming, "Playing"));
            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "Playing" }, 4);

            await StudioRig.EventuallyAsync(() => rig.Studio.Scenes.Contains("Playing"), "the new name");
            Assert.Same(scene, rig.Studio.Scenes["Playing"]);
            Assert.Throws<ObsNotFoundException>(() => rig.Studio.Scenes["Gaming"]);
            await StudioRig.EventuallyAsync(() => rig.Changes.Any(change => change.PropertyName == "Name"), "the notification");
            var change = rig.Changes.Single(c => c.PropertyName == "Name");
            Assert.Equal((ObsChangeKind.PropertyChanged, "Gaming", "Playing"), (change.Kind, change.OldValue, change.NewValue));
            Assert.Same(scene, change.Entity);
            await StudioRig.EventuallyAsync(() => raised.Contains("Name"), "the PropertyChanged event of the scene");
        }

        [Fact]
        public async Task TheMuteOfAnInputAndTheVisibilityOfASceneItem_FollowTheEvents()
        {
            Guid gaming = Guid.Empty;
            Guid mic = Guid.Empty;
            await using var rig = await StudioRig.ConnectedAsync(world =>
            {
                gaming = world.AddScene("Gaming");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                world.AddItem(gaming, mic, "Mic");
            });
            var input = rig.Studio.Inputs["Mic"];
            var item = rig.Studio.Scenes["Gaming"].Items["Mic"];

            await rig.Responder.SendEventAsync("InputMuteStateChanged", new { inputName = "Mic", inputUuid = mic, inputMuted = true }, 8);
            await rig.Responder.SendEventAsync("SceneItemEnableStateChanged", new { sceneName = "Gaming", sceneUuid = gaming, sceneItemId = 1, sceneItemEnabled = false }, 128);

            await StudioRig.EventuallyAsync(() => input.IsMuted && !item.IsEnabled, "both changes");
            await StudioRig.EventuallyAsync(() => rig.Changes.Count(change => change.PropertyName is "IsMuted" or "IsEnabled") == 2, "both notifications");
            Assert.Equal(new[] { "IsMuted", "IsEnabled" }, rig.Changes.Where(change => change.PropertyName is "IsMuted" or "IsEnabled").Select(change => change.PropertyName!).ToArray());
        }

        [Fact]
        public async Task ANewSceneAndItsItems_AreAvailable_WhenTheModelIsSettled()
        {
            Guid created = Guid.Empty;
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddInput("Mic"));
            created = Guid.NewGuid();
            rig.Responder.Change(world =>
            {
                world.Scenes.Insert(0, (created, "New"));
                world.Items[created] = [];
                world.AddItem(created, world.Inputs[0].Uuid, "Mic");
            });

            await rig.Responder.SendEventAsync("SceneCreated", new { sceneName = "New", sceneUuid = created, isGroup = false }, 4);
            await StudioRig.EventuallyAsync(() => rig.Studio.Scenes.Contains("New"), "the new scene");
            await rig.Studio.WaitUntilSettledAsync(Timeout);

            Assert.Single(rig.Studio.Scenes["New"].Items);
            Assert.Equal("Mic", rig.Studio.Scenes["New"].Items[0].Source.Name);
            await StudioRig.EventuallyAsync(() => rig.Changes.Any(change => change.Kind == ObsChangeKind.Added && change.Entity is ObsScene { Name: "New" }), "the notification of the scene");
            await StudioRig.EventuallyAsync(() => rig.Changes.Any(change => change.Kind == ObsChangeKind.Added && change.Entity is ObsSceneItem), "the notification of the item");
        }

        [Fact]
        public async Task AHandlerThatWaits_HoldsUpOnlyTheNotifications_NotTheModel()
        {
            Guid gaming = Guid.Empty;
            await using var rig = await StudioRig.ConnectedAsync(world => gaming = world.AddScene("Gaming"));
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var gate = new ManualResetEventSlim();
            rig.Studio.Changed += (_, e) =>
            {
                if (e.PropertyName == "Name")
                {
                    entered.TrySetResult();
                    gate.Wait(Timeout);
                }
            };

            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "A" }, 4);
            await entered.Task.WithTimeoutAsync(Timeout);

            // The handler is blocked in the middle of the first notification. The model goes on.
            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "A", sceneName = "B" }, 4);
            await StudioRig.EventuallyAsync(() => rig.Studio.Scenes.Contains("B"), "the model to go on while a handler waits");
            Assert.Single(rig.Changes, change => change.PropertyName == "Name");

            gate.Set();
            await StudioRig.EventuallyAsync(() => rig.Changes.Count(change => change.PropertyName == "Name") == 2, "the notifications behind the handler");
            Assert.Equal(new object?[] { "A", "B" }, rig.Changes.Where(change => change.PropertyName == "Name").Select(change => change.NewValue).ToArray());
        }

        [Fact]
        public async Task AHandlerThatThrows_DoesNotKeepTheOthersFromRunning_NorStopTheModel()
        {
            Guid gaming = Guid.Empty;
            await using var rig = await StudioRig.ConnectedAsync(world => gaming = world.AddScene("Gaming"));
            var seen = new ConcurrentQueue<string>();
            rig.Studio.Changed += (_, _) => throw new InvalidOperationException("A handler that is wrong.");
            rig.Studio.Changed += (_, e) => seen.Enqueue(e.PropertyName ?? string.Empty);

            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "A" }, 4);
            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "A", sceneName = "B" }, 4);

            await StudioRig.EventuallyAsync(() => seen.Count(name => name == "Name") == 2, "the handler behind the one that throws");
            Assert.True(rig.Studio.Scenes.Contains("B"));
        }

        [Fact]
        public async Task TheNotifications_CanBeRaisedOnAContext_OnTheThreadOfThatContext()
        {
            using var context = new SingleThreadContext();
            Guid gaming = Guid.Empty;
            await using var rig = StudioRig.Create(world => gaming = world.AddScene("Gaming"), new ObsStudioOptions { NotificationContext = context });
            var threads = new ConcurrentQueue<int>();
            rig.Studio.Changed += (_, _) => threads.Enqueue(Environment.CurrentManagedThreadId);
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "A" }, 4);

            await StudioRig.EventuallyAsync(() => rig.Changes.Any(change => change.PropertyName == "Name"), "the notification");

            Assert.NotEmpty(threads);
            Assert.All(threads, thread => Assert.Equal(context.ThreadId, thread));
        }

        [Fact]
        public async Task NotificationsThatWerePostedToAContextAndNotRunYet_AreDroppedWhenTheStudioIsDisposed()
        {
            var context = new HeldContext();
            Guid gaming = Guid.Empty;
            await using var rig = StudioRig.Create(world => gaming = world.AddScene("Gaming"), new ObsStudioOptions { NotificationContext = context });
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "A" }, 4);
            await StudioRig.EventuallyAsync(() => rig.Studio.Scenes.Contains("A"), "the model to have the new name");
            await StudioRig.EventuallyAsync(() => context.Count >= 2, "the notifications to be posted to the context");

            // The context is busy (a user interface that is closing): the studio is disposed of before it gets to what was posted.
            rig.Studio.Dispose();
            context.RunAll();

            Assert.Empty(rig.Changes);
            Assert.Empty(rig.Properties);
        }

        [Fact]
        public async Task ARetryRightAfterATimeout_IsNotFailedByTheEndOfTheConnectionOfTheFirstAttempt()
        {
            await using var rig = StudioRig.Create(options: new ObsStudioOptions { SynchronizationTimeout = TimeSpan.FromMilliseconds(1500) });
            rig.Responder.AnswerBatches = false;
            using var held = new ManualResetEventSlim();
            var atTheEnd = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Studio.Host.BeforeIngress = item =>
            {
                // The loop of the host is held at the end of the first connection until the retry has begun: the order in which an immediate retry can meet it,
                // because the client says that it is disconnected before the loop has got to the end of the connection.
                if (item.Kind == IngressKind.SessionEnded && !held.IsSet)
                {
                    atTheEnd.TrySetResult();
                    held.Wait(Timeout);
                }
            };

            await Assert.ThrowsAsync<TimeoutException>(() => rig.ConnectAsync());
            await atTheEnd.Task.WithTimeoutAsync(Timeout);
            rig.Responder.AnswerBatches = true;
            var retry = rig.ConnectAsync();
            await Task.Delay(400);
            held.Set();

            await retry.WithTimeoutAsync(Timeout);
            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
        }

        [Fact]
        public async Task ARetryWhileTheStartOfTheFirstConnectionIsStillQueued_IsNotBoundToThatConnection()
        {
            await using var rig = StudioRig.Create(options: new ObsStudioOptions { SynchronizationTimeout = TimeSpan.FromMilliseconds(1500) });
            using var held = new ManualResetEventSlim();
            var atTheStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStartQueued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var starts = 0;
            rig.Studio.Host.BeforeIngress = item =>
            {
                // The loop of the host is held at the start of the first connection, so that the start is still queued when the retry begins: the order in
                // which a loop that is late can meet it.
                if (item.Kind == IngressKind.SessionStarted && !held.IsSet)
                {
                    atTheStart.TrySetResult();
                    held.Wait(Timeout);
                }
            };
            rig.Studio.Host.AfterIngressPosted = item =>
            {
                if (item.Kind == IngressKind.SessionStarted && Interlocked.Increment(ref starts) == 2)
                {
                    secondStartQueued.TrySetResult();
                }
            };

            await Assert.ThrowsAsync<TimeoutException>(() => rig.ConnectAsync());
            await atTheStart.Task.WithTimeoutAsync(Timeout);
            var retry = rig.ConnectAsync();
            await secondStartQueued.Task.WithTimeoutAsync(Timeout);
            held.Set();

            // The loop handles the start and the end of the first connection first: they are not for the retry.
            await retry.WithTimeoutAsync(Timeout);
            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
        }

        [Fact]
        public async Task WhenTheClientStopsReconnecting_TheStateFollows_AndTheStudioCanConnectAgain()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"));
            rig.Responder.AbortConnection();
            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Reconnecting, "the state to follow the connection");

            // The client does this by itself when OBS Studio ends the session on purpose, a moment after the model was told that the connection ended.
            rig.Studio.Client.AutoReconnect = false;

            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Disconnected, "the state to say that nothing reconnects");
            Assert.True(rig.Studio.IsStale);
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
            Assert.True(rig.Studio.Scenes.Contains("Gaming"));
        }

        [Fact]
        public async Task WhenOBSStudioKicksTheSession_TheStateIsDisconnected_AndTheStudioCanConnectAgain()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"));

            await rig.Responder.CloseConnectionAsync(4011, "SessionInvalidated");

            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Disconnected, "the state to say that nothing reconnects");
            Assert.False(rig.Studio.Client.AutoReconnect);
            Assert.True(rig.Studio.IsStale);
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
        }

        [Fact]
        public async Task AFaultWhileAReadIsApplied_MakesTheModelReadAgain_AtOnce()
        {
            Guid gaming = Guid.Empty;
            await using var rig = StudioRig.Create(world => gaming = world.AddScene("Gaming"), new ObsStudioOptions { SynchronizationTimeout = TimeSpan.FromSeconds(5) });
            var faults = 1;
            rig.Studio.Host.BeforeReadApplied = _ =>
            {
                if (Interlocked.Decrement(ref faults) >= 0)
                {
                    throw new InvalidOperationException("A fault in the model, made by the test.");
                }
            };

            // Nothing else is on its way when the first read fails: the host has to start the next read itself.
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
            Assert.True(rig.Studio.Scenes.Contains("Gaming"));
        }

        [Fact]
        public async Task DisconnectAsync_ClosesTheConnection_AndTheModelKeepsWhatItKnew()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"));

            await rig.Studio.DisconnectAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsStudioState.Disconnected, rig.Studio.State);
            Assert.True(rig.Studio.IsStale);
            Assert.True(rig.Studio.Scenes.Contains("Gaming"));
            Assert.Equal(ConnectionState.Disconnected, rig.Studio.Client.ConnectionState);
            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Studio.WaitUntilSettledAsync(Timeout));
        }

        [Fact]
        public async Task DisconnectAsync_WithoutAConnection_DoesNothing()
        {
            using var obs = new ObsStudio();

            await obs.DisconnectAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsStudioState.Disconnected, obs.State);
            Assert.False(obs.IsStale);
        }

        [Fact]
        public async Task ALostConnection_WithoutAutoReconnect_LeavesAStaleModel()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"), new ObsStudioOptions { AutoReconnect = false });

            rig.Responder.AbortConnection();

            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Disconnected, "the state to follow the connection");
            Assert.True(rig.Studio.IsStale);
            Assert.True(rig.Studio.Scenes.Contains("Gaming"));
            await StudioRig.EventuallyAsync(() => rig.Properties.Contains("IsStale"), "the notification of IsStale");
        }

        [Fact]
        public async Task ALostConnection_WithAutoReconnect_IsReconnecting()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"));

            rig.Responder.AbortConnection();

            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Reconnecting, "the state to follow the connection");
            Assert.True(rig.Studio.IsStale);
        }

        [Fact]
        public async Task DisconnectAsync_ThenConnectAsync_WorksAtOnce_EveryTime()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"));

            // The client says that it is closed a little after the model is told: connecting right away must not be refused for that.
            for (var i = 0; i < 4; i++)
            {
                await rig.Studio.DisconnectAsync().WithTimeoutAsync(Timeout);
                Assert.Equal(ConnectionState.Disconnected, rig.Studio.Client.ConnectionState);
                await rig.ConnectAsync().WithTimeoutAsync(Timeout);
                Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
            }

            Assert.True(rig.Studio.Scenes.Contains("Gaming"));
        }

        [Fact]
        public async Task AReconnect_ReadsTheModelAgain_KeepsTheObjects_AndIsAnnouncedOnce()
        {
            Guid gaming = Guid.Empty;
            await using var rig = await StudioRig.ConnectedAsync(world => gaming = world.AddScene("Gaming"));
            var scene = rig.Studio.Scenes["Gaming"];
            var resynchronized = 0;
            rig.Studio.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);

            // The connection is lost, and OBS gets a new scene and renames one, while nobody is listening.
            rig.Responder.AbortConnection();
            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Reconnecting, "the state to follow the connection");
            Assert.True(rig.Studio.IsStale);
            var created = Guid.NewGuid();
            rig.Responder.Change(world =>
            {
                world.Scenes.Insert(0, (created, "New"));
                world.Items[created] = [];
                world.Scenes[world.Scenes.FindIndex(s => s.Uuid == gaming)] = (gaming, "Playing");
            });

            // The client tries again every 3 seconds.
            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Synchronized, "the model to be read again", TimeSpan.FromSeconds(30));

            Assert.False(rig.Studio.IsStale);
            Assert.Equal(new[] { "New", "Playing", "Scene" }, rig.Studio.Scenes.Select(s => s.Name).ToArray());
            Assert.Same(scene, rig.Studio.Scenes["Playing"]);
            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 1, "Resynchronized");

            // While the model is read it does not report object by object: Resynchronized says that everything may have changed.
            Assert.DoesNotContain(rig.Changes, change => change.Kind == ObsChangeKind.Added && change.Entity is ObsScene { Name: "New" });
        }

        [Fact]
        public async Task Dispose_EndsTheConnection_AndTheStudioCannotBeUsedAgain()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"));
            var obs = rig.Studio;

            obs.Dispose();
            obs.Dispose();

            // The client says that it is closed a little after Dispose returns: its receive loop ends on its own thread.
            await StudioRig.EventuallyAsync(() => obs.Client.ConnectionState == ConnectionState.Disconnected, "the client to close the connection");
            await Assert.ThrowsAsync<ObjectDisposedException>(() => obs.ConnectAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => obs.DisconnectAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => obs.WaitUntilSettledAsync());
            await obs.DisposeAsync();
        }

        [Fact]
        public async Task AfterDispose_TheStateIsDisconnected_AndWhatTheModelHoldsIsStale()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"));
            var obs = rig.Studio;
            Assert.Equal(ObsStudioState.Synchronized, obs.State);
            Assert.False(obs.IsStale);

            obs.Dispose();

            // The model may never be told that the connection ended (the observation of the client is stopped first): a studio that is disposed of is
            // not connected, and what it holds is the last that was known.
            Assert.Equal(ObsStudioState.Disconnected, obs.State);
            Assert.True(obs.IsStale);
            Assert.True(obs.Scenes.Contains("Gaming"));
        }

        [Fact]
        public async Task ConnectAsync_WhenNothingListens_Throws_AndTheStateIsDisconnected()
        {
            using var obs = new ObsStudio();

            var exception = await Assert.ThrowsAsync<ObsNotConnectedException>(() => obs.ConnectAsync("127.0.0.1", FreePort()));

            Assert.Contains("Could not connect", exception.Message);
            Assert.Equal(ObsStudioState.Disconnected, obs.State);
            Assert.Equal(ConnectionState.Disconnected, obs.Client.ConnectionState);
        }

        [Fact]
        public async Task ConnectAsync_WithACancelledToken_Throws_AndChangesNothing()
        {
            using var obs = new ObsStudio();
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => obs.ConnectAsync("127.0.0.1", FreePort(), cancellationToken: cancelled.Token));

            Assert.Equal(ObsStudioState.Disconnected, obs.State);
        }

        [Fact]
        public async Task ConnectAsync_TwiceInARow_Throws()
        {
            await using var rig = await StudioRig.ConnectedAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => rig.ConnectAsync());

            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
        }

        [Fact]
        public async Task ConnectAsync_ToAnObsThatIsTooOld_Throws_AndLeavesNothingConnected()
        {
            await using var rig = StudioRig.Create();
            rig.Responder.WebSocketVersion = "5.3.0";

            var exception = await Assert.ThrowsAsync<ObsVersionNotSupportedException>(() => rig.ConnectAsync().WithTimeoutAsync(Timeout));

            Assert.Equal(("5.3.0", "5.4.0"), (exception.ObsWebSocketVersion, exception.RequiredVersion));
            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Disconnected && rig.Studio.Client.ConnectionState == ConnectionState.Disconnected, "everything to be closed");
            Assert.Empty(rig.Studio.Scenes);
        }

        [Fact]
        public async Task ConnectAsync_ToAnObsWithAVersionSuffix_ReadsTheNumber()
        {
            await using var rig = StudioRig.Create();
            rig.Responder.WebSocketVersion = "5.5.0-beta1";

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);

            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
        }

        [Fact]
        public async Task ConnectAsync_ToAnObsBefore5_6_0_WhichSendsNoInputCapabilities_KnowsWhichInputsHaveAudio()
        {
            await using var rig = StudioRig.Create(world =>
            {
                world.OmitInputKindCaps = true;
                world.AddInput("Mic", "wasapi_input_capture", 2, muted: true);
                world.AddInput("Color");
            });
            rig.Responder.WebSocketVersion = "5.4.2";

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);

            var obs = rig.Studio;
            Assert.Equal(ObsStudioState.Synchronized, obs.State);
            Assert.Equal((true, true), (obs.Inputs["Mic"].HasAudio, obs.Inputs["Mic"].IsMuted));
            Assert.False(obs.Inputs["Color"].HasAudio);
        }

        [Fact]
        public async Task ConnectAsync_WhenTheModelIsNotCompleteInTime_Throws_AndClosesTheConnection()
        {
            await using var rig = StudioRig.Create(options: new ObsStudioOptions { SynchronizationTimeout = TimeSpan.FromMilliseconds(500) });
            rig.Responder.AnswerBatches = false;

            await Assert.ThrowsAsync<TimeoutException>(() => rig.ConnectAsync().WithTimeoutAsync(Timeout));

            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Disconnected && rig.Studio.Client.ConnectionState == ConnectionState.Disconnected, "everything to be closed");
        }

        [Fact]
        public async Task ConnectAsync_WhenTheConnectionIsLostBeforeTheModelIsComplete_Throws()
        {
            await using var rig = StudioRig.Create();
            rig.Responder.AnswerBatches = false;

            var connecting = rig.ConnectAsync();
            await StudioRig.EventuallyAsync(() => rig.Responder.Batches >= 1, "the first read to be sent");
            rig.Responder.AbortConnection();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => connecting.WithTimeoutAsync(Timeout));
            Assert.Equal(ConnectionState.Disconnected, rig.Studio.Client.ConnectionState);
        }

        [Fact]
        public async Task ConnectAsync_CanBeCancelledWhileTheModelIsRead()
        {
            await using var rig = StudioRig.Create();
            rig.Responder.AnswerBatches = false;
            using var cancellation = new CancellationTokenSource();

            var connecting = rig.ConnectAsync(cancellation.Token);
            await StudioRig.EventuallyAsync(() => rig.Responder.Batches >= 1, "the first read to be sent");
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting.WithTimeoutAsync(Timeout));
            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Disconnected, "the state");
        }

        [Fact]
        public async Task WaitUntilSettledAsync_CanTimeOut_AndBeCancelled()
        {
            await using var rig = await StudioRig.ConnectedAsync();
            rig.Responder.AnswerBatches = false;
            var created = Guid.NewGuid();
            rig.Responder.Change(world =>
            {
                world.Scenes.Insert(0, (created, "New"));
                world.Items[created] = [];
            });

            // A scene whose scene items are never answered: the model is never settled.
            await rig.Responder.SendEventAsync("SceneCreated", new { sceneName = "New", sceneUuid = created, isGroup = false }, 4);
            await StudioRig.EventuallyAsync(() => rig.Studio.Scenes.Contains("New"), "the new scene");

            await Assert.ThrowsAsync<TimeoutException>(() => rig.Studio.WaitUntilSettledAsync(TimeSpan.FromMilliseconds(300)));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Studio.WaitUntilSettledAsync(cancellationToken: cancellation.Token));
        }

        [Fact]
        public async Task AWaitThatTimedOutOrWasCancelled_IsForgottenByTheModel()
        {
            await using var rig = await StudioRig.ConnectedAsync();
            rig.Responder.AnswerBatches = false;
            var created = Guid.NewGuid();
            rig.Responder.Change(world =>
            {
                world.Scenes.Insert(0, (created, "New"));
                world.Items[created] = [];
            });
            await rig.Responder.SendEventAsync("SceneCreated", new { sceneName = "New", sceneUuid = created, isGroup = false }, 4);
            await StudioRig.EventuallyAsync(() => rig.Studio.Scenes.Contains("New"), "the new scene");

            // The model stays unsettled, so the waits are not ended by it: each of them ends with its time or its token.
            for (var i = 0; i < 10; i++)
            {
                await Assert.ThrowsAsync<TimeoutException>(() => rig.Studio.WaitUntilSettledAsync(TimeSpan.FromMilliseconds(20)));
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Studio.WaitUntilSettledAsync(cancellationToken: cancellation.Token));
            }

            await StudioRig.EventuallyAsync(() => rig.Studio.Host.SettleWaiterCount == 0, "the model to forget the waits that ended");
        }

        [Fact]
        public async Task AConnectionThatEndsBeforeTheModelIsComplete_IsNotPublishedAsEnded_BeforeTheFailedCallHasUnwound()
        {
            await using var rig = StudioRig.Create();
            rig.Responder.AnswerBatches = false;
            var atTheAbandon = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            rig.Studio.BeforeAbandon = () =>
            {
                atTheAbandon.TrySetResult();
                release.Wait(Timeout);
            };

            var connecting = rig.ConnectAsync();
            await StudioRig.EventuallyAsync(() => rig.Responder.Batches >= 1, "the first read to be sent");
            rig.Responder.AbortConnection();
            await atTheAbandon.Task.WithTimeoutAsync(Timeout);

            // The model has been told that the connection ended, and the call has failed, but it has not ended what it connected yet. The state is not
            // Disconnected (or Reconnecting) before it has: a handler of that state could connect again, and the older call would then end that connection.
            Assert.Equal(ObsStudioState.Synchronizing, rig.Studio.State);
            release.Set();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => connecting.WithTimeoutAsync(Timeout));
            Assert.Equal(ObsStudioState.Disconnected, rig.Studio.State);
        }

        [Fact]
        public async Task AFailedCallThatIsStillUnwinding_DoesNotEndTheConnectionOfANewerCall()
        {
            await using var rig = StudioRig.Create(world => world.AddScene("Gaming"));
            rig.Responder.AnswerBatches = false;
            var atTheAbandon = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            var abandons = 0;
            rig.Studio.BeforeAbandon = () =>
            {
                if (Interlocked.Increment(ref abandons) == 1)
                {
                    atTheAbandon.TrySetResult();
                    release.Wait(Timeout);
                }
            };

            var first = rig.ConnectAsync();
            await StudioRig.EventuallyAsync(() => rig.Responder.Batches >= 1, "the first read to be sent");
            rig.Responder.AbortConnection();
            await atTheAbandon.Task.WithTimeoutAsync(Timeout);

            // While the first call is held, the application disconnects and connects again: the connection is the new call's.
            await rig.Studio.DisconnectAsync().WithTimeoutAsync(Timeout);
            rig.Responder.AnswerBatches = true;
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            release.Set();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => first.WithTimeoutAsync(Timeout));
            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
            Assert.Equal(ConnectionState.Connected, rig.Studio.Client.ConnectionState);
            Assert.True(rig.Studio.Scenes.Contains("Gaming"));
        }

        [Fact]
        public async Task DisconnectAsync_WhenTheClientDoesNotCloseInTime_ThrowsATimeout()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"));
            rig.Studio.DisconnectTimeout = TimeSpan.FromMilliseconds(400);
            using var release = new ManualResetEventSlim();
            var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Studio.Host.AfterIngressPosted = item =>
            {
                // The model was told that the connection ended, and the receive loop of the client, which is what closes the connection, does not go on: it
                // is held in the call that told the model (a handler of the client that does not return holds it in the same way).
                if (item.Kind == IngressKind.SessionEnded)
                {
                    blocked.TrySetResult();
                    release.Wait(Timeout);
                }
            };

            await Assert.ThrowsAsync<TimeoutException>(() => rig.Studio.DisconnectAsync());
            await blocked.Task.WithTimeoutAsync(Timeout);
            release.Set();
            await StudioRig.EventuallyAsync(() => rig.Studio.Client.ConnectionState == ConnectionState.Disconnected, "the client to close the connection");
        }

        [Fact]
        public async Task DisconnectAsync_WhenTheStudioIsDisposedMeanwhile_IsNotLeftWaiting()
        {
            await using var rig = await StudioRig.ConnectedAsync(world => world.AddScene("Gaming"));

            // The end of the connection never reaches the model: the observation of the client is stopped before it can (as a disposal does).
            rig.Studio.Client.SetIngressTap(null);
            var disconnecting = rig.Studio.DisconnectAsync();
            await StudioRig.EventuallyAsync(() => rig.Studio.Host.DisconnectWaiterCount == 1, "the model to hold the wait");

            rig.Studio.Dispose();

            // It ends with the model, and not after the ten seconds that the wait for the model is allowed.
            await disconnecting.WithTimeoutAsync(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public async Task TheConstructors_RefuseWhatCannotWork()
        {
            Assert.Throws<ArgumentNullException>(() => new ObsStudio((ObsClient)null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { ReadTimeout = TimeSpan.Zero }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { SynchronizationTimeout = TimeSpan.FromSeconds(-1) }));

            await using var rig = await StudioRig.ConnectedAsync();
            Assert.Throws<InvalidOperationException>(() => new ObsStudio(rig.Studio.Client));

            using var client = new ObsClient();
            using var first = new ObsStudio(client);
            Assert.Throws<InvalidOperationException>(() => new ObsStudio(client));
        }

        [Fact]
        public async Task AnObsClientThatWasGiven_IsDisconnected_NotDisposed_WhenTheStudioIsDisposed()
        {
            using var client = new ObsClient();
            var obs = new ObsStudio(client);

            obs.Dispose();

            // It can still be used: it was not made by the studio.
            Assert.False(await client.ConnectAsync(hostname: "127.0.0.1", port: FreePort()));
        }

        // A context that keeps what is posted to it until the test runs it: a user interface thread that is busy.
        private sealed class HeldContext : SynchronizationContext
        {
            private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _held = new();

            public int Count => this._held.Count;

            public override void Post(SendOrPostCallback d, object? state)
            {
                this._held.Enqueue((d, state));
            }

            public void RunAll()
            {
                while (this._held.TryDequeue(out var held))
                {
                    held.Callback(held.State);
                }
            }
        }

        // A context that runs what is posted to it, in order, on one thread of its own, as the thread of a user interface does.
        private sealed class SingleThreadContext : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];
            private readonly Thread _thread;

            public SingleThreadContext()
            {
                this._thread = new Thread(this.Run) { IsBackground = true, Name = "Test context" };
                this._thread.Start();
            }

            public int ThreadId => this._thread.ManagedThreadId;

            public override void Post(SendOrPostCallback d, object? state)
            {
                this._queue.Add((d, state));
            }

            public void Dispose()
            {
                this._queue.CompleteAdding();
                this._thread.Join(TimeSpan.FromSeconds(5));
            }

            private void Run()
            {
                foreach (var (callback, state) in this._queue.GetConsumingEnumerable())
                {
                    callback(state);
                }
            }
        }
    }
}
