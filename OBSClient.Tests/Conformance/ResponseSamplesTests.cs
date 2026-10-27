namespace OBSStudioClient.Tests.Conformance
{
    /// <summary>
    /// Guards the responses that the conformance tests feed to the client. A sample that degrades (an empty array, a
    /// Guid that is not one) would let a wrongly typed response property pass unnoticed.
    /// </summary>
    public class ResponseSamplesTests
    {
        private static readonly ResponseSamples Samples = new(ProtocolSnapshot.Current);

        [Fact]
        public void AnArrayOfStrings_HasOneStringElement()
        {
            var groups = Assert.IsType<object[]>(Samples.For("GetGroupList")["groups"]);

            Assert.Equal("sample", Assert.Single(groups));
        }

        [Fact]
        public void AnArrayOfObjects_HasOneObjectElement()
        {
            var scenes = Assert.IsType<object[]>(Samples.For("GetSceneList")["scenes"]);

            Assert.IsType<Dictionary<string, object?>>(Assert.Single(scenes));
        }

        [Fact]
        public void AFieldThatTheClientModelsAsAGuid_GetsAGuid()
        {
            var uuid = Assert.IsType<string>(Samples.For("GetSceneList")["currentProgramSceneUuid"]);

            Assert.True(Guid.TryParse(uuid, out _));
        }

        [Fact]
        public void ANumberThatTheClientModelsAsAGuid_StaysANumber()
        {
            // A Guid string would hide a client that models a documented number as a Guid.
            Assert.Equal(1, ResponseSamples.SampleFor("Number", typeof(Guid), "field"));
        }

        [Fact]
        public void ABooleanThatTheClientModelsAsAnEnum_StaysABoolean()
        {
            Assert.Equal(true, ResponseSamples.SampleFor("Boolean", typeof(DayOfWeek), "field"));
        }

        [Fact]
        public void AStringThatTheClientModelsAsAnEnum_IsAMemberOfThatEnum()
        {
            Assert.Equal(nameof(DayOfWeek.Sunday), ResponseSamples.SampleFor("String", typeof(DayOfWeek?), "field"));
        }

        [Fact]
        public void AnArrayOfStringsThatTheClientModelsAsGuids_HasAGuidElement()
        {
            var elements = Assert.IsType<object[]>(ResponseSamples.SampleFor("Array<String>", typeof(List<Guid>), "field"));

            Assert.True(Guid.TryParse(Assert.IsType<string>(Assert.Single(elements)), out _));
        }

        [Fact]
        public void EveryDocumentedResponseField_HasASample()
        {
            var missing = ProtocolSnapshot.Current.Requests
                .SelectMany(request => request.Value.ResponseFields
                    .Where(field => !Samples.For(request.Key).ContainsKey(field.Name))
                    .Select(field => $"{request.Key}.{field.Name}"))
                .ToList();

            Assert.Empty(missing);
        }
    }
}
