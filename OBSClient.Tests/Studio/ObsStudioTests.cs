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
            Assert.Equal(new[] { item }, obs.Inputs["Mic"].SceneItems);
            Assert.Equal("Scene", obs.ProgramScene!.Name);
            Assert.True(obs.Scenes["Scene"].IsProgram);
            Assert.False(obs.Scenes["Gaming"].IsProgram);
            Assert.False(obs.IsStudioModeEnabled);
            Assert.Same(rig.Studio.Client, obs.Client);
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
