namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Model.Engine;

    /// <summary>
    /// How the settings of an input or a filter are compared: whether two of them are the same, and whether one holds what another one asks for.
    /// </summary>
    public class SettingsValueTests
    {
        private static JsonElement Parse(string text)
        {
            return JsonDocument.Parse(text).RootElement;
        }

        [Theory]
        [InlineData("""{"a":1,"b":2}""", """{"b":2,"a":1}""")]
        [InlineData("""{"a":1}""", """{"a":1.0}""")]
        [InlineData("""{"a":1.50}""", """{"a":1.5}""")]
        [InlineData("""{"a":{"x":[1,2,{"y":true}],"z":null}}""", """{"a":{"z":null,"x":[1,2,{"y":true}]}}""")]
        [InlineData("""{"a":1E2}""", """{"a":100}""")]
        [InlineData("""{}""", """{}""")]
        public void TwoObjectsWithTheSameFields_AreTheSame_InAnyOrderAndWhateverTheNotationOfTheNumbers(string left, string right)
        {
            Assert.True(SettingsValue.DeepEquals(Parse(left), Parse(right)));
            Assert.Equal(SettingsValue.From(Parse(left)), SettingsValue.From(Parse(right)));
            Assert.Equal(SettingsValue.From(Parse(left)).GetHashCode(), SettingsValue.From(Parse(right)).GetHashCode());
        }

        [Theory]
        [InlineData("""{"a":1}""", """{"a":2}""")]
        [InlineData("""{"a":1}""", """{"a":"1"}""")]
        [InlineData("""{"a":1}""", """{"a":1,"b":2}""")]
        [InlineData("""{"a":1,"b":2}""", """{"a":1}""")]
        [InlineData("""{"a":[1,2]}""", """{"a":[2,1]}""")]
        [InlineData("""{"a":[1,2]}""", """{"a":[1,2,3]}""")]
        [InlineData("""{"a":null}""", """{}""")]
        [InlineData("""{"a":true}""", """{"a":false}""")]
        [InlineData("""{"a":{"x":1}}""", """{"a":{"x":2}}""")]
        [InlineData("""{"a":"A"}""", """{"a":"a"}""")]
        public void TwoObjectsThatDiffer_AreNotTheSame(string left, string right)
        {
            Assert.False(SettingsValue.DeepEquals(Parse(left), Parse(right)));
            Assert.NotEqual(SettingsValue.From(Parse(left)), SettingsValue.From(Parse(right)));
        }

        [Fact]
        public void TheSettingsOfARead_AreTheFieldsThatTheClientHandsOver()
        {
            var fields = new Dictionary<string, object> { ["width"] = Parse("640"), ["font"] = Parse("""{"size":48}""") };

            var value = SettingsValue.From(fields);

            Assert.Equal(SettingsValue.From(Parse("""{"font":{"size":48},"width":640}""")), value);
            Assert.Same(SettingsValue.Empty, SettingsValue.From((IReadOnlyDictionary<string, object>?)null));
            Assert.Same(SettingsValue.Empty, SettingsValue.From(new Dictionary<string, object>()));
        }

        [Fact]
        public void AValueThatWasMadeFromAnElement_DoesNotDependOnTheDocumentOfTheElement()
        {
            var document = JsonDocument.Parse("""{"a":1}""");
            var value = SettingsValue.From(document.RootElement);

            document.Dispose();

            Assert.Equal("""{"a":1}""", value.ToString());
        }

        [Theory]
        [InlineData("""{"a":1,"b":2,"c":{"x":1,"y":2}}""", """{"a":1}""", true)]
        [InlineData("""{"a":1,"b":2,"c":{"x":1,"y":2}}""", """{"c":{"x":1,"y":2}}""", true)]
        [InlineData("""{"a":1,"b":2,"c":{"x":1,"y":2}}""", """{"c":{"x":1}}""", false)]
        [InlineData("""{"a":1,"b":2}""", """{}""", true)]
        [InlineData("""{"a":1}""", """{"a":1,"b":2}""", false)]
        [InlineData("""{"a":1}""", """{"a":2}""", false)]
        [InlineData("""{"a":1}""", """{"a":null}""", true)]
        [InlineData("""{"b":1}""", """{"a":null}""", true)]
        [InlineData("""{"c":{"x":1}}""", """{"c":{"x":1,"y":null}}""", true)]
        [InlineData("""{"c":{"x":1,"y":2}}""", """{"c":{"x":1,"y":null}}""", false)]
        [InlineData("""{"c":{"x":1,"d":{"p":1,"q":2}}}""", """{"c":{"x":1,"d":{"p":1}}}""", false)]
        [InlineData("""{"c":{"x":1}}""", """{"c":null}""", true)]
        [InlineData("""{"c":{"x":1}}""", """{"c":{"x":2}}""", false)]
        [InlineData("""{"c":[1,2,3]}""", """{"c":[1,2]}""", false)]
        [InlineData("""{"c":[1,2,3]}""", """{"c":[1,2,3]}""", true)]
        public void AnObjectHoldsWhatAnotherOneAsksFor_WhenEveryFieldIsThereWithTheValue_AnObjectThatIsAValueIsHeldWhole_AndANullAsksForNothing(string whole, string subset, bool expected)
        {
            Assert.Equal(expected, SettingsValue.Contains(Parse(whole), Parse(subset)));
        }

        [Theory]
        [InlineData("""{"a":1,"b":null}""", """{"a":1}""")]
        [InlineData("""{"a":null}""", """{}""")]
        [InlineData("""{"a":{"b":null,"c":2},"d":[{"e":null,"f":3}]}""", """{"a":{"c":2},"d":[{"f":3}]}""")]
        [InlineData("""{"a":{"b":1}}""", """{"a":{"b":1}}""")]
        public void TheNullFieldsAreTakenOut_AtEveryDepth_AsOBSStudioDoesWhenItKeepsSettings(string given, string expected)
        {
            Assert.True(SettingsValue.DeepEquals(Parse(expected), SettingsValue.WithoutNulls(Parse(given))), SettingsValue.WithoutNulls(Parse(given)).GetRawText());
        }

        [Fact]
        public void ANumberThatDoesNotFitADecimal_IsComparedByItsValueOrItsText()
        {
            Assert.True(SettingsValue.DeepEquals(Parse("""{"a":1e300}"""), Parse("""{"a":1E300}""")));
            Assert.False(SettingsValue.DeepEquals(Parse("""{"a":1e300}"""), Parse("""{"a":2e300}""")));
            Assert.True(SettingsValue.DeepEquals(Parse("""{"a":18446744073709551615}"""), Parse("""{"a":18446744073709551615}""")));
            Assert.False(SettingsValue.DeepEquals(Parse("""{"a":18446744073709551615}"""), Parse("""{"a":18446744073709551614}""")));
        }

        [Theory]
        [InlineData("0", "1e-29", false)]
        [InlineData("1", "1.0", true)]
        [InlineData("100", "1e2", true)]
        [InlineData("0.1", "0.10", true)]
        [InlineData("79228162514264337593543950335", "79228162514264337593543950336", false)]
        [InlineData("0.12345678901234567890123456789", "0.12345678901234567890123456788", false)]
        [InlineData("-0", "0", true)]
        public void TwoNumbers_AreTheSameWhenTheyHaveTheSameValue_AtAnyPrecision(string left, string right, bool expected)
        {
            Assert.Equal(expected, SettingsValue.DeepEquals(Parse($$"""{"a":{{left}}}"""), Parse($$"""{"a":{{right}}}""")));
        }
    }
}
