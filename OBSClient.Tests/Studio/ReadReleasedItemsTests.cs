namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio.Engine;

    /// <summary>
    /// <see cref="ReadReleasedItems"/>: the scene items that a read found gone, and that the events have not told about yet.
    /// </summary>
    public class ReadReleasedItemsTests
    {
        private static readonly TimeSpan Zero = TimeSpan.Zero;

        [Fact]
        public void ASourceIsHeld_WhileASceneItemThatUsedItIs()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var source = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), source, Zero);
            held.Add(new ItemKey(scene, 2), source, Zero);

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

            held.Add(key, first, Zero);
            held.Add(key, second, Zero);

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
            held.Add(new ItemKey(scene, 1), source, Zero);
            held.Add(new ItemKey(scene, 2), source, Zero);
            held.Add(new ItemKey(other, 1), source, Zero);

            held.RemoveScene(scene);

            Assert.Equal(1, held.Count);
            Assert.True(held.Holds(source));
            held.RemoveScene(other);
            Assert.False(held.Holds(source));
        }

        [Fact]
        public void TheSceneItemsThatUsedASource_AreLetGoOfWhenItIsDestroyed()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var source = Guid.NewGuid();
            var other = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), source, Zero);
            held.Add(new ItemKey(scene, 2), other, Zero);
            held.Add(new ItemKey(Guid.NewGuid(), 1), source, Zero);

            held.RemoveSource(source);

            Assert.False(held.Holds(source));
            Assert.True(held.Holds(other));
            Assert.Equal(1, held.Count);
            held.RemoveSource(source);
            Assert.Equal(1, held.Count);
        }

        [Fact]
        public void Clear_LetsGoOfEverything()
        {
            var held = new ReadReleasedItems();
            var source = Guid.NewGuid();
            held.Add(new ItemKey(Guid.NewGuid(), 1), source, Zero);

            held.Clear();

            Assert.Equal(0, held.Count);
            Assert.False(held.Holds(source));
        }

        [Fact]
        public void Prune_LetsGoOfTheSceneItemsThatWereHeldBeforeATime_AndOfTheirSources()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var oldSource = Guid.NewGuid();
            var newSource = Guid.NewGuid();
            var sharedSource = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), oldSource, TimeSpan.FromSeconds(1));
            held.Add(new ItemKey(scene, 2), sharedSource, TimeSpan.FromSeconds(2));
            held.Add(new ItemKey(scene, 3), sharedSource, TimeSpan.FromSeconds(10));
            held.Add(new ItemKey(scene, 4), newSource, TimeSpan.FromSeconds(10));

            // What was held at the time itself stays.
            var pruned = held.Prune(TimeSpan.FromSeconds(10));

            Assert.Equal(2, pruned);
            Assert.Equal(2, held.Count);
            Assert.False(held.Holds(oldSource));
            Assert.True(held.Holds(sharedSource));
            Assert.True(held.Holds(newSource));
            Assert.Equal(0, held.Prune(TimeSpan.FromSeconds(10)));
        }
    }
}
