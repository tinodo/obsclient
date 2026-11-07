namespace OBSStudioClient.Tests.Studio
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Studio;
    using OBSStudioClient.Studio.Engine;
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
            var events = Task.CompletedTask;
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                var value = data!.Value.GetProperty("sceneItemEnabled").GetBoolean();
                test.SetEnabledInWorld(value);

                // OBS Studio sends its events in order, a moment after the answer (this runs on the one thread that answers, so the chain needs no lock).
                events = events.ContinueWith(
                    async _ =>
                    {
                        await Task.Delay(40);
                        await test.SendEnabledEventAsync(value);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default).Unwrap();
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
        public async Task AChangeToTheValueThatTheModelStillShowsFromBeforeAnEarlierChange_IsNotShownBeforeTheEarlierChangeIs()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromSeconds(5), WriteTimeout = TimeSpan.FromSeconds(30) });
            var events = Task.CompletedTask;
            var eventSentAt = new long[2];
            var answered = 0;
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                var value = data!.Value.GetProperty("sceneItemEnabled").GetBoolean();
                var number = answered++;
                test.SetEnabledInWorld(value);

                // OBS Studio sends its events in order, a good while after the answers (this runs on the one thread that answers, so the chain needs no lock).
                events = events.ContinueWith(
                    async _ =>
                    {
                        await Task.Delay(150);
                        Volatile.Write(ref eventSentAt[number], Stopwatch.GetTimestamp());
                        await test.SendEnabledEventAsync(value);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default).Unwrap();
                return (100, null);
            };

            // The camera is shown. It is hidden and shown again, and the second call does not wait for the first.
            var hide = test.Camera.SetEnabledAsync(false);
            var show = test.Camera.SetEnabledAsync(true);
            await show.WithTimeoutAsync(Timeout);
            var returnedAt = Stopwatch.GetTimestamp();

            // The model shows the camera when the calls are made, which is what the second call asks for: it must not take that for its own change. It returns when the model has
            // been told of the first change, and then of the second.
            var secondEventSentAt = Volatile.Read(ref eventSentAt[1]);
            Assert.NotEqual(0, secondEventSentAt);
            Assert.True(returnedAt >= secondEventSentAt);
            Assert.True(test.Camera.IsEnabled);
            await hide.WithTimeoutAsync(Timeout);
        }

        [Fact]
        public async Task ChangesThatAreAskedForTogether_AreEachShownByTheirOwnEvent_WithoutWaitingForTheTimeout()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromSeconds(30), WriteTimeout = TimeSpan.FromSeconds(30) });
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                var value = data!.Value.GetProperty("sceneItemEnabled").GetBoolean();
                test.SetEnabledInWorld(value);

                // The event is on its way before the answer, and the next change is sent when the answer has come: the model passes through the values one after the other, quickly.
                _ = test.SendEnabledEventAsync(value);
                return (100, null);
            };

            var calls = Enumerable.Range(0, 8).Select(_ => test.Camera.ToggleEnabledAsync()).ToArray();
            var results = await Task.WhenAll(calls).WithTimeoutAsync(TimeSpan.FromSeconds(5));

            // With a confirmation timeout of 30 seconds, a call that did not see the value that it waits for would not have returned in 5.
            Assert.Equal(Enumerable.Range(0, 8).Select(i => i % 2 == 1).ToArray(), results);
            Assert.True(test.Camera.IsEnabled);
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
        public async Task ChangesThatAreAskedForAfterAToggle_DoNotOvertakeIt()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromSeconds(30) });
            var sent = new List<string>();
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                switch (type)
                {
                    case "SetSceneItemLocked":
                        // The first change is held in OBS Studio: what is asked for meanwhile waits for its turn.
                        return (-1, null);
                    case "SetSceneItemEnabled":
                        var value = data!.Value.GetProperty("sceneItemEnabled").GetBoolean();
                        lock (sent)
                        {
                            sent.Add($"set {value}");
                        }

                        test.SetEnabledInWorld(value);
                        _ = test.SendEnabledEventAsync(value);
                        return (100, null);
                    default:
                        return null;
                }
            };
            var held = test.Camera.SetLockedAsync(true);
            await StudioRig.EventuallyAsync(() => test.Rig.Responder.HeldCount == 1, "the first change to be held");

            var first = test.Camera.ToggleEnabledAsync();
            var second = test.Camera.ToggleEnabledAsync();
            var third = test.Camera.SetEnabledAsync(false);
            await test.Rig.Responder.AnswerHeldAsync();
            await Task.WhenAll(held, first, second, third).WithTimeoutAsync(Timeout);

            // The camera is shown at the start. The toggles hide it and show it again, and the change that was asked for after them hides it. A change that overtook the second
            // toggle would have been made before it, and the camera would have been left shown.
            Assert.Equal(new[] { false, true }, new[] { await first, await second });
            lock (sent)
            {
                Assert.Equal(new[] { "set False", "set True", "set False" }, sent.ToArray());
            }

            Assert.False(test.Camera.IsEnabled);
        }

        [Fact]
        public async Task AToggle_TurnsOverWhatOBSStudioHoldsAtItsTurn_NotWhatTheModelShowedWhenItWasAskedFor()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromSeconds(30) });
            var sent = new List<string>();
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                switch (type)
                {
                    case "SetSceneItemLocked":
                        return (-1, null);
                    case "SetSceneItemEnabled":
                        var value = data!.Value.GetProperty("sceneItemEnabled").GetBoolean();
                        lock (sent)
                        {
                            sent.Add($"set {value}");
                        }

                        // OBS Studio makes the change, and no event tells the model: it shows the old value for as long as nothing reads it again.
                        test.SetEnabledInWorld(value);
                        return (100, null);
                    default:
                        return null;
                }
            };
            var held = test.Camera.SetLockedAsync(true);
            await StudioRig.EventuallyAsync(() => test.Rig.Responder.HeldCount == 1, "the first change to be held");

            // Both are asked for while the camera is shown, and the model shows it until the toggle has been made.
            var hide = test.Camera.SetEnabledAsync(false);
            var toggle = test.Camera.ToggleEnabledAsync();
            Assert.True(test.Camera.IsEnabled);
            await test.Rig.Responder.AnswerHeldAsync();
            await Task.WhenAll(held, hide, toggle).WithTimeoutAsync(Timeout);

            // The toggle turned over what OBS Studio held at its turn (hidden), not what the model showed when it was asked for (shown): it showed the camera again.
            Assert.True(await toggle);
            lock (sent)
            {
                Assert.Equal(new[] { "set False", "set True" }, sent.ToArray());
            }

            Assert.True(test.Camera.IsEnabled);
        }

        [Fact]
        public async Task AToggleOfAnItemThatOBSStudioDoesNotKnow_IsARemovedException_AndNothingIsChanged()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) => type == "GetSceneItemEnabled" ? (600, null) : null;

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => test.Camera.ToggleEnabledAsync().WithTimeoutAsync(Timeout));

            Assert.Equal("scene item 1 of scene 'Gaming'", exception.Description);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AToggleWhoseQuestionIsNotAnswered_IsANotReadyException_AndNothingIsChanged_AndTheLineGoesOn()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) => type == "GetSceneItemEnabled" ? (0, null) : null;

            var exception = await Assert.ThrowsAsync<ObsNotReadyException>(() => test.Camera.ToggleEnabledAsync().WithTimeoutAsync(Timeout));

            Assert.IsType<TimeoutException>(exception.InnerException);
            Assert.Contains("Nothing was changed", exception.Message);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
            Assert.True(test.Camera.IsEnabled);

            // The line is not stuck behind the question that nobody answered.
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                test.SetEnabledInWorld(false);
                _ = test.SendEnabledEventAsync(false);
                return (100, null);
            };
            await test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout);
            Assert.False(test.Camera.IsEnabled);
        }

        [Fact]
        public async Task AToggleWhoseQuestionIsUnderWayWhenTheConnectionEnds_IsANotConnectedException_AndNothingIsChanged()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30), AutoReconnect = false });
            test.Rig.Responder.OnRequest = (type, data) => type == "GetSceneItemEnabled" ? (0, null) : null;
            var toggle = test.Camera.ToggleEnabledAsync();
            await StudioRig.EventuallyAsync(() => test.Count("GetSceneItemEnabled") == 1, "the question to be sent");

            test.Rig.Responder.AbortConnection();

            var exception = await Assert.ThrowsAsync<ObsNotConnectedException>(() => toggle.WithTimeoutAsync(Timeout));
            Assert.Contains("Nothing was changed", exception.Message);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AToggleWhoseQuestionIsAnsweredAfterOBSStudioChangedItsSceneCollection_IsNotMade()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30) });
            test.Rig.Responder.OnRequest = (type, data) => type == "GetSceneItemEnabled" ? (-1, null) : null;
            var toggle = test.Camera.ToggleEnabledAsync();
            await StudioRig.EventuallyAsync(() => test.Rig.Responder.HeldCount == 1, "the question to be held");
            await test.Rig.Responder.SendEventAsync("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            await StudioRig.EventuallyAsync(() => test.Rig.Studio.Host.Gate.Switching, "the host to know that OBS Studio is switching");

            await test.Rig.Responder.AnswerHeldAsync(_ => (100, new { sceneItemEnabled = true }));

            var exception = await Assert.ThrowsAsync<ObsNotReadyException>(() => toggle.WithTimeoutAsync(Timeout));
            Assert.Contains("changed its scene collection before", exception.Message);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AToggleWhoseCallerGivesUpWhileOBSStudioIsAsked_IsNotMade_AndTheLineGoesOn()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30) });
            test.Rig.Responder.OnRequest = (type, data) => type == "GetSceneItemEnabled" ? (-1, null) : null;
            using var cancellation = new CancellationTokenSource();
            var toggle = test.Camera.ToggleEnabledAsync(cancellation.Token);
            await StudioRig.EventuallyAsync(() => test.Rig.Responder.HeldCount == 1, "the question to be held");

            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => toggle.WithTimeoutAsync(Timeout));

            // The question changes nothing: the line does not wait for its answer, and the change is not made when the answer comes.
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                test.SetEnabledInWorld(false);
                _ = test.SendEnabledEventAsync(false);
                return (100, null);
            };
            await test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout);
            await test.Rig.Responder.AnswerHeldAsync(_ => (100, new { sceneItemEnabled = true }));
            await Task.Delay(200);
            Assert.Equal(1, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AChangeThatTheCallerGaveUpAfterItWasSent_KeepsItsTurnUntilOBSStudioAnswers_AndTheModelFollowsIt()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromSeconds(30) });
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                switch (type)
                {
                    case "SetSceneItemEnabled":
                        return (-1, null);
                    case "SetSceneItemLocked":
                        // Made, and announced at once: the second change is shown by its event, so it does not make the model read the scene items (which would show the first change
                        // too, and hide whether the model follows it by itself).
                        test.SetLockedInWorld(true);
                        _ = test.SendLockedEventAsync(true);
                        return (100, null);
                    default:
                        return null;
                }
            };
            using var cancellation = new CancellationTokenSource();
            var first = test.Camera.SetEnabledAsync(false, cancellation.Token);
            await StudioRig.EventuallyAsync(() => test.Rig.Responder.HeldCount == 1, "the first change to be held");

            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WithTimeoutAsync(Timeout));
            var second = test.Camera.SetLockedAsync(true);
            await Task.Delay(300);

            // The caller is free, and OBS Studio may still make the first change: the second one must not be sent before OBS Studio has answered the first, or OBS Studio could
            // make them in the other order.
            Assert.Equal(0, test.Count("SetSceneItemLocked"));

            test.SetEnabledInWorld(false);
            await test.Rig.Responder.AnswerHeldAsync();
            await second.WithTimeoutAsync(Timeout);

            Assert.Equal(1, test.Count("SetSceneItemLocked"));

            // The model follows the first change, whether the caller waits or not: no event came, so it reads the scene items.
            await StudioRig.EventuallyAsync(() => !test.Camera.IsEnabled, "the model to follow the change that the caller gave up");
        }

        [Fact]
        public async Task AChangeThatTheCallerGaveUpAfterItWasSent_KeepsItsTurnUntilTheWriteTimeout_WhenOBSStudioDoesNotAnswer()
        {
            var writeTimeout = TimeSpan.FromMilliseconds(1000);
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = writeTimeout });
            long firstSent = 0;
            long secondSent = 0;
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                switch (type)
                {
                    case "SetSceneItemEnabled":
                        Interlocked.Exchange(ref firstSent, Stopwatch.GetTimestamp());
                        return (0, null);
                    case "SetSceneItemLocked":
                        Interlocked.Exchange(ref secondSent, Stopwatch.GetTimestamp());
                        return (100, null);
                    default:
                        return null;
                }
            };
            using var cancellation = new CancellationTokenSource();
            var first = test.Camera.SetEnabledAsync(false, cancellation.Token);
            await StudioRig.EventuallyAsync(() => test.Count("SetSceneItemEnabled") == 1, "the first request to be sent");
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WithTimeoutAsync(Timeout));

            await test.Camera.SetLockedAsync(true).WithTimeoutAsync(Timeout);

            var waited = Stopwatch.GetElapsedTime(Interlocked.Read(ref firstSent), Interlocked.Read(ref secondSent));
            Assert.True(waited >= writeTimeout * 0.8, $"The second change was sent {waited.TotalMilliseconds:F0} ms after the first, which nobody answered.");
        }

        [Fact]
        public async Task AChangeThatOBSStudioMade_ButThatTheModelCanNeitherBeToldNorReadAgain_IsANotConfirmedException()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions
            {
                ConfirmationTimeout = TimeSpan.FromMilliseconds(200),
                ReadTimeout = TimeSpan.FromMilliseconds(300),
                WriteTimeout = TimeSpan.FromSeconds(5),
            });
            var silent = false;
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type == "SetSceneItemEnabled")
                {
                    // OBS Studio makes the change, sends no event, and does not answer the reads of the model any more.
                    test.SetEnabledInWorld(false);
                    Volatile.Write(ref silent, true);
                    return (100, null);
                }

                return Volatile.Read(ref silent) && type == "GetSceneItemList" ? (0, null) : null;
            };

            var exception = await Assert.ThrowsAsync<ObsNotConfirmedException>(() => test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout));

            Assert.Equal("hiding scene item 1 of scene 'Gaming'", exception.Operation);
            Assert.Contains("Do not send the change again", exception.Message);
            Assert.Equal(1, test.Count("SetSceneItemEnabled"));

            // The change is not made again, and the model goes on reading: it shows what OBS Studio holds as soon as OBS Studio answers.
            Volatile.Write(ref silent, false);
            await StudioRig.EventuallyAsync(() => !test.Camera.IsEnabled, "the model to catch up", TimeSpan.FromSeconds(30));
            Assert.Equal(1, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AChangeThatTheCallerGaveUpWhileItWaitedForItsTurnToBeWritten_IsNeverWritten_AndTheLineGoesOn()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromSeconds(30) });
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemLocked")
                {
                    return null;
                }

                test.SetLockedInWorld(true);
                _ = test.SendLockedEventAsync(true);
                return (100, null);
            };

            // Another message is being written on the connection (a request that was sent through the client to an OBS Studio that does not read): nothing else is written until
            // it is done, and the batch of the change waits for its turn.
            var turn = await test.Rig.Studio.Client.TakeTheTurnToWriteAsync();
            using var cancellation = new CancellationTokenSource();
            var given = test.Camera.SetEnabledAsync(false, cancellation.Token);
            await Task.Delay(300);
            Assert.False(given.IsCompleted);

            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => given.WithTimeoutAsync(Timeout));
            turn.Dispose();

            // The batch was not written when the turn came (the caller had gone, and the batch was not on its way yet), and the line goes on: the next change is made.
            await test.Camera.SetLockedAsync(true).WithTimeoutAsync(Timeout);
            await Task.Delay(200);
            var log = string.Join(", ", test.Rig.Responder.RequestLog.Where(type => type.StartsWith("SetSceneItem", StringComparison.Ordinal)));
            Assert.True(test.Count("SetSceneItemEnabled") == 0, $"The change that the caller gave up was written. What the server received: {log}.");
            Assert.Equal(1, test.Count("SetSceneItemLocked"));
            Assert.True(test.Camera.IsEnabled);
        }

        [Fact]
        public async Task AChangeAfterAnEarlierOneThatTheEventsDidNotShow_IsNotShownAgainstWhatTheModelShowedBeforeTheModelIsReadAgain()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromSeconds(30) });
            var holdReads = false;
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type == "SetSceneItemEnabled")
                {
                    // OBS Studio makes the change and answers, and sends no event: the model knows nothing of it.
                    test.SetEnabledInWorld(data!.Value.GetProperty("sceneItemEnabled").GetBoolean());
                    Volatile.Write(ref holdReads, true);
                    return (100, null);
                }

                return type == "GetSceneItemList" && Volatile.Read(ref holdReads) ? (-1, null) : null;
            };

            // The camera is shown. It is hidden and shown again, and no event says so.
            var hide = test.Camera.SetEnabledAsync(false);
            var show = test.Camera.SetEnabledAsync(true);

            // The events do not come within 300 ms, the model is asked to read the scene items, and OBS Studio does not answer that yet. The second change asks for what the model
            // has shown from the start (shown): it must not be taken for shown before the model has been read again, or the call would return against what an earlier change left.
            await StudioRig.EventuallyAsync(() => test.Rig.Responder.HeldCount == 1, "the read of the scene items to be held");
            await Task.Delay(300);
            Assert.False(hide.IsCompleted);
            Assert.False(show.IsCompleted);

            // The read is answered: the model shows what OBS Studio holds (shown), and both calls end.
            Volatile.Write(ref holdReads, false);
            (int Code, object? Data) answer = default;
            test.Rig.Responder.Change(world => answer = world.Answer(new ReadRequest(ReadKind.Items, RequestType.GetSceneItemList, null, test.Gaming)));
            await test.Rig.Responder.AnswerHeldAsync(_ => answer);
            await Task.WhenAll(hide, show).WithTimeoutAsync(Timeout);
            Assert.True(test.Camera.IsEnabled);
        }

        [Fact]
        public void WhenTheCallerGivesUp_TheWaitForTheTurnToWriteEndsBeforeTheCallDoes()
        {
            // A batch that waits for its turn to be written on the connection is given up first: whoever sees the call end can let the turn go at once, and the batch must not
            // be the one that gets it.
            var job = new WriteJob { Description = "something", BuildRequests = () => [] };
            job.PrepareToGiveUp();
            bool? callEndedWhenTheWaitEnded = null;
            using var registration = job.BeforeWritten.Register(() => callEndedWhenTheWaitEnded = job.IsCompleted);

            job.GiveUp();

            Assert.False(callEndedWhenTheWaitEnded);
            Assert.True(job.IsCompleted);
        }

        [Fact]
        public async Task AChangeWhoseBatchWaitsForItsTurnToBeWritten_IsNotWritten_WhenOBSStudioChangesItsSceneCollectionMeanwhile()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30) });

            // Another message holds the turn to write on the connection, and the batch of the change waits for it. What was true at the turn of the change is not true when the
            // batch would be written: OBS Studio begins to change its scene collection, and the model knows.
            var turn = await test.Rig.Studio.Client.TakeTheTurnToWriteAsync();
            var change = test.Camera.SetEnabledAsync(false);
            await Task.Delay(300);
            Assert.False(change.IsCompleted);
            await test.Rig.Responder.SendEventAsync("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            await StudioRig.EventuallyAsync(() => test.Rig.Studio.Host.Gate.Switching, "the host to know that OBS Studio is switching");

            turn.Dispose();

            var exception = await Assert.ThrowsAsync<ObsNotReadyException>(() => change.WithTimeoutAsync(Timeout));
            Assert.Contains("changed its scene collection before", exception.Message);
            await Task.Delay(200);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AChangeWhoseBatchWaitsForItsTurnToBeWritten_IsNotWritten_WhenItsObjectIsRemovedMeanwhile()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30) });
            var turn = await test.Rig.Studio.Client.TakeTheTurnToWriteAsync();
            var change = test.Camera.SetEnabledAsync(false);
            await Task.Delay(300);
            Assert.False(change.IsCompleted);
            await test.Rig.Responder.SendEventAsync("SceneItemRemoved", new { sceneName = "Gaming", sceneUuid = test.Gaming, sourceName = "Camera", sourceUuid = Guid.NewGuid(), sceneItemId = 1 }, SceneItemEvents);
            await StudioRig.EventuallyAsync(() => test.Camera.IsRemoved, "the scene item to be removed");

            turn.Dispose();

            var exception = await Assert.ThrowsAsync<ObsRemovedException>(() => change.WithTimeoutAsync(Timeout));
            Assert.Equal(ObsObjectState.Gone, exception.State);
            await Task.Delay(200);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AChangeWhoseMessageIsBeingLogged_IsNotWritten_WhenOBSStudioBeginsToChangeItsSceneCollectionWhileTheLoggerIsBusy()
        {
            // The logger is code of the application, and it takes as long as it takes: the look at whether the batch can still be written has to come after the log.
            using var logger = new HoldingLoggerFactory("SetSceneItemEnabled");
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30) }, logger);
            var change = test.Camera.SetEnabledAsync(false);
            await logger.WaitUntilHoldingAsync();

            // The batch has passed the look at its turn, and the logger holds it: OBS Studio begins to change its scene collection, and the model knows.
            await test.Rig.Responder.SendEventAsync("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            await StudioRig.EventuallyAsync(() => test.Rig.Studio.Host.Gate.Switching, "the host to know that OBS Studio is switching");
            logger.Release();

            var exception = await Assert.ThrowsAsync<ObsNotReadyException>(() => change.WithTimeoutAsync(Timeout));
            Assert.Contains("changed its scene collection before", exception.Message);
            await Task.Delay(200);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AChangeWhoseCallerGivesUpWhileItIsBeingPutInTheLine_IsNeverWritten()
        {
            await using var test = await Fixture.StartAsync();

            // The caller gives up while its change is being checked, before the change is in the line: it is given up, and the line must not write it. (The registration of
            // the token comes before the change is put in the line; the other way round, the line could take the change before the caller was told to give up.)
            for (var round = 0; round < 200; round++)
            {
                using var cancellation = new CancellationTokenSource();
                var call = test.Rig.Studio.Writer.ExecuteAsync<object?>(
                    new WriteJob
                    {
                        Description = "hiding the camera",
                        Check = () => cancellation.Cancel(),
                        BuildRequests = () => [new WriteRequest(RequestType.SetSceneItemEnabled, new { sceneUuid = test.Gaming, sceneItemId = 1, sceneItemEnabled = false })],
                    },
                    cancellation.Token);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WithTimeoutAsync(Timeout));
            }

            await Task.Delay(300);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AChangeWhoseCallerGivesUpRightAfterItWasPutInTheLine_IsNeverWritten()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(300), WriteTimeout = TimeSpan.FromSeconds(30) });

            // The line is busy with a change that OBS Studio holds, so it cannot take the next change before the test lets it. (A line that is free races with the caller: it can
            // take the change and write it before the caller has given up at all, and then the test would prove nothing, and fail on a computer that is slow enough.)
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemLocked" ? (-1, null) : null;
            var held = test.Camera.SetLockedAsync(true);
            await StudioRig.EventuallyAsync(() => test.Rig.Responder.HeldCount == 1, "the first change to be held");

            using var cancellation = new CancellationTokenSource();
            Task? answered = null;
            test.Rig.Studio.Writer.AfterPutInTheLine = () =>
            {
                // The change is in the line, behind the one that OBS Studio holds. The caller gives up; then OBS Studio answers the first change, so that the line is free to take
                // this one, and the test waits long enough to see whether the line sends it all the same.
                cancellation.Cancel();
                answered = test.Rig.Responder.AnswerHeldAsync();
                Thread.Sleep(300);
            };

            var call = test.Camera.SetEnabledAsync(false, cancellation.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WithTimeoutAsync(Timeout));
            await answered!.WithTimeoutAsync(Timeout);
            await held.WithTimeoutAsync(Timeout);
            await Task.Delay(300);
            Assert.True(test.Count("SetSceneItemEnabled") == 0, "The change was written although its caller had given up before the line took it.");
        }

        [Fact]
        public async Task AChangeThatWasAskedForOnAConnectionThatWasReplaced_IsNotWrittenOnTheNewOne_WhenTheModelDoesNotKnowYet()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30) });
            using var held = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();

            // The loop of the model is held before it learns that the connection ended: the model still shows that connection (and so does what a change checks first) while the
            // client has made another. A loop that is busy with a large read is behind the client in the same way.
            test.Rig.Studio.Host.BeforeIngress = item =>
            {
                if (item.Kind == IngressKind.SessionEnded)
                {
                    held.Set();
                    release.Wait(Timeout);
                }
            };

            try
            {
                test.Rig.Responder.AbortConnection();
                Assert.True(held.Wait(Timeout), "The loop of the model was not told that the connection ended.");
                await StudioRig.EventuallyAsync(() => test.Rig.Responder.Handshakes == 2 && test.Rig.Studio.Client.ConnectionState == ConnectionState.Connected, "the client to be connected again");

                // Asked for on the connection that ended, as far as the model knows. The client would write it on the new one: it must not.
                var change = test.Camera.SetEnabledAsync(false);

                await Assert.ThrowsAsync<ObsNotConnectedException>(() => change.WithTimeoutAsync(Timeout));
                Assert.Equal(0, test.Count("SetSceneItemEnabled"));
            }
            finally
            {
                release.Set();
            }
        }

        [Fact]
        public async Task AChange_IsNotSent_WhileTheModelHasNotAppliedTheEndOfASwitchThatTheClientHasRead()
        {
            await using var test = await Fixture.StartAsync();
            var camera = test.Camera;
            using var held = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();

            // The loop of the model is held before it applies the first event of the switch, and the client goes on reading: it reads the end of the switch, OBS Studio is ready
            // again, and the model (and the objects that the application holds) are still those of the collection that was left. Asking for a change now would reach the collection
            // that is loaded, with what the application learned from the other.
            test.Rig.Studio.Host.BeforeIngress = item =>
            {
                if (item.Kind == IngressKind.Event && item.Event!.EventType == EventType.CurrentSceneCollectionChanging)
                {
                    held.Set();
                    release.Wait(Timeout);
                }
            };

            try
            {
                await test.Rig.Responder.SendEventAsync("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
                Assert.True(held.Wait(Timeout), "The loop of the model was not given the first event of the switch.");
                await test.Rig.Responder.SendEventAsync("CurrentSceneCollectionChanged", new { sceneCollectionName = "Untitled" }, 2);
                await StudioRig.EventuallyAsync(() => test.Rig.Studio.Host.CollectionEventsRead == 2, "the client to read both events of the switch");
                Assert.False(test.Rig.Studio.Host.Gate.Switching);
                Assert.False(camera.IsRemoved);

                var exception = await Assert.ThrowsAsync<ObsNotReadyException>(() => camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout));

                Assert.Contains("has not caught up", exception.Message);
                Assert.Equal(0, test.Count("SetSceneItemEnabled"));
            }
            finally
            {
                release.Set();
            }

            // The loop applies the switch: the model is read again, the objects of the old collection are Gone, and what is asked for now goes to the new ones.
            await StudioRig.EventuallyAsync(() => camera.IsRemoved, "the model to apply the switch");
            await Assert.ThrowsAsync<ObsRemovedException>(() => camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout));
            await test.Rig.Studio.WaitUntilSettledAsync(Timeout);
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemLocked" ? (100, null) : null;
            await test.Rig.Studio.Scenes["Gaming"].Items["Camera"].SetLockedAsync(true).WithTimeoutAsync(Timeout);
            Assert.Equal(1, test.Count("SetSceneItemLocked"));
        }

        [Fact]
        public async Task AChangeThatWaitsInTheLine_IsNotSent_WhenTheClientReadTheEndOfASwitchBeforeItsTurn_AndTheModelHasNotAppliedItYet()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { WriteTimeout = TimeSpan.FromSeconds(30) });
            using var held = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemLocked" ? (-1, null) : null;
            test.Rig.Studio.Host.BeforeIngress = item =>
            {
                if (item.Kind == IngressKind.Event && item.Event!.EventType == EventType.CurrentSceneCollectionChanging)
                {
                    held.Set();
                    release.Wait(Timeout);
                }
            };

            // OBS Studio holds the first change, and the second is asked for while the model is still of the collection that it knows, and waits for its turn.
            var first = test.Camera.SetLockedAsync(true);
            await StudioRig.EventuallyAsync(() => test.Rig.Responder.HeldCount == 1, "the first change to be held");
            var second = test.Camera.SetEnabledAsync(false);

            try
            {
                await test.Rig.Responder.SendEventAsync("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
                Assert.True(held.Wait(Timeout), "The loop of the model was not given the first event of the switch.");
                await test.Rig.Responder.SendEventAsync("CurrentSceneCollectionChanged", new { sceneCollectionName = "Untitled" }, 2);
                await StudioRig.EventuallyAsync(() => test.Rig.Studio.Host.CollectionEventsRead == 2, "the client to read both events of the switch");

                // The first change is answered, and the second has its turn: the model has not applied the switch, so only the number of events that the client read says that it
                // must not be sent.
                await test.Rig.Responder.AnswerHeldAsync();
                var exception = await Assert.ThrowsAsync<ObsNotReadyException>(() => second.WithTimeoutAsync(Timeout));

                Assert.Contains("changed its scene collection before", exception.Message);
                Assert.Equal(0, test.Count("SetSceneItemEnabled"));
            }
            finally
            {
                release.Set();
            }

            // The first change was made in the collection that was left: its call ends when the model has applied the switch.
            await first.WithTimeoutAsync(Timeout);
        }

        [Fact]
        public async Task AReadThatEndedBeforeTheWaitForItHadWrittenItDown_StillEndsTheWait()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(200), ReadTimeout = TimeSpan.FromSeconds(10), WriteTimeout = TimeSpan.FromSeconds(30) });

            // OBS Studio answers that it made the change, and made none, and sends no event: only the end of a read ends the wait (what a read says is what OBS Studio holds).
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemEnabled" ? (100, null) : null;
            var waiting = test.Rig.Studio.Host.SettleWaiterCount;
            test.Rig.Studio.Writer.AfterTheReadWasAskedFor = read =>
            {
                // The thread that asked for the read is held until the loop of the model has had the answer and looked at the wait of the change (the loop publishes the number of
                // waits that it holds at the end of each step): it finds no read written down yet.
                var ended = new ManualResetEventSlim();
                _ = read.ContinueWith(_ => ended.Set(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                ended.Wait(Timeout);
                SpinWait.SpinUntil(() => test.Rig.Studio.Host.SettleWaiterCount == waiting, Timeout);
            };

            // Nothing but the next item of the loop makes it look at the wait again, and none is coming: without the look after the read was written down, the call would end
            // with the time that the wait was given (more than twenty seconds), not with the read.
            await test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout);

            Assert.True(test.Camera.IsEnabled);
        }

        [Fact]
        public async Task AnAnswerWithAnEntryThatIsNull_IsAnUnknownOutcome_AndTheModelReadsAgain()
        {
            await using var test = await Fixture.StartAsync();

            // OBS Studio ran the batch, and the entry of its request says nothing: it may have made the change (here it did), and no event tells the model.
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                test.SetEnabledInWorld(false);
                return (-2, null);
            };
            var reads = test.Count("GetSceneItemList");

            var exception = await Assert.ThrowsAsync<ObsUnknownOutcomeException>(() => test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout));

            Assert.Contains("can not be read", exception.InnerException!.Message);
            await StudioRig.EventuallyAsync(() => test.Count("GetSceneItemList") > reads && !test.Camera.IsEnabled, "the model to read the scene items again, and to show what OBS Studio made");
            Assert.Equal(1, test.Count("SetSceneItemEnabled"));
        }

        [Fact]
        public async Task AnAnswerToTheQuestionOfAToggle_WithAnEntryThatIsNull_ChangesNothing_AndSaysSo()
        {
            await using var test = await Fixture.StartAsync();
            test.Rig.Responder.OnRequest = (type, data) => type == "GetSceneItemEnabled" ? (-2, null) : null;

            var exception = await Assert.ThrowsAsync<ObsClientException>(() => test.Camera.ToggleEnabledAsync().WithTimeoutAsync(Timeout));

            Assert.Contains("Nothing was changed", exception.Message);
            Assert.Equal(0, test.Count("SetSceneItemEnabled"));

            // The line goes on.
            test.Rig.Responder.OnRequest = (type, data) => type == "SetSceneItemLocked" ? (100, null) : null;
            await test.Camera.SetLockedAsync(true).WithTimeoutAsync(Timeout);
            Assert.Equal(1, test.Count("SetSceneItemLocked"));
        }

        [Fact]
        public async Task AChangeThatOBSStudioDidNotMake_EndsWithTheReadOfItsPart_WhileAnotherPartOfTheModelKeepsFailing()
        {
            await using var rig = await StudioRig.ConnectedAsync(
                world =>
                {
                    var gaming = world.AddScene("Gaming");
                    world.AddItem(gaming, world.AddInput("Camera"), "Camera");
                    world.AddInput("Mic", "wasapi_input_capture", 2, false, 1.0);
                },
                new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(200), ReadTimeout = TimeSpan.FromSeconds(10), WriteTimeout = TimeSpan.FromSeconds(30) });
            var camera = rig.Studio.Scenes["Gaming"].Items["Camera"];
            var mic = rig.Studio.Inputs["Mic"];

            // OBS Studio is not ready for the audio of the microphone, which the model asks for: it tries again and again, and never settles. And OBS Studio answers that it made
            // a change, and made none, and sends no event: only a read of the scene items shows what it holds.
            rig.Responder.OnRequest = (type, data) => type switch
            {
                "GetInputMute" or "GetInputVolume" => (207, null),
                "SetSceneItemEnabled" => (100, null),
                _ => null,
            };
            using var asked = new CancellationTokenSource();
            _ = rig.Studio.Host.RefreshAsync([new RefreshTarget(ReadKind.AudioMute, mic.Uuid)], asked.Token);
            await StudioRig.EventuallyAsync(() => rig.Responder.RequestLog.Count(type => type == "GetInputMute") >= 2, "the model to try to read the audio of the microphone again");

            try
            {
                // The call ends with the read of the scene items, which says what OBS Studio holds. Waiting for the whole model to settle, it would end with the time that the wait
                // was given (more than twenty seconds), and with an ObsNotConfirmedException, although the part that the change was about had been read.
                await camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout);
            }
            finally
            {
                asked.Cancel();
            }

            Assert.True(camera.IsEnabled);
        }

        [Fact]
        public async Task TheLongestTimesThatTheOptionsAllow_DoNotMakeAChangeFail()
        {
            // The wait for the model adds up the time for the events and twice the time for a read: three times 24 days is more than a timer can be given.
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromDays(24), ReadTimeout = TimeSpan.FromDays(24), WriteTimeout = TimeSpan.FromSeconds(30) });
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type != "SetSceneItemEnabled")
                {
                    return null;
                }

                test.SetEnabledInWorld(false);
                _ = test.SendEnabledEventAsync(false);
                return (100, null);
            };

            await test.Camera.SetEnabledAsync(false).WithTimeoutAsync(Timeout);

            Assert.False(test.Camera.IsEnabled);
        }

        [Fact]
        public async Task AReadThatWasAskedForAfterTheTimeForTheEvents_IsNotWaitedForAnyMore_WhenALateEventShowsTheChange()
        {
            await using var test = await Fixture.StartAsync(new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(200), ReadTimeout = TimeSpan.FromSeconds(30), WriteTimeout = TimeSpan.FromSeconds(30) });
            var silent = false;
            test.Rig.Responder.OnRequest = (type, data) =>
            {
                if (type == "SetSceneItemEnabled")
                {
                    // OBS Studio makes the change, and does not answer the reads of the model from now on.
                    test.SetEnabledInWorld(false);
                    Volatile.Write(ref silent, true);
                    return (100, null);
                }

                return Volatile.Read(ref silent) && type == "GetSceneItemList" ? (0, null) : null;
            };
            var waiting = test.Rig.Studio.Host.SettleWaiterCount;

            var call = test.Camera.SetEnabledAsync(false);

            // The time for the events ends, and the model is asked to read the scene items, which nobody answers: the wait for that read is in the loop of the model.
            await StudioRig.EventuallyAsync(() => test.Rig.Studio.Host.SettleWaiterCount > waiting, "the read to be asked for");

            // The event comes late: the change is shown and the call ends. The wait for the read is of no use any more, and it must not stay in the loop for as long as OBS
            // Studio fails the reads (the model goes on with them).
            await test.SendEnabledEventAsync(false);
            await call.WithTimeoutAsync(Timeout);
            await StudioRig.EventuallyAsync(() => test.Rig.Studio.Host.SettleWaiterCount == waiting, "the wait for the read to be let go of");
        }

        [Fact]
        public async Task AVolumeNearSilence_IsNotTakenForAnotherOneThatIsFewDecibelsAway()
        {
            // The volume of the microphone is 0.0001, and 0.00019 is 5.6 dB louder. OBS Studio makes no change and says nothing: the model shows 0.0001 all the time, which is not
            // what the call asks for, so the call has to wait for the events (and then for a read) and must not return at once.
            await using var rig = await StudioRig.ConnectedAsync(
                world =>
                {
                    world.AddScene("Gaming");
                    world.AddInput("Mic", "wasapi_input_capture", 2, false, 0.0001);
                },
                new ObsStudioOptions { ConfirmationTimeout = TimeSpan.FromMilliseconds(400), WriteTimeout = TimeSpan.FromSeconds(5) });
            rig.Responder.OnRequest = (type, data) => type == "SetInputVolume" ? (100, null) : null;
            var mic = rig.Studio.Inputs["Mic"];
            Assert.InRange(mic.VolumeMul, 0.00009f, 0.00011f);

            var started = Stopwatch.GetTimestamp();
            await mic.SetVolumeMulAsync(0.00019).WithTimeoutAsync(Timeout);
            var took = Stopwatch.GetElapsedTime(started);

            Assert.True(took >= TimeSpan.FromMilliseconds(300), $"The call returned after {took.TotalMilliseconds:F0} ms, against a volume that is 5.6 dB away from the one that it asked for.");
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

        // A logger factory whose loggers hold the thread that logs the message about a batch that is about to be written ("Sending: …") when it contains a text, until the test lets
        // it go: the application's own logger that takes its time.
        private sealed class HoldingLoggerFactory : ILoggerFactory
        {
            private readonly string _text;
            private readonly ManualResetEventSlim _holding = new();
            private readonly ManualResetEventSlim _released = new();

            public HoldingLoggerFactory(string text)
            {
                this._text = text;
            }

            public Task WaitUntilHoldingAsync()
            {
                return Task.Run(() =>
                {
                    if (!this._holding.Wait(Timeout))
                    {
                        throw new TimeoutException("The message was never logged.");
                    }
                });
            }

            public void Release()
            {
                this._released.Set();
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new HoldingLogger(this);
            }

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public void Dispose()
            {
                this._released.Set();
            }

            private sealed class HoldingLogger(HoldingLoggerFactory owner) : ILogger
            {
                public IDisposable? BeginScope<TState>(TState state)
                    where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    var message = formatter(state, exception);
                    if (message.StartsWith("Sending: ", StringComparison.Ordinal) && message.Contains(owner._text, StringComparison.Ordinal))
                    {
                        owner._holding.Set();
                        owner._released.Wait(Timeout);
                    }
                }
            }
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

            public static async Task<Fixture> StartAsync(ObsStudioOptions? options = null, ILoggerFactory? loggerFactory = null)
            {
                var gaming = Guid.Empty;
                var rig = await StudioRig.ConnectedAsync(
                    world =>
                    {
                        gaming = world.AddScene("Gaming");
                        var camera = world.AddInput("Camera");
                        world.AddItem(gaming, camera, "Camera");
                    },
                    options ?? Options(),
                    loggerFactory);
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

            // OBS Studio makes the change: the world holds it from now on.
            public void SetLockedInWorld(bool locked)
            {
                this.Rig.Responder.Change(world =>
                {
                    var item = world.Items[this.Gaming][0];
                    world.Items[this.Gaming][0] = (item.Id, item.Source, item.IsScene, item.SourceName, item.Index, item.Enabled, locked);
                });
            }

            public Task SendLockedEventAsync(bool locked)
            {
                return this.Rig.Responder.SendEventAsync("SceneItemLockStateChanged", new { sceneName = "Gaming", sceneUuid = this.Gaming, sceneItemId = 1, sceneItemLocked = locked }, SceneItemEvents);
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
