namespace OBSStudioClient.Tests.Model
{
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// <see cref="TimedSet{T}"/>: a set whose members can be let go of when they are old.
    /// </summary>
    public class TimedSetTests
    {
        [Fact]
        public void AMemberIsThere_UntilItIsRemoved()
        {
            var set = new TimedSet<string>();

            set.Add("a", TimeSpan.FromSeconds(1));

            Assert.True(set.Contains("a"));
            Assert.False(set.Contains("b"));
            Assert.Equal(1, set.Count);
            Assert.True(set.Remove("a"));
            Assert.False(set.Remove("a"));
            Assert.False(set.Contains("a"));
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void Prune_LetsGoOfWhatWasAddedBeforeATime_AndKeepsWhatWasAddedAtIt()
        {
            var set = new TimedSet<int>();
            set.Add(1, TimeSpan.FromSeconds(1));
            set.Add(2, TimeSpan.FromSeconds(5));
            set.Add(3, TimeSpan.FromSeconds(9));
            set.Add(4, TimeSpan.FromSeconds(10));

            var pruned = set.Prune(TimeSpan.FromSeconds(9));

            Assert.Equal(2, pruned);
            Assert.False(set.Contains(1));
            Assert.False(set.Contains(2));
            Assert.True(set.Contains(3));
            Assert.True(set.Contains(4));
            Assert.Equal(0, set.Prune(TimeSpan.FromSeconds(9)));
        }

        [Fact]
        public void AMemberThatIsAddedAgain_IsRememberedFromThen()
        {
            var set = new TimedSet<int>();
            set.Add(1, TimeSpan.FromSeconds(1));
            set.Add(1, TimeSpan.FromSeconds(8));

            Assert.Equal(0, set.Prune(TimeSpan.FromSeconds(5)));

            Assert.True(set.Contains(1));
            Assert.Equal(1, set.Count);
            Assert.Equal(1, set.Prune(TimeSpan.FromSeconds(9)));
        }

        [Fact]
        public void Clear_LetsGoOfEverything()
        {
            var set = new TimedSet<int>();
            set.Add(1, TimeSpan.Zero);
            set.Add(2, TimeSpan.Zero);

            set.Clear();

            Assert.Equal(0, set.Count);
            Assert.False(set.Contains(1));
        }
    }
}
