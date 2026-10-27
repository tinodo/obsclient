namespace OBSStudioClient.Tests.TestAppTests
{
    using OBSStudioClient.TestApp.Services;
    using System.Text.Json;

    /// <summary>
    /// The log of the test app reads what a message is (an operation, a request, an event, a failure) from the message itself. The
    /// message of a server that does not follow the protocol has to be kept and shown, not to break the connection: the client
    /// logs a message before it reads it, so what is wrong with a message must not make the logging fail.
    /// </summary>
    public class FrameInfoTests
    {
        [Fact]
        public void AResponse_IsRead_WithItsRequestAndItsId()
        {
            var info = FrameInfo.Read("""{"op":7,"d":{"requestType":"GetVersion","requestId":"a1","requestStatus":{"result":true,"code":100}}}""");

            Assert.Equal(7, info.OpCode);
            Assert.Equal("GetVersion", info.RequestType);
            Assert.Equal("a1", info.RequestId);
            Assert.False(info.Failed);
            Assert.Equal("Response GetVersion", info.Title);
        }

        [Fact]
        public void AResponseThatFailed_ShowsTheCodeAndTheComment()
        {
            var info = FrameInfo.Read("""{"op":7,"d":{"requestType":"SetCurrentProgramScene","requestId":"a2","requestStatus":{"result":false,"code":600,"comment":"No scene."}}}""");

            Assert.True(info.Failed);
            Assert.Equal("Response SetCurrentProgramScene failed (600): No scene.", info.Title);
        }

        [Theory]
        [InlineData("""{"op":7,"d":{"requestType":"GetVersion","requestStatus":{"result":false,"code":"600","comment":"No scene."}}}""")]
        [InlineData("""{"op":7,"d":{"requestType":"GetVersion","requestStatus":{"result":false,"code":null}}}""")]
        [InlineData("""{"op":7,"d":{"requestType":"GetVersion","requestStatus":{"result":false,"code":{"a":1}}}}""")]
        [InlineData("""{"op":7,"d":{"requestType":"GetVersion","requestStatus":{"result":false,"code":6000000000}}}""")]
        public void AResponseThatFailedWithACodeThatIsNotAnInteger_IsStillRead(string json)
        {
            var info = FrameInfo.Read(json);

            Assert.True(info.Failed);
            Assert.StartsWith("Response GetVersion failed (?)", info.Title, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("""{"op":"7","d":{"requestType":"GetVersion"}}""")]
        [InlineData("""{"op":null,"d":{"requestType":"GetVersion"}}""")]
        [InlineData("""{"op":7.5,"d":{"requestType":"GetVersion"}}""")]
        [InlineData("""{"op":[7],"d":{"requestType":"GetVersion"}}""")]
        public void AMessageWithAnOperationThatIsNotAnInteger_HasNoOperation(string json)
        {
            var info = FrameInfo.Read(json);

            Assert.Null(info.OpCode);
            Assert.Equal("Message", info.Title);
        }

        [Theory]
        [InlineData("""{"op":7,"d":{"requestType":"GetVersion","requestStatus":5}}""")]
        [InlineData("""{"op":7,"d":{"requestType":"GetVersion","requestStatus":"failed"}}""")]
        [InlineData("""{"op":7,"d":{"requestType":"GetVersion","requestStatus":{"result":"no"}}}""")]
        public void AStatusThatIsNotWhatTheProtocolSays_IsNotAFailure(string json)
        {
            var info = FrameInfo.Read(json);

            Assert.False(info.Failed);
            Assert.Equal("Response GetVersion", info.Title);
        }

        [Theory]
        [InlineData("""{"op":7,"d":{"requestType":5,"requestId":6}}""")]
        [InlineData("""{"op":7,"d":[1,2]}""")]
        [InlineData("""[1,2,3]""")]
        [InlineData("""5""")]
        [InlineData("not json")]
        [InlineData("")]
        public void AMessageThatIsNotWhatTheProtocolSays_IsReadWithoutThrowing(string json)
        {
            var exception = Record.Exception(() => FrameInfo.Read(json));

            Assert.Null(exception);
        }

        [Fact]
        public void AnEvent_IsRead_WithItsType()
        {
            var info = FrameInfo.Read("""{"op":5,"d":{"eventType":"InputMuteStateChanged","eventIntent":8,"eventData":{"inputMuted":true}}}""");

            Assert.Equal(5, info.OpCode);
            Assert.Equal("InputMuteStateChanged", info.EventType);
            Assert.Equal("Event InputMuteStateChanged", info.Title);
        }

        [Theory]
        [InlineData(0, "Hello")]
        [InlineData(1, "Identify")]
        [InlineData(2, "Identified")]
        [InlineData(3, "Reidentify")]
        [InlineData(8, "Request batch")]
        [InlineData(9, "Request batch response")]
        [InlineData(4, "Message")]
        public void TheOperationsWithoutADetail_AreNamedByTheirOperation(int op, string title)
        {
            using var document = JsonDocument.Parse("{\"op\":" + op + ",\"d\":{}}");

            Assert.Equal(title, FrameInfo.Read(document.RootElement).Title);
        }
    }
}
