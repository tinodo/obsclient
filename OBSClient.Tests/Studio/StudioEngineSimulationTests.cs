namespace OBSStudioClient.Tests.Studio
{
    using OBSStudioClient.Studio.Engine;
    using OBSStudioClient.Tests.Simulation;

    /// <summary>
    /// The model against a simulated OBS Studio: the engine is given what a client would be given while someone else changes OBS Studio at
    /// random, with the lag, the order and the repeats of a real one, and when everything has settled, what it holds has to be what OBS Studio
    /// holds. A walk that fails says its seed: set <c>OBSCLIENT_SIM_FIRST</c> to it and <c>OBSCLIENT_SIM_SEEDS</c> to 1 to run just that walk, or
    /// <c>OBSCLIENT_SIM_SEEDS</c> to a large number for a long campaign.
    /// </summary>
    public class StudioEngineSimulationTests
    {
        private const int Steps = 200;
        private const int DefaultSeeds = 25;

        private static int Seeds => ReadNumber("OBSCLIENT_SIM_SEEDS", DefaultSeeds);

        private static int FirstSeed => ReadNumber("OBSCLIENT_SIM_FIRST", 1);

        [Theory]
        [InlineData("Ideal", 50, false)]
        [InlineData("Ideal", 50, true)]
        [InlineData("Real", 50, false)]
        [InlineData("Real", 50, true)]
        [InlineData("Real", 4, false)]
        [InlineData("Real", 4, true)]
        [InlineData("Hostile", 50, false)]
        [InlineData("Hostile", 50, true)]
        [InlineData("Hostile", 4, true)]
        public void After_a_random_walk_the_model_holds_what_OBS_Studio_holds(string quirks, int maxBatch, bool reconnects)
        {
            var failures = new List<string>();
            for (var seed = FirstSeed; seed < FirstSeed + Seeds; seed++)
            {
                var result = Run(seed, quirks, maxBatch, reconnects);
                if (result.Problem is not null)
                {
                    failures.Add(result.Problem);
                    if (failures.Count == 3)
                    {
                        break;
                    }
                }
            }

            Assert.True(
                failures.Count == 0,
                $"{failures.Count} of the walks ended with a model that is not what OBS Studio is (the first ones are shown):{Environment.NewLine}{string.Join(Environment.NewLine + new string('=', 100) + Environment.NewLine, failures)}");
        }

        [Fact]
        public void The_walks_exercise_what_they_are_meant_to()
        {
            var reads = 0;
            var late = 0;
            var timeouts = 0;
            var reconnects = 0;
            var messages = 0;
            for (var seed = 1; seed <= 20; seed++)
            {
                var result = Run(seed, "Hostile", 4, reconnects: true);
                reads += result.Reads;
                late += result.LateResults;
                timeouts += result.Timeouts;
                reconnects += result.Reconnects;
                messages += result.Messages;
            }

            // If the walks did not do these, a model that is right after them would prove little.
            Assert.True(messages > 2000, $"Only {messages} messages arrived.");
            Assert.True(reads > 400, $"Only {reads} reads were made.");
            Assert.True(late > 20, $"Only {late} results of reads were handed to the engine after other messages.");
            Assert.True(timeouts > 5, $"Only {timeouts} reads were given up.");
            Assert.True(reconnects > 5, $"The connection ended only {reconnects} times.");
        }

        [Fact]
        public void The_same_seed_gives_the_same_walk()
        {
            var first = Run(7, "Real", 50, reconnects: true);
            var second = Run(7, "Real", 50, reconnects: true);
            Assert.Equal(first, second);
        }

        private static EngineSimResult Run(int seed, string quirks, int maxBatch, bool reconnects)
        {
            var presets = quirks switch
            {
                "Ideal" => SimQuirks.Ideal,
                "Real" => SimQuirks.Real,
                "Hostile" => SimQuirks.Hostile,
                _ => throw new ArgumentOutOfRangeException(nameof(quirks), quirks, null),
            };
            return EngineSimRun.Execute(seed, presets, quirks, Steps, reconnects, new StudioEngineOptions { MaxBatch = maxBatch });
        }

        private static int ReadNumber(string name, int fallback)
        {
            return int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;
        }
    }
}
