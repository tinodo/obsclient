namespace OBSStudioClient.Tests.Model
{
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging.Abstractions;
    using OBSStudioClient.Model;

    /// <summary>
    /// <see cref="ModelNotifier"/>: the order of the notifications, and what happens when the handlers are slower than the model.
    /// </summary>
    public class ModelNotifierTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private sealed class Recorder
        {
            private readonly ConcurrentQueue<string> _raised = new();

            public string[] Raised => this._raised.ToArray();

            public Action Note(string what)
            {
                return () => this._raised.Enqueue(what);
            }

            public void Recovered()
            {
                this._raised.Enqueue("recovered");
            }
        }

        [Fact]
        public async Task TheNotifications_AreRaisedOneAtATime_InTheOrderInWhichTheyWereQueued()
        {
            var recorder = new Recorder();
            var notifier = new ModelNotifier(null, NullLogger.Instance, 100, recorder.Recovered);

            for (var i = 0; i < 50; i++)
            {
                notifier.Enqueue(recorder.Note(i.ToString()));
            }

            await ModelRig.EventuallyAsync(() => recorder.Raised.Length == 50, "the notifications");
            Assert.Equal(Enumerable.Range(0, 50).Select(i => i.ToString()), recorder.Raised);
            Assert.Equal(0, notifier.WaitingCount);
        }

        [Fact]
        public async Task WhenTooManyWait_TheNewOnesAreDropped_AndTheRecoveryComesAfterTheOnesThatWait()
        {
            var recorder = new Recorder();
            var notifier = new ModelNotifier(null, NullLogger.Instance, 5, recorder.Recovered);
            using var gate = new ManualResetEventSlim();
            var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // A handler that does not return: the notifications behind it wait.
            notifier.Enqueue(() =>
            {
                blocked.TrySetResult();
                gate.Wait(Timeout);
                recorder.Note("blocker")();
            });
            await blocked.Task.WaitAsync(Timeout);
            for (var i = 1; i <= 5; i++)
            {
                notifier.Enqueue(recorder.Note($"n{i}"));
            }

            Assert.Equal(5, notifier.WaitingCount);

            // The sixth is one too many: the recovery takes its place, and what comes after it is dropped.
            notifier.Enqueue(recorder.Note("n6"));
            notifier.Enqueue(recorder.Note("n7"));
            notifier.Enqueue(recorder.Note("n8"));
            Assert.Equal(6, notifier.WaitingCount);
            gate.Set();

            await ModelRig.EventuallyAsync(() => recorder.Raised.Contains("recovered"), "the recovery");
            Assert.Equal(new[] { "blocker", "n1", "n2", "n3", "n4", "n5", "recovered" }, recorder.Raised);
            Assert.Equal(0, notifier.WaitingCount);

            // Notifications are accepted again.
            notifier.Enqueue(recorder.Note("later"));
            await ModelRig.EventuallyAsync(() => recorder.Raised.Contains("later"), "a notification after the recovery");
            Assert.Equal("later", recorder.Raised[^1]);
        }

        [Fact]
        public async Task OnAContext_WhatWaitsThereCounts_ThatIsHowTheMemoryOfAUserInterfaceThatDoesNotRespondIsBounded()
        {
            var context = new HeldContext();
            var recorder = new Recorder();
            var notifier = new ModelNotifier(context, NullLogger.Instance, 3, recorder.Recovered);

            for (var i = 1; i <= 3; i++)
            {
                notifier.Enqueue(recorder.Note($"n{i}"));
            }

            await ModelRig.EventuallyAsync(() => context.Count == 3, "the notifications to be posted");
            Assert.Equal(3, notifier.WaitingCount);

            // They are posted, but the thread of the context has not run them: they still count, so the fourth is one too many.
            notifier.Enqueue(recorder.Note("n4"));
            notifier.Enqueue(recorder.Note("n5"));
            await ModelRig.EventuallyAsync(() => context.Count == 4, "the recovery to be posted");
            context.RunAll();

            Assert.Equal(new[] { "n1", "n2", "n3", "recovered" }, recorder.Raised);
            Assert.Equal(0, notifier.WaitingCount);
        }

        [Fact]
        public async Task AfterStop_NothingIsRaised_AndNothingIsQueued()
        {
            var context = new HeldContext();
            var recorder = new Recorder();
            var notifier = new ModelNotifier(context, NullLogger.Instance, 10, recorder.Recovered);
            notifier.Enqueue(recorder.Note("posted"));
            await ModelRig.EventuallyAsync(() => context.Count == 1, "the notification to be posted");

            notifier.Stop();
            notifier.Enqueue(recorder.Note("after"));
            context.RunAll();

            Assert.Empty(recorder.Raised);
            Assert.Equal(0, notifier.WaitingCount);
        }

        [Fact]
        public async Task Stop_LetsGoOfWhatWaits_SoThatNothingIsHeldForAsLongAsTheModelCanBeReached()
        {
            var recorder = new Recorder();
            var notifier = new ModelNotifier(null, NullLogger.Instance, 100, recorder.Recovered);
            using var gate = new ManualResetEventSlim();
            var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            notifier.Enqueue(() =>
            {
                blocked.TrySetResult();
                gate.Wait(Timeout);
            });
            await blocked.Task.WaitAsync(Timeout);
            for (var i = 1; i <= 5; i++)
            {
                notifier.Enqueue(recorder.Note($"n{i}"));
            }

            Assert.Equal(5, notifier.WaitingCount);

            notifier.Stop();

            Assert.Equal(0, notifier.WaitingCount);
            gate.Set();
            await Task.Delay(100);
            Assert.Empty(recorder.Raised);
        }

        [Fact]
        public async Task TheRecovery_IsQueuedAfterEverythingThatWasAdmitted_WhateverTheOrderInWhichProducersMeet()
        {
            // The notifications are posted to a context that does not run them yet, so what was admitted stays counted: the second producer finds the queue full
            // whatever the delivery does meanwhile.
            var context = new HeldContext();
            var recorder = new Recorder();
            var notifier = new ModelNotifier(context, NullLogger.Instance, 1, recorder.Recovered);
            using var paused = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var inserts = 0;
            notifier.BeforeInsert = () =>
            {
                // The first producer has been admitted, and has not queued its notification yet.
                if (Interlocked.Increment(ref inserts) == 1)
                {
                    paused.Set();
                    release.Wait(Timeout);
                }
            };

            var first = Task.Run(() => notifier.Enqueue(recorder.Note("first")));
            Assert.True(paused.Wait(Timeout));

            // The second producer comes now: its notification is one too many, so the recovery takes its place, and that must be behind the first notification.
            var second = Task.Run(() => notifier.Enqueue(recorder.Note("second")));
            await Task.Delay(200);
            release.Set();
            await Task.WhenAll(first, second).WaitAsync(Timeout);

            await ModelRig.EventuallyAsync(() => context.Count == 2, "both to be posted to the context");
            context.RunAll();
            Assert.Equal(new[] { "first", "recovered" }, recorder.Raised);
        }

        [Fact]
        public async Task AContextThatCannotBePostedTo_DoesNotStopTheDeliveryForGood()
        {
            var context = new HeldContext { Failing = true };
            var recorder = new Recorder();
            var notifier = new ModelNotifier(context, NullLogger.Instance, 100, recorder.Recovered);

            // The context refuses what is posted to it (it was closed, or is closing): the notifications are dropped, and do not count as waiting.
            notifier.Enqueue(recorder.Note("lost"));
            notifier.Enqueue(recorder.Note("lost too"));
            await ModelRig.EventuallyAsync(() => notifier.WaitingCount == 0, "the notifications that the context refused to be let go of");

            // When it takes them again, the delivery goes on: it was not left stuck by what went wrong.
            context.Failing = false;
            notifier.Enqueue(recorder.Note("later"));
            await ModelRig.EventuallyAsync(() => context.Count == 1, "a notification to be posted after the failure");
            context.RunAll();

            Assert.Equal(new[] { "later" }, recorder.Raised);
            Assert.Equal(0, notifier.WaitingCount);
        }

        [Fact]
        public async Task WhenTheContextRefusesTheRecovery_NotificationsAreAcceptedAgain()
        {
            var context = new HeldContext();
            var recorder = new Recorder();
            var notifier = new ModelNotifier(context, NullLogger.Instance, 2, recorder.Recovered);
            notifier.Enqueue(recorder.Note("n1"));
            notifier.Enqueue(recorder.Note("n2"));
            await ModelRig.EventuallyAsync(() => context.Count == 2, "the notifications to be posted");

            // The third is one too many: the recovery takes its place, and the context refuses it. Nothing would run the recovery, which is what makes the
            // notifier accept notifications again, so the refusal has to do it.
            context.Failing = true;
            notifier.Enqueue(recorder.Note("n3"));
            await ModelRig.EventuallyAsync(() => notifier.WaitingCount == 2, "the recovery to be refused by the context");
            context.Failing = false;

            // The two that are posted still count, so the next one is one too many again: that it is not simply dropped shows that notifications are accepted,
            // and the recovery that takes its place is posted.
            notifier.Enqueue(recorder.Note("n4"));
            await ModelRig.EventuallyAsync(() => context.Count == 3, "a recovery to be posted after the refusal");
            context.RunAll();

            Assert.Equal(new[] { "n1", "n2", "recovered" }, recorder.Raised);
            Assert.Equal(0, notifier.WaitingCount);
        }

        [Fact]
        public void TheCapacity_MustBeAtLeastOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModelNotifier(null, NullLogger.Instance, 0, () => { }));
        }
    }
}
