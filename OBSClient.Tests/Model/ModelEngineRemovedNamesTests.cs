namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// The names of sources that OBS removed and keeps alive for the scene items that show them. A scene of that kind is in no list of scenes, so the model has
    /// only the removal and the scene items to learn its name from; and a rename that the model missed (the connection ended in between) is not sent again.
    /// Found by the walks of the simulation (seeds 9685 and 8502 of a long campaign).
    /// </summary>
    public class ModelEngineRemovedNamesTests
    {
        // The scene "Parent" shows the scene "Child" and the input "Mic".
        private static EngineHarness Started(out Guid child, out Guid mic)
        {
            var harness = new EngineHarness();
            harness.World.AddScene("Scene");
            child = harness.World.AddScene("Child");
            var parent = harness.World.AddScene("Parent");
            harness.World.AddItem(parent, child, "Child", isScene: true);
            mic = harness.World.AddInput("Mic", "wasapi_input_capture", 2);
            harness.World.AddItem(parent, mic, "Mic");
            harness.Start();
            harness.SettleWithTime();
            harness.Engine.TakeChanges();
            return harness;
        }

        [Fact]
        public void TheEventThatRemovesAScene_GivesTheNameThatTheSceneHadThen()
        {
            var h = Started(out var child, out _);

            // OBS renamed the scene and the model was not told, and then OBS removed it, and keeps it alive for the scene item of "Parent".
            h.World.Rename(child, "Renamed");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == child);
            EngineEvents.SceneRemoved(h, child, "Renamed");

            Assert.Equal("Renamed", Assert.Single(h.Engine.Snapshot().DetachedScenes).Name);
            var changes = h.Engine.TakeChanges().Where(change => change.Kind is ModelChangeKind.SceneRenamed or ModelChangeKind.SourceDetached).ToArray();
            Assert.Equal(new[] { ModelChangeKind.SceneRenamed, ModelChangeKind.SourceDetached }, changes.Select(change => change.Kind).ToArray());
            Assert.Equal(("Child", "Renamed"), (changes[0].OldValue, changes[0].NewValue));

            h.SettleWithTime();
            Assert.Equal("Renamed", Assert.Single(h.Engine.Snapshot().DetachedScenes).Name);
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void TheEventThatRemovesAScene_ChangesNothing_WhenTheModelHasTheNameAlready()
        {
            var h = Started(out var child, out _);
            h.World.Scenes.RemoveAll(scene => scene.Uuid == child);

            EngineEvents.SceneRemoved(h, child, "Child");

            Assert.Equal("Child", Assert.Single(h.Engine.Snapshot().DetachedScenes).Name);
            Assert.DoesNotContain(h.Engine.TakeChanges(), change => change.Kind == ModelChangeKind.SceneRenamed);
        }

        [Fact]
        public void TheEventThatRemovesAnInput_GivesTheNameThatTheInputHadThen()
        {
            var h = Started(out _, out var mic);

            // OBS keeps the input alive for the scene item of "Parent", and its list of inputs still has it.
            h.World.Rename(mic, "Microphone");
            EngineEvents.InputRemoved(h, mic, "Microphone");

            var input = Assert.Single(h.Engine.Snapshot().Inputs);
            Assert.Equal(("Microphone", SourceState.Detached), (input.Name, input.State));
            var changes = h.Engine.TakeChanges().Where(change => change.Kind is ModelChangeKind.InputRenamed or ModelChangeKind.SourceDetached).ToArray();
            Assert.Equal(new[] { ModelChangeKind.InputRenamed, ModelChangeKind.SourceDetached }, changes.Select(change => change.Kind).ToArray());
            Assert.Equal(("Mic", "Microphone"), (changes[0].OldValue, changes[0].NewValue));
        }

        [Fact]
        public void AnEventWithoutAName_LeavesTheNameAsItIs()
        {
            var h = Started(out var child, out var mic);

            EngineEvents.SceneRemoved(h, child, string.Empty);
            EngineEvents.InputRemoved(h, mic, string.Empty);

            Assert.Equal("Child", Assert.Single(h.Engine.Snapshot().DetachedScenes).Name);
            Assert.Equal("Mic", Assert.Single(h.Engine.Snapshot().Inputs).Name);
        }

        [Fact]
        public void ASceneThatWasRenamedAndRemovedWhileTheConnectionWasDown_HasTheNameThatTheSceneItemsGive()
        {
            var h = Started(out var child, out _);

            // No event of either reaches the model: the next connection finds the scene gone from the list, and shown by the scene item of "Parent".
            h.End();
            h.World.Rename(child, "Renamed");
            h.World.Scenes.RemoveAll(scene => scene.Uuid == child);
            h.Start();
            h.SettleWithTime();

            var detached = Assert.Single(h.Engine.Snapshot().DetachedScenes);
            Assert.Equal(("Renamed", SourceState.Detached), (detached.Name, detached.State));
            Assert.True(h.Engine.IsSettled);
        }

        [Fact]
        public void ASceneItemsReadThatWasSentBeforeARenameArrived_DoesNotUndoTheRename()
        {
            var h = Started(out var child, out _);
            var fresh = h.World.AddScene("Fresh");
            h.World.AddItem(fresh, child, "Child", isScene: true);
            EngineEvents.SceneCreated(h, fresh, "Fresh");
            var read = h.Take()!;
            Assert.Contains(read.Requests, request => request.Kind == ReadKind.Items && request.Target == fresh);

            // The rename arrives after the read was sent, so it is newer than the name that the read says (the world still has the old one).
            h.Event("SceneNameChanged", new { sceneUuid = child, oldSceneName = "Child", sceneName = "Renamed" }, 4);
            h.Complete(read);

            Assert.Equal("Renamed", h.Engine.Snapshot().Scenes.Single(scene => scene.Uuid == child).Name);
        }
    }
}
