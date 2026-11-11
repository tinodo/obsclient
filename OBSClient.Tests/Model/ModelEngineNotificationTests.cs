namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Enums;
    using OBSStudioClient.Events;
    using OBSStudioClient.Model;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// What the model does with the events that say that something happened and that are no state of the model (a file was written, OBS Studio is exiting, somebody sent a custom event):
    /// it hands them on as notifications, in the order of arrival, and keeps the files that the outputs announced.
    /// </summary>
    public class ModelEngineNotificationTests
    {
        private const int GeneralEvents = 1;
        private const int OutputEvents = 64;
        private const int VendorEvents = 512;

        private static EngineHarness Started()
        {
            var harness = new EngineHarness();
            harness.World.AddScene("Scene");
            harness.Start();
            harness.SettleWithTime();
            harness.Engine.TakeChanges();
            return harness;
        }

        private static ModelChange[] Notifications(EngineHarness h)
        {
            return h.Engine.TakeChanges().Where(change => change.Kind == ModelChangeKind.Notification).ToArray();
        }

        private static string FileOf(EngineHarness h, ObsOutputKind kind)
        {
            return h.Engine.Output(kind).LastFile.Value!;
        }

        [Fact]
        public void WhatHappened_IsANotification_WithTheTypeOfTheEventAndItsData()
        {
            var h = Started();

            h.Event("CustomEvent", new { source = "a test", number = 3 }, GeneralEvents);
            h.Event("VendorEvent", new { vendorName = "a-plugin", eventType = "Something", eventData = new { value = 1 } }, VendorEvents);
            h.Event("ScreenshotSaved", new { savedScreenshotPath = "C:\\Pictures\\shot.png" }, GeneralEvents);
            h.Event("ExitStarted", new { }, GeneralEvents);
            h.Event("RecordFileChanged", new { newOutputPath = "C:\\Videos\\b.mkv" }, OutputEvents);
            h.Event("ReplayBufferSaved", new { savedReplayPath = "C:\\Videos\\replay.mkv" }, OutputEvents);

            var notifications = Notifications(h);

            Assert.Equal(
                new[] { EventType.CustomEvent, EventType.VendorEvent, EventType.ScreenshotSaved, EventType.ExitStarted, EventType.RecordFileChanged, EventType.ReplayBufferSaved },
                notifications.Select(change => (EventType)change.OldValue!).ToArray());
            Assert.Equal(
                new[] { "CustomEvent", "VendorEvent", "ScreenshotSaved", "ExitStarted", "RecordFileChanged", "ReplayBufferSaved" },
                notifications.Select(change => change.Text).ToArray());
            Assert.Equal("a test", Assert.IsType<CustomEventEventArgs>(notifications[0].NewValue).EventData!.Value.GetProperty("source").GetString());
            var vendor = Assert.IsType<VendorEventEventArgs>(notifications[1].NewValue);
            Assert.Equal("a-plugin", vendor.VendorName);
            Assert.Equal("Something", vendor.EventType);
            Assert.Equal("C:\\Pictures\\shot.png", Assert.IsType<ScreenshotSavedEventArgs>(notifications[2].NewValue).SavedScreenshotPath);
            Assert.NotNull(notifications[3].NewValue);
            Assert.Equal("C:\\Videos\\b.mkv", Assert.IsType<RecordFileChangedEventArgs>(notifications[4].NewValue).NewOutputPath);
            Assert.Equal("C:\\Videos\\replay.mkv", Assert.IsType<ReplayBufferSavedEventArgs>(notifications[5].NewValue).SavedReplayPath);
        }

        [Fact]
        public void TheNotifications_KeepTheOrderInWhichTheEventsArrived_AmongTheChangesOfTheModel()
        {
            var h = Started();

            h.Event("CustomEvent", new { n = 1 }, GeneralEvents);
            h.Event("StudioModeStateChanged", new { studioModeEnabled = true }, 1024);
            h.Event("CustomEvent", new { n = 2 }, GeneralEvents);
            h.Event("ScreenshotSaved", new { savedScreenshotPath = "C:\\a.png" }, GeneralEvents);

            var changes = h.Engine.TakeChanges();

            Assert.Equal(
                new[] { ModelChangeKind.Notification, ModelChangeKind.StudioModeChanged, ModelChangeKind.Notification, ModelChangeKind.Notification },
                changes.Select(change => change.Kind).ToArray());
            Assert.Equal(
                new[] { 1, 2 },
                changes.Where(change => change.Kind == ModelChangeKind.Notification && change.OldValue is EventType.CustomEvent)
                    .Select(change => ((CustomEventEventArgs)change.NewValue!).EventData!.Value.GetProperty("n").GetInt32()).ToArray());
        }

        [Fact]
        public void TheNotifications_ComeWhileOBSStudioChangesItsSceneCollection_ButTheEventsOfTheCollectionDoNot()
        {
            var h = Started();
            h.Event("CurrentSceneCollectionChanging", new { sceneCollectionName = "Untitled" }, 2);
            Assert.True(h.Engine.IsSwitching);
            h.Engine.TakeChanges();

            // What is about the collection that OBS Studio is leaving does not count; what is not about a collection does.
            h.Event("SceneCreated", new { sceneName = "New", sceneUuid = Guid.NewGuid(), isGroup = false }, 4);
            h.Event("CustomEvent", new { n = 1 }, GeneralEvents);
            h.Event("VendorEvent", new { vendorName = "a-plugin", eventType = "Something", eventData = new { } }, VendorEvents);
            h.Event("ScreenshotSaved", new { savedScreenshotPath = "C:\\a.png" }, GeneralEvents);
            h.Event("ExitStarted", new { }, GeneralEvents);
            h.Event("RecordFileChanged", new { newOutputPath = "C:\\Videos\\b.mkv" }, OutputEvents);
            h.Event("ReplayBufferSaved", new { savedReplayPath = "C:\\Videos\\replay.mkv" }, OutputEvents);

            var changes = h.Engine.TakeChanges();

            Assert.DoesNotContain(changes, change => change.Kind is ModelChangeKind.SceneAdded);
            Assert.Equal(
                new[] { EventType.CustomEvent, EventType.VendorEvent, EventType.ScreenshotSaved, EventType.ExitStarted, EventType.RecordFileChanged, EventType.ReplayBufferSaved },
                changes.Where(change => change.Kind == ModelChangeKind.Notification).Select(change => (EventType)change.OldValue!).ToArray());
            Assert.True(h.Engine.IsSwitching);
            Assert.True(h.Engine.Exiting);
            Assert.Equal("C:\\Videos\\replay.mkv", FileOf(h, ObsOutputKind.ReplayBuffer));
        }

        [Fact]
        public void TheFileOfTheRecording_IsTheOneThatItSaidWhenItStarted_WhenItWentOnInANewFile_AndWhenItStopped()
        {
            var h = Started();
            Assert.Null(h.Engine.Output(ObsOutputKind.Record).LastFile.Value);

            // Only the events that say a path make the file: the others (the start, the stop) change nothing about it.
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STARTING", outputPath = (string?)null }, OutputEvents);
            Assert.Null(h.Engine.Output(ObsOutputKind.Record).LastFile.Value);

            h.Event("RecordStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED", outputPath = "C:/Videos/a.mkv" }, OutputEvents);
            Assert.Equal("C:/Videos/a.mkv", FileOf(h, ObsOutputKind.Record));

            h.Event("RecordFileChanged", new { newOutputPath = "C:/Videos/a (2).mkv" }, OutputEvents);
            Assert.Equal("C:/Videos/a (2).mkv", FileOf(h, ObsOutputKind.Record));

            // A recording that was split says the file that it started with when it stops (seen with OBS Studio 33.0.0): the file that it wrote last is the one that the split announced.
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPING", outputPath = (string?)null }, OutputEvents);
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = "C:/Videos/a.mkv" }, OutputEvents);
            Assert.Equal("C:/Videos/a (2).mkv", FileOf(h, ObsOutputKind.Record));

            // The next recording does not make the file of the last one forget: it is the last that OBS Studio announced, until it announces another. A stop that says no path does not either
            // (obs-websocket before 5.5.0 says no path when the recording starts).
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STARTING", outputPath = (string?)null }, OutputEvents);
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = (string?)null }, OutputEvents);
            Assert.Equal("C:/Videos/a (2).mkv", FileOf(h, ObsOutputKind.Record));

            // A recording that was not split says the file that it wrote when it stops, which is also the one that it said when it started.
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = "C:/Videos/b.mkv" }, OutputEvents);
            Assert.Equal("C:/Videos/b.mkv", FileOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void ARecordingThatWasSplitBeforeTheModelConnected_ShowsTheFileThatItStartedWith_WhenItStops_BecauseNothingTellsMore()
        {
            var h = Started();

            // The model never saw the recording start, nor the split: all that it is told is the stop.
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = "C:/Videos/first.mkv" }, OutputEvents);

            Assert.Equal("C:/Videos/first.mkv", FileOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void TheStopOfARecording_ThatBeganWhileThereWasNoConnection_IsNotTakenForTheStopOfTheOneThatWasSplit()
        {
            var h = Started();
            h.Event("RecordStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED", outputPath = "C:/Videos/a.mkv" }, OutputEvents);
            h.Event("RecordFileChanged", new { newOutputPath = "C:/Videos/a (2).mkv" }, OutputEvents);

            // The connection is lost, the recording ends and another one begins while there is none, and the model is connected again during it.
            h.End();
            h.Start();
            h.SettleWithTime();
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = "C:/Videos/b.mkv" }, OutputEvents);

            // It says a file of its own: that is the file, and not the file of the split that came before it.
            Assert.Equal("C:/Videos/b.mkv", FileOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void ARecordingThatWasSplit_KeepsTheFileOfTheSplit_WhenItStopsAfterTheConnectionWasMadeAgain()
        {
            var h = Started();
            h.Event("RecordStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED", outputPath = "C:/Videos/a.mkv" }, OutputEvents);
            h.Event("RecordFileChanged", new { newOutputPath = "C:/Videos/a (2).mkv" }, OutputEvents);

            h.End();
            h.Start();
            h.SettleWithTime();
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = "C:/Videos/a.mkv" }, OutputEvents);

            // The recording that goes on says the file that it started with, which the model knows from before the connection was lost.
            Assert.Equal("C:/Videos/a (2).mkv", FileOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void ASplit_ThatIsSeenAfterTheConnectionWasMadeAgain_IsOfARecordingWhoseStartTheModelDoesNotKnow()
        {
            var h = Started();
            h.Event("RecordStateChanged", new { outputActive = true, outputState = "OBS_WEBSOCKET_OUTPUT_STARTED", outputPath = "C:/Videos/a.mkv" }, OutputEvents);

            // The recording that goes on may be another one: the file that the model saw it start with does not count for it.
            h.End();
            h.Start();
            h.SettleWithTime();
            h.Event("RecordFileChanged", new { newOutputPath = "C:/Videos/b (2).mkv" }, OutputEvents);
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = "C:/Videos/b.mkv" }, OutputEvents);

            Assert.Equal("C:/Videos/b (2).mkv", FileOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void TheStopOfARecordingThatWasSplit_EndsTheSplit_SoThatTheStopOfTheNextRecordingIsTaken()
        {
            var h = Started();

            // The model did not see the recording start: it saw the split and the stop.
            h.Event("RecordFileChanged", new { newOutputPath = "C:/Videos/a (2).mkv" }, OutputEvents);
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = "C:/Videos/a.mkv" }, OutputEvents);
            Assert.Equal("C:/Videos/a (2).mkv", FileOf(h, ObsOutputKind.Record));

            // Another recording that the model did not see start stops: nothing of the first one holds for it.
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = "C:/Videos/b.mkv" }, OutputEvents);

            Assert.Equal("C:/Videos/b.mkv", FileOf(h, ObsOutputKind.Record));
        }

        [Fact]
        public void TheFileOfTheReplayBuffer_IsTheReplayThatWasSavedLast()
        {
            var h = Started();

            h.Event("ReplayBufferSaved", new { savedReplayPath = "C:\\Videos\\one.mkv" }, OutputEvents);
            h.Event("ReplayBufferSaved", new { savedReplayPath = "C:\\Videos\\two.mkv" }, OutputEvents);

            Assert.Equal("C:\\Videos\\two.mkv", FileOf(h, ObsOutputKind.ReplayBuffer));
            Assert.Null(h.Engine.Output(ObsOutputKind.Record).LastFile.Value);
        }

        [Fact]
        public void TheFiles_AreKeptWhenTheConnectionEnds_AndWhenANewOneIsMade()
        {
            var h = Started();
            h.Event("RecordStateChanged", new { outputActive = false, outputState = "OBS_WEBSOCKET_OUTPUT_STOPPED", outputPath = "C:\\Videos\\a.mkv" }, OutputEvents);
            h.Event("ReplayBufferSaved", new { savedReplayPath = "C:\\Videos\\replay.mkv" }, OutputEvents);

            h.End();
            h.Start();
            h.SettleWithTime();

            Assert.Equal("C:\\Videos\\a.mkv", FileOf(h, ObsOutputKind.Record));
            Assert.Equal("C:\\Videos\\replay.mkv", FileOf(h, ObsOutputKind.ReplayBuffer));
        }

        [Fact]
        public void ExitStarted_MakesTheModelExiting_UntilAConnectionIsMadeToAnOBSStudioThatWasStartedAgain()
        {
            var h = Started();
            Assert.False(h.Engine.Exiting);

            h.Event("ExitStarted", new { }, GeneralEvents);
            Assert.True(h.Engine.Exiting);

            // The connection ends because OBS Studio is closing: the model still says so.
            h.End();
            Assert.True(h.Engine.Exiting);

            h.Start();
            Assert.False(h.Engine.Exiting);
            h.SettleWithTime();
            Assert.False(h.Engine.Exiting);
        }

        [Fact]
        public void ARecordedFileAndAReplay_AreNotStatesOfTheOutputs_SoTheyDoNotMakeTheOutputsBeRead()
        {
            var h = Started();

            h.Event("RecordFileChanged", new { newOutputPath = "C:\\Videos\\b.mkv" }, OutputEvents);
            h.Event("ReplayBufferSaved", new { savedReplayPath = "C:\\Videos\\replay.mkv" }, OutputEvents);

            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind == ModelChangeKind.OutputChanged);
            Assert.Null(h.Take());
        }
    }
}
