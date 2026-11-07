namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio;
    using OBSStudioClient.Studio.Engine;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The waits of the model for it to show something (<see cref="StudioHost.ConfirmAsync"/>): the waits that are about the same part of the model are looked at in the
    /// order in which they were made, so that a change to the value that the model still shows from before an earlier change is not taken for shown at once, and a value
    /// that the model passes through is not missed.
    /// </summary>
    public class StudioHostConfirmationTests
    {
        private const int SceneItemEvents = 128;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        // An OBS Studio with two scenes: "Gaming", which shows one input without audio, "Camera" (a scene item that is shown), and an empty "Chatting".
        private static async Task<Fixture> StartAsync()
        {
            var gaming = Guid.Empty;
            var rig = await StudioRig.ConnectedAsync(world =>
            {
                gaming = world.AddScene("Gaming");
                world.AddItem(gaming, world.AddInput("Camera"), "Camera");
                world.AddScene("Chatting");
            });
            return new Fixture(rig, gaming);
        }

        [Fact]
        public async Task WaitsAboutTheSamePart_AreLookedAtInTheOrderInWhichTheyWereMade()
        {
            await using var test = await StartAsync();
            using var cancellation = new CancellationTokenSource();

            // The camera is shown. The first wait is for it to be hidden, and the second for it to be shown: the model shows the second right now, from before the change that
            // the first one waits for, and that must not be taken for the change.
            var hidden = test.Confirm(() => !test.Camera.IsEnabled, cancellation.Token, test.Gaming);
            var shown = test.Confirm(() => test.Camera.IsEnabled, cancellation.Token, test.Gaming);
            await Task.Delay(150);
            Assert.False(hidden.IsCompleted);
            Assert.False(shown.IsCompleted);

            await test.SendEnabledEventAsync(false);
            await hidden.WithTimeoutAsync(Timeout);
            await Task.Delay(100);
            Assert.False(shown.IsCompleted);

            await test.SendEnabledEventAsync(true);
            await shown.WithTimeoutAsync(Timeout);
        }

        [Fact]
        public async Task AWaitAboutAnotherPart_OrAboutNone_IsNotHeldBack()
        {
            await using var test = await StartAsync();
            using var cancellation = new CancellationTokenSource();
            var chatting = test.Rig.Studio.Scenes["Chatting"].Uuid;

            var held = test.Confirm(() => !test.Camera.IsEnabled, cancellation.Token, test.Gaming);
            var anotherPart = test.Confirm(() => test.Camera.IsEnabled, cancellation.Token, chatting);
            var noPart = test.Rig.Studio.Host.ConfirmAsync(() => test.Camera.IsEnabled, cancellation.Token);

            await anotherPart.WithTimeoutAsync(Timeout);
            await noPart.WithTimeoutAsync(Timeout);
            Assert.False(held.IsCompleted);
        }

        [Fact]
        public async Task AWait_IsLookedAtInTheStepInWhichTheWaitBeforeItIsDone()
        {
            await using var test = await StartAsync();
            using var cancellation = new CancellationTokenSource();
            var first = test.Confirm(() => !test.Camera.IsEnabled, cancellation.Token, test.Gaming);
            var second = test.Confirm(() => !test.Camera.IsEnabled, cancellation.Token, test.Gaming);

            // There is one event, and the model does not publish again: the second wait has what it waits for in the same step as the first, or it never has.
            await test.SendEnabledEventAsync(false);

            await first.WithTimeoutAsync(Timeout);
            await second.WithTimeoutAsync(Timeout);
        }

        [Fact]
        public async Task WaitsThatAreMadeBeforeTheModelPassesThroughSeveralValues_EachSeeTheirOwn()
        {
            await using var test = await StartAsync();
            using var cancellation = new CancellationTokenSource();
            var values = new[] { false, true, false, true, false };
            var waits = values.Select(value => test.Confirm(() => test.Camera.IsEnabled == value, cancellation.Token, test.Gaming)).ToArray();

            // The camera is shown at the start. The second and the fourth wait are for a value that the model shows from before the first change: they must not take it for
            // their own.
            await Task.Delay(150);
            Assert.All(waits, wait => Assert.False(wait.IsCompleted));

            // The events come one after the other. After each of them the wait for that value is done, and no wait after it is: each wait is looked at after the one before it,
            // in the step of that one, so none of them looks too early or too late.
            for (var k = 0; k < values.Length; k++)
            {
                await test.SendEnabledEventAsync(values[k]);
                await waits[k].WithTimeoutAsync(Timeout);
                await Task.Delay(50);
                Assert.Equal(k + 1, waits.Count(wait => wait.IsCompleted));
            }
        }

        [Fact]
        public async Task AWaitThatIsHeldBack_HoldsBackTheWaitsAfterIt_ForAllOfItsParts()
        {
            await using var test = await StartAsync();
            using var cancellation = new CancellationTokenSource();
            var gamingPart = new RefreshTarget(ReadKind.Items, test.Gaming);
            var chattingPart = new RefreshTarget(ReadKind.Items, test.Rig.Studio.Scenes["Chatting"].Uuid);

            // The first wait is for the camera to be hidden. The second is about both scenes, and the first holds it back. The third is about the second scene only, and the model
            // shows what it waits for right now (the camera is shown): it must wait behind the second, which has not got what it waited for either.
            var first = test.Rig.Studio.Host.ConfirmAsync(() => !test.Camera.IsEnabled, cancellation.Token, [gamingPart]);
            var second = test.Rig.Studio.Host.ConfirmAsync(() => test.Camera.IsEnabled, cancellation.Token, [gamingPart, chattingPart]);
            var third = test.Rig.Studio.Host.ConfirmAsync(() => test.Camera.IsEnabled, cancellation.Token, [chattingPart]);
            await Task.Delay(150);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            Assert.False(third.IsCompleted);

            await test.SendEnabledEventAsync(false);
            await first.WithTimeoutAsync(Timeout);
            await Task.Delay(100);
            Assert.False(second.IsCompleted);
            Assert.False(third.IsCompleted);

            await test.SendEnabledEventAsync(true);
            await second.WithTimeoutAsync(Timeout);
            await third.WithTimeoutAsync(Timeout);
        }

        [Fact]
        public async Task AWaitThatEnds_ReleasesTheWaitsBehindIt()
        {
            await using var test = await StartAsync();
            using var cancelFirst = new CancellationTokenSource();
            using var cancellation = new CancellationTokenSource();
            var first = test.Confirm(() => false, cancelFirst.Token, test.Gaming);
            var second = test.Confirm(() => test.Camera.IsEnabled, cancellation.Token, test.Gaming);
            await Task.Delay(100);
            Assert.False(second.IsCompleted);

            await cancelFirst.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WithTimeoutAsync(Timeout));
            await second.WithTimeoutAsync(Timeout);
        }

        [Fact]
        public async Task AWaitWhosePredicateFails_EndsWithTheFailure_AndDoesNotHoldBackTheWaitsBehindIt()
        {
            await using var test = await StartAsync();
            using var cancellation = new CancellationTokenSource();
            var failing = test.Confirm(() => throw new InvalidOperationException("The predicate fails."), cancellation.Token, test.Gaming);
            var behind = test.Confirm(() => test.Camera.IsEnabled, cancellation.Token, test.Gaming);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => failing.WithTimeoutAsync(Timeout));

            Assert.Equal("The predicate fails.", exception.Message);
            await behind.WithTimeoutAsync(Timeout);
        }

        private sealed class Fixture : IAsyncDisposable
        {
            public Fixture(StudioRig rig, Guid gaming)
            {
                this.Rig = rig;
                this.Gaming = gaming;
                this.Camera = rig.Studio.Scenes["Gaming"].Items["Camera"];
            }

            public StudioRig Rig { get; }

            public Guid Gaming { get; }

            public ObsSceneItem Camera { get; }

            public Task Confirm(Func<bool> predicate, CancellationToken cancellationToken, Guid scene)
            {
                return this.Rig.Studio.Host.ConfirmAsync(predicate, cancellationToken, [new RefreshTarget(ReadKind.Items, scene)]);
            }

            public Task SendEnabledEventAsync(bool enabled)
            {
                return this.Rig.Responder.SendEventAsync("SceneItemEnableStateChanged", new { sceneName = "Gaming", sceneUuid = this.Gaming, sceneItemId = 1, sceneItemEnabled = enabled }, SceneItemEvents);
            }

            public ValueTask DisposeAsync()
            {
                return this.Rig.DisposeAsync();
            }
        }
    }
}
