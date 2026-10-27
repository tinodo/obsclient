namespace OBSStudioClient.Tests.TestAppTests
{
    using OBSStudioClient.TestApp.Analysis;
    using System.Text.Json;

    /// <summary>
    /// The test app compares what OBS Studio sent with what the client made of it; these tests check that comparison.
    /// </summary>
    public class JsonShapeTests
    {
        private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

        [Fact]
        public void Compare_WithTheSameShape_IsIdentical_WhateverTheValues()
        {
            var comparison = JsonShape.Compare(Json("""{"a":1,"b":{"c":"x"}}"""), Json("""{"a":2,"b":{"c":"y"}}"""));

            Assert.True(comparison.IsIdentical);
        }

        [Fact]
        public void Compare_ReportsAMemberThatOnlyObsStudioSent_WithItsKind()
        {
            var comparison = JsonShape.Compare(Json("""{"a":1,"extra":{"x":true}}"""), Json("""{"a":1}"""));

            Assert.Equal(new[] { "$.extra (object)" }, comparison.OnlyInObs);
            Assert.Empty(comparison.OnlyInClient);
            Assert.Empty(comparison.TypeDifferences);
        }

        [Fact]
        public void Compare_ReportsAMemberThatOnlyTheClientGives()
        {
            var comparison = JsonShape.Compare(Json("""{"a":1}"""), Json("""{"a":1,"computed":"x"}"""));

            Assert.Equal(new[] { "$.computed (string)" }, comparison.OnlyInClient);
            Assert.Empty(comparison.OnlyInObs);
        }

        [Fact]
        public void Compare_ReportsAMemberThatIsAnotherKindOfValue()
        {
            var comparison = JsonShape.Compare(Json("""{"a":"1"}"""), Json("""{"a":1}"""));

            Assert.Equal(new[] { "$.a: OBS Studio sent string, the client gives number" }, comparison.TypeDifferences);
        }

        [Fact]
        public void Compare_TreatsNullAsNoValue_NotAsAnotherKind()
        {
            var comparison = JsonShape.Compare(Json("""{"a":null,"b":"x"}"""), Json("""{"a":"x","b":null}"""));

            Assert.True(comparison.IsIdentical);
        }

        [Fact]
        public void Compare_ComparesEveryItemOfAnArray_AndReportsAMemberOnce()
        {
            var comparison = JsonShape.Compare(
                Json("""{"scenes":[{"n":"a","u":"x"},{"n":"b","u":"y"}]}"""),
                Json("""{"scenes":[{"n":"a"},{"n":"b"}]}"""));

            Assert.Equal(new[] { "$.scenes[].u (string)" }, comparison.OnlyInObs);
        }

        [Fact]
        public void Compare_ReportsAnArrayOfAnotherLength()
        {
            var comparison = JsonShape.Compare(Json("""{"a":[1,2,3]}"""), Json("""{"a":[1]}"""));

            Assert.Equal(new[] { "$.a: OBS Studio sent 3 items, the client gives 1" }, comparison.TypeDifferences);
        }

        [Fact]
        public void Compare_ComparesWhatItIsGiven_SoTheCallerPicksTheMemberThatTheClientReturns()
        {
            var response = Json("""{"sceneItemTransform":{"positionX":1.5,"positionY":2.5}}""");
            var returned = Json("""{"positionX":1.5,"positionY":2.5}""");

            Assert.True(JsonShape.Compare(response.GetProperty("sceneItemTransform"), returned).IsIdentical);
            Assert.False(JsonShape.Compare(response, returned).IsIdentical);
        }

        [Fact]
        public void Compare_OfAValueTheClientGivesBack_ReportsAnotherKind()
        {
            Assert.Equal(
                new[] { "$: OBS Studio sent string, the client gives number" },
                JsonShape.Compare(Json("\"x\""), Json("5")).TypeDifferences);
            Assert.True(JsonShape.Compare(Json("true"), Json("false")).IsIdentical);
        }

        [Fact]
        public void Describe_SaysSoWhenTheShapesAreIdentical()
        {
            var comparison = JsonShape.Compare(Json("""{"a":1}"""), Json("""{"a":2}"""));

            Assert.StartsWith("Identical:", comparison.Describe(), StringComparison.Ordinal);
        }

        [Fact]
        public void Describe_ListsTheDifferencesInASectionForEachKind_WithTheirNumber()
        {
            var comparison = JsonShape.Compare(Json("""{"a":"1","extra":{"x":true}}"""), Json("""{"a":1,"computed":"x"}"""));

            var description = comparison.Describe();

            Assert.Contains("OBS Studio sent these, and the client does not return them (1):", description, StringComparison.Ordinal);
            Assert.Contains("  $.extra (object)", description, StringComparison.Ordinal);
            Assert.Contains("The client returns these, and OBS Studio did not send them (1):", description, StringComparison.Ordinal);
            Assert.Contains("  $.computed (string)", description, StringComparison.Ordinal);
            Assert.Contains("These are another kind of value (1):", description, StringComparison.Ordinal);
            Assert.Contains("  $.a: OBS Studio sent string, the client gives number", description, StringComparison.Ordinal);
        }

        [Fact]
        public void Describe_StartsEveryLineWithTheIndent()
        {
            var comparison = JsonShape.Compare(Json("""{"extra":1}"""), Json("{}"));

            var lines = comparison.Describe("> ").Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(new[] { "> OBS Studio sent these, and the client does not return them (1):", ">   $.extra (number)" }, lines);
        }

        [Fact]
        public void Observe_RecordsEveryPathWithTheKindsOfValueThatWereSeen()
        {
            var observations = new SortedDictionary<string, PathObservation>(StringComparer.Ordinal);

            JsonShape.Observe("$", Json("""{"a":[1,"x"],"b":null}"""), observations);
            JsonShape.Observe("$", Json("""{"a":[],"b":"text"}"""), observations);

            Assert.Equal(new[] { "$", "$.a", "$.a[]", "$.b" }, observations.Keys);
            Assert.Equal(new[] { "array" }, observations["$.a"].Types);
            Assert.Equal(new[] { "number", "string" }, observations["$.a[]"].Types);
            Assert.Equal(new[] { "null", "string" }, observations["$.b"].Types);
            Assert.Equal(2, observations["$"].Seen);
            Assert.Equal(2, observations["$.a[]"].Seen);
        }
    }
}
