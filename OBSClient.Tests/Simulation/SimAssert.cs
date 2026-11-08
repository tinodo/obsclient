namespace OBSStudioClient.Tests.Simulation
{
    /// <summary>Assertions that say what is wrong, with the details that a failure needs, instead of only that something is.</summary>
    internal static class SimAssert
    {
        /// <summary>Fails when there are problems, and shows the first few.</summary>
        /// <param name="problems">What was found.</param>
        /// <param name="what">What the problems are, for the message.</param>
        /// <param name="show">How many problems to show.</param>
        public static void NoProblems(IEnumerable<string> problems, string what, int show = 2)
        {
            var list = problems.ToList();
            Assert.True(
                list.Count == 0,
                $"{list.Count} {what}. The first {Math.Min(show, list.Count)}:{Environment.NewLine}{string.Join(Environment.NewLine + "----" + Environment.NewLine, list.Take(show))}");
        }
    }
}
