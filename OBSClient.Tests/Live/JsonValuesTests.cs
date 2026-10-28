namespace OBSStudioClient.Tests.Live
{
    using System.Text.Json;

    /// <summary>The comparison by value that the live tests make of what OBS Studio sent and what the client gives back. It needs no OBS Studio.</summary>
    public class JsonValuesTests
    {
        private static IReadOnlyList<string> Differences(string sent, string returned, params string[] nullsReadAsEmpty) =>
            JsonValues.Differences(JsonDocument.Parse(sent).RootElement, JsonDocument.Parse(returned).RootElement, nullsReadAsEmpty);

        [Fact]
        public void TheSameValues_AreTheSame_AlsoWhenAFloatIsNotAsLongAsADouble()
        {
            var sent = """{"name":"Scene","uuid":"7e5df8fc-cbfc-427f-b9a3-1cb02c3b3a93","index":3,"scale":0.20000000298023224,"big":123456789.0,"on":true,"none":null,"items":[{"id":1},{"id":2}]}""";
            var returned = """{"name":"Scene","uuid":"7e5df8fc-cbfc-427f-b9a3-1cb02c3b3a93","index":3,"scale":0.2,"big":123456792,"on":true,"none":null,"items":[{"id":1},{"id":2}]}""";

            Assert.Empty(Differences(sent, returned));
        }

        [Fact]
        public void TwoMembersThatWereSwapped_AreFoundByTheirPaths()
        {
            var differences = Differences("""{"oldSceneName":"a","sceneName":"b"}""", """{"oldSceneName":"b","sceneName":"a"}""");

            Assert.Equal(2, differences.Count);
            Assert.StartsWith("$.oldSceneName:", differences[0], StringComparison.Ordinal);
            Assert.StartsWith("$.sceneName:", differences[1], StringComparison.Ordinal);
        }

        [Fact]
        public void ANumberThatChanged_AFlagThatChangedAndANullThatBecameAValue_AreFound()
        {
            var differences = Differences("""{"x":100.0,"on":true,"parent":null}""", """{"x":150.0,"on":false,"parent":"scene"}""");

            Assert.Equal(["$.x", "$.on", "$.parent"], differences.Select(difference => difference[..difference.IndexOf(':')]));
        }

        [Fact]
        public void AnItemOfAnArrayThatDiffers_IsFoundByItsIndex_AndALengthThatDiffers_IsFoundToo()
        {
            var item = Differences("""{"items":[{"name":"a"},{"name":"b"}]}""", """{"items":[{"name":"a"},{"name":"c"}]}""");
            var length = Differences("""{"items":[1,2,3]}""", """{"items":[1,2]}""");

            Assert.Single(item);
            Assert.StartsWith("$.items[1].name:", item[0], StringComparison.Ordinal);
            Assert.Single(length);
            Assert.Contains("3 items", length[0], StringComparison.Ordinal);
        }

        [Fact]
        public void ANullThatTheClientReadsAsEmptyOnPurpose_IsTheSameAsEmpty_AtThatPathOnly()
        {
            var sent = """{"transitionConfigurable":false,"transitionSettings":null,"other":null}""";

            var asEmptyObject = Differences(sent, """{"transitionConfigurable":false,"transitionSettings":{},"other":null}""", "$.transitionSettings");
            var asEmptyArray = Differences("""{"transitionSettings":null}""", """{"transitionSettings":[]}""", "$.transitionSettings");
            var atAnotherPath = Differences(sent, """{"transitionConfigurable":false,"transitionSettings":{},"other":{}}""", "$.transitionSettings");
            var withoutTheAllowance = Differences(sent, """{"transitionConfigurable":false,"transitionSettings":{},"other":null}""");

            Assert.Empty(asEmptyObject);
            Assert.Empty(asEmptyArray);
            Assert.Single(atAnotherPath);
            Assert.StartsWith("$.other:", atAnotherPath[0], StringComparison.Ordinal);
            Assert.Single(withoutTheAllowance);
            Assert.StartsWith("$.transitionSettings:", withoutTheAllowance[0], StringComparison.Ordinal);
        }

        [Fact]
        public void ANullThatTheClientReadsAsSomethingMade_IsFound_EvenAtThePathOfTheAllowance()
        {
            var differences = Differences("""{"transitionSettings":null}""", """{"transitionSettings":{"made":"up"}}""", "$.transitionSettings");

            Assert.Single(differences);
            Assert.StartsWith("$.transitionSettings:", differences[0], StringComparison.Ordinal);
        }

        [Fact]
        public void AMemberThatIsMissing_IsNotReportedHere_BecauseTheShapeComparisonDoesThat()
        {
            Assert.Empty(Differences("""{"kept":1,"obsoleteAlias":"x"}""", """{"kept":1}"""));
        }

        [Fact]
        public void AdjacentCounters_AreNotTheSame_WhenOBSStudioSentThemAsIntegers()
        {
            // 1000000, 1000001 and 16777217 are exactly what a float or a rounding allowance cannot tell apart, or can: an integer is exact.
            var differences = Differences(
                """{"renderTotalFrames":1000000,"outputTotalFrames":16777216,"sceneItemId":7}""",
                """{"renderTotalFrames":1000001,"outputTotalFrames":16777217,"sceneItemId":8}""");

            Assert.Equal(["$.renderTotalFrames", "$.outputTotalFrames", "$.sceneItemId"], differences.Select(difference => difference[..difference.IndexOf(':')]));
        }

        [Fact]
        public void AnIntegerThatLostItsPrecision_IsFound_ButTheSameIntegerWrittenAnotherWay_IsNot()
        {
            var lost = Differences("""{"bytes":123456789}""", """{"bytes":123456792}""");
            var written = Differences("""{"bytes":5,"frames":1000000}""", """{"bytes":5.0,"frames":1E+6}""");

            Assert.Single(lost);
            Assert.Empty(written);
        }

        [Fact]
        public void AFloatingPointValue_IsTheSameWhenItIsTheSameFloat_AndNotWhenItIsAnotherOne_AlsoWhenItIsSmall()
        {
            Assert.Empty(Differences("""{"scale":0.20000000298023224}""", """{"scale":0.2}"""));
            Assert.Single(Differences("""{"scale":0.2}""", """{"scale":0.2000001}"""));
            Assert.Single(Differences("""{"volume":0.000002}""", """{"volume":0.000001}"""));
        }

        [Fact]
        public void ANumberThatBecameAnotherKindOfValue_IsFound()
        {
            var differences = Differences("""{"count":3,"ratio":0.5}""", """{"count":"3","ratio":null}""");

            Assert.Equal(["$.count", "$.ratio"], differences.Select(difference => difference[..difference.IndexOf(':')]));
        }

        [Fact]
        public void ABigNumberThatIsOff_IsFound_AndTheContentOfAStringIsNeverInTheMessage()
        {
            var secret = "stream-key-that-must-not-be-shown";

            var differences = Differences($$"""{"key":"{{secret}}","bytes":1000000}""", """{"key":"something else","bytes":1100000}""");

            Assert.Equal(2, differences.Count);
            Assert.DoesNotContain(secret, string.Concat(differences), StringComparison.Ordinal);
            Assert.DoesNotContain("something else", string.Concat(differences), StringComparison.Ordinal);
        }
    }
}
