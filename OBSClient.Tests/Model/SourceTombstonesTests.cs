namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// <see cref="SourceTombstones"/>: the sources that OBS destroyed, which are remembered until a list shows that OBS is done with them.
    /// </summary>
    public class SourceTombstonesTests
    {
        private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");

        private static TimeSpan Seconds(double seconds) => TimeSpan.FromSeconds(seconds);

        [Fact]
        public void ATombstone_IsThere_UntilItIsRemoved()
        {
            var tombstones = new SourceTombstones();

            tombstones.Add(A, SourceKind.Input, 5, Seconds(1));

            Assert.True(tombstones.Contains(A));
            Assert.False(tombstones.Contains(B));
            Assert.Equal(1, tombstones.Count);
            Assert.True(tombstones.Remove(A));
            Assert.False(tombstones.Remove(A));
            Assert.False(tombstones.Contains(A));
            Assert.Equal(0, tombstones.Count);
        }

        [Fact]
        public void Clear_LetsGoOfEverything()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 1, Seconds(1));
            tombstones.Add(B, SourceKind.Scene, 1, Seconds(1));

            tombstones.Clear();

            Assert.Equal(0, tombstones.Count);
            Assert.False(tombstones.Contains(A));
        }

        [Fact]
        public void AListThatLacksTheSource_ConfirmsIt_AndItIsLetGoOfAfterwards()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 5, Seconds(1));

            tombstones.ApplyList(SourceKind.Input, [], 6, Seconds(2));

            // Confirmed at 2 seconds: it is let go of for a time after that, not at it.
            Assert.Equal(0, tombstones.Prune(Seconds(2)));
            Assert.True(tombstones.Contains(A));
            Assert.Equal(1, tombstones.Prune(Seconds(3)));
            Assert.False(tombstones.Contains(A));
        }

        [Fact]
        public void AListThatHasTheSource_DoesNotConfirmIt_HoweverOldItIs()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 5, Seconds(1));

            tombstones.ApplyList(SourceKind.Input, [A], 6, Seconds(2));

            Assert.Equal(0, tombstones.Prune(TimeSpan.FromDays(365)));
            Assert.True(tombstones.Contains(A));
        }

        [Fact]
        public void ASourceThatNoListConfirmed_IsNeverLetGoOf()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 5, Seconds(1));
            tombstones.Add(B, SourceKind.Scene, 5, Seconds(1));

            Assert.Equal(0, tombstones.Prune(TimeSpan.FromDays(365)));
            Assert.Equal(2, tombstones.Count);
        }

        [Fact]
        public void AListThatHasTheSourceAgain_TakesTheConfirmationBack()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 5, Seconds(1));
            tombstones.ApplyList(SourceKind.Input, [], 6, Seconds(2));

            tombstones.ApplyList(SourceKind.Input, [A], 7, Seconds(3));

            Assert.Equal(0, tombstones.Prune(TimeSpan.FromDays(365)));
            Assert.True(tombstones.Contains(A));
        }

        [Fact]
        public void AListThatWasSentBeforeTheTombstoneWasMade_SaysNothingAboutIt()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 10, Seconds(1));

            // The source was made after this list was sent, so the list lacks it whatever OBS does with it.
            tombstones.ApplyList(SourceKind.Input, [], 9, Seconds(2));
            Assert.Equal(0, tombstones.Prune(TimeSpan.FromDays(365)));

            // A list that was sent when the message of the removal had arrived is after it.
            tombstones.ApplyList(SourceKind.Input, [], 10, Seconds(3));
            Assert.Equal(1, tombstones.Prune(TimeSpan.FromDays(365)));
        }

        [Fact]
        public void AListSaysNothingAboutTheSourcesOfAnotherKind()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 5, Seconds(1));
            tombstones.Add(B, SourceKind.Scene, 5, Seconds(1));

            tombstones.ApplyList(SourceKind.Scene, [], 6, Seconds(2));

            Assert.Equal(1, tombstones.Prune(TimeSpan.FromDays(365)));
            Assert.True(tombstones.Contains(A));
            Assert.False(tombstones.Contains(B));
        }

        [Fact]
        public void ATombstoneThatIsAddedAgain_StartsAgain()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 5, Seconds(1));
            tombstones.ApplyList(SourceKind.Input, [], 6, Seconds(2));

            tombstones.Add(A, SourceKind.Input, 9, Seconds(3));

            Assert.Equal(1, tombstones.Count);
            Assert.Equal(0, tombstones.Prune(TimeSpan.FromDays(365)));
            Assert.False(tombstones.NeedsConfirmation(SourceKind.Input, Seconds(3)));
            Assert.True(tombstones.NeedsConfirmation(SourceKind.Input, Seconds(4)));
        }

        [Fact]
        public void ASourceThatNoListConfirmed_NeedsAListOnceItIsOld()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 5, Seconds(10));

            // Made at 10 seconds: old when the time that it is compared with is after that.
            Assert.False(tombstones.NeedsConfirmation(SourceKind.Input, Seconds(10)));
            Assert.True(tombstones.NeedsConfirmation(SourceKind.Input, Seconds(11)));

            // A list of the other kind is no use to it.
            Assert.False(tombstones.NeedsConfirmation(SourceKind.Scene, Seconds(11)));
        }

        [Fact]
        public void ASourceThatAListConfirmed_NeedsNoList()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 5, Seconds(10));

            tombstones.ApplyList(SourceKind.Input, [], 6, Seconds(20));

            Assert.False(tombstones.NeedsConfirmation(SourceKind.Input, TimeSpan.FromDays(1)));
        }

        [Fact]
        public void ASourceThatAListHad_NeedsAListAgainWhenThatIsLongAgo()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 5, Seconds(10));

            tombstones.ApplyList(SourceKind.Input, [A], 6, Seconds(20));

            // Asked at 20 seconds: it is not old again before then.
            Assert.False(tombstones.NeedsConfirmation(SourceKind.Input, Seconds(20)));
            Assert.True(tombstones.NeedsConfirmation(SourceKind.Input, Seconds(21)));
        }

        [Fact]
        public void AListThatWasSentBeforeTheTombstone_DoesNotCountAsAskingAboutIt()
        {
            var tombstones = new SourceTombstones();
            tombstones.Add(A, SourceKind.Input, 10, Seconds(10));

            tombstones.ApplyList(SourceKind.Input, [A], 9, Seconds(20));

            Assert.True(tombstones.NeedsConfirmation(SourceKind.Input, Seconds(11)));
        }
    }
}
