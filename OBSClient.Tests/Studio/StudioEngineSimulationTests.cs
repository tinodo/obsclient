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

        // A retention of this many seconds (of virtual time) is more than twice what a read may take (10 seconds), and far less than the default (5 minutes).
        private const int ShortRetention = 25;

        private static int Seeds => ReadNumber("OBSCLIENT_SIM_SEEDS", DefaultSeeds);

        private static int FirstSeed => ReadNumber("OBSCLIENT_SIM_FIRST", 1);

        [Theory]
        [InlineData("Ideal", 50, false, 0)]
        [InlineData("Ideal", 50, true, 0)]
        [InlineData("Real", 50, false, 0)]
        [InlineData("Real", 50, true, 0)]
        [InlineData("Real", 4, false, 0)]
        [InlineData("Real", 4, true, 0)]
        [InlineData("Hostile", 50, false, 0)]
        [InlineData("Hostile", 50, true, 0)]
        [InlineData("Hostile", 4, true, 0)]
        [InlineData("Real", 50, true, ShortRetention)]
        [InlineData("Hostile", 50, false, ShortRetention)]
        [InlineData("Hostile", 4, true, ShortRetention)]
        public void After_a_random_walk_the_model_holds_what_OBS_Studio_holds(string quirks, int maxBatch, bool reconnects, int retentionSeconds)
        {
            var failures = new List<string>();
            var walks = 0;
            foreach (var seed in SeedRange(FirstSeed, Seeds))
            {
                walks++;
                var result = Run(seed, quirks, maxBatch, reconnects, retentionSeconds);
                if (result.Problem is not null)
                {
                    failures.Add(result.Problem);
                    if (failures.Count == 3)
                    {
                        break;
                    }
                }
            }

            Assert.True(walks > 0, "No walk was run.");
            Assert.True(
                failures.Count == 0,
                $"{failures.Count} of the walks ended with a model that is not what OBS Studio is (the first ones are shown):{Environment.NewLine}{string.Join(Environment.NewLine + new string('=', 100) + Environment.NewLine, failures)}");
        }

        // The seeds of a run. The first seed and the number of seeds are not added in an int: for a first seed near the end of the range the sum overflows, the loop
        // does not run, and the test passes without having walked once.
        internal static IEnumerable<int> SeedRange(int first, int count)
        {
            var last = Math.Min((long)first + count - 1, int.MaxValue);
            for (var seed = (long)first; seed <= last; seed++)
            {
                yield return (int)seed;
            }
        }

        [Fact]
        public void The_seeds_of_a_run_are_the_ones_that_were_asked_for()
        {
            Assert.Equal(new[] { 5, 6, 7 }, SeedRange(5, 3));
            Assert.Equal(25, SeedRange(1, 25).Count());
        }

        [Fact]
        public void The_seeds_of_a_run_do_not_overflow_at_the_end_of_the_range_and_are_never_none()
        {
            Assert.Equal(new[] { int.MaxValue - 1, int.MaxValue }, SeedRange(int.MaxValue - 1, 10));
            Assert.Equal(new[] { int.MaxValue }, SeedRange(int.MaxValue, 5));
        }

        [Fact]
        public void The_walks_with_a_short_retention_let_go_of_what_they_remember()
        {
            var letGo = 0L;
            for (var seed = 1; seed <= 20; seed++)
            {
                // Without changes of the scene collection: a switch lets the model forget what it remembers, all at once, before the retention does.
                var result = Run(seed, "Hostile", 4, reconnects: true, ShortRetention, collections: false);
                Assert.Null(result.Problem);
                letGo += result.LetGo;
            }

            // If nothing was let go of, the walks that use a short retention would prove nothing about it.
            Assert.True(letGo > 200, $"Only {letGo} things were let go of.");
        }

        [Fact]
        public void The_walks_exercise_what_they_are_meant_to()
        {
            var reads = 0;
            var late = 0;
            var timeouts = 0;
            var reconnects = 0;
            var messages = 0;
            var switches = 0;
            var outdated = 0;
            for (var seed = 1; seed <= 20; seed++)
            {
                var result = Run(seed, "Hostile", 4, reconnects: true);
                reads += result.Reads;
                late += result.LateResults;
                timeouts += result.Timeouts;
                reconnects += result.Reconnects;
                messages += result.Messages;
                switches += result.Switches;
                outdated += result.Outdated;
            }

            // If the walks did not do these, a model that is right after them would prove little.
            Assert.True(messages > 2000, $"Only {messages} messages arrived.");
            Assert.True(reads > 400, $"Only {reads} reads were made.");
            Assert.True(late > 20, $"Only {late} results of reads were handed to the engine after other messages.");
            Assert.True(timeouts > 5, $"Only {timeouts} reads were given up.");
            Assert.True(reconnects > 5, $"The connection ended only {reconnects} times.");
            Assert.True(switches > 40, $"The engine was told of only {switches} changes of the scene collection.");
        }

        [Fact]
        public void The_walks_let_OBS_Studio_change_its_scene_collection_while_a_read_is_under_way()
        {
            var switches = 0;
            var outdated = 0;
            foreach (var quirks in new[] { "Ideal", "Real", "Hostile" })
            {
                for (var seed = 1; seed <= 40; seed++)
                {
                    var result = Run(seed, quirks, 4, reconnects: true);
                    Assert.Null(result.Problem);
                    switches += result.Switches;
                    outdated += result.Outdated;
                }
            }

            // If no read was under way when OBS Studio changed its scene collection, the answers that the model has to ignore would never have reached it.
            Assert.True(switches > 400, $"The engine was told of only {switches} changes of the scene collection.");
            Assert.True(outdated > 20, $"Only {outdated} reads were under way when the scene collection changed.");
        }

        [Fact]
        public void The_same_seed_gives_the_same_walk()
        {
            var first = Run(7, "Real", 50, reconnects: true);
            var second = Run(7, "Real", 50, reconnects: true);

            // Both walks ended well, and everything that happened in them is the same: the operations, the messages with what they carry, the reads that were
            // sent, and the states that were compared. Not only how many of each there were.
            Assert.Null(first.Problem);
            Assert.Null(second.Problem);
            Assert.NotEmpty(first.Digest);
            Assert.Equal(first.Digest, second.Digest);
            Assert.Equal(first, second);
        }

        [Fact]
        public void Another_seed_gives_another_walk()
        {
            var first = Run(7, "Real", 50, reconnects: true);
            var other = Run(8, "Real", 50, reconnects: true);

            Assert.Null(other.Problem);
            Assert.NotEqual(first.Digest, other.Digest);
        }

        [Fact]
        public void The_digest_depends_on_what_the_quirks_make_of_the_walk()
        {
            var real = Run(7, "Real", 50, reconnects: true);
            var hostile = Run(7, "Hostile", 50, reconnects: true);

            Assert.NotEqual(real.Digest, hostile.Digest);
        }

        // The volumes of OBS Studio and of the model are compared in decibels as well as by the multiplier: at quiet levels a difference of 6 dB is a
        // difference of 0.001 in the multiplier, which an absolute tolerance on the multiplier does not see.
        [Theory]
        [InlineData(-60.0, -54.0, false)]
        [InlineData(-60.0, -60.0, true)]
        [InlineData(-60.0, -60.001, true)]
        [InlineData(-40.0, -39.9, false)]
        [InlineData(0.0, 0.0, true)]
        [InlineData(0.0, 0.05, false)]
        [InlineData(12.0, 12.002, true)]
        [InlineData(12.0, 11.0, false)]
        [InlineData(-100.0, -100.0, true)]
        [InlineData(-100.0, -99.0, false)]
        public void A_difference_of_volume_is_seen_at_every_level(double truthDb, double modelDb, bool same)
        {
            var uuid = Guid.NewGuid();
            var truth = new SimInputSnapshot(uuid, "Mic", "wasapi_input_capture", true, false, DbToMul(truthDb), truthDb, false, false);
            var model = new EngineInput(uuid, "Mic", "wasapi_input_capture", true, false, (float)DbToMul(modelDb), (float)modelDb, SourceState.Live);

            Assert.Equal(same, EngineSimRun.SameVolume(truth, model));
        }

        [Fact]
        public void A_volume_whose_multiplier_and_decibels_do_not_belong_together_is_not_the_same()
        {
            var uuid = Guid.NewGuid();
            var truth = new SimInputSnapshot(uuid, "Mic", "wasapi_input_capture", true, false, DbToMul(-6), -6, false, false);
            var model = new EngineInput(uuid, "Mic", "wasapi_input_capture", true, false, (float)DbToMul(-6) * 2, -6, SourceState.Live);

            Assert.False(EngineSimRun.SameVolume(truth, model));
        }

        private static double DbToMul(double db)
        {
            return db <= -100 ? 0 : Math.Pow(10, db / 20);
        }

        private static EngineSimResult Run(int seed, string quirks, int maxBatch, bool reconnects, int retentionSeconds = 0, bool collections = true)
        {
            var presets = quirks switch
            {
                "Ideal" => SimQuirks.Ideal,
                "Real" => SimQuirks.Real,
                "Hostile" => SimQuirks.Hostile,
                _ => throw new ArgumentOutOfRangeException(nameof(quirks), quirks, null),
            };
            var options = new StudioEngineOptions { MaxBatch = maxBatch };
            if (retentionSeconds > 0)
            {
                // Much shorter than the default, so that the walks (a minute or two of virtual time) let go of what they remember, and show that nothing needs more
                // than what the messages that it guards against take: a read that is given up after 10 seconds, and the lag of OBS Studio.
                options.TombstoneRetention = TimeSpan.FromSeconds(retentionSeconds);
                options.TombstoneCleanupInterval = TimeSpan.FromSeconds(1);
            }

            return EngineSimRun.Execute(seed, presets, quirks, Steps, reconnects, options, collections: collections);
        }

        private static int ReadNumber(string name, int fallback)
        {
            return int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;
        }
    }
}
