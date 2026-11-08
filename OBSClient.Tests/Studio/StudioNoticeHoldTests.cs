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
            var hold = new StudioNoticeHold(100);
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
            var hold = new StudioNoticeHold(100);

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
            var hold = new StudioNoticeHold(100);

            hold.Hold([Property(first, "X"), Property(second, "X")]);

            Assert.Equal(2, hold.Count);
        }

        [Fact]
        public void Release_HandsOverOnlyTheStudio_ForTheFirstTime_AndForgetsTheRest()
        {
            using var studio = new ObsStudio();
            var scene = new object();
            var hold = new StudioNoticeHold(100);
            hold.Hold([Property(scene, "Name"), Property(studio, "Scenes"), Property(studio, "Inputs")]);

            var released = Released(hold, onlyStudio: true);

            Assert.Equal(new (object?, string?)[] { (studio, "Scenes"), (studio, "Inputs") }, released.Select(r => (r.Entity, r.Property)).ToArray());
            Assert.Equal(0, hold.Count);
            Assert.Empty(Released(hold, onlyStudio: false));
        }

        [Fact]
        public void Release_HandsOverTheOutputsToo_ForTheFirstTime()
        {
            using var studio = new ObsStudio();
            var scene = new object();
            var hold = new StudioNoticeHold(100);
            hold.Hold([Property(scene, "Name"), Property(studio.Outputs.Record, "Status"), Property(studio, "Scenes")]);

            // The outputs are the same objects before and after the first synchronization, like the studio: what they held back is told then.
            var released = Released(hold, onlyStudio: true);

            Assert.Equal(new (object?, string?)[] { (studio.Outputs.Record, "Status"), (studio, "Scenes") }, released.Select(r => (r.Entity, r.Property)).ToArray());
            Assert.Equal(0, hold.Count);
        }

        [Fact]
        public void Release_Forgets_SoThatTheSamePropertyIsHeldAgainAfterwards()
        {
            var a = new object();
            var hold = new StudioNoticeHold(100);
            hold.Hold([Property(a, "X")]);
            Assert.Single(Released(hold, onlyStudio: false));

            hold.Hold([Property(a, "X")]);

            Assert.Equal(1, hold.Count);
            Assert.Single(Released(hold, onlyStudio: false));
        }

        [Fact]
        public void WhenMoreChangeThanMayBeHeld_NothingIsKept_AndTheReleaseSaysSo()
        {
            var objects = Enumerable.Range(0, 10).Select(_ => new object()).ToArray();
            var hold = new StudioNoticeHold(4);

            // Four properties fit. The fifth does not, and what was held is let go of with the objects that it referred to.
            hold.Hold(objects.Take(3).Select(o => Property(o, "X")).ToArray());
            Assert.Equal(3, hold.Count);
            Assert.False(hold.Overflowed);
            hold.Hold([Property(objects[3], "X"), Property(objects[4], "X")]);

            Assert.True(hold.Overflowed);
            Assert.Equal(0, hold.Count);

            // Nothing is kept from then on, however many more come, so the memory does not grow while the model cannot be read.
            hold.Hold(objects.Select(o => Property(o, "Y")).ToArray());
            Assert.Equal(0, hold.Count);

            var released = new List<StudioNotice>();
            Assert.True(hold.Release(released.Add, onlyStudio: false));
            Assert.Empty(released);
        }

        [Fact]
        public void AfterAnOverflowIsReleased_NotificationsAreHeldAgain()
        {
            var a = new object();
            var hold = new StudioNoticeHold(1);
            hold.Hold([Property(a, "X"), Property(a, "Y")]);
            Assert.True(hold.Release(_ => { }, onlyStudio: false));
            Assert.False(hold.Overflowed);

            hold.Hold([Property(a, "X")]);

            Assert.Equal(1, hold.Count);
            var released = new List<StudioNotice>();
            Assert.False(hold.Release(released.Add, onlyStudio: false));
            Assert.Single(released);
        }

        [Fact]
        public void TheSameProperty_AgainAndAgain_DoesNotFillTheHold()
        {
            var a = new object();
            var hold = new StudioNoticeHold(2);

            for (var i = 0; i < 100; i++)
            {
                hold.Hold([Property(a, "X")]);
            }

            Assert.False(hold.Overflowed);
            Assert.Equal(1, hold.Count);
        }

        [Fact]
        public void Clear_LetsGoOfWhatIsHeld_AndOfAnOverflow()
        {
            var a = new object();
            var hold = new StudioNoticeHold(1);
            hold.Hold([Property(a, "X"), Property(a, "Y")]);
            Assert.True(hold.Overflowed);

            hold.Clear();

            Assert.False(hold.Overflowed);
            Assert.Equal(0, hold.Count);
        }

        [Fact]
        public void TheCapacity_MustBeAtLeastOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StudioNoticeHold(0));
        }
    }
}
