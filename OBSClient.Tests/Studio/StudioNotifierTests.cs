namespace OBSStudioClient.Tests.Studio
{
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging.Abstractions;
    using OBSStudioClient.Studio;

    /// <summary>
    /// <see cref="StudioNotifier"/>: the order of the notifications, and what happens when the handlers are slower than the model.
    /// </summary>
    public class StudioNotifierTests
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
            var notifier = new StudioNotifier(null, NullLogger.Instance, 100, recorder.Recovered);

            for (var i = 0; i < 50; i++)
            {
                notifier.Enqueue(recorder.Note(i.ToString()));
            }

            await StudioRig.EventuallyAsync(() => recorder.Raised.Length == 50, "the notifications");
            Assert.Equal(Enumerable.Range(0, 50).Select(i => i.ToString()), recorder.Raised);
            Assert.Equal(0, notifier.WaitingCount);
        }

        [Fact]
        public async Task WhenTooManyWait_TheNewOnesAreDropped_AndTheRecoveryComesAfterTheOnesThatWait()
        {
            var recorder = new Recorder();
            var notifier = new StudioNotifier(null, NullLogger.Instance, 5, recorder.Recovered);
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

            await StudioRig.EventuallyAsync(() => recorder.Raised.Contains("recovered"), "the recovery");
            Assert.Equal(new[] { "blocker", "n1", "n2", "n3", "n4", "n5", "recovered" }, recorder.Raised);
            Assert.Equal(0, notifier.WaitingCount);

            // Notifications are accepted again.
            notifier.Enqueue(recorder.Note("later"));
            await StudioRig.EventuallyAsync(() => recorder.Raised.Contains("later"), "a notification after the recovery");
            Assert.Equal("later", recorder.Raised[^1]);
        }

        [Fact]
        public async Task OnAContext_WhatWaitsThereCounts_ThatIsHowTheMemoryOfAUserInterfaceThatDoesNotRespondIsBounded()
        {
            var context = new HeldContext();
            var recorder = new Recorder();
            var notifier = new StudioNotifier(context, NullLogger.Instance, 3, recorder.Recovered);

            for (var i = 1; i <= 3; i++)
            {
                notifier.Enqueue(recorder.Note($"n{i}"));
            }

            await StudioRig.EventuallyAsync(() => context.Count == 3, "the notifications to be posted");
            Assert.Equal(3, notifier.WaitingCount);

            // They are posted, but the thread of the context has not run them: they still count, so the fourth is one too many.
            notifier.Enqueue(recorder.Note("n4"));
            notifier.Enqueue(recorder.Note("n5"));
            await StudioRig.EventuallyAsync(() => context.Count == 4, "the recovery to be posted");
            context.RunAll();

            Assert.Equal(new[] { "n1", "n2", "n3", "recovered" }, recorder.Raised);
            Assert.Equal(0, notifier.WaitingCount);
        }

        [Fact]
        public async Task AfterStop_NothingIsRaised_AndNothingIsQueued()
        {
            var context = new HeldContext();
            var recorder = new Recorder();
            var notifier = new StudioNotifier(context, NullLogger.Instance, 10, recorder.Recovered);
            notifier.Enqueue(recorder.Note("posted"));
            await StudioRig.EventuallyAsync(() => context.Count == 1, "the notification to be posted");

            notifier.Stop();
            notifier.Enqueue(recorder.Note("after"));
            context.RunAll();

            Assert.Empty(recorder.Raised);
            Assert.Equal(0, notifier.WaitingCount);
        }

        [Fact]
        public async Task Stop_LetsGoOfWhatWaits_SoThatNothingIsHeldForAsLongAsTheStudioCanBeReached()
        {
            var recorder = new Recorder();
            var notifier = new StudioNotifier(null, NullLogger.Instance, 100, recorder.Recovered);
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
            var notifier = new StudioNotifier(context, NullLogger.Instance, 1, recorder.Recovered);
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

            await StudioRig.EventuallyAsync(() => context.Count == 2, "both to be posted to the context");
            context.RunAll();
            Assert.Equal(new[] { "first", "recovered" }, recorder.Raised);
        }

        [Fact]
        public void TheCapacity_MustBeAtLeastOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StudioNotifier(null, NullLogger.Instance, 0, () => { }));
        }
    }
}
