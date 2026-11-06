namespace OBSStudioClient.Tests.Simulation
{
    /// <summary>
    /// <see cref="SimReplay"/>: the state that is rebuilt from the events starts from the snapshot that it is given, items and inputs included.
    /// </summary>
    public class SimReplayTests
    {
        private static SimHarness Populated()
        {
            var h = new SimHarness(SimQuirks.Ideal);
            var scene = h.Sim.External.CreateScene("A");
            var (mic, item) = h.Sim.External.CreateInput(scene, "Mic", "wasapi_input_capture");
            h.Sim.External.CreateInput(scene, "Logo", "image_source");
            h.Sim.External.SetItemEnabled(scene, item, false);
            h.Sim.External.SetItemLocked(scene, item, true);
            h.Sim.External.SetMute(mic, true);
            h.Sim.External.SetVolumeDb(mic, -6);
            h.Sim.External.CreateGlobalInput("Desktop", "wasapi_output_capture");
            h.Settle();
            return h;
        }

        [Fact]
        public void APopulatedSnapshotThatNothingHappenedTo_IsTheSameAsItself()
        {
            var h = Populated();
            var snapshot = h.Sim.Snapshot();

            Assert.Empty(SimReplay.Compare(snapshot, [], snapshot));
        }

        [Fact]
        public void TheEventsAfterAPopulatedSnapshot_AreFollowedFromWhatItHolds()
        {
            var h = Populated();
            var initial = h.Sim.Snapshot();
            h.ClearLog();
            var scene = initial.Scenes.Single(candidate => candidate.Name == "A");

            // The scene items and the inputs of the snapshot are what the events refer to: an item is removed, and with it its input.
            h.Sim.External.SetItemIndex(scene.Uuid, scene.Items[0].Id, 1);
            h.Sim.External.RemoveItem(scene.Uuid, scene.Items[1].Id);
            h.Sim.External.SetMute(initial.Inputs.Single(input => input.Name == "Desktop").Uuid, true);
            h.Settle();

            Assert.Empty(SimReplay.Compare(initial, h.Log.Select(entry => entry.Message), h.Sim.Snapshot()));
        }

        // The comparison has to be able to fail, or the tests above prove nothing: each of these takes away one thing that the events need, or adds one that they
        // do not tell, and the comparison has to say which part of the state differs.
        [Fact]
        public void AnEventThatIsLeftOut_IsFound()
        {
            var h = Populated();
            var initial = h.Sim.Snapshot();
            h.ClearLog();
            var scene = initial.Scenes.Single(candidate => candidate.Name == "A");
            h.Sim.External.SetItemEnabled(scene.Uuid, scene.Items[0].Id, true);
            h.Sim.External.SetMute(initial.Inputs.Single(input => input.Name == "Desktop").Uuid, true);
            h.Sim.External.SwitchProgramScene(h.Sim.External.CreateScene("B"));
            h.Settle();
            var final = h.Sim.Snapshot();
            var all = h.Log.Select(entry => entry.Message).ToArray();

            Assert.Empty(SimReplay.Compare(initial, all, final));
            Assert.Contains(SimReplay.Compare(initial, Without(all, "InputMuteStateChanged"), final), problem => problem.StartsWith("The inputs that the events leave", StringComparison.Ordinal));
            Assert.Contains(SimReplay.Compare(initial, Without(all, "SceneItemEnableStateChanged"), final), problem => problem.StartsWith("The items of \"A\" that the events leave", StringComparison.Ordinal));
            Assert.Contains(SimReplay.Compare(initial, Without(all, "SceneCreated"), final), problem => problem.Contains("scene", StringComparison.Ordinal));
            Assert.Contains(SimReplay.Compare(initial, Without(all, "CurrentProgramSceneChanged"), final), problem => problem.StartsWith("The events say that the program scene is", StringComparison.Ordinal));
        }

        [Fact]
        public void AChangeThatTheEventsDoNotTellOf_IsFound()
        {
            var h = Populated();
            var initial = h.Sim.Snapshot();
            h.ClearLog();
            h.Sim.External.CreateScene("B");
            h.Settle();
            var events = h.Log.Select(entry => entry.Message).ToArray();

            h.Sim.External.RenameScene(initial.Scenes.Single(candidate => candidate.Name == "A").Uuid, "Renamed");

            Assert.Contains(SimReplay.Compare(initial, events, h.Sim.Snapshot()), problem => problem.StartsWith("The scenes that the events leave", StringComparison.Ordinal));
        }

        [Fact]
        public void TheStateOfARemovedInputThatObsStudioKeepsAlive_IsFollowedToo()
        {
            var h = new SimHarness(SimQuirks.Ideal);
            var scene = h.Sim.External.CreateScene("A");
            var (mic, _) = h.Sim.External.CreateInput(scene, "Mic", "wasapi_input_capture");
            h.Settle();
            var initial = h.Sim.Snapshot();
            h.ClearLog();

            // The scene item keeps the input alive, and OBS Studio goes on announcing what happens to it.
            h.Sim.External.RemoveInput(mic);
            h.Sim.External.SetMute(mic, true);
            h.Sim.External.SetVolumeDb(mic, -12);
            h.Settle();
            var final = h.Sim.Snapshot();
            var all = h.Log.Select(entry => entry.Message).ToArray();

            Assert.True(final.Inputs.Single(input => input.Uuid == mic).Removed, "The input has to be alive and removed for this test to mean anything.");
            Assert.Empty(SimReplay.Compare(initial, all, final));
            Assert.Contains(SimReplay.Compare(initial, Without(all, "InputVolumeChanged"), final), problem => problem.StartsWith("The removed inputs that OBS Studio still holds", StringComparison.Ordinal));
            Assert.Contains(SimReplay.Compare(initial, Without(all, "InputMuteStateChanged"), final), problem => problem.StartsWith("The removed inputs that OBS Studio still holds", StringComparison.Ordinal));
            Assert.Contains(SimReplay.Compare(initial, Without(all, "InputRemoved"), final), problem => problem.Contains("input", StringComparison.Ordinal));
        }

        private static SimMessage[] Without(SimMessage[] messages, string eventType)
        {
            return messages.Where(message => !message.Json.Contains($"\"{eventType}\"", StringComparison.Ordinal)).ToArray();
        }
    }
}
