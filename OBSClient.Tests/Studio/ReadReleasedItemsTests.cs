namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// <see cref="ReadReleasedItems"/>: the scene items that a read found gone, and that the events have not told about yet.
    /// </summary>
    public class ReadReleasedItemsTests
    {
        [Fact]
        public void ASourceIsHeld_WhileASceneItemThatUsedItIs()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var source = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), source);
            held.Add(new ItemKey(scene, 2), source);

            Assert.True(held.Holds(source));
            Assert.True(held.Remove(new ItemKey(scene, 1)));
            Assert.True(held.Holds(source));
            Assert.True(held.Remove(new ItemKey(scene, 2)));
            Assert.False(held.Holds(source));
            Assert.Equal(0, held.Count);
        }

        [Fact]
        public void ASceneItemThatIsNotHeld_IsNotRemoved()
        {
            var held = new ReadReleasedItems();

            Assert.False(held.Remove(new ItemKey(Guid.NewGuid(), 1)));
            Assert.False(held.Holds(Guid.NewGuid()));
        }

        [Fact]
        public void ASceneItemThatIsHeldAgain_CountsOnce()
        {
            var held = new ReadReleasedItems();
            var key = new ItemKey(Guid.NewGuid(), 1);
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();

            held.Add(key, first);
            held.Add(key, second);

            Assert.Equal(1, held.Count);
            Assert.False(held.Holds(first));
            Assert.True(held.Holds(second));
        }

        [Fact]
        public void TheSceneItemsOfAScene_AreLetGoOfTogether()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var other = Guid.NewGuid();
            var source = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), source);
            held.Add(new ItemKey(scene, 2), source);
            held.Add(new ItemKey(other, 1), source);

            held.RemoveScene(scene);

            Assert.Equal(1, held.Count);
            Assert.True(held.Holds(source));
            held.RemoveScene(other);
            Assert.False(held.Holds(source));
        }

        [Fact]
        public void Clear_LetsGoOfEverything()
        {
            var held = new ReadReleasedItems();
            var source = Guid.NewGuid();
            held.Add(new ItemKey(Guid.NewGuid(), 1), source);

            held.Clear();

            Assert.Equal(0, held.Count);
            Assert.False(held.Holds(source));
        }
    }
}
