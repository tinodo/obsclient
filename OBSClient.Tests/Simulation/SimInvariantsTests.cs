namespace OBSStudioClient.Tests.Simulation
{
    using System.Globalization;

    /// <summary>
    /// Random walks on a simulated OBS Studio (<see cref="SimWalk"/>): requests of a client, valid and not, and batches, mixed with the
    /// changes of someone else, with time passing, under the hostile quirks, the real ones and none. The rules of the state are checked
    /// after every step and after settling; after settling the answers of the requests have to equal the snapshot; and the events have to
    /// be enough to rebuild the state. A failure names the seed, so that the walk can be run again.
    /// </summary>
    public class SimInvariantsTests
    {
        private const int Steps = 60;

        private static readonly Lazy<IReadOnlyList<SimWalkResult>> HostileWalks = new(() => Walks(SimQuirks.Hostile, 300));
        private static readonly Lazy<IReadOnlyList<SimWalkResult>> RealWalks = new(() => Walks(SimQuirks.Real, 60));
        private static readonly Lazy<IReadOnlyList<SimWalkResult>> IdealWalks = new(() => Walks(SimQuirks.Ideal, 60));

        private static IReadOnlyList<SimWalkResult> Walks(SimQuirks quirks, int seeds)
        {
            // Every seed is a walk of its own, so they can run side by side, and the results stay in the order of the seeds.
            var results = new SimWalkResult[seeds];
            Parallel.For(0, seeds, index => results[index] = SimWalk.Execute(index + 1, quirks, Steps, keepMessages: false, out _));
            return results;
        }

        public static IEnumerable<object[]> Presets()
        {
            yield return new object[] { "Hostile" };
            yield return new object[] { "Real" };
            yield return new object[] { "Ideal" };
        }

        private static IReadOnlyList<SimWalkResult> Of(string preset)
        {
            return preset switch
            {
                "Hostile" => HostileWalks.Value,
                "Real" => RealWalks.Value,
                _ => IdealWalks.Value,
            };
        }

        [Theory]
        [MemberData(nameof(Presets))]
        public void TheRulesOfTheStateHold_AfterEveryStepOfARandomWalk_AndAfterSettling(string preset)
        {
            SimAssert.NoProblems(Of(preset).SelectMany(result => result.Invariants), $"walks of the {preset} quirks broke a rule");
        }

        [Theory]
        [MemberData(nameof(Presets))]
        public void AfterSettling_TheAnswersOfTheRequestsEqualTheSnapshot(string preset)
        {
            // GetSceneList, GetSceneItemList (also of the scenes that were removed but are alive), GetInputList, GetInputMute and GetInputVolume.
            SimAssert.NoProblems(Of(preset).SelectMany(result => result.Views), $"walks of the {preset} quirks ended in a state that the answers do not show");
        }

        [Theory]
        [MemberData(nameof(Presets))]
        public void EveryRequestIsAnsweredOnce_ExceptTheOnesThatHang(string preset)
        {
            SimAssert.NoProblems(Of(preset).SelectMany(result => result.Answers), $"walks of the {preset} quirks answered too much or too little");
        }

        [Theory]
        [MemberData(nameof(Presets))]
        public void MessagesLeaveInTheOrderOfTheirTimeAndSequence(string preset)
        {
            SimAssert.NoProblems(Of(preset).SelectMany(result => result.Order), $"walks of the {preset} quirks sent messages in the wrong order");
        }

        [Fact]
        public void TheEventsAlone_RebuildTheStateOfOBSStudio_WhenTheyArriveInOrder()
        {
            // What a model of OBS Studio does with the events (without a single request), in the one setting in which they are not mixed up. A switch of the scene collection
            // ends a stretch of events and begins another, from what OBS Studio held when it loaded the collection.
            SimAssert.NoProblems(IdealWalks.Value.SelectMany(result => result.Replays), "walks whose events do not lead to the state that OBS Studio has");

            // The replay is not left out where it is not looked for: every walk was replayed, and some of them in stretches, across switches of the scene collection.
            var replayed = IdealWalks.Value.Where(result => result.Invariants.Count == 0).ToArray();
            Assert.All(replayed, result => Assert.True(result.Counters.GetValueOrDefault("replay: stretches") >= 1, "A walk was not replayed."));
            Assert.True(replayed.Sum(result => result.Counters.GetValueOrDefault("replay: stretches")) > replayed.Length + 10, "Too few of the walks were replayed across a switch of the scene collection.");
        }

        [Fact]
        public void Without_TheHangQuirk_NoRequestHangs()
        {
            Assert.All(IdealWalks.Value, result => Assert.Equal(0, result.Counters.GetValueOrDefault("hung")));
            Assert.Contains(HostileWalks.Value, result => result.Counters.GetValueOrDefault("hung") > 0);
        }

        [Fact]
        public void TheWalks_ReachTheStatesAndTheEventsThatTheRulesAreAbout()
        {
            // A walk that never gets anywhere interesting proves nothing: each of these has to happen, over all the seeds of the hostile walks.
            var total = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var result in HostileWalks.Value)
            {
                foreach (var (name, count) in result.Counters)
                {
                    total[name] = total.GetValueOrDefault(name) + count;
                }
            }

            var wanted = new List<string>
            {
                "state: a scene is detached",
                "state: an input is removed and alive",
                "state: an input is pinned",
                "state: a scene is nested",
                "garbage noticed",
                "hung",
                "answer NoSuchRequest 204",
                "answer  203",
                "answer GetCurrentPreviewScene 506",
            };
            wanted.AddRange(SimRequests.Implemented.Where(type => type != "GetCurrentPreviewScene").Select(type => $"answer {type} 100"));
            wanted.AddRange(new[]
            {
                "SceneCreated", "SceneRemoved", "SceneNameChanged", "SceneListChanged", "CurrentProgramSceneChanged",
                "InputCreated", "InputRemoved", "InputNameChanged", "InputMuteStateChanged", "InputVolumeChanged",
                "SceneTransitionStarted", "SceneTransitionEnded", "SceneTransitionVideoEnded",
                "SceneItemCreated", "SceneItemRemoved", "SceneItemListReindexed", "SceneItemEnableStateChanged", "SceneItemLockStateChanged", "SceneItemSelected",
                "CurrentSceneCollectionChanging", "CurrentSceneCollectionChanged",
            }.Select(name => "event " + name));

            // The codes of the answers: "answer GetSceneList 100" and "answer GetSceneList 100 (in a batch)" are both a 100.
            var codes = total.Keys
                .Where(key => key.StartsWith("answer ", StringComparison.Ordinal))
                .Select(key => key.Replace(" (in a batch)", string.Empty, StringComparison.Ordinal).Split(' '))
                .Where(parts => parts.Length == 3 && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                .Select(parts => int.Parse(parts[2], CultureInfo.InvariantCulture))
                .ToHashSet();
            var missing = wanted.Where(name => total.GetValueOrDefault(name) == 0).ToList();
            missing.AddRange(new[] { 207, 600, 601, 602, 604, 300, 401, 402, 700, 703 }.Where(code => !codes.Contains(code)).Select(code => $"an answer with the code {code.ToString(CultureInfo.InvariantCulture)}"));
            Assert.True(missing.Count == 0, "The hostile walks never reached: " + string.Join(", ", missing) + Environment.NewLine + string.Join(Environment.NewLine, total.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => $"{entry.Key}: {entry.Value}")));
        }

        [Fact]
        public void TheWalks_AreNotAllSuccessesAndNotAllFailures()
        {
            var total = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var result in HostileWalks.Value)
            {
                foreach (var (name, count) in result.Counters)
                {
                    total[name] = total.GetValueOrDefault(name) + count;
                }
            }

            var refused = total.Where(entry => entry.Key.StartsWith("refused ", StringComparison.Ordinal)).Sum(entry => entry.Value);
            var external = total.Where(entry => entry.Key.StartsWith("op external ", StringComparison.Ordinal)).Sum(entry => entry.Value);
            Assert.InRange(refused, 1, external / 2);

            var answers = total.Where(entry => entry.Key.StartsWith("answer ", StringComparison.Ordinal) && !entry.Key.EndsWith("(in a batch)", StringComparison.Ordinal)).ToArray();
            var succeeded = answers.Where(entry => entry.Key.EndsWith(" 100", StringComparison.Ordinal)).Sum(entry => entry.Value);
            var all = answers.Sum(entry => entry.Value);
            Assert.InRange(succeeded, all / 3, all - (all / 20));
        }
    }
}
