namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The time that the host of the model gives the engine, and how long the engine remembers what OBS Studio removed.
    /// </summary>
    public class ModelHostTimeTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Theory]
        [InlineData(0.001, 300)]
        [InlineData(10, 300)]
        [InlineData(75, 300)]
        [InlineData(76, 304)]
        [InlineData(120, 480)]
        [InlineData(86400, 345600)]
        public void TheRetention_IsFiveMinutes_OrFourTimesTheReadTimeWhenThatIsMore(double readTimeoutSeconds, double retentionSeconds)
        {
            Assert.Equal(TimeSpan.FromSeconds(retentionSeconds), ModelHost.RetentionFor(TimeSpan.FromSeconds(readTimeoutSeconds)));
        }

        [Fact]
        public async Task TheHost_GivesTheEngineARetentionThatFollowsTheReadTimeout()
        {
            await using var rig = ModelRig.Create(options: new ObsModelOptions { ReadTimeout = TimeSpan.FromMinutes(3) });
            Assert.Equal(TimeSpan.FromMinutes(12), rig.Model.Host.Engine.Options.TombstoneRetention);

            await using var byDefault = ModelRig.Create();
            Assert.Equal(TimeSpan.FromMinutes(5), byDefault.Model.Host.Engine.Options.TombstoneRetention);
        }

        [Fact]
        public async Task AnItemThatWaitsInTheInbox_IsHandledWithTheTimeAtWhichItArrived()
        {
            var time = new SteppedTimeProvider();
            Guid gaming = Guid.Empty;
            await using var rig = await ModelRig.ConnectedAsync(world => gaming = world.AddScene("Gaming"), new ObsModelOptions { TimeProvider = time });

            // The time that the loop gives each event, in the order in which it handles them. The loop is held at the first one, and the second waits in the inbox.
            var given = new List<TimeSpan>();
            var lastGiven = TimeSpan.Zero;
            var posted = 0;
            using var held = new ManualResetEventSlim();
            var atTheFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            rig.Model.Host.BeforeItemHandled = now => lastGiven = now;
            rig.Model.Host.AfterIngressPosted = item =>
            {
                if (item.Kind == IngressKind.Event)
                {
                    Interlocked.Increment(ref posted);
                }
            };
            rig.Model.Host.BeforeIngress = item =>
            {
                if (item.Kind == IngressKind.Event)
                {
                    lock (given)
                    {
                        given.Add(lastGiven);
                    }

                    if (!held.IsSet)
                    {
                        atTheFirst.TrySetResult();
                        held.Wait(Timeout);
                    }
                }
            };

            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "A" }, 4);
            await atTheFirst.Task.WithTimeoutAsync(Timeout);
            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "A", sceneName = "B" }, 4);
            await ModelRig.EventuallyAsync(() => Volatile.Read(ref posted) == 2, "the second event to be in the inbox");

            // The loop is late: ten minutes pass while it is held (it was stalled, or the computer slept). The event that waited is of the time at which it arrived, and not of
            // the time at which the loop gets to it, or what the engine remembers for the events behind it could be let go of before they are handled.
            time.Advance(TimeSpan.FromMinutes(10));
            held.Set();
            await ModelRig.EventuallyAsync(() => rig.Model.Scenes.Contains("B"), "the model to have the second name");

            // An event that arrives now is of this time.
            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "B", sceneName = "C" }, 4);
            await ModelRig.EventuallyAsync(() => rig.Model.Scenes.Contains("C"), "the model to have the third name");

            lock (given)
            {
                Assert.Equal([TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMinutes(10)], given);
            }
        }

        [Fact]
        public async Task TheTimeThatTheLoopGives_NeverGoesBack()
        {
            var time = new SteppedTimeProvider();
            Guid gaming = Guid.Empty;
            await using var rig = await ModelRig.ConnectedAsync(world => gaming = world.AddScene("Gaming"), new ObsModelOptions { TimeProvider = time });
            var given = new List<TimeSpan>();
            var lastGiven = TimeSpan.Zero;
            rig.Model.Host.BeforeItemHandled = now => lastGiven = now;
            rig.Model.Host.BeforeIngress = item =>
            {
                if (item.Kind == IngressKind.Event)
                {
                    lock (given)
                    {
                        given.Add(lastGiven);
                    }
                }
            };

            // Items are put in the inbox by several threads: one that took its time first can be put in after one that took it later.
            time.Set(TimeSpan.FromMinutes(5));
            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "Gaming", sceneName = "A" }, 4);
            await ModelRig.EventuallyAsync(() => rig.Model.Scenes.Contains("A"), "the model to have the first name");
            time.Set(TimeSpan.FromMinutes(1));
            await rig.Responder.SendEventAsync("SceneNameChanged", new { sceneUuid = gaming, oldSceneName = "A", sceneName = "B" }, 4);
            await ModelRig.EventuallyAsync(() => rig.Model.Scenes.Contains("B"), "the model to have the second name");

            lock (given)
            {
                Assert.Equal([TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)], given);
            }
        }

        // A source of time that the test moves by hand. Its timers are the ones of the system.
        private sealed class SteppedTimeProvider : TimeProvider
        {
            private long _ticks;

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp()
            {
                return Interlocked.Read(ref this._ticks);
            }

            public void Advance(TimeSpan by)
            {
                Interlocked.Add(ref this._ticks, by.Ticks);
            }

            public void Set(TimeSpan time)
            {
                Interlocked.Exchange(ref this._ticks, time.Ticks);
            }
        }
    }
}
