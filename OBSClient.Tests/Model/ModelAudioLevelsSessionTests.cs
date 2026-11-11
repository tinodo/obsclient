namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using Microsoft.Extensions.Logging.Abstractions;
    using OBSStudioClient;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Messages;
    using OBSStudioClient.Model;

    /// <summary>
    /// What <see cref="ModelAudioLevels"/> does with the connections that the levels belong to, which a model that talks to a server cannot be made to meet: a message of a connection that has not started, that
    /// ended, or that is not the one that the model is connected to, and the start and end of another connection. The inputs are made by the test, and the messages are given to it as the receive loop of the client does.
    /// </summary>
    public sealed class ModelAudioLevelsSessionTests : IAsyncDisposable
    {
        private readonly ObsModel _model = new();
        private readonly ModelNotifier _notifier = new(null, NullLogger.Instance, 100, () => { });
        private readonly ModelAudioLevels _levels;
        private readonly ObsNamedCollection<ObsInput> _inputs;
        private readonly ObsInput _mic;
        private readonly List<ObsAudioLevelsEventArgs> _raised = [];
        private long _sequence;

        public ModelAudioLevelsSessionTests()
        {
            this._mic = new ObsInput(this._model, Guid.NewGuid(), "Mic", ObsObjectState.Live, "wasapi_input_capture", true);
            this._inputs = new ObsNamedCollection<ObsInput>("input", [this._mic], false);
            this._levels = new ModelAudioLevels(
                () => this._inputs,
                this._notifier,
                args =>
                {
                    lock (this._raised)
                    {
                        this._raised.Add(args);
                    }
                });
        }

        public ValueTask DisposeAsync()
        {
            this._notifier.Stop();
            return this._model.DisposeAsync();
        }

        private int Told()
        {
            lock (this._raised)
            {
                return this._raised.Count;
            }
        }

        private IngressItem Meters(int session, float magnitude)
        {
            var raw = JsonSerializer.SerializeToElement(new { inputs = new[] { new { inputName = this._mic.Name, inputUuid = this._mic.Uuid, inputLevelsMul = new[] { new[] { magnitude, 0f, 0f } } } } });
            var message = new EventMessage(EventType.InputVolumeMeters, EventSubscriptions.InputVolumeMeters, raw);
            message.OnDeserialized();
            return new IngressItem(IngressKind.Event, Interlocked.Increment(ref this._sequence), session, message);
        }

        private IngressItem Other(int session)
        {
            var message = new EventMessage(EventType.InputMuteStateChanged, EventSubscriptions.Inputs, JsonSerializer.SerializeToElement(new { inputName = this._mic.Name, inputUuid = this._mic.Uuid, inputMuted = true }));
            message.OnDeserialized();
            return new IngressItem(IngressKind.Event, Interlocked.Increment(ref this._sequence), session, message);
        }

        private IngressItem Session(IngressKind kind, int session)
        {
            return new IngressItem(kind, Interlocked.Increment(ref this._sequence), session, null);
        }

        [Fact]
        public void TheLevels_AreTakenByTheModel_ButNothingElseIs()
        {
            Assert.False(this._levels.Observe(this.Session(IngressKind.SessionStarted, 1)));
            Assert.True(this._levels.Observe(this.Meters(1, 0.5f)));
            Assert.False(this._levels.Observe(this.Other(1)));
            Assert.False(this._levels.Observe(this.Session(IngressKind.SessionEnded, 1)));
        }

        [Fact]
        public void AMessage_BeforeAConnectionStarted_SaysNothing()
        {
            Assert.True(this._levels.Observe(this.Meters(1, 0.5f)));

            Assert.Null(this._mic.Levels);
        }

        [Fact]
        public void AMessage_OfAnotherConnection_SaysNothing()
        {
            this._levels.Observe(this.Session(IngressKind.SessionStarted, 2));

            this._levels.Observe(this.Meters(1, 0.25f));
            Assert.Null(this._mic.Levels);

            this._levels.Observe(this.Meters(2, 0.75f));
            Assert.Equal(0.75f, this._mic.Levels!.Channels[0].Magnitude);
        }

        [Fact]
        public async Task TheEndOfAnotherConnection_TakesNothing_AndTheEndOfTheConnectionTakesTheLevels()
        {
            this._levels.Observe(this.Session(IngressKind.SessionStarted, 1));
            this._levels.Observe(this.Meters(1, 0.5f));
            await ModelRig.EventuallyAsync(() => this.Told() == 1, "the levels to be told");

            this._levels.Observe(this.Session(IngressKind.SessionEnded, 7));
            Assert.NotNull(this._mic.Levels);

            this._levels.Observe(this.Session(IngressKind.SessionEnded, 1));
            Assert.Null(this._mic.Levels);
            await ModelRig.EventuallyAsync(() => this.Told() == 2, "the end of the levels to be told");
            lock (this._raised)
            {
                Assert.Same(this._mic, Assert.Single(this._raised[1].Ended));
                Assert.Empty(this._raised[1].Levels);
            }
        }

        [Fact]
        public async Task AMessage_AfterTheEndOfItsConnection_SaysNothing_AndTheNextConnectionStartsFromNothing()
        {
            this._levels.Observe(this.Session(IngressKind.SessionStarted, 1));
            this._levels.Observe(this.Meters(1, 0.5f));
            await ModelRig.EventuallyAsync(() => this.Told() == 1, "the levels to be told");
            this._levels.Observe(this.Session(IngressKind.SessionEnded, 1));
            await ModelRig.EventuallyAsync(() => this.Told() == 2, "the end of the levels to be told");

            this._levels.Observe(this.Meters(1, 0.9f));
            Assert.Null(this._mic.Levels);

            this._levels.Observe(this.Session(IngressKind.SessionStarted, 2));
            this._levels.Observe(this.Meters(2, 0.1f));
            await ModelRig.EventuallyAsync(() => this.Told() == 3, "the levels of the new connection to be told");
            Assert.Equal(0.1f, this._mic.Levels!.Channels[0].Magnitude);
            lock (this._raised)
            {
                Assert.Empty(this._raised[2].Ended);
            }
        }

        [Fact]
        public async Task AnInputThatEndedAndIsListedAgainBeforeTheNotificationRan_IsNotAnInputThatEnded()
        {
            using var gate = new ManualResetEventSlim();
            var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this._notifier.Enqueue(() =>
            {
                blocked.TrySetResult();
                gate.Wait(TimeSpan.FromSeconds(10));
            });
            await blocked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            this._levels.Observe(this.Session(IngressKind.SessionStarted, 1));

            // While the lane is held: listed, not listed, listed again with other levels. The notification that is queued tells what is so when it runs.
            this._levels.Observe(this.Meters(1, 0.5f));
            this._levels.Observe(new IngressItem(IngressKind.Event, Interlocked.Increment(ref this._sequence), 1, EmptyMessage()));
            this._levels.Observe(this.Meters(1, 0.25f));
            gate.Set();
            await ModelRig.EventuallyAsync(() => this.Told() == 1, "the levels to be told");

            await Task.Delay(100);
            Assert.Equal(1, this.Told());
            lock (this._raised)
            {
                Assert.Empty(this._raised[0].Ended);
                Assert.Equal(0.25f, Assert.Single(this._raised[0].Levels).Channels[0].Magnitude);
            }
        }

        [Fact]
        public async Task WhenTheContextRefusesTheNotification_TheNextLevelsQueueAnotherOne()
        {
            var context = new HeldContext { Failing = true };
            var notifier = new ModelNotifier(context, NullLogger.Instance, 100, () => { });
            var raised = 0;
            var levels = new ModelAudioLevels(() => this._inputs, notifier, _ => Interlocked.Increment(ref raised));

            // The notifier counts the notification as no longer waiting a moment before it says that it dropped it: it is the producer being told that lets the next levels queue another one.
            var dropped = 0;
            var producerIsTold = notifier.Dropped!;
            notifier.Dropped = () =>
            {
                producerIsTold();
                Interlocked.Increment(ref dropped);
            };
            levels.Observe(this.Session(IngressKind.SessionStarted, 1));

            // The notification is queued, and the context refuses it: nothing will run it, so nothing may wait for it.
            levels.Observe(this.Meters(1, 0.5f));
            await ModelRig.EventuallyAsync(() => Volatile.Read(ref dropped) == 1, "the context to refuse the notification");
            Assert.Equal(0, notifier.WaitingCount);

            context.Failing = false;
            levels.Observe(this.Meters(1, 0.6f));
            await ModelRig.EventuallyAsync(() => context.Count == 1, "a notification to be posted after the refusal");
            context.RunAll();

            Assert.Equal(1, Volatile.Read(ref raised));
            Assert.Equal(0.6f, this._mic.Levels!.Channels[0].Magnitude);
        }

        private static EventMessage EmptyMessage()
        {
            var message = new EventMessage(EventType.InputVolumeMeters, EventSubscriptions.InputVolumeMeters, JsonSerializer.SerializeToElement(new { inputs = Array.Empty<object>() }));
            message.OnDeserialized();
            return message;
        }
    }
}
