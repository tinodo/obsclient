namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json;
    using OBSStudioClient.Tests.Conformance;
    using OBSStudioClient.TestApp.Analysis;

    /// <summary>
    /// Every message that the simulation sends has the shape of what a real OBS Studio 33.0.0 with obs-websocket 5.7.6 sends: event names,
    /// categories, member names and kinds of value from the protocol snapshot of the tests, and the members of the nested objects (which the
    /// protocol only describes in prose) from the capture of a real OBS Studio. What OBS Studio sends beyond the protocol is listed in
    /// <see cref="SimShape"/>, and nothing else is allowed.
    /// </summary>
    public class SimShapeTests
    {
        private static readonly Lazy<(List<string> Problems, HashSet<string> Events, HashSet<string> Requests, int Messages)> Walks = new(Run);

        private static (List<string> Problems, HashSet<string> Events, HashSet<string> Requests, int Messages) Run()
        {
            var shape = SimShape.Instance;
            var problems = new List<string>();
            var events = new HashSet<string>(StringComparer.Ordinal);
            var requests = new HashSet<string>(StringComparer.Ordinal);
            var messages = 0;
            var walks = new List<(SimQuirks Quirks, int Seed)>();
            walks.AddRange(Enumerable.Range(1, 70).Select(seed => (SimQuirks.Hostile, seed)));
            walks.AddRange(Enumerable.Range(1, 15).Select(seed => (SimQuirks.Real, seed)));
            walks.AddRange(Enumerable.Range(1, 15).Select(seed => (SimQuirks.Ideal, seed)));

            // The walks are independent, so they run side by side (not more at a time than there are processors, as in SimInvariantsTests); what they find is put together under a lock.
            Parallel.ForEach(walks, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, entry =>
            {
                var result = SimWalk.Execute(entry.Seed, entry.Quirks, 60, keepMessages: true, out var walk);
                var found = new List<string>(result.Invariants);
                var seenEvents = new HashSet<string>(StringComparer.Ordinal);
                var seenRequests = new HashSet<string>(StringComparer.Ordinal);
                foreach (var message in walk!.Messages)
                {
                    found.AddRange(shape.Check(message, walk.ParallelBatches).Select(problem => $"seed {entry.Seed}: {problem}{Environment.NewLine}    {message.Json}"));
                    using var document = JsonDocument.Parse(message.Json);
                    var data = document.RootElement.GetProperty("d");
                    if (message.Op == SimOp.Event)
                    {
                        seenEvents.Add(data.GetProperty("eventType").GetString()!);
                    }
                    else if (message.Op == SimOp.RequestResponse && data.GetProperty("requestStatus").GetProperty("result").GetBoolean())
                    {
                        seenRequests.Add(data.GetProperty("requestType").GetString()!);
                    }
                }

                lock (problems)
                {
                    problems.AddRange(found);
                    events.UnionWith(seenEvents);
                    requests.UnionWith(seenRequests);
                    messages += walk.Messages.Count;
                }
            });

            return (problems, events, requests, messages);
        }

        [Fact]
        public void EveryMessageOfTheRandomWalks_HasTheShapeThatARealObsStudioSends()
        {
            // Events: the name, the intent, the members and their kinds; responses: the status and the members of the data; batches.
            Assert.True(Walks.Value.Messages > 5_000, "The walks sent too few messages to prove anything.");
            SimAssert.NoProblems(Walks.Value.Problems, "problems with the shape of the messages of the random walks", show: 3);
        }

        [Fact]
        public void TheRandomWalks_SendEveryEventAndEveryResponse_ThatIsChecked()
        {
            var missingEvents = SimShape.EventCategories.Keys.Where(name => !Walks.Value.Events.Contains(name)).ToArray();
            var missingResponses = SimRequests.Implemented.Where(name => !Walks.Value.Requests.Contains(name)).ToArray();
            Assert.True(missingEvents.Length == 0, "Events that the walks never sent: " + string.Join(", ", missingEvents));
            Assert.True(missingResponses.Length == 0, "Requests that the walks never saw succeed: " + string.Join(", ", missingResponses));
        }

        [Fact]
        public void TheEventsThatTheSimulationSends_AreExactlyTheOnesOfTheTable()
        {
            Assert.Equal(SimShape.EventCategories.Keys.Order(StringComparer.Ordinal), Walks.Value.Events.Order(StringComparer.Ordinal));
        }

        [Fact]
        public void EveryCategoryOfTheTable_IsOneOfTheProtocol()
        {
            var categories = ProtocolSnapshot.Current.Enums["EventSubscription"];
            Assert.All(SimShape.EventCategories, entry => Assert.True(categories.ContainsKey(entry.Value) && categories[entry.Value] is > 0, $"{entry.Key}: {entry.Value}"));
            Assert.All(SimShape.EventCategories.Keys, name => Assert.True(ProtocolSnapshot.Current.Events.ContainsKey(name), name));
        }

        [Fact]
        public void EveryRequestThatTheSimulationAnswers_IsARequestOfTheProtocol()
        {
            Assert.All(SimRequests.Implemented, name => Assert.True(ProtocolSnapshot.Current.Requests.ContainsKey(name), name));
        }

        [Fact]
        public void WhatIsAllowedBeyondTheProtocol_IsReallyInTheCaptureAndReallyNotInTheProtocol()
        {
            var shape = SimShape.Instance;
            foreach (var (type, members) in SimShape.ExtraResponseMembers)
            {
                var documented = ProtocolSnapshot.Current.Requests[type].ResponseFields.Select(field => field.Name).ToArray();
                Assert.All(members, member => Assert.DoesNotContain(member, documented));
                if (SimShape.ExtraResponseMembersNotCaptured.Contains(type))
                {
                    // The capture has to be the one that holds them as soon as it can: this is the way out for the answers that it lacks, and no more.
                    Assert.DoesNotContain(type, shape.CapturedRequests);
                    continue;
                }

                Assert.Contains(type, shape.CapturedRequests);
                Assert.All(members, member => Assert.True(shape.CapturedHas(type, "$." + member, isEvent: false), $"{type}.{member} is not in the capture"));
            }

            foreach (var (type, members) in SimShape.ExtraEventMembers)
            {
                var documented = ProtocolSnapshot.Current.Events[type].Select(field => field.Name).ToArray();
                Assert.All(members, member => Assert.DoesNotContain(member, documented));
                Assert.All(members, member => Assert.True(shape.CapturedHas(type, "$." + member, isEvent: true), $"{type}.{member} is not in the capture"));
            }

            foreach (var key in SimShape.ExtraTypes.Keys)
            {
                var (type, path) = (key[..key.IndexOf(':')], key[(key.IndexOf(':') + 1)..]);
                Assert.True(shape.CapturedHas(type, path, isEvent: false), $"{key} is not in the capture");
            }
        }

        [Theory]
        [InlineData("""{"d":{"eventData":{"isGroup":false,"sceneName":"A","sceneUuid":"6c69626f-6273-4c00-9d88-c5136d61696e"},"eventIntent":4,"eventType":"SceneCreated"},"op":5}""", null)]
        [InlineData("""{"d":{"eventData":{"isGroup":false,"sceneName":"A","sceneUuid":"6c69626f-6273-4c00-9d88-c5136d61696e"},"eventIntent":4,"eventType":"SceneBorn"},"op":5}""", "is not an event")]
        [InlineData("""{"d":{"eventData":{"isGroup":false,"sceneName":"A","sceneUuid":"6c69626f-6273-4c00-9d88-c5136d61696e"},"eventIntent":8,"eventType":"SceneCreated"},"op":5}""", "intent")]
        [InlineData("""{"d":{"eventData":{"isGroup":false,"sceneName":"A"},"eventIntent":4,"eventType":"SceneCreated"},"op":5}""", "no member `sceneUuid`")]
        [InlineData("""{"d":{"eventData":{"isGroup":false,"sceneName":"A","sceneUuid":"6c69626f-6273-4c00-9d88-c5136d61696e","zzz":1},"eventIntent":4,"eventType":"SceneCreated"},"op":5}""", "member `zzz`")]
        [InlineData("""{"d":{"eventData":{"isGroup":false,"sceneName":5,"sceneUuid":"6c69626f-6273-4c00-9d88-c5136d61696e"},"eventIntent":4,"eventType":"SceneCreated"},"op":5}""", "`sceneName` is number")]
        [InlineData("""{"d":{"eventData":{"sceneUuid":"6c69626f-6273-4c00-9d88-c5136d61696e","sceneName":"A","isGroup":false},"eventIntent":4,"eventType":"SceneCreated"},"op":5}""", "not sorted")]
        [InlineData("""{"d":{"eventData":{"isGroup":false,"sceneName":"A","sceneUuid":"6C69626F-6273-4C00-9D88-C5136D61696E"},"eventIntent":4,"eventType":"SceneCreated"},"op":5}""", "not a UUID in lower case")]
        [InlineData("""{"d":{"eventData":{"sceneItemId":1.5,"sceneItemIndex":0,"sceneName":"A","sceneUuid":"6c69626f-6273-4c00-9d88-c5136d61696e","sourceName":"B","sourceUuid":"6c69626f-6273-4c00-9d88-c5136d61696f"},"eventIntent":128,"eventType":"SceneItemCreated"},"op":5}""", "not a whole number")]
        [InlineData("""{"d":{"eventData":{"scenes":[{"sceneIndex":0,"sceneName":"A","sceneUuid":"6c69626f-6273-4c00-9d88-c5136d61696e","extra":1}]},"eventIntent":4,"eventType":"SceneListChanged"},"op":5}""", "not in the capture")]
        [InlineData("""{"d":{"eventData":{"scenes":[{"sceneIndex":0,"sceneName":3,"sceneUuid":"6c69626f-6273-4c00-9d88-c5136d61696e"}]},"eventIntent":4,"eventType":"SceneListChanged"},"op":5}""", "capture of a real OBS Studio has string")]
        [InlineData("""{"d":{"requestId":"a","requestStatus":{"code":100,"result":true},"requestType":"GetStudioModeEnabled","responseData":{"studioModeEnabled":false}},"op":7}""", null)]
        [InlineData("""{"d":{"requestId":"a","requestStatus":{"code":100,"result":true},"requestType":"GetStudioModeEnabled"},"op":7}""", "has no response data")]
        [InlineData("""{"d":{"requestId":"a","requestStatus":{"code":123,"result":false},"requestType":"GetStudioModeEnabled"},"op":7}""", "not one of the protocol")]
        [InlineData("""{"d":{"requestId":"a","requestStatus":{"code":100,"comment":"fine","result":true},"requestType":"GetStudioModeEnabled","responseData":{"studioModeEnabled":false}},"op":7}""", "a success has a comment")]
        [InlineData("""{"d":{"requestId":"a","requestStatus":{"code":600,"result":false},"requestType":"GetStudioModeEnabled","responseData":{"studioModeEnabled":false}},"op":7}""", "a failure has response data")]
        [InlineData("""{"d":{"requestId":"a","requestStatus":{"code":100,"result":false},"requestType":"GetStudioModeEnabled","responseData":{"studioModeEnabled":false}},"op":7}""", "result is False")]
        [InlineData("""{"d":{"requestId":"a","requestStatus":{"code":100,"result":true},"requestType":"RemoveScene","responseData":{"x":1}},"op":7}""", "lists no fields")]
        [InlineData("""{"d":{"requestId":"a","requestStatus":{"code":100,"result":true},"requestType":"GetStudioModeEnabled","responseData":{"studioModeEnabled":"no"}},"op":7}""", "`studioModeEnabled` is string")]
        [InlineData("""{"d":{"requestId":"a","requestStatus":{"code":100,"result":true},"requestType":"GetInputMute","responseData":{"inputMuted":false,"other":1}},"op":7}""", "member `other`")]
        [InlineData("""{"d":{"requestId":"a","requestStatus":{"code":100,"result":true},"requestType":"NoSuchRequest","responseData":{}},"op":7}""", "is not a request")]
        [InlineData("""{"d":{"requestId":"a","results":[{"requestType":"RemoveScene"}]},"op":9}""", "has no member `requestStatus`")]
        [InlineData("""{"d":{"requestId":"a","results":[{"requestStatus":{"code":100,"result":true},"requestType":"RemoveScene"}]},"op":9}""", null)]
        [InlineData("""{"d":{"requestId":"a"},"op":9}""", "has no member `results`")]
        [InlineData("""{"d":{"requestId":"a"},"op":6}""", "not one that OBS Studio sends")]
        public void TheValidator_FindsWhatIsWrongWithAMessage(string json, string? expectedProblem)
        {
            using var parsed = JsonDocument.Parse(json);
            var op = parsed.RootElement.GetProperty("op").GetInt32();

            var problems = SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, op, json));

            if (expectedProblem is null)
            {
                Assert.Empty(problems);
            }
            else
            {
                Assert.Contains(problems, problem => problem.Contains(expectedProblem, StringComparison.Ordinal));
            }
        }

        private const string SceneItem = """{"inputKind":"color_source_v3","isGroup":null,"sceneItemBlendMode":"OBS_BLEND_NORMAL","sceneItemEnabled":true,"sceneItemId":1,"sceneItemIndex":0,"sceneItemLocked":false,"sceneItemTransform":{"alignment":5,"boundsAlignment":0,"boundsHeight":0.0,"boundsType":"OBS_BOUNDS_NONE","boundsWidth":0.0,"cropBottom":0,"cropLeft":0,"cropRight":0,"cropToBounds":false,"cropTop":0,"height":1.0,"positionX":0.0,"positionY":0.0,"rotation":0.0,"scaleX":1.0,"scaleY":1.0,"sourceHeight":1.0,"sourceWidth":1.0,"width":1.0},"sourceName":"A","sourceType":"OBS_SOURCE_TYPE_INPUT","sourceUuid":"6c69626f-6273-4c00-9d88-c5136d61696e"}""";

        private static string SceneItemsMessage(string body)
        {
            return """{"d":{"requestId":"a","requestStatus":{"code":100,"result":true},"requestType":"GetSceneItemList","responseData":{"sceneItems":[""" + body + """]}},"op":7}""";
        }

        [Fact]
        public void AResponseWithoutTheIdOfItsRequest_IsFound_ButAResultOfABatchMayLeaveItOut()
        {
            const string standalone = """{"d":{"requestStatus":{"code":100,"result":true},"requestType":"GetStudioModeEnabled","responseData":{"studioModeEnabled":false}},"op":7}""";
            const string withNullId = """{"d":{"requestId":null,"requestStatus":{"code":100,"result":true},"requestType":"GetStudioModeEnabled","responseData":{"studioModeEnabled":false}},"op":7}""";
            const string batch = """{"d":{"requestId":"a","results":[{"requestStatus":{"code":100,"result":true},"requestType":"GetStudioModeEnabled","responseData":{"studioModeEnabled":false}}]},"op":9}""";

            Assert.Contains(SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, standalone)), problem => problem.Contains("has no member `requestId`", StringComparison.Ordinal));
            Assert.Empty(SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, withNullId)));
            Assert.Empty(SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestBatchResponse, batch)));
        }

        [Fact]
        public void TheIdOfABatchCanBeOfAnyKind_WhenSomeBatchesAreNotChecked()
        {
            const string numeric = """{"d":{"requestId":7,"results":[{"requestStatus":{"code":100,"result":true},"requestType":"GetStudioModeEnabled","responseData":{"studioModeEnabled":false}}]},"op":9}""";
            const string wrongType = """{"d":{"requestId":null,"results":[{"requestStatus":{"code":100,"result":true},"requestType":"NoSuchRequest","responseData":{}}]},"op":9}""";
            ISet<string> unordered = new HashSet<string>(["other"]);

            Assert.Empty(SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestBatchResponse, numeric), unordered));
            Assert.Contains(SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestBatchResponse, wrongType), unordered), problem => problem.Contains("is not a request", StringComparison.Ordinal));
        }

        [Fact]
        public void ANestedObjectOfAnotherShapeThanTheCapture_IsFound()
        {
            const string item = SceneItem;
            static string Message(string body) => SceneItemsMessage(body);
            var valid = SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, Message(item)));
            var withoutBlendMode = SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, Message(item.Replace("\"sceneItemBlendMode\":\"OBS_BLEND_NORMAL\",", string.Empty, StringComparison.Ordinal))));
            var strangeTransform = SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, Message(item.Replace("\"alignment\":5,", "\"alignment\":\"left\",", StringComparison.Ordinal))));
            var movedMember = SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, Message(item.Replace("\"cropTop\":0,", "\"cropTop\":0,\"cropMiddle\":0,", StringComparison.Ordinal))));

            Assert.Empty(valid);
            Assert.Contains(withoutBlendMode, problem => problem.Contains("has no $.sceneItems[].sceneItemBlendMode", StringComparison.Ordinal));
            Assert.Contains(strangeTransform, problem => problem.Contains("alignment is string", StringComparison.Ordinal));
            Assert.Contains(movedMember, problem => problem.Contains("cropMiddle", StringComparison.Ordinal));
        }

        [Fact]
        public void AnObjectThatLacksWhatAnotherObjectOfTheArrayHas_IsFound()
        {
            var complete = SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, SceneItemsMessage(SceneItem + "," + SceneItem)));
            var emptyAfter = SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, SceneItemsMessage(SceneItem + ",{}")));
            var emptyBefore = SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, SceneItemsMessage("{}," + SceneItem)));
            var withoutId = SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, SceneItemsMessage(SceneItem + "," + SceneItem.Replace("\"sceneItemId\":1,", string.Empty, StringComparison.Ordinal))));
            var withoutScale = SimShape.Instance.Check(new SimMessage(1, TimeSpan.Zero, SimOp.RequestResponse, SceneItemsMessage(SceneItem + "," + SceneItem.Replace("\"scaleX\":1.0,", string.Empty, StringComparison.Ordinal))));

            Assert.Empty(complete);
            Assert.Contains(emptyAfter, problem => problem.Contains("an object at $.sceneItems[] has no $.sceneItems[].sceneItemId", StringComparison.Ordinal));
            Assert.Contains(emptyBefore, problem => problem.Contains("an object at $.sceneItems[] has no $.sceneItems[].sceneItemId", StringComparison.Ordinal));
            Assert.Contains(withoutId, problem => problem.Contains("an object at $.sceneItems[] has no $.sceneItems[].sceneItemId", StringComparison.Ordinal));
            Assert.Contains(withoutScale, problem => problem.Contains("an object at $.sceneItems[].sceneItemTransform has no $.sceneItems[].sceneItemTransform.scaleX", StringComparison.Ordinal));
        }
    }
}
