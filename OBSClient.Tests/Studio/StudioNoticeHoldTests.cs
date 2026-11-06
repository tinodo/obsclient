namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio;

    /// <summary>
    /// <see cref="StudioNoticeHold"/>: what is kept of the notifications of the steps in which the model is read, and what is handed over.
    /// </summary>
    public class StudioNoticeHoldTests
    {
        private static StudioNotice Property(object entity, string property)
        {
            return new StudioNotice(null, entity, property);
        }

        private static (object? Entity, string? Property, ObsChangedEventArgs? Args)[] Released(StudioNoticeHold hold, bool onlyStudio)
        {
            var released = new List<StudioNotice>();
            hold.Release(released.Add, onlyStudio);
            return released.Select(n => ((object?)n.Entity, n.Property, n.Args)).ToArray();
        }

        [Fact]
        public void Hold_KeepsEachPropertyOfEachObjectOnce_InTheOrderOfTheFirstNotice()
        {
            var a = new object();
            var b = new object();
            var hold = new StudioNoticeHold();
            var record = new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, a, "X", 1, 2);

            hold.Hold([new StudioNotice(record, a, "X"), Property(b, "X"), Property(a, "Y")]);
            hold.Hold([Property(a, "X"), Property(b, "X"), Property(b, "Y")]);

            Assert.Equal(4, hold.Count);
            var released = Released(hold, onlyStudio: false);
            Assert.Equal(new (object?, string?)[] { (a, "X"), (b, "X"), (a, "Y"), (b, "Y") }, released.Select(r => (r.Entity, r.Property)).ToArray());

            // What is raised from the hold has no record for Changed.
            Assert.All(released, r => Assert.Null(r.Args));
        }

        [Fact]
        public void Hold_DropsTheNotificationsThatAreNotAboutAProperty()
        {
            var a = new object();
            var hold = new StudioNoticeHold();

            hold.Hold([new StudioNotice(new ObsChangedEventArgs(ObsChangeKind.Repaired, a), null, null), new StudioNotice(null, a, null), new StudioNotice(null, null, "X")]);

            Assert.Equal(0, hold.Count);
            Assert.Empty(Released(hold, onlyStudio: false));
        }

        [Fact]
        public void Hold_KeepsObjectsApart_ByReference_NotByEquality()
        {
            var first = new string('a', 3);
            var second = new string('a', 3);
            Assert.NotSame(first, second);
            Assert.Equal(first, second);
            var hold = new StudioNoticeHold();

            hold.Hold([Property(first, "X"), Property(second, "X")]);

            Assert.Equal(2, hold.Count);
        }

        [Fact]
        public void Release_HandsOverOnlyTheStudio_ForTheFirstTime_AndForgetsTheRest()
        {
            using var studio = new ObsStudio();
            var scene = new object();
            var hold = new StudioNoticeHold();
            hold.Hold([Property(scene, "Name"), Property(studio, "Scenes"), Property(studio, "Inputs")]);

            var released = Released(hold, onlyStudio: true);

            Assert.Equal(new (object?, string?)[] { (studio, "Scenes"), (studio, "Inputs") }, released.Select(r => (r.Entity, r.Property)).ToArray());
            Assert.Equal(0, hold.Count);
            Assert.Empty(Released(hold, onlyStudio: false));
        }

        [Fact]
        public void Release_Forgets_SoThatTheSamePropertyIsHeldAgainAfterwards()
        {
            var a = new object();
            var hold = new StudioNoticeHold();
            hold.Hold([Property(a, "X")]);
            Assert.Single(Released(hold, onlyStudio: false));

            hold.Hold([Property(a, "X")]);

            Assert.Equal(1, hold.Count);
            Assert.Single(Released(hold, onlyStudio: false));
        }
    }
}
