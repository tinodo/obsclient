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
    }
}
