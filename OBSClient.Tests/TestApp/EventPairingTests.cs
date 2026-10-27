namespace OBSStudioClient.Tests.TestAppTests
{
    using OBSStudioClient.TestApp.Services;
    using System.Text.Json;

    /// <summary>
    /// The test app shows what OBS Studio sent next to what the client made of each event. The client raises the events on a thread
    /// of its own, a little after the messages were logged, so several messages of one type can be waiting when an event is raised.
    /// </summary>
    public class EventPairingTests
    {
        private static LogEntry Message(string eventType, object? eventData = null) => new()
        {
            Kind = LogKind.Received,
            OpCode = 5,
            EventType = eventType,
            Text = JsonSerializer.Serialize(new { op = 5, d = new { eventType, eventIntent = 1, eventData } }),
        };

        [Fact]
        public void TheEventsOfOneType_GetTheirMessagesInTheOrderInWhichTheyArrived()
        {
            var pairing = new EventPairing();
            var first = Message("InputMuteStateChanged", new { inputName = "Mic", inputMuted = true });
            var second = Message("InputMuteStateChanged", new { inputName = "Mic", inputMuted = true });
            var third = Message("InputMuteStateChanged", new { inputName = "Mic", inputMuted = true });
            pairing.Received(first);
            pairing.Received(second);
            pairing.Received(third);

            // Identical events: nothing but the order tells them apart.
            Assert.Same(first, pairing.Match("InputMuteStateChanged", """{"inputName":"Mic","inputMuted":true}"""));
            Assert.Same(second, pairing.Match("InputMuteStateChanged", """{"inputName":"Mic","inputMuted":true}"""));
            Assert.Same(third, pairing.Match("InputMuteStateChanged", """{"inputName":"Mic","inputMuted":true}"""));
            Assert.Null(pairing.Match("InputMuteStateChanged", "{}"));
        }

        [Fact]
        public void TheMessagesOfOneType_ThatComeAtOnce_AreMatchedByWhatTheyCarry()
        {
            var pairing = new EventPairing();
            var camera = Message("InputActiveStateChanged", new { inputName = "Camera", videoActive = true });
            var game = Message("InputActiveStateChanged", new { inputName = "Game", videoActive = true });
            pairing.Received(camera);
            pairing.Received(game);

            Assert.Same(camera, pairing.Match("InputActiveStateChanged", """{"inputName":"Camera","videoActive":true}"""));
            Assert.Same(game, pairing.Match("InputActiveStateChanged", """{"inputName":"Game","videoActive":true}"""));
        }

        [Fact]
        public void AMessageThatTheClientSkipped_DoesNotPutEveryLaterPairOffByOne()
        {
            var pairing = new EventPairing();
            var skipped = Message("InputActiveStateChanged", new { inputName = "Unreadable", videoActive = "yes" });
            var next = Message("InputActiveStateChanged", new { inputName = "Camera", videoActive = true });
            pairing.Received(skipped);
            pairing.Received(next);

            // The client could not read the first message, so it raised one event, for the second: that is the one it is matched with.
            Assert.Same(next, pairing.Match("InputActiveStateChanged", """{"inputName":"Camera","videoActive":true}"""));

            // And the skipped message is gone, so the next event is not matched with it.
            var later = Message("InputActiveStateChanged", new { inputName = "Game", videoActive = false });
            pairing.Received(later);
            Assert.Same(later, pairing.Match("InputActiveStateChanged", """{"inputName":"Game","videoActive":false}"""));
        }

        [Fact]
        public void AnEventWhoseMembersAreNamedDifferentlyAndAnEnumerationIsAName_IsStillMatched()
        {
            var pairing = new EventPairing();
            var first = Message("StreamStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED" });
            var second = Message("StreamStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED" });
            pairing.Received(first);
            pairing.Received(second);

            // The client shows an enumeration by its name, and the names of the members may differ in case: only what agrees counts.
            Assert.Same(second, pairing.Match("StreamStateChanged", """{"OutputActive":false,"OutputState":"Stopped"}"""));
        }

        [Fact]
        public void TheTypesOfEvents_AreKeptApart()
        {
            var pairing = new EventPairing();
            var scene = Message("CurrentProgramSceneChanged", new { sceneName = "Scene" });
            var mute = Message("InputMuteStateChanged", new { inputName = "Mic", inputMuted = true });
            pairing.Received(scene);
            pairing.Received(mute);

            Assert.Same(mute, pairing.Match("InputMuteStateChanged", "{}"));
            Assert.Same(scene, pairing.Match("CurrentProgramSceneChanged", "{}"));
        }

        [Fact]
        public void TheVolumeMeters_AreMatchedWithTheLatestMessage()
        {
            // The client passes on one volume meter in every 100 ms and skips the rest, so the messages are not in step with the events.
            var pairing = new EventPairing();
            for (var count = 0; count < 5; count++)
            {
                pairing.Received(Message("InputVolumeMeters", new { inputs = Array.Empty<object>(), count }));
            }

            var latest = Message("InputVolumeMeters", new { inputs = Array.Empty<object>(), count = 5 });
            pairing.Received(latest);

            Assert.Same(latest, pairing.Match("InputVolumeMeters", """{"inputs":[]}"""));
            Assert.Null(pairing.Match("InputVolumeMeters", """{"inputs":[]}"""));
        }

        [Fact]
        public void AnEventWithoutData_AndAnEventThatCouldNotBeShown_AreMatchedByOrder()
        {
            var pairing = new EventPairing();
            var first = Message("ExitStarted");
            var second = Message("ExitStarted");
            pairing.Received(first);
            pairing.Received(second);

            Assert.Same(first, pairing.Match("ExitStarted", "(the event has no data)"));
            Assert.Same(second, pairing.Match("ExitStarted", "(the event arguments could not be shown: boom)"));
        }

        [Fact]
        public void AMessageThatIsNotAnEvent_IsIgnored_AndNothingWaitsForever()
        {
            var pairing = new EventPairing();
            pairing.Received(new LogEntry { Kind = LogKind.Sent, OpCode = 5, EventType = "InputMuteStateChanged", Text = "{}" });
            pairing.Received(new LogEntry { Kind = LogKind.Received, OpCode = 7, RequestType = "GetVersion", Text = "{}" });
            Assert.Null(pairing.Match("InputMuteStateChanged", "{}"));

            // An event type that nobody asks for does not make the pairing grow without end: the oldest messages are dropped.
            var oldest = Message("SomeFutureEvent");
            pairing.Received(oldest);
            for (var count = 0; count < 300; count++)
            {
                pairing.Received(Message("SomeFutureEvent"));
            }

            Assert.NotSame(oldest, pairing.Match("SomeFutureEvent", "{}"));
        }

        [Fact]
        public void AfterClear_NothingWaits()
        {
            var pairing = new EventPairing();
            pairing.Received(Message("ExitStarted"));

            pairing.Clear();

            Assert.Null(pairing.Match("ExitStarted", "{}"));
        }

        [Fact]
        public void TheMessagesThatAReplacedClientLeftBehind_AreNotMatchedWithTheEventsOfTheNewOne()
        {
            // The app connects again with a new client and keeps its log. A message whose event the old client did not get to raise (it was
            // disposed first) stays behind, and of two identical messages the oldest is the one that is taken, so the new client needs a
            // pairing of its own: in a shared one the event of the new client would be shown with the message of the old connection.
            var stale = Message("InputMuteStateChanged", new { inputName = "Mic", inputMuted = true });
            var fresh = Message("InputMuteStateChanged", new { inputName = "Mic", inputMuted = true });
            const string raised = """{"inputName":"Mic","inputMuted":true}""";

            var shared = new EventPairing();
            shared.Received(stale);
            shared.Received(fresh);
            Assert.Same(stale, shared.Match("InputMuteStateChanged", raised));

            var replaced = new EventPairing();
            var current = new EventPairing();
            replaced.Received(stale);
            current.Received(fresh);
            Assert.Same(fresh, current.Match("InputMuteStateChanged", raised));
            Assert.Same(stale, replaced.Match("InputMuteStateChanged", raised));
        }
    }
}
