namespace OBSStudioClient.Tests.Studio
{
    using System.Reflection;
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// <see cref="StudioEngine.CheckConsistency"/>: the check that the random walks make of what the model holds, apart from comparing it with OBS Studio. It
    /// has to be quiet for a model that is right and to speak for one that holds what it should not, so the model is damaged on purpose.
    /// </summary>
    public class StudioEngineConsistencyTests
    {
        private static EngineHarness Started(out Guid doomed, out Guid mic)
        {
            var harness = new EngineHarness();
            harness.World.AddScene("Scene");
            var scene = harness.World.AddScene("Doomed");
            var input = harness.World.AddInput("Mic", "wasapi_input_capture", 2);
            harness.World.AddItem(scene, input, "Mic");
            harness.Start();
            harness.SettleWithTime();
            doomed = scene;
            mic = input;
            return harness;
        }

        private static T Field<T>(StudioEngine engine, string name)
        {
            return (T)typeof(StudioEngine).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        }

        [Fact]
        public void AModelThatIsRight_IsConsistent()
        {
            var h = Started(out var doomed, out var mic);
            Assert.Empty(h.Engine.CheckConsistency());

            // Also after sources were removed and destroyed: the scene goes, and the input that only it showed is destroyed with it.
            h.World.Scenes.RemoveAll(scene => scene.Uuid == doomed);
            h.World.Items.Remove(doomed);
            h.World.Inputs.RemoveAll(input => input.Uuid == mic);
            EngineEvents.SceneRemoved(h, doomed, "Doomed");
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();

            Assert.DoesNotContain(h.Engine.Snapshot().Inputs, input => input.Uuid == mic);
            Assert.Empty(h.Engine.CheckConsistency());
        }

        [Fact]
        public void AModelThatKeepsAnInputAlive_IsConsistent()
        {
            var h = Started(out _, out var mic);
            EngineEvents.InputRemoved(h, mic, "Mic");
            h.SettleWithTime();
            Assert.Equal(SourceState.Detached, h.Engine.Snapshot().Inputs.Single(input => input.Uuid == mic).State);

            Assert.Empty(h.Engine.CheckConsistency());
        }

        [Fact]
        public void ReferencesThatNoSceneItemHas_AreReported()
        {
            var h = Started(out _, out _);
            Field<Dictionary<Guid, HashSet<ItemKey>>>(h.Engine, "_refs")[Guid.NewGuid()] = [new ItemKey(Guid.NewGuid(), 1)];

            var problem = Assert.Single(h.Engine.CheckConsistency());

            Assert.Contains("1 references and 0 scene items", problem);
        }

        [Fact]
        public void ASceneItemThatIsNotAmongTheReferencesOfItsSource_IsReported()
        {
            var h = Started(out _, out var mic);
            Field<Dictionary<Guid, HashSet<ItemKey>>>(h.Engine, "_refs").Remove(mic);

            var problems = h.Engine.CheckConsistency();

            Assert.Contains(problems, problem => problem.Contains("is not among the references of its source"));
        }

        [Fact]
        public void ADestroyedInputThatIsStillHeld_IsReported()
        {
            var h = Started(out _, out var mic);
            Field<Dictionary<Guid, InputNode>>(h.Engine, "_inputs")[mic].State = SourceState.Gone;

            var problem = Assert.Single(h.Engine.CheckConsistency());

            Assert.Contains("was destroyed, and is still held", problem);
        }

        [Fact]
        public void ADestroyedSceneThatIsStillHeld_IsReported()
        {
            var h = Started(out var doomed, out _);
            Field<Dictionary<Guid, SceneNode>>(h.Engine, "_scenes")[doomed].State = SourceState.Gone;

            var problems = h.Engine.CheckConsistency();

            Assert.Contains(problems, problem => problem.Contains("was destroyed, and is still held"));
        }

        [Fact]
        public void RemovedSourcesThatWaitForADecision_KeepTheEngineFromBeingSettled()
        {
            var h = Started(out _, out _);
            Assert.True(h.Engine.IsSettled);

            Field<HashSet<Guid>>(h.Engine, "_unresolved").Add(Guid.NewGuid());

            Assert.False(h.Engine.IsSettled);
        }
    }
}
