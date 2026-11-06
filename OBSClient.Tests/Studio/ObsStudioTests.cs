namespace OBSStudioClient.Tests.Studio
{
    using System.Collections.Concurrent;
    using System.ComponentModel;
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
            await using var rig = StudioRig.Create(world => gaming = world.AddScene("Gaming"));
            var resynchronized = 0;
            rig.Studio.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);
            await rig.ConnectAsync().WithTimeoutAsync(TimeSpan.FromSeconds(15));
            var scene = rig.Studio.Scenes["Gaming"];

            // The notifications are raised on a thread of their own: the one of the first connection can come after ConnectAsync has returned.
            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 1, "Resynchronized for the first connection");

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
            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 2, "Resynchronized for the reconnect");
            await Task.Delay(100);
            Assert.Equal(2, Volatile.Read(ref resynchronized));

            // While the model is read it does not report object by object: Resynchronized says that everything may have changed.
            Assert.DoesNotContain(rig.Changes, change => change.Kind == ObsChangeKind.Added && change.Entity is ObsScene { Name: "New" });
        }

        [Fact]
        public async Task ConnectAsync_RaisesPropertyChanged_ForTheCollectionsOfTheStudio_SoThatABindingToThemRefreshes()
        {
            await using var rig = StudioRig.Create(world =>
            {
                world.AddScene("Gaming");
                world.AddInput("Mic", "wasapi_input_capture", 2);
            });
            var resynchronized = 0;
            rig.Studio.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);
            Assert.Empty(rig.Studio.Scenes);

            await rig.ConnectAsync().WithTimeoutAsync(TimeSpan.FromSeconds(15));
            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 1, "Resynchronized for the first connection");

            // An application that is bound to the collections before it connects has to be told that they are new. The objects in them are new to it too,
            // and nothing is said about those one by one.
            var properties = rig.Properties;
            Assert.Equal(1, properties.Count(property => property == "Scenes"));
            Assert.Equal(1, properties.Count(property => property == "Inputs"));
            Assert.Equal(1, properties.Count(property => property == "ProgramScene"));
            Assert.Equal(3, properties.Count(property => property is not ("State" or "IsStale")));
        }

        [Fact]
        public async Task TheFirstConnection_RaisesNothingForTheObjects_BecauseTheyAreNewToTheApplication()
        {
            var context = new HeldContext();
            await using var rig = StudioRig.Create(
                world =>
                {
                    var gaming = world.AddScene("Gaming");
                    var chat = world.AddScene("Chat");
                    var mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                    var camera = world.AddInput("Camera");
                    world.AddItem(gaming, mic, "Mic");
                    world.AddItem(gaming, camera, "Camera");
                    world.AddItem(chat, camera, "Camera");
                    world.AddItem(chat, gaming, "Gaming", isScene: true);
                },
                new ObsStudioOptions { NotificationContext = context });
            var resynchronized = 0;
            rig.Studio.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);

            // The notifications are posted to a context that has not run them yet: the application gets hold of the objects first.
            var raised = new ConcurrentQueue<string>();
            foreach (var scene in rig.Studio.Scenes)
            {
                scene.PropertyChanged += (_, e) => raised.Enqueue($"scene {scene.Name} {e.PropertyName}");
                foreach (var item in scene.Items)
                {
                    item.PropertyChanged += (_, e) => raised.Enqueue($"item {item.Id} {e.PropertyName}");
                }
            }

            foreach (var input in rig.Studio.Inputs)
            {
                input.PropertyChanged += (_, e) => raised.Enqueue($"input {input.Name} {e.PropertyName}");
            }

            await StudioRig.EventuallyAsync(
                () =>
                {
                    context.RunAll();
                    return Volatile.Read(ref resynchronized) == 1;
                },
                "Resynchronized for the first connection");

            // Only the studio is told: its collections were empty before the connection.
            Assert.Empty(raised);
            Assert.Contains("Scenes", rig.Properties);
            Assert.Contains("Inputs", rig.Properties);
        }

        [Fact]
        public async Task WhenTheHandlersAreTooSlow_TheNotificationsAreDropped_TheModelStaysRight_AndTheApplicationIsToldToReadAgain()
        {
            Guid gaming = Guid.Empty;
            await using var rig = StudioRig.Create(world => gaming = world.AddScene("Gaming"), new ObsStudioOptions { MaxPendingNotifications = 20 });
            var resynchronized = 0;
            rig.Studio.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);
            await rig.ConnectAsync().WithTimeoutAsync(TimeSpan.FromSeconds(15));
            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 1, "Resynchronized for the first connection");
            var scene = rig.Studio.Scenes["Gaming"];
            var everything = 0;
            scene.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == string.Empty)
                {
                    Interlocked.Increment(ref everything);
                }
            };
            var changes = 0;
            using var gate = new ManualResetEventSlim();
            var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Studio.Changed += (_, e) =>
            {
                if (e.PropertyName == "Name")
                {
                    Interlocked.Increment(ref changes);
                    blocked.TrySetResult();
                    gate.Wait(Timeout);
                }
            };

            // A handler that does not return, and OBS Studio goes on.
            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "N0" }, 4);
            await blocked.Task.WithTimeoutAsync(Timeout);
            for (var i = 1; i <= 100; i++)
            {
                await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = $"N{i - 1}", sceneName = $"N{i}" }, 4);
            }

            await StudioRig.EventuallyAsync(() => rig.Studio.Scenes.Contains("N100"), "the model to have the last name");
            gate.Set();

            // The notifications that were waiting are raised, the others were dropped, and the application is told that everything may have changed.
            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 2, "Resynchronized after the notifications were dropped");
            Assert.Equal("N100", scene.Name);
            Assert.Equal(1, Volatile.Read(ref everything));
            Assert.InRange(Volatile.Read(ref changes), 2, 25);
            Assert.Contains(string.Empty, rig.Properties);
        }

        [Fact]
        public async Task AReconnect_RaisesPropertyChanged_OnTheObjectsThatTheApplicationHolds_ForWhatChangedMeanwhile()
        {
            Guid gaming = Guid.Empty;
            Guid mic = Guid.Empty;
            Guid old = Guid.Empty;
            await using var rig = StudioRig.Create(world =>
            {
                gaming = world.AddScene("Gaming");
                mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                old = world.AddInput("Old", "wasapi_input_capture", 2);
            });
            var resynchronized = 0;
            rig.Studio.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);
            await rig.ConnectAsync().WithTimeoutAsync(TimeSpan.FromSeconds(15));
            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 1, "Resynchronized for the first connection");
            var scene = rig.Studio.Scenes["Gaming"];
            var micInput = rig.Studio.Inputs["Mic"];
            var oldInput = rig.Studio.Inputs["Old"];

            // What the application sees, as a binding does: the PropertyChanged events of the objects that it holds.
            var raised = new ConcurrentQueue<(object Sender, string Property)>();
            PropertyChangedEventHandler handler = (sender, e) => raised.Enqueue((sender!, e.PropertyName!));
            scene.PropertyChanged += handler;
            micInput.PropertyChanged += handler;
            oldInput.PropertyChanged += handler;
            rig.Studio.PropertyChanged += handler;
            var recorded = rig.Changes.Length;

            rig.Responder.AbortConnection();
            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Reconnecting, "the state to follow the connection");
            rig.Responder.Change(world =>
            {
                var index = world.Inputs.FindIndex(i => i.Uuid == mic);
                var input = world.Inputs[index];
                world.Inputs[index] = (input.Uuid, input.Name, input.Kind, input.Caps, true, input.VolumeMul);
                world.Inputs.RemoveAll(i => i.Uuid == old);
                world.Scenes[world.Scenes.FindIndex(s => s.Uuid == gaming)] = (gaming, "Playing");
                var created = Guid.NewGuid();
                world.Scenes.Insert(0, (created, "New"));
                world.Items[created] = [];
            });

            // Resynchronized comes after the notifications that were held, in the same queue.
            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 2, "Resynchronized for the reconnect", TimeSpan.FromSeconds(30));
            await Task.Delay(100);

            Assert.True(micInput.IsMuted);
            Assert.Equal("Playing", scene.Name);
            Assert.True(oldInput.IsRemoved);
            var notifications = raised.ToArray();
            int Count(object sender, string property) => notifications.Count(n => ReferenceEquals(n.Sender, sender) && n.Property == property);

            // Once for each property of each object, however many steps changed it.
            Assert.Equal(1, Count(micInput, "IsMuted"));
            Assert.Equal(1, Count(scene, "Name"));
            Assert.Equal(1, Count(scene, "Index"));
            Assert.Equal(1, Count(oldInput, "State"));
            Assert.Equal(1, Count(oldInput, "IsRemoved"));
            Assert.Equal(1, Count(rig.Studio, "Scenes"));
            Assert.Equal(1, Count(rig.Studio, "Inputs"));

            // The records of Changed are not kept: Resynchronized says what they would (only the state of the studio is reported).
            Assert.All(rig.Changes.Skip(recorded), change => Assert.Equal("State", change.PropertyName));
        }

        [Fact]
        public async Task WhenMorePropertiesChangeWhileTheModelIsReadThanMayBeKept_EveryObjectSaysThatEverythingChanged_Once()
        {
            Guid gaming = Guid.Empty;
            Guid mic = Guid.Empty;
            Guid old = Guid.Empty;
            await using var rig = StudioRig.Create(
                world =>
                {
                    gaming = world.AddScene("Gaming");
                    mic = world.AddInput("Mic", "wasapi_input_capture", 2);
                    old = world.AddInput("Old", "wasapi_input_capture", 2);
                },
                new ObsStudioOptions { MaxPendingNotifications = 5 });
            var resynchronized = 0;
            rig.Studio.Resynchronized += (_, _) => Interlocked.Increment(ref resynchronized);
            await rig.ConnectAsync().WithTimeoutAsync(TimeSpan.FromSeconds(15));
            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 1, "Resynchronized for the first connection");
            var scene = rig.Studio.Scenes["Gaming"];
            var micInput = rig.Studio.Inputs["Mic"];
            var oldInput = rig.Studio.Inputs["Old"];
            var raised = new ConcurrentQueue<(object Sender, string Property)>();
            PropertyChangedEventHandler handler = (sender, e) => raised.Enqueue((sender!, e.PropertyName!));
            scene.PropertyChanged += handler;
            micInput.PropertyChanged += handler;
            oldInput.PropertyChanged += handler;
            rig.Studio.PropertyChanged += handler;

            // While the connection is down seven properties change: more than the five that may be kept for the application.
            rig.Responder.AbortConnection();
            await StudioRig.EventuallyAsync(() => rig.Studio.State == ObsStudioState.Reconnecting, "the state to follow the connection");
            rig.Responder.Change(world =>
            {
                var index = world.Inputs.FindIndex(i => i.Uuid == mic);
                var input = world.Inputs[index];
                world.Inputs[index] = (input.Uuid, input.Name, input.Kind, input.Caps, true, input.VolumeMul);
                world.Inputs.RemoveAll(i => i.Uuid == old);
                world.Scenes[world.Scenes.FindIndex(s => s.Uuid == gaming)] = (gaming, "Playing");
                var created = Guid.NewGuid();
                world.Scenes.Insert(0, (created, "New"));
                world.Items[created] = [];
            });

            await StudioRig.EventuallyAsync(() => Volatile.Read(ref resynchronized) == 2, "Resynchronized for the reconnect", TimeSpan.FromSeconds(30));
            await Task.Delay(100);

            // What the model holds is right, and what could not be kept is told in one go: every object that survived says that all of its properties changed.
            Assert.True(micInput.IsMuted);
            Assert.Equal("Playing", scene.Name);
            Assert.True(oldInput.IsRemoved);
            var notifications = raised.ToArray();
            int Count(object sender, string property) => notifications.Count(n => ReferenceEquals(n.Sender, sender) && n.Property == property);
            Assert.Equal(1, Count(micInput, string.Empty));
            Assert.Equal(1, Count(scene, string.Empty));
            Assert.Equal(1, Count(rig.Studio, string.Empty));
            Assert.Equal(0, Count(micInput, "IsMuted"));
            Assert.Equal(0, Count(scene, "Name"));
            Assert.Equal(0, Count(rig.Studio, "Scenes"));
            Assert.Equal(2, Volatile.Read(ref resynchronized));
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
        public async Task TheWaitForTheClientToBeClosed_EndsWhenItIsClosed_WhenTheTimeIsUp_AndWhenItIsCancelled()
        {
            await using var responder = new WorldResponder();
            responder.Start();
            using var client = new ObsClient();
            using var obs = new ObsStudio(client);
            Assert.True(await client.ConnectAsync(hostname: "127.0.0.1", port: responder.Port).WithTimeoutAsync(Timeout));

            // The time is up: the caller looks at the state.
            await obs.WaitForClientDisconnectedAsync(TimeSpan.FromMilliseconds(100)).WithTimeoutAsync(Timeout);
            Assert.Equal(ConnectionState.Connected, client.ConnectionState);

            // A token ends the wait at once, and not after the time that the client is given.
            using var cancellation = new CancellationTokenSource();
            var waiting = obs.WaitForClientDisconnectedAsync(TimeSpan.FromSeconds(30), cancellation.Token);
            await Task.Delay(200);
            Assert.False(waiting.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WithTimeoutAsync(TimeSpan.FromSeconds(2)));

            // And it ends when the client says that it is closed.
            var closing = obs.WaitForClientDisconnectedAsync(TimeSpan.FromSeconds(30));
            await Task.Delay(200);
            Assert.False(closing.IsCompleted);
            client.Disconnect();
            await closing.WithTimeoutAsync(Timeout);
            Assert.Equal(ConnectionState.Disconnected, client.ConnectionState);
        }

        [Fact]
        public async Task WhenTheVersionIsRejected_AHandlerOfTheClientDoesNotHoldUpTheModel()
        {
            await using var rig = StudioRig.Create();
            rig.Responder.WebSocketVersion = "5.3.0";

            // A handler of the client that waits: the connection is ended on the thread that runs it. The loop of the model rejects the version, and must not
            // be that thread (a handler is the application's, and the loop holds up everything behind it).
            using var gate = new ManualResetEventSlim();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Studio.Client.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsClient.AutoReconnect) && !rig.Studio.Client.AutoReconnect)
                {
                    entered.TrySetResult();
                    gate.Wait(Timeout);
                }
            };

            var connecting = Task.Run(() => rig.ConnectAsync());
            await entered.Task.WithTimeoutAsync(Timeout);

            // The loop takes what is asked of it, which only the loop can do: it either answers at once (the connection is over), or keeps the wait.
            var asked = rig.Studio.Host.WaitUntilDisconnectedAsync(TimeSpan.FromSeconds(30));
            try
            {
                await StudioRig.EventuallyAsync(() => asked.IsCompleted || rig.Studio.Host.DisconnectWaiterCount == 1, "the loop of the model to take what is asked of it");
            }
            finally
            {
                gate.Set();
            }

            await Assert.ThrowsAsync<ObsVersionNotSupportedException>(() => connecting.WithTimeoutAsync(Timeout));
            await asked.WithTimeoutAsync(Timeout);
        }

        [Fact]
        public async Task WhenAConnectFails_AHandlerOfTheClientRunsWithoutTheLockOfTheState()
        {
            await using var rig = StudioRig.Create();
            var checkedOnce = 0;
            var gotTheLock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // The notification that the client raises when the failed call ends the connection is the application's. What it does can need the state of the
            // studio (here another thread disconnects it, which takes the lock of the state): it cannot be raised while the call holds that lock.
            rig.Studio.Client.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ObsClient.AutoReconnect) && !rig.Studio.Client.AutoReconnect && Interlocked.Increment(ref checkedOnce) == 1)
                {
                    var other = Task.Run(() => rig.Studio.DisconnectAsync());
                    gotTheLock.TrySetResult(other.Wait(TimeSpan.FromSeconds(4)));
                }
            };

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => rig.Studio.ConnectAsync("127.0.0.1", FreePort()).WithTimeoutAsync(Timeout));

            Assert.True(await gotTheLock.Task.WithTimeoutAsync(Timeout), "The handler ran under the lock of the state of the studio.");
        }

        [Fact]
        public async Task DisconnectAsync_WaitsForTheCallOfConnectAsyncThatItEnds_AndNoOtherCallCanStartBeforeThat()
        {
            await using var rig = StudioRig.Create(world => world.AddScene("Gaming"), new ObsStudioOptions { SynchronizationTimeout = TimeSpan.FromSeconds(4) });
            var beginCalls = 0;
            using var releaseFirst = new ManualResetEventSlim();
            var firstWaits = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Studio.BeforeBegin = () =>
            {
                if (Interlocked.Increment(ref beginCalls) == 1)
                {
                    firstWaits.TrySetResult();
                    releaseFirst.Wait(Timeout);
                }
            };

            // The first call is held where it has waited for the client to be closed (as it does when a connection ended a moment ago).
            var first = Task.Run(() => rig.ConnectAsync());
            await firstWaits.Task.WithTimeoutAsync(Timeout);

            // The application disconnects: the call is ended, and the disconnection is not complete before the call has unwound. Until then the call has the state,
            // and another one cannot start: two calls that connect at the same time would share the one client.
            var disconnecting = rig.Studio.DisconnectAsync();
            await Task.Delay(300);
            Assert.False(disconnecting.IsCompleted);
            Assert.Equal(ObsStudioState.Connecting, rig.Studio.State);
            await Assert.ThrowsAsync<InvalidOperationException>(() => rig.ConnectAsync());

            releaseFirst.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WithTimeoutAsync(Timeout));
            await disconnecting.WithTimeoutAsync(Timeout);
            Assert.Equal(ObsStudioState.Disconnected, rig.Studio.State);

            // The application can connect at once.
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
            Assert.True(rig.Studio.Scenes.Contains("Gaming"));
        }

        [Fact]
        public async Task ConnectAsync_CanBeCalledAgainAtOnce_WhateverMomentDisconnectAsyncEndedTheCallAt()
        {
            await using var rig = StudioRig.Create(world => world.AddScene("Gaming"));

            // The call is ended at every stage of its connection: before it asks the client, while the client connects, while the model is read, and after.
            for (var i = 0; i < 30; i++)
            {
                var connecting = Task.Run(() => rig.ConnectAsync());
                await StudioRig.EventuallyAsync(() => rig.Studio.State != ObsStudioState.Disconnected, "the call to have the state");
                await Task.Delay(i);
                await rig.Studio.DisconnectAsync().WithTimeoutAsync(Timeout);
                try
                {
                    await connecting.WithTimeoutAsync(Timeout);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObsNotConnectedException)
                {
                }

                Assert.Equal(ObsStudioState.Disconnected, rig.Studio.State);
                Assert.Equal(ConnectionState.Disconnected, rig.Studio.Client.ConnectionState);
            }

            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
            Assert.True(rig.Studio.Scenes.Contains("Gaming"));
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
        public async Task AFailedCallThatIsStillUnwinding_HasTheState_UntilItHasEndedWhatItConnected()
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

            // The call is held where it ends what it connected, and it has the state: the application that disconnects waits for the call to unwind, and a new
            // call cannot start (it would find the connection of the older one, which that is about to end).
            Assert.Equal(ObsStudioState.Synchronizing, rig.Studio.State);
            var disconnecting = rig.Studio.DisconnectAsync();
            await Task.Delay(300);
            Assert.False(disconnecting.IsCompleted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => rig.ConnectAsync());
            release.Set();

            await Assert.ThrowsAsync<ObsNotConnectedException>(() => first.WithTimeoutAsync(Timeout));
            await disconnecting.WithTimeoutAsync(Timeout);
            Assert.Equal(ObsStudioState.Disconnected, rig.Studio.State);
            Assert.Equal(ConnectionState.Disconnected, rig.Studio.Client.ConnectionState);

            rig.Responder.AnswerBatches = true;
            await rig.ConnectAsync().WithTimeoutAsync(Timeout);
            Assert.Equal(ObsStudioState.Synchronized, rig.Studio.State);
            Assert.Equal(ConnectionState.Connected, rig.Studio.Client.ConnectionState);
            Assert.True(rig.Studio.Scenes.Contains("Gaming"));
        }

        [Fact]
        public async Task DisconnectAsync_WhileConnectAsyncIsStillBeforeTheConnection_EndsThatCall_AndNothingIsConnected()
        {
            await using var rig = StudioRig.Create(world => world.AddScene("Gaming"));
            var atTheConnection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            rig.Studio.BeforeConnect = () =>
            {
                atTheConnection.TrySetResult();
                release.Wait(Timeout);
            };

            var connecting = Task.Run(() => rig.ConnectAsync());
            await atTheConnection.Task.WithTimeoutAsync(Timeout);

            // The application asks to disconnect while the call is getting ready to connect: the call must not connect after that, and the disconnection
            // is complete when the call has unwound.
            var disconnecting = rig.Studio.DisconnectAsync();
            release.Set();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting.WithTimeoutAsync(Timeout));
            await disconnecting.WithTimeoutAsync(Timeout);
            Assert.Equal(ObsStudioState.Disconnected, rig.Studio.State);
            Assert.Equal(ConnectionState.Disconnected, rig.Studio.Client.ConnectionState);
        }

        [Fact]
        public async Task Dispose_WhileConnectAsyncIsStillBeforeTheConnection_DoesNotConnectTheClient()
        {
            await using var responder = new WorldResponder();
            responder.World.AddScene("Scene");
            responder.Start();
            using var client = new ObsClient();
            using var obs = new ObsStudio(client);
            var atTheConnection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var release = new ManualResetEventSlim();
            obs.BeforeConnect = () =>
            {
                atTheConnection.TrySetResult();
                release.Wait(Timeout);
            };

            var connecting = Task.Run(() => obs.ConnectAsync("127.0.0.1", responder.Port));
            await atTheConnection.Task.WithTimeoutAsync(Timeout);

            // The client is the application's own: a studio that is disposed of must not connect it.
            obs.Dispose();
            release.Set();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting.WithTimeoutAsync(Timeout));
            Assert.Equal(ConnectionState.Disconnected, client.ConnectionState);
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
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { MaxPendingNotifications = 0 }));

            await using var rig = await StudioRig.ConnectedAsync();
            Assert.Throws<InvalidOperationException>(() => new ObsStudio(rig.Studio.Client));

            using var client = new ObsClient();
            using var first = new ObsStudio(client);
            Assert.Throws<InvalidOperationException>(() => new ObsStudio(client));
        }

        [Theory]
        [InlineData(0.001)]
        [InlineData(0.9)]
        public void ATimeoutThatIsNotAWholeMillisecond_IsRefused(double milliseconds)
        {
            // It would be taken for no time at all.
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { ReadTimeout = TimeSpan.FromMilliseconds(milliseconds) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { SynchronizationTimeout = TimeSpan.FromMilliseconds(milliseconds) }));
        }

        [Fact]
        public void ATimeoutThatNoWaitCanTake_IsRefusedWhenTheOptionsAreGiven_NotWhenItIsUsed()
        {
            // Such a time fails when it is waited for, which is after the connection was made.
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { ReadTimeout = TimeSpan.FromDays(25) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { ReadTimeout = TimeSpan.MaxValue }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { SynchronizationTimeout = TimeSpan.FromDays(25) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { SynchronizationTimeout = TimeSpan.MaxValue }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { SynchronizationTimeout = System.Threading.Timeout.InfiniteTimeSpan }));

            // The limits themselves are accepted.
            using var studio = new ObsStudio(new ObsStudioOptions { ReadTimeout = TimeSpan.FromMilliseconds(1), SynchronizationTimeout = TimeSpan.FromMilliseconds(int.MaxValue) });
        }

        [Fact]
        public async Task WaitUntilSettledAsync_RefusesATimeoutThatCannotBeWaitedFor()
        {
            await using var rig = await StudioRig.ConnectedAsync();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => rig.Studio.WaitUntilSettledAsync(TimeSpan.FromSeconds(-1)));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => rig.Studio.WaitUntilSettledAsync(TimeSpan.FromDays(25)));

            // No limit, by null or by the infinite time, and a limit of the longest time: the model is settled, so they return.
            await rig.Studio.WaitUntilSettledAsync().WithTimeoutAsync(Timeout);
            await rig.Studio.WaitUntilSettledAsync(System.Threading.Timeout.InfiniteTimeSpan).WithTimeoutAsync(Timeout);
            await rig.Studio.WaitUntilSettledAsync(TimeSpan.FromMilliseconds(int.MaxValue)).WithTimeoutAsync(Timeout);
        }

        [Fact]
        public void WhenTheStudioCannotStart_AClientThatWasGivenIsLetGoOf()
        {
            using var client = new ObsClient();

            Assert.Throws<InvalidOperationException>(() => new ObsStudio(client, new ObsStudioOptions { TimeProvider = new FailingTimeProvider() }));

            // The client is the application's own: it is not left with the observation of a studio that was never made.
            using var studio = new ObsStudio(client);
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

        // A source of time that cannot make a timer: the studio cannot start with it.
        private sealed class FailingTimeProvider : TimeProvider
        {
            public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            {
                throw new InvalidOperationException("No timers.");
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
