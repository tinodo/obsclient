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

        [Fact]
        public void ASourceWhoseLastSceneItemWasLetGoOfByAnEvent_IsSettling_UntilTheTimeThatTheHoldWouldHaveLastedIsUp()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var source = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), source, TimeSpan.FromSeconds(1));
            held.Add(new ItemKey(scene, 2), source, TimeSpan.FromSeconds(2));

            // While another scene item is held the source is held, not settling.
            Assert.True(held.Remove(new ItemKey(scene, 1)));
            Assert.True(held.Holds(source));
            Assert.False(held.Settling(source));

            // The event of the last one: nothing is held for the source, and it settles, counted from the read that took the scene item away.
            Assert.True(held.Remove(new ItemKey(scene, 2)));
            Assert.False(held.Holds(source));
            Assert.True(held.Settling(source));
            Assert.Equal(1, held.SettlingCount);
            Assert.Equal(0, held.Count);

            // The time at which it was held stays settling, and the time ends it: the source is told to the caller, who judges it.
            var freed = new List<Guid>();
            Assert.Equal(0, held.Prune(TimeSpan.FromSeconds(2), freed));
            Assert.True(held.Settling(source));
            Assert.Empty(freed);
            Assert.Equal(1, held.Prune(TimeSpan.FromSeconds(3), freed));
            Assert.False(held.Settling(source));
            Assert.Equal(0, held.SettlingCount);
            Assert.Equal(new[] { source }, freed);
        }

        [Fact]
        public void ASceneItemThatIsNotHeld_LeavesNothingSettling()
        {
            var held = new ReadReleasedItems();

            Assert.False(held.Remove(new ItemKey(Guid.NewGuid(), 1)));
            Assert.Equal(0, held.SettlingCount);
        }

        [Fact]
        public void ASourceThatIsHeldAgainWhileItSettles_IsToldWhenTheNewHoldEnds_NotBefore()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var source = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), source, TimeSpan.FromSeconds(1));
            held.Remove(new ItemKey(scene, 1));
            held.Add(new ItemKey(scene, 2), source, TimeSpan.FromSeconds(5));

            // The time for the first is up while the second is held: the settling goes, and nothing is told.
            var freed = new List<Guid>();
            Assert.Equal(1, held.Prune(TimeSpan.FromSeconds(3), freed));
            Assert.False(held.Settling(source));
            Assert.True(held.Holds(source));
            Assert.Empty(freed);

            // The time for the second is up: that is when the source is told, once.
            Assert.Equal(1, held.Prune(TimeSpan.FromSeconds(6), freed));
            Assert.Equal(new[] { source }, freed);
            Assert.False(held.Holds(source));
            Assert.Equal(0, held.SettlingCount);
        }

        [Fact]
        public void ASourceWhoseTimeIsUp_IsToldOnce_WhetherItWasHeldOrSettling()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var source = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), source, TimeSpan.FromSeconds(1));
            held.Remove(new ItemKey(scene, 1));
            held.Add(new ItemKey(scene, 2), source, TimeSpan.FromSeconds(2));

            // The first scene item settles the source, the second holds it, and both are old: the source is told once.
            var freed = new List<Guid>();
            Assert.Equal(2, held.Prune(TimeSpan.FromSeconds(5), freed));

            Assert.Equal(new[] { source }, freed);
            Assert.False(held.Settling(source));
            Assert.False(held.Holds(source));
        }

        [Fact]
        public void TheSourcesOfTheSceneItemsOfAScene_SettleWhenAnEventRemovesTheScene()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var other = Guid.NewGuid();
            var gone = Guid.NewGuid();
            var shared = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), gone, TimeSpan.FromSeconds(1));
            held.Add(new ItemKey(scene, 2), shared, TimeSpan.FromSeconds(1));
            held.Add(new ItemKey(other, 1), shared, TimeSpan.FromSeconds(1));

            held.RemoveScene(scene);

            // The source that a scene item of another scene that is held still used is held, and settles when that one goes.
            Assert.True(held.Settling(gone));
            Assert.False(held.Settling(shared));
            Assert.True(held.Holds(shared));
        }

        [Fact]
        public void ASourceThatIsDestroyed_IsNeitherHeldNorSettling()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var held1 = Guid.NewGuid();
            var settling = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), held1, Zero);
            held.Add(new ItemKey(scene, 2), settling, Zero);
            held.Remove(new ItemKey(scene, 2));
            Assert.True(held.Settling(settling));

            held.RemoveSource(held1);
            held.RemoveSource(settling);

            Assert.False(held.Holds(held1));
            Assert.False(held.Settling(settling));
            Assert.Equal(0, held.Count);
            Assert.Equal(0, held.SettlingCount);
        }

        [Fact]
        public void Prune_LeavesNothingSettling_ThatDidNotSettleByAnEvent()
        {
            var held = new ReadReleasedItems();
            var source = Guid.NewGuid();
            held.Add(new ItemKey(Guid.NewGuid(), 1), source, Zero);

            var freed = new List<Guid>();
            held.Prune(TimeSpan.FromSeconds(1), freed);

            Assert.Equal(new[] { source }, freed);
            Assert.Equal(0, held.SettlingCount);
        }

        [Fact]
        public void Prune_SaysWhichSourcesNoSceneItemThatIsHeldUsesAnyMore()
        {
            var held = new ReadReleasedItems();
            var scene = Guid.NewGuid();
            var gone = Guid.NewGuid();
            var goneTwice = Guid.NewGuid();
            var shared = Guid.NewGuid();
            held.Add(new ItemKey(scene, 1), gone, TimeSpan.FromSeconds(1));
            held.Add(new ItemKey(scene, 2), goneTwice, TimeSpan.FromSeconds(1));
            held.Add(new ItemKey(scene, 3), goneTwice, TimeSpan.FromSeconds(2));
            held.Add(new ItemKey(scene, 4), shared, TimeSpan.FromSeconds(2));
            held.Add(new ItemKey(scene, 5), shared, TimeSpan.FromSeconds(10));

            // The list that the caller gives may have entries already: a source is in it once however many scene items went.
            var other = Guid.NewGuid();
            var freed = new List<Guid> { gone, other };
            held.Prune(TimeSpan.FromSeconds(5), freed);

            // The source that another scene item that is held still used is not freed, and a source is told once however many scene items went.
            Assert.Equal(new[] { gone, goneTwice, other }.OrderBy(uuid => uuid), freed.OrderBy(uuid => uuid));
            Assert.True(held.Holds(shared));
        }
    }
}
