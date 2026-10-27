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
        public void Compare_ComparesTheOnlyFieldOfAResponse_WithTheValueTheClientGivesBack()
        {
            Assert.True(JsonShape.Compare(Json("""{"outputActive":true}"""), Json("true")).IsIdentical);
            Assert.Equal(
                new[] { "$: OBS Studio sent string, the client gives number" },
                JsonShape.Compare(Json("""{"name":"x"}"""), Json("5")).TypeDifferences);
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
