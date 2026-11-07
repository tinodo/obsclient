namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The changes that the application asks for, against a stand-in for OBS Studio that answers what a test tells it to, and sends the events that a test tells it to
    /// send: what the line of changes does when OBS Studio refuses, does not answer, answers late, or is changing its scene collection. (With the simulation of
    /// OBS Studio: <see cref="StudioWritesOverSimulationTests"/>.)
    /// </summary>
    public class StudioWriterTests
    {
        private const int SceneItemEvents = 128;
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static ObsStudioOptions Options()
        {
            return new ObsStudioOptions
            {
                ConfirmationTimeout = TimeSpan.FromMilliseconds(300),
                WriteTimeout = TimeSpan.FromMilliseconds(600),
            };
        }

        [Fact]
        public async Task AChange_IsShownByTheModel_WhenTheEventComesAfterTheAnswer_WithoutReadingAgain()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                test.SetEnabledInWorld(false);
                _ = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    await test.SendEnabledEventAsync(false);
                });
                return (100, null);
            };
            var reads = test.Count("GetSceneItemList");

            await test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout);

            Assert.False(test.Camera.IsEnabled);
            Assert.Equal(1, test.Count("SetSceneItemEnabled"));
            Assert.Equal(reads, test.Count("GetSceneItemList"));
        }

        [Fact]
        public async Task AChange_WhoseEventDoesNotCome_IsShownByAReadOfTheScene()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                test.SetEnabledInWorld(false);
                return (100, null);
            };
            var reads = test.Count("GetSceneItemList");

            await test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout);

            // The model shows what OBS Studio holds, and it was read for that.
            Assert.False(test.Camera.IsEnabled);
            Assert.True(test.Count("GetSceneItemList") > reads);
        }

        [Fact]
        public async Task AChange_ThatOBSStudioAnsweredAndDidNotMake_EndsWithWhatOBSStudioHolds()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (100, null) : null;

            await test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout);

            // Somebody changed it back, or OBS Studio did not mean it: the model says what is true, and the call does not pretend that it made the change.
            Assert.True(test.Camera.IsEnabled);
        }

        [Fact]
        public async Task ARequestThatIsAnsweredNotReady_IsANotReadyException_AndNothingIsChanged()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (207, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNotReadyException>(() => test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout));

            Assert.Contains("hiding scene item 1 of scene 'Gaming'", exception.Message);
            Assert.True(test.Camera.IsEnabled);
            Assert.Equal(1, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task ARequestThatIsAnsweredNotFound_IsARemovedException()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (600, null) : null;

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout));

            Assert.Equal("scene item 1 of scene 'Gaming'", exception.Description);
            Assert.Equal(ObsObjectState.Gone, exception.State);
        }

        [Fact]
        public async Task ARequestThatOBSStudioRefuses_IsAResponseException_WithItsCode()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (604, null) : null;

            var exception = await Assert.ThrowsAsync<ObsResponseException>(() => test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.InvalidResourceState, exception.ErrorCode);
        }

        [Fact]
        public async Task AChangeThatIsNotAnswered_IsAnUnknownOutcome_NeverSentAgain_AndTheModelIsReadAgain()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (0, null) : null;
            var reads = test.Count("GetSceneItemList");

            var exception = await Assert.ThrowsAsync<ObsUnknownOutcomeException>(() => test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout));

            Assert.Contains("scene item 1 of scene 'Gaming'", exception.Operation);
            await StudioRig.EventuallyAsync(() => test.Count("GetSceneItemList") > reads, "the scene items to be read again");
            await Task.Delay(300);
            Assert.Equal(1, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AConnectionThatEndsWhileAChangeIsUnanswered_IsAnUnknownOutcome()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30), AutoReconnect = false });
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (0, null) : null;

            var change = test.Camera.SetEnabledAsync(false);
            await StudioRig.EventuallyAsync(() => test.Count("SetSceneItemEnabled") == 1, "the request to be sent");
            test.Rig.Responder.AbortConnection();

            await Assert.ThrowsAsync<ObsUnknownOutcomeException>(() => change.WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task AChangeThatWaitsForItsTurn_IsNotSent_WhenTheConnectionEnds()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30), AutoReconnect = false });
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (0, null) : null;
            var first = test.Camera.SetEnabledAsync(false);
            await StudioRig.EventuallyAsync(() => test.Count("SetSceneItemEnabled") == 1, "the first request to be sent");
            var second = test.Camera.SetLockedAsync(true);

            test.Rig.Responder.AbortConnection();

            await Assert.ThrowsAsync<ObsUnknownOutcomeException>(() => first.WithTimeoutAsync(Timeout));
            var exception = await Assert.ThrowsAsync<ObsNotConnectedException>(() => second.WithTimeoutAsync(Timeout));
            Assert.Contains("Nothing was changed", exception.Message);
            Assert.Equal(0, test.Count("SetSceneItemLocked"));
        }

        [Fact]
        public async Task ADisposedStudio_EndsTheChangesThatAreUnderWayAndThatWait()
        {
            var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30) });
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (0, null) : null;
            var first = test.Camera.SetEnabledAsync(false);
            await StudioRig.EventuallyAsync(() => test.Count("SetSceneItemEnabled") == 1, "the first request to be sent");
            var second = test.Camera.SetLockedAsync(true);

            await test.DisposeAsync();

            await Assert.ThrowsAsync<ObsUnknownOutcomeException>(() => first.WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => second.WithTimeoutAsync(Timeout));
        }

        [Fact]
        public async Task AChangeThatWaitsForItsTurn_IsNotSent_WhenTheCallerGivesUp()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(1) });
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (0, null) : null;
            var first = test.Camera.SetEnabledAsync(false);
            await StudioRig.EventuallyAsync(() => test.Count("SetSceneItemEnabled") == 1, "the first request to be sent");
            using var cancellation = new CancellationTokenSource();
            var second = test.Camera.SetLockedAsync(true, cancellation.Token);

            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.WithTimeoutAsync(Timeout));
            await Assert.ThrowsAsync<ObsUnknownOutcomeException>(() => first.WithTimeoutAsync(Timeout));
            await Task.Delay(200);
            Assert.Equal(0, test.Count("SetSceneItemLocked"));
        }

        [Fact]
        public async Task AChange_IsNotAsked_OfAnObjectThatWasRemoved()
        {
            await using var test = await Fixture.StartAsync();
            await test.Rig.Responder.SendEventAsync("SceneItemRemoved", new { sceneName = "Gaming", sceneUuid = test.Gaming, sourceName = "Camera", sourceUuid = Guid.NewGuid(), sceneItemId = 1 }, SceneItemEvents);
            await StudioRig.EventuallyAsync(() => test.Camera.IsRemoved, "the scene item to be removed");

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout));

            Assert.Equal(ObsObjectState.Gone, exception.State);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AChange_IsNotAsked_WithoutAConnection()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { AutoReconnect = false });
            await test.Rig.Studio.DisconnectAsync().WithTimeoutAsync(Timeout);

            var exception = await Assert.ThrowsAsync<ObsNotConnectedException>(() => test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout));

            Assert.Contains("was not sent", exception.Message);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AChange_IsNotAsked_WhileOBSStudioChangesItsSceneCollection()
        {
            await using var test = await Fixture.StartAsync();
            await test.Rig.Responder.SendEventAsync("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            await StudioRig.EventuallyAsync(() => test.Rig.Studio.Host.Gate.Switching, "the host to know that OBS Studio is switching");

            var exception = await Assert.ThrowsAsync<ObsNotReadyException>(() => test.Camera.SetEnabledAsync(true).WithTimeoutAsync(Timeout));

            Assert.Contains("was not sent", exception.Message);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task ChangesThatAreAskedForOneAfterTheOther_AreSentInThatOrder_OneAtATime()
        {
            await using var test = await Fixture.StartAsync();
            var sent = new List<bool>();
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                var value = data!.Value.GetProperty("sceneItemEnabled").GetBoolean();
                lock (sent)
                {
                    sent.Add(value);
                }

                // Every answer takes a while: a line that did not wait for it would send the next change meanwhile, and the order is the order of the answers.
                Thread.Sleep(20);
                test.SetEnabledInWorld(value);
                _ = test.SendEnabledEventAsync(value);
                return (100, null);
            };

            var expected = Enumerable.Range(0, 12).Select(i => i % 3 == 0).ToArray();
            var calls = expected.Select(value => test.Camera.SetEnabledAsync(value)).ToArray();
            await Task.WhenAll(calls).WithTimeoutAsync(Timeout);

            lock (sent)
            {
                Assert.Equal(expected, sent);
            }

            Assert.Equal(expected[^1], test.Camera.IsEnabled);
        }

        [Fact]
        public async Task TwoToggles_MakeTwoToggles_EvenWhenAskedForTogether()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                var value = data!.Value.GetProperty("sceneItemEnabled").GetBoolean();
                test.SetEnabledInWorld(value);
                _ = Task.Run(async () =>
                {
                    await Task.Delay(40);
                    await test.SendEnabledEventAsync(value);
                });
                return (100, null);
            };

            var first = test.Camera.ToggleEnabledAsync();
            var second = test.Camera.ToggleEnabledAsync();
            var third = test.Camera.ToggleEnabledAsync();
            var results = await Task.WhenAll(first, second, third).WithTimeoutAsync(Timeout);

            // The second toggle starts from what the first one made: off, on, off.
            Assert.Equal([false, true, false], results);
            Assert.False(test.Camera.IsEnabled);
            Assert.Equal(3, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AChangeThatWaitsForItsTurn_IsNotSent_WhenOBSStudioChangesItsSceneCollectionMeanwhile()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30) });
            using var release = new ManualResetEventSlim();
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type == "SetSceneItemEnabled")
                {
                    // The first change is held up in OBS Studio: it answers when the test lets it.
                    release.Wait(Timeout);
                    return (100, null);
                }

                return null;
            };
            var first = test.Camera.SetEnabledAsync(false);
            await StudioRig.EventuallyAsync(() => test.Count("SetSceneItemEnabled") == 1, "the first request to be sent");
            var waiting = test.Camera.SetLockedAsync(true);

            // OBS Studio begins to change its scene collection, the model is told, and then the first change is answered: the one that waited has its turn in a switch.
            await test.Rig.Responder.SendEventAsync("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            await StudioRig.EventuallyAsync(() => test.Rig.Studio.Host.Gate.Switching, "the host to know that OBS Studio is switching");
            release.Set();

            var exception = await Assert.ThrowsAsync<ObsNotReadyException>(() => waiting.WithTimeoutAsync(Timeout));
            Assert.Contains("changed its scene collection before", exception.Message);
            Assert.Equal(0, test.Count("SetSceneItemLocked"));
            await first.WithTimeoutAsync(Timeout);
        }

        [Fact]
        public void ARequestThatWasRefusedBeforeItWasWritten_IsNotSent_AndAnyOtherFailureIsNot()
        {
            Assert.True(StudioWriter.IsNotSent(new ObsNotConnectedException()));
            Assert.True(StudioWriter.IsNotSent(new ObsClientException("Not connected.")));

            // A failure while the message was written has the failure of the socket inside: the message may have gone.
            Assert.False(StudioWriter.IsNotSent(new ObsClientException("Not connected.", new IOException("reset"))));
            Assert.False(StudioWriter.IsNotSent(new ObsClientException("The connection to OBS Studio was closed before a response was received.")));
            Assert.False(StudioWriter.IsNotSent(new TimeoutException("Timeout waiting for OBS Studio response to request 1.")));
        }

        [Fact]
        public async Task AnArgumentThatIsNotAPosition_IsRefusedAtOnce()
        {
            await using var test = await Fixture.StartAsync();

            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Camera.SetIndexAsync(-1); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Camera.SetIndexAsync(1); });
            Assert.Equal(0, test.Count("SetSceneItemIndex"));
        }

        [Fact]
        public async Task AnInputWithoutAudio_CanNotBeMuted()
        {
            await using var test = await Fixture.StartAsync();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => test.Rig.Studio.Inputs["Camera"].SetMutedAsync(true).WithTimeoutAsync(Timeout));

            Assert.Contains("has no audio", exception.Message);
        }

        [Fact]
        public void TheTimesOfTheOptions_AreChecked()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.Zero }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObsStudio(new ObsStudioOptions { WriteTimeout = TimeSpan.FromDays(25) }));
        }

        // An OBS Studio with one scene, "Gaming", that shows one input without audio, "Camera", and the studio that follows it.
        private sealed class Fixture : IAsyncDisposable
        {
            private Fixture(StudioRig rig, Guid gaming)
            {
                this.Rig = rig;
                this.Gaming = gaming;
                this.Camera = rig.Studio.Scenes["Gaming"].Items["Camera"];
            }

            public StudioRig Rig { get; }

            public Guid Gaming { get; }

            public ObsSceneItem Camera { get; }

            public static async Task<Fixture> StartAsync(ObsStudioOptions? options = null)
            {
                var gaming = Guid.Empty;
                var rig = await StudioRig.ConnectedAsync(
                    world =>
                    {
                        gaming = world.AddScene("Gaming");
                        var camera = world.AddInput("Camera");
                        world.AddItem(gaming, camera, "Camera");
                    },
                    options ?? Options());
                return new Fixture(rig, gaming);
            }

            // OBS Studio makes the change: the world holds it from now on.
            public void SetEnabledInWorld(bool enabled)
            {
                this.Rig.Responder.Change(world =>
                {
                    var item = world.Items[this.Gaming][0];
                    world.Items[this.Gaming][0] = (item.Id, item.Source, item.IsScene, item.SourceName, item.Index, enabled, item.Locked);
                });
            }

            public Task SendEnabledEventAsync(bool enabled)
            {
                return this.Rig.Responder.SendEventAsync("SceneItemEnableStateChanged", new { sceneName = "Gaming", sceneUuid = this.Gaming, sceneItemId = 1, sceneItemEnabled = enabled }, SceneItemEvents);
            }

            public int Count(string type)
            {
                return this.Rig.Responder.RequestLog.Count(logged => logged == type);
            }

            public ValueTask DisposeAsync()
            {
                return this.Rig.DisposeAsync();
            }
        }
    }
}
