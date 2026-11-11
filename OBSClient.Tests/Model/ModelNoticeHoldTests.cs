namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model;

    /// <summary>
    /// <see cref="ModelNoticeHold"/>: what is kept of the notifications of the steps in which the model is read, and what is handed over.
    /// </summary>
    public class ModelNoticeHoldTests
    {
        private static ModelNotice Property(object entity, string property)
        {
            return new ModelNotice(null, entity, property);
        }

        private static (object? Entity, string? Property, ObsChangedEventArgs? Args)[] Released(ModelNoticeHold hold, bool onlyModel)
        {
            var released = new List<ModelNotice>();
            hold.Release(released.Add, onlyModel);
            return released.Select(n => ((object?)n.Entity, n.Property, n.Args)).ToArray();
        }

        [Fact]
        public void Hold_KeepsEachPropertyOfEachObjectOnce_InTheOrderOfTheFirstNotice()
        {
            var a = new object();
            var b = new object();
            var hold = new ModelNoticeHold(100);
            var record = new ObsChangedEventArgs(ObsChangeKind.PropertyChanged, a, "X", 1, 2);

            hold.Hold([new ModelNotice(record, a, "X"), Property(b, "X"), Property(a, "Y")]);
            hold.Hold([Property(a, "X"), Property(b, "X"), Property(b, "Y")]);

            Assert.Equal(4, hold.Count);
            var released = Released(hold, onlyModel: false);
            Assert.Equal(new (object?, string?)[] { (a, "X"), (b, "X"), (a, "Y"), (b, "Y") }, released.Select(r => (r.Entity, r.Property)).ToArray());

            // What is raised from the hold has no record for Changed.
            Assert.All(released, r => Assert.Null(r.Args));
        }

        [Fact]
        public void Hold_DropsTheNotificationsThatAreNotAboutAProperty()
        {
            var a = new object();
            var hold = new ModelNoticeHold(100);

            hold.Hold([new ModelNotice(new ObsChangedEventArgs(ObsChangeKind.Repaired, a), null, null), new ModelNotice(null, a, null), new ModelNotice(null, null, "X")]);

            Assert.Equal(0, hold.Count);
            Assert.Empty(Released(hold, onlyModel: false));
        }

        [Fact]
        public void Hold_KeepsObjectsApart_ByReference_NotByEquality()
        {
            var first = new string('a', 3);
            var second = new string('a', 3);
            Assert.NotSame(first, second);
            Assert.Equal(first, second);
            var hold = new ModelNoticeHold(100);

            hold.Hold([Property(first, "X"), Property(second, "X")]);

            Assert.Equal(2, hold.Count);
        }

        [Fact]
        public void Release_HandsOverOnlyTheModel_ForTheFirstTime_AndForgetsTheRest()
        {
            using var model = new ObsModel();
            var scene = new object();
            var hold = new ModelNoticeHold(100);
            hold.Hold([Property(scene, "Name"), Property(model, "Scenes"), Property(model, "Inputs")]);

            var released = Released(hold, onlyModel: true);

            Assert.Equal(new (object?, string?)[] { (model, "Scenes"), (model, "Inputs") }, released.Select(r => (r.Entity, r.Property)).ToArray());
            Assert.Equal(0, hold.Count);
            Assert.Empty(Released(hold, onlyModel: false));
        }

        [Fact]
        public void Release_HandsOverTheOutputsToo_ForTheFirstTime()
        {
            using var model = new ObsModel();
            var scene = new object();
            var hold = new ModelNoticeHold(100);
            hold.Hold([Property(scene, "Name"), Property(model.Outputs.Record, "Status"), Property(model, "Scenes")]);

            // The outputs are the same objects before and after the first synchronization, like the model: what they held back is told then.
            var released = Released(hold, onlyModel: true);

            Assert.Equal(new (object?, string?)[] { (model.Outputs.Record, "Status"), (model, "Scenes") }, released.Select(r => (r.Entity, r.Property)).ToArray());
            Assert.Equal(0, hold.Count);
        }

        [Fact]
        public void Release_HandsOverTheTransitions_TheProfilesAndTheSceneCollectionsToo_ForTheFirstTime()
        {
            using var model = new ObsModel();
            var scene = new object();
            var hold = new ModelNoticeHold(100);
            hold.Hold([Property(scene, "Name"), Property(model.Transitions, "Current"), Property(model.Profiles, "Names"), Property(model.Profiles, "Current"), Property(model.SceneCollections, "Current")]);

            // Like the outputs, they are the same objects before and after the first synchronization, and the application can hold them from the start.
            var released = Released(hold, onlyModel: true);

            Assert.Equal(
                new (object?, string?)[] { (model.Transitions, "Current"), (model.Profiles, "Names"), (model.Profiles, "Current"), (model.SceneCollections, "Current") },
                released.Select(r => (r.Entity, r.Property)).ToArray());
            Assert.Equal(0, hold.Count);
        }

        [Fact]
        public void Release_Forgets_SoThatTheSamePropertyIsHeldAgainAfterwards()
        {
            var a = new object();
            var hold = new ModelNoticeHold(100);
            hold.Hold([Property(a, "X")]);
            Assert.Single(Released(hold, onlyModel: false));

            hold.Hold([Property(a, "X")]);

            Assert.Equal(1, hold.Count);
            Assert.Single(Released(hold, onlyModel: false));
        }

        [Fact]
        public void WhenMoreChangeThanMayBeHeld_NothingIsKept_AndTheReleaseSaysSo()
        {
            var objects = Enumerable.Range(0, 10).Select(_ => new object()).ToArray();
            var hold = new ModelNoticeHold(4);

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

            var released = new List<ModelNotice>();
            Assert.True(hold.Release(released.Add, onlyModel: false));
            Assert.Empty(released);
        }

        [Fact]
        public void AfterAnOverflowIsReleased_NotificationsAreHeldAgain()
        {
            var a = new object();
            var hold = new ModelNoticeHold(1);
            hold.Hold([Property(a, "X"), Property(a, "Y")]);
            Assert.True(hold.Release(_ => { }, onlyModel: false));
            Assert.False(hold.Overflowed);

            hold.Hold([Property(a, "X")]);

            Assert.Equal(1, hold.Count);
            var released = new List<ModelNotice>();
            Assert.False(hold.Release(released.Add, onlyModel: false));
            Assert.Single(released);
        }

        [Fact]
        public void TheSameProperty_AgainAndAgain_DoesNotFillTheHold()
        {
            var a = new object();
            var hold = new ModelNoticeHold(2);

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
            var hold = new ModelNoticeHold(1);
            hold.Hold([Property(a, "X"), Property(a, "Y")]);
            Assert.True(hold.Overflowed);

            hold.Clear();

            Assert.False(hold.Overflowed);
            Assert.Equal(0, hold.Count);
        }

        [Fact]
        public void TheCapacity_MustBeAtLeastOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModelNoticeHold(0));
        }
    }
}
