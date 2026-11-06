namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json;

    /// <summary>
    /// The quirks of the simulation do what they say, over many seeds: the order of events and responses, the latencies, requests that run
    /// in another order than they were sent in, the lag of the list of scenes, the hostile preset, and parallel batches. And the simulation
    /// is deterministic: the same seed and the same calls give the same messages, byte for byte, whatever the time of the machine.
    /// </summary>
    public class SimQuirksTests
    {
        private static readonly SimQuirks Ideal = SimQuirks.Ideal;

        private static string Shape(JsonElement result)
        {
            return result.TryGetProperty("responseData", out var data) ? string.Join(",", data.EnumerateObject().Select(member => member.Name)) : string.Empty;
        }

        // A scenario that uses requests, changes of someone else, a batch and time, and gives everything that OBS Studio sent.
        private static string Script(int seed, SimQuirks quirks)
        {
            var h = new SimHarness(quirks, seed);
            h.Send("CreateScene", new { sceneName = "A" });
            h.Send("CreateScene", new { sceneName = "B" });
            h.Run(5);
            var external = h.Sim.External.CreateScene("C");
            h.Sim.External.CreateInput(external, "Mic", "wasapi_input_capture");
            h.Send("CreateInput", new { sceneUuid = external.ToString(), inputName = "I", inputKind = "color_source_v3" });
            h.SendBatch(
                [
                    new { requestType = "CreateScene", requestData = new { sceneName = "D" } },
                    new { requestType = "GetSceneList" },
                    new { requestType = "GetInputList" },
                ],
                executionType: 2);
            h.Run(50);
            h.Send("SetCurrentProgramScene", new { sceneUuid = external.ToString() });
            h.Send("RemoveScene", new { sceneUuid = external.ToString() });
            h.Settle();
            return string.Join("\n", h.Log.Select(entry => $"{entry.Message.Sequence} {entry.Message.SentAt.Ticks} {entry.Message.Op} {entry.Message.Json}"));
        }

        [Fact]
        public void ThePresetsAreWhatTheMeasurementsSay()
        {
            var ideal = SimQuirks.Ideal;
            var real = SimQuirks.Real;
            var hostile = SimQuirks.Hostile;

            Assert.Equal(SimEventResponseOrder.ResponseFirst, ideal.EventResponseOrder);
            Assert.True(ideal.ResponseLatency.IsNone && ideal.EventLatency.IsNone && ideal.SceneListLag.IsNone && ideal.ExecutionJitter.IsNone);
            Assert.False(ideal.ReorderConcurrentRequests || ideal.DuplicateSceneCreated || ideal.HangOnRemovedSceneRename || ideal.ParallelBatchMisorders || ideal.StaleSceneListChangedPayload);
            Assert.Equal(TimeSpan.Zero, ideal.TransitionDuration);

            Assert.Equal(SimEventResponseOrder.Random, real.EventResponseOrder);
            Assert.Equal(new SimRange(0, 3), real.ResponseLatency);
            Assert.Equal(new SimRange(0, 15), real.EventLatency);
            Assert.True(real.EventLatencyTailChance > 0);
            Assert.Equal(50, real.EventLatencyTail.MaxMs);
            Assert.Equal(new SimRange(14, 50), real.InputSettingsChangedLatency);
            Assert.Equal(330, real.InputSettingsChangedOutlier.MaxMs);
            Assert.True(real.ReorderConcurrentRequests);
            Assert.Equal(new SimRange(0, 8), real.ExecutionJitter);
            Assert.True(real.DuplicateSceneCreated);
            Assert.Equal(new SimRange(0, 2000), real.SceneListLag);
            Assert.False(real.StaleSceneListChangedPayload);
            Assert.Equal(TimeSpan.FromMilliseconds(300), real.TransitionDuration);
            Assert.True(real.HangOnRemovedSceneRename && real.ParallelBatchMisorders);
            Assert.Equal(TimeSpan.FromMilliseconds(2), real.DestroyCascadeDelay);

            Assert.True(hostile.ResponseLatency.MaxMs > real.ResponseLatency.MaxMs);
            Assert.True(hostile.EventLatency.MaxMs > real.EventLatency.MaxMs);
            Assert.True(hostile.EventLatencyTail.MaxMs > real.EventLatencyTail.MaxMs);
            Assert.True(hostile.EventLatencyTailChance > real.EventLatencyTailChance);
            Assert.True(hostile.ExecutionJitter.MaxMs > real.ExecutionJitter.MaxMs);
            Assert.True(hostile.SceneListLag.MinMs > 0, "The hostile list of scenes always lags.");
            Assert.True(hostile.StaleSceneListChangedPayload);
            Assert.Equal(real.DuplicateSceneCreated, hostile.DuplicateSceneCreated);
            Assert.Equal(real.HangOnRemovedSceneRename, hostile.HangOnRemovedSceneRename);
        }

        [Fact]
        public void TheDefaultQuirksAreTheRealOnes()
        {
            Assert.Equal(SimQuirks.Real, new SimObs().Quirks);
        }

        [Fact]
        public void Fact1_EventFirst_SendsEveryEventOfAChangeBeforeItsResponse()
        {
            var h = new SimHarness(Ideal with { EventResponseOrder = SimEventResponseOrder.EventFirst, DuplicateSceneCreated = true });

            h.Send("CreateScene", new { sceneName = "A" });
            h.Settle();

            Assert.Equal(["SceneCreated", "SceneListChanged", "SceneCreated", ">CreateScene"], h.Names());
        }

        [Fact]
        public void Fact1_ResponseFirst_SendsTheResponseBeforeEveryEventOfAChange()
        {
            var h = new SimHarness(Ideal with { EventResponseOrder = SimEventResponseOrder.ResponseFirst, DuplicateSceneCreated = true });

            h.Send("CreateScene", new { sceneName = "A" });
            h.Settle();

            Assert.Equal([">CreateScene", "SceneCreated", "SceneListChanged", "SceneCreated"], h.Names());
        }

        [Fact]
        public void Fact1_WithRandomOrder_TheResponseComesBeforeAndAfterTheEvents_AndTheEventsKeepTheirOrder()
        {
            var positions = new HashSet<int>();
            for (var seed = 1; seed <= 200; seed++)
            {
                var h = new SimHarness(Ideal with { EventResponseOrder = SimEventResponseOrder.Random, DuplicateSceneCreated = true }, seed);

                h.Send("CreateScene", new { sceneName = "A" });
                h.Settle();

                var names = h.Names();
                positions.Add(Array.IndexOf(names, ">CreateScene"));
                Assert.Equal(["SceneCreated", "SceneListChanged", "SceneCreated"], names.Where(name => name[0] != '>').ToArray());
            }

            // Fact 26: the response is also somewhere in the middle of the events of its change.
            Assert.Equal([0, 1, 2, 3], positions.Order().ToArray());
        }

        [Fact]
        public void Fact1_TheEventsOfOneChangeKeepTheirOrder_WhateverTheLatencies()
        {
            for (var seed = 1; seed <= 200; seed++)
            {
                var h = new SimHarness(SimQuirks.Hostile, seed);

                h.Sim.External.CreateScene("A");
                h.Settle();

                Assert.Equal(["SceneCreated", "SceneListChanged", "SceneCreated"], h.EventNames());
            }
        }

        [Fact]
        public void Fact3_TheEventsOfDifferentChangesKeepTheirOrder_ByDefault()
        {
            foreach (var quirks in new[] { Ideal with { EventLatency = new SimRange(0, 100) }, SimQuirks.Real, SimQuirks.Hostile })
            {
                for (var seed = 1; seed <= 100; seed++)
                {
                    var h = new SimHarness(quirks, seed);

                    h.Sim.External.CreateScene("A");
                    h.Sim.External.CreateScene("B");
                    h.Sim.External.CreateScene("C");
                    h.Settle();

                    var names = h.Log.Where(entry => entry.Name == "SceneCreated").Select(entry => entry.String("sceneName")).Distinct().ToArray();
                    Assert.Equal(["A", "B", "C"], names);
                }
            }
        }

        [Fact]
        public void Fact1_TheEventsOfDifferentChangesInterleave_AccordingToTheirLatencies_WhenTheyMay()
        {
            var otherFirst = false;
            var sameFirst = false;
            for (var seed = 1; seed <= 100; seed++)
            {
                var h = new SimHarness(Ideal with { EventLatency = new SimRange(0, 100), ReorderEventsAcrossChanges = true }, seed);

                h.Sim.External.CreateScene("A");
                h.Sim.External.CreateScene("B");
                h.Settle();

                var names = h.Log.Where(entry => entry.Name == "SceneCreated").Select(entry => entry.String("sceneName")).ToArray();
                otherFirst |= names[0] == "B";
                sameFirst |= names[0] == "A";

                // Each of the two keeps its own order: its SceneCreated comes before its SceneListChanged.
                var created = h.Log.FindIndex(entry => entry.Name == "SceneCreated" && entry.String("sceneName") == "A");
                var listed = h.Log.FindIndex(entry => entry.Name == "SceneListChanged" && entry.Strings("scenes", "sceneName").SequenceEqual(["A", "Scene"]));
                Assert.True(created < listed, "SceneCreated of A came after the SceneListChanged that follows it.");
            }

            Assert.True(otherFirst && sameFirst, "The events of two changes never interleaved.");
        }

        [Fact]
        public void Fact1_TheResponseLatencyIsDrawnFromItsRange()
        {
            var seen = new HashSet<long>();
            for (var seed = 1; seed <= 60; seed++)
            {
                var h = new SimHarness(Ideal with { ResponseLatency = new SimRange(5, 9) }, seed);

                var response = h.Call("GetVersion");

                var milliseconds = (long)response.At.TotalMilliseconds;
                Assert.InRange(milliseconds, 5, 9);
                seen.Add(milliseconds);
            }

            Assert.Equal([5L, 6L, 7L, 8L, 9L], seen.Order().ToArray());
        }

        [Fact]
        public void Fact1_TheEventLatencyIsDrawnFromItsRange()
        {
            for (var seed = 1; seed <= 60; seed++)
            {
                var h = new SimHarness(Ideal with { EventLatency = new SimRange(10, 20), DuplicateSceneCreated = true }, seed);

                h.Sim.External.CreateScene("A");
                h.Settle();

                Assert.All(h.Log, entry => Assert.InRange((long)entry.At.TotalMilliseconds, 10, 20));
                Assert.Equal(["SceneCreated", "SceneListChanged", "SceneCreated"], h.EventNames());
            }
        }

        [Fact]
        public void Fact1_InARealObsStudio_EventsComeWithin50Milliseconds_AndNowAndThenLaterThan15()
        {
            var longest = TimeSpan.Zero;
            var beyond15 = 0;
            var total = 0;
            for (var seed = 1; seed <= 300; seed++)
            {
                var h = new SimHarness(SimQuirks.Real, seed);

                h.Sim.External.CreateScene("A");
                h.Settle();

                foreach (var entry in h.Log)
                {
                    total++;
                    longest = TimeSpan.FromTicks(Math.Max(longest.Ticks, entry.At.Ticks));
                    beyond15 += entry.At > TimeSpan.FromMilliseconds(15) ? 1 : 0;
                }
            }

            Assert.True(longest <= TimeSpan.FromMilliseconds(50), $"An event took {longest.TotalMilliseconds} ms.");
            Assert.InRange(beyond15, 1, total / 4);
        }

        [Fact]
        public void Fact3_RequestsThatAreInFlightTogether_CanRunInAnotherOrderThanTheyWereSent()
        {
            var sent = Enumerable.Range(0, 100).Select(index => "R" + index).ToArray();
            for (var seed = 1; seed <= 10; seed++)
            {
                var h = new SimHarness(Ideal with { ReorderConcurrentRequests = true, ExecutionJitter = new SimRange(0, 8) }, seed);

                foreach (var name in sent)
                {
                    h.Send("CreateScene", new { sceneName = name });
                }

                h.Settle();

                var ran = h.Events("SceneCreated").Select(entry => entry.String("sceneName")).ToArray();
                Assert.Equal(sent.Order().ToArray(), ran.Order().ToArray());
                Assert.False(ran.SequenceEqual(sent), $"Seed {seed}: the requests ran in the order in which they were sent.");
                Assert.Equal(100, h.Log.Count(entry => entry.IsResponse && entry.Succeeded));
            }
        }

        [Fact]
        public void Fact3_WithoutTheQuirk_RequestsRunInTheOrderInWhichTheyWereReceived()
        {
            var sent = Enumerable.Range(0, 100).Select(index => "R" + index).ToArray();
            for (var seed = 1; seed <= 10; seed++)
            {
                var h = new SimHarness(Ideal with { ReorderConcurrentRequests = false, ExecutionJitter = new SimRange(0, 8) }, seed);

                foreach (var name in sent)
                {
                    h.Send("CreateScene", new { sceneName = name });
                }

                h.Settle();

                Assert.Equal(sent, h.Events("SceneCreated").Select(entry => entry.String("sceneName")).ToArray());
            }
        }

        [Fact]
        public void Fact3_RequestsWithTheSameJitterRunInTheOrderInWhichTheyWereReceived()
        {
            var h = new SimHarness(Ideal with { ReorderConcurrentRequests = true, ExecutionJitter = SimRange.Exactly(3) });

            h.Send("CreateScene", new { sceneName = "A" });
            h.Send("CreateScene", new { sceneName = "B" });
            h.Send("CreateScene", new { sceneName = "C" });
            h.Settle();

            Assert.Equal(["A", "B", "C"], h.Events("SceneCreated").Select(entry => entry.String("sceneName")).ToArray());
        }

        [Fact]
        public void Fact3_ARequestRunsAfterItsJitter_AndNotBefore()
        {
            var h = new SimHarness(Ideal with { ReorderConcurrentRequests = true, ExecutionJitter = SimRange.Exactly(5) });

            h.Send("CreateScene", new { sceneName = "A" });
            h.Sim.Advance(TimeSpan.FromMilliseconds(4));
            var before = h.Sim.Snapshot().Scenes.Count;
            h.Sim.Advance(TimeSpan.FromMilliseconds(1));
            var after = h.Sim.Snapshot().Scenes.Count;

            Assert.Equal(1, before);
            Assert.Equal(2, after);
        }

        [Fact]
        public void Fact4_EachSceneHasALagOfItsOwn_AndOnceItIsListedItStaysListed()
        {
            var h = new SimHarness(Ideal with { SceneListLag = new SimRange(0, 2000) }, seed: 3);
            for (var index = 0; index < 40; index++)
            {
                h.Sim.External.CreateScene("S" + index);
            }

            var firstListed = new Dictionary<string, int>();
            for (var at = 0; at <= 2100; at += 50)
            {
                var listed = h.Ok("GetSceneList").Strings("scenes", "sceneName");
                foreach (var name in listed)
                {
                    firstListed.TryAdd(name, at);
                }

                // A scene that was listed is listed in every list after it.
                Assert.All(firstListed.Where(entry => entry.Value < at), entry => Assert.Contains(entry.Key, listed));
                h.Run(50);
            }

            Assert.Equal(41, firstListed.Count);
            Assert.Equal(0, firstListed["Scene"]);
            Assert.InRange(firstListed.Values.Max(), 1500, 2050);
            Assert.True(firstListed.Values.Distinct().Count() >= 10, "The scenes did not have lags of their own.");
        }

        [Fact]
        public void Fact4_ARemovedSceneIsStillListedUntilItsLagIsOver_AndANewSceneIsListedAtOnceInTheEvent()
        {
            var h = new SimHarness(Ideal with { SceneListLag = SimRange.Exactly(500) });
            var created = h.Sim.External.CreateScene("X");
            h.Run(600);
            h.ClearLog();

            h.Sim.External.RemoveScene(created);
            h.Run(499);
            var during = h.Ok("GetSceneList").Strings("scenes", "sceneName");
            h.Run(1);
            var after = h.Ok("GetSceneList").Strings("scenes", "sceneName");

            Assert.Equal(["X", "Scene"], during);
            Assert.Equal(["Scene"], after);
            Assert.Equal(["Scene"], h.Events("SceneListChanged").Single().Strings("scenes", "sceneName"));
        }

        [Fact]
        public void Fact4_ANewSceneIsNotInTheListYet_ButItIsInTheEvent()
        {
            var h = new SimHarness(Ideal with { SceneListLag = SimRange.Exactly(500) });

            h.Sim.External.CreateScene("X");
            h.Run(499);
            var during = h.Ok("GetSceneList").Strings("scenes", "sceneName");
            h.Run(1);
            var after = h.Ok("GetSceneList").Strings("scenes", "sceneName");

            Assert.Equal(["Scene"], during);
            Assert.Equal(["X", "Scene"], after);
            Assert.Equal(["X", "Scene"], h.Events("SceneListChanged").Single().Strings("scenes", "sceneName"));
        }

        [Fact]
        public void Fact10_ThePayloadOfSceneListChangedListsAScenesThatWasRemovedLessThanItsLagAgo_WhenThatIsSwitchedOn()
        {
            foreach (var stale in new[] { false, true })
            {
                var h = new SimHarness(Ideal with { SceneListLag = SimRange.Exactly(1000), StaleSceneListChangedPayload = stale });
                var created = h.Sim.External.CreateScene("X");
                h.Run(1100);
                h.ClearLog();

                h.Sim.External.RemoveScene(created);
                h.Settle();

                var payload = h.Events("SceneListChanged").Single().Strings("scenes", "sceneName");
                Assert.Equal(stale ? new[] { "X", "Scene" } : new[] { "Scene" }, payload);
            }
        }

        [Fact]
        public void Fact10_AnExternalChangeAnnouncesANewSceneTwiceToo()
        {
            var h = new SimHarness(Ideal with { DuplicateSceneCreated = true });

            h.Sim.External.CreateScene("X");
            h.Settle();

            Assert.Equal(["SceneCreated", "SceneListChanged", "SceneCreated"], h.EventNames());
        }

        [Fact]
        public void TheHostileList_AlwaysLags_WhateverTheSeed()
        {
            for (var seed = 1; seed <= 60; seed++)
            {
                var h = new SimHarness(SimQuirks.Hostile, seed);

                h.Sim.External.CreateScene("X");
                var early = h.Ok("GetSceneList").Strings("scenes", "sceneName");
                h.Run(3100);
                var late = h.Ok("GetSceneList").Strings("scenes", "sceneName");

                Assert.DoesNotContain("X", early);
                Assert.Contains("X", late);
            }
        }

        [Fact]
        public void TheHostileEvents_AreOftenDelayedBehindSeveralLaterResponses()
        {
            var seedsWithALateEvent = 0;
            var longest = TimeSpan.Zero;
            for (var seed = 1; seed <= 40; seed++)
            {
                var h = new SimHarness(SimQuirks.Hostile, seed);
                var responses = new List<SimEntry>();
                for (var index = 0; index < 8; index++)
                {
                    responses.Add(h.Ok("CreateScene", new { sceneName = "S" + index }));
                }

                h.Settle();

                var late = false;
                for (var index = 0; index < responses.Count; index++)
                {
                    var announced = h.Log.First(entry => entry.Name == "SceneCreated" && entry.String("sceneName") == "S" + index);
                    var position = h.Log.IndexOf(announced);
                    var laterResponsesBefore = responses.Skip(index + 1).Count(response => h.Log.IndexOf(response) < position);
                    late |= laterResponsesBefore >= 3;
                    longest = TimeSpan.FromTicks(Math.Max(longest.Ticks, announced.At.Ticks - responses[index].At.Ticks));
                }

                seedsWithALateEvent += late ? 1 : 0;
            }

            Assert.True(seedsWithALateEvent >= 5, $"Only {seedsWithALateEvent} of 40 seeds had an event behind three later responses.");
            Assert.True(longest > TimeSpan.FromMilliseconds(100), "A hostile event was never more than 100 ms behind its response.");
        }

        [Fact]
        public void Fact16_TheResultsOfAParallelBatchAreMisordered_ButThoseOfASerialBatchNever()
        {
            object[] requests =
            [
                new { requestType = "GetStudioModeEnabled", requestId = "a" },
                new { requestType = "GetGroupList", requestId = "b" },
                new { requestType = "GetCurrentProgramScene", requestId = "c" },
                new { requestType = "GetInputKindList", requestId = "d" },
            ];
            var inOrder = new[] { "studioModeEnabled", "groups", "currentProgramSceneName,currentProgramSceneUuid,sceneName,sceneUuid", "inputKinds" };
            var misordered = 0;
            for (var seed = 1; seed <= 100; seed++)
            {
                foreach (var executionType in new[] { 0, 1, 2 })
                {
                    var h = new SimHarness(Ideal with { ParallelBatchMisorders = true }, seed);

                    var results = h.CallBatch(requests, executionType: executionType).Data.EnumerateArray().ToArray();

                    var shapes = results.Select(Shape).ToArray();
                    Assert.Equal(["a", "b", "c", "d"], results.Select(result => result.GetProperty("requestId").GetString()!).ToArray());
                    Assert.Equal(inOrder.Order(StringComparer.Ordinal).ToArray(), shapes.Order(StringComparer.Ordinal).ToArray());
                    if (executionType == 2)
                    {
                        misordered += shapes.SequenceEqual(inOrder) ? 0 : 1;
                    }
                    else
                    {
                        Assert.Equal(inOrder, shapes);
                    }
                }
            }

            Assert.InRange(misordered, 80, 100);
        }

        [Fact]
        public void TheTransitionEndsAfterItsDurationPlusItsOverhead()
        {
            var ends = new List<double>();
            for (var seed = 1; seed <= 40; seed++)
            {
                var h = new SimHarness(Ideal with { TransitionDuration = TimeSpan.FromMilliseconds(300), TransitionOverhead = new SimRange(0, 40) }, seed);
                var other = h.Sim.External.CreateScene("Other");
                h.Sim.External.SwitchProgramScene(other);

                h.Settle();

                var ended = h.Events("SceneTransitionEnded").Single();
                string[] transition = ["SceneTransitionStarted", "SceneTransitionVideoEnded", "CurrentProgramSceneChanged", "SceneTransitionEnded"];
                Assert.Equal(transition, h.EventNames().Where(name => transition.Contains(name)).ToArray());
                ends.Add(ended.At.TotalMilliseconds);
            }

            Assert.All(ends, end => Assert.InRange(end, 300, 340));
            Assert.True(ends.Max() - ends.Min() > 20, "The overhead of the transition did not vary.");
        }

        [Fact]
        public void TheClockOnlyMovesWhenAsked_AndNeverGoesBack()
        {
            var sim = new SimObs(Ideal);

            Assert.Equal(TimeSpan.Zero, sim.Clock.Now);
            Assert.Null(sim.NextDeadline);

            sim.Request("CreateScene", new { sceneName = "A" });
            Assert.Equal(TimeSpan.Zero, sim.NextDeadline);
            Assert.Equal(TimeSpan.Zero, sim.Clock.Now);
            Assert.Single(sim.Snapshot().Scenes);

            sim.Advance(TimeSpan.Zero);
            Assert.Equal(2, sim.Snapshot().Scenes.Count);
            Assert.NotEmpty(sim.TakeDue());
            Assert.Null(sim.NextDeadline);

            sim.AdvanceTo(TimeSpan.FromSeconds(5));
            Assert.Equal(TimeSpan.FromSeconds(5), sim.Clock.Now);
            Assert.Throws<ArgumentOutOfRangeException>(() => sim.AdvanceTo(TimeSpan.FromSeconds(4)));
            Assert.Empty(sim.TakeDue());
        }

        [Fact]
        public void TheNextDeadline_IsTheNextThingThatIsScheduled()
        {
            var sim = new SimObs(Ideal with { ResponseLatency = SimRange.Exactly(7), SceneListLag = SimRange.Exactly(30), TransitionDuration = TimeSpan.FromMilliseconds(20) });

            sim.Request("CreateScene", new { sceneName = "A" });
            sim.Advance(TimeSpan.Zero);

            // The response leaves in 7 ms; the scene joins the list in 30 ms.
            Assert.Equal(TimeSpan.FromMilliseconds(7), sim.NextDeadline);
            sim.Advance(TimeSpan.FromMilliseconds(7));
            Assert.NotEmpty(sim.TakeDue());
            Assert.Equal(TimeSpan.FromMilliseconds(30), sim.NextDeadline);
            sim.Advance(TimeSpan.FromMilliseconds(23));
            Assert.Null(sim.NextDeadline);

            sim.External.SwitchProgramScene(sim.Snapshot().Scenes[0].Uuid);
            sim.TakeDue();
            Assert.Equal(TimeSpan.FromMilliseconds(50), sim.NextDeadline);
        }

        [Fact]
        public void TheSameSeedAndTheSameCalls_GiveTheSameMessagesByteForByte()
        {
            foreach (var quirks in new[] { SimQuirks.Real, SimQuirks.Hostile, Ideal })
            {
                Assert.Equal(Script(7, quirks), Script(7, quirks));
            }
        }

        [Fact]
        public void ADifferentSeedGivesDifferentMessages()
        {
            var logs = Enumerable.Range(1, 10).Select(seed => Script(seed, SimQuirks.Real)).ToArray();

            Assert.Equal(10, logs.Distinct().Count());
            Assert.NotEqual(Script(7, SimQuirks.Real), Script(7, SimQuirks.Hostile));
        }

        [Fact]
        public async Task TheSimulationDoesNotLookAtTheClockOfTheMachine()
        {
            static async Task<SimHarness> Play(int pauseMilliseconds)
            {
                var h = new SimHarness(SimQuirks.Hostile, 11);
                h.Send("CreateScene", new { sceneName = "A" });
                await Pause(pauseMilliseconds);
                h.Send("CreateScene", new { sceneName = "B" });
                await Pause(pauseMilliseconds);
                h.Run(5);
                h.Sim.External.CreateScene("C");
                await Pause(pauseMilliseconds);
                h.Settle();
                return h;
            }

            static Task Pause(int milliseconds) => milliseconds == 0 ? Task.CompletedTask : Task.Delay(milliseconds);

            var quick = await Play(0);
            var slow = await Play(25);

            Assert.Equal(quick.Log.Select(entry => entry.Message), slow.Log.Select(entry => entry.Message));
        }

        [Fact]
        public void TheSameSeedGivesTheSameRandomWalk()
        {
            var first = SimWalk.Execute(5, SimQuirks.Hostile, 60, keepMessages: true, out var one);
            var second = SimWalk.Execute(5, SimQuirks.Hostile, 60, keepMessages: true, out var two);
            SimWalk.Execute(6, SimQuirks.Hostile, 60, keepMessages: true, out var other);

            Assert.False(first.Failed || second.Failed);
            Assert.Equal(one!.Messages, two!.Messages);
            Assert.Equal(one.Trace, two.Trace);
            Assert.NotEqual(one.Messages, other!.Messages);
        }

        [Fact]
        public void TheGeneratorGivesTheSameNumbersOnEveryRuntime()
        {
            // Golden values: if these change, every seed that ever failed means something else. They have to be the same on every
            // version of .NET, which is why the simulation has a generator of its own, and not System.Random.
            var random = new SimRandom(1);
            Assert.Equal([801136547209682575UL, 622979572246657876UL, 6350632930977709289UL], new[] { random.NextUInt64(), random.NextUInt64(), random.NextUInt64() });

            random = new SimRandom(1);
            Assert.Equal([75, 76, 89], new[] { random.Next(100), random.Next(100), random.Next(100) });

            random = new SimRandom(2);
            Assert.Equal(["ac9ed770-a8ed-425e-94dc-471433e8476d", "e1ab5b60-d929-49f6-8525-39e9844812d7"], new[] { random.NextGuid().ToString(), random.NextGuid().ToString() });

            Assert.Equal(16552511340266482817UL, new SimRandom(-5).NextUInt64());
            Assert.Equal("f1041e8f-3571-4b1e-943b-16ca9e44a508", new SimObs(Ideal, 1).Snapshot().Scenes[0].Uuid.ToString());
        }

        [Fact]
        public void TheUuidsAreVersion4Uuids_AndTheGeneratorIsFair()
        {
            var random = new SimRandom(9);
            var uuids = Enumerable.Range(0, 1000).Select(_ => random.NextGuid()).ToArray();
            Assert.Equal(1000, uuids.Distinct().Count());
            Assert.All(uuids, uuid =>
            {
                var text = uuid.ToString("D");
                Assert.Equal('4', text[14]);
                Assert.Contains(text[19], "89ab");
            });

            var counts = new int[10];
            for (var index = 0; index < 10_000; index++)
            {
                counts[random.Next(10)]++;
            }

            Assert.All(counts, count => Assert.InRange(count, 850, 1150));
            Assert.True(Enumerable.Range(0, 1000).Select(_ => random.NextDouble()).All(value => value is >= 0 and < 1));
        }
    }
}
