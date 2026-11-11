namespace OBSStudioClient.Tests.Model
{
    using System.Text.Json;
    using OBSStudioClient.Enums;
    using OBSStudioClient.Exceptions;
    using OBSStudioClient.Model;
    using OBSStudioClient.Tests.TestSupport;

    /// <summary>
    /// The calls about the settings of OBS Studio itself (the video, the stream service, the folder for recordings, the parameters of a profile, the persistent data), against a stand-in for OBS Studio that answers as the
    /// test says: the requests that they send (nothing but what the application gave), how they read the answers, and how they end when OBS Studio refuses. The model keeps none of it.
    /// </summary>
    public class ModelProfileSettingsCallTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static JsonElement Only(CallFixture test, string type) => Assert.Single(test.AllSent(type));

        private static IReadOnlyList<string> Members(JsonElement data) => data.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal).ToArray();

        [Fact]
        public async Task GetVideoSettings_AsksWithoutData_AndSaysTheNumbersOfOBSStudio()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetVideoSettings", 100, new { baseHeight = 1080, baseWidth = 1920, fpsDenominator = 1001, fpsNumerator = 60000, outputHeight = 720, outputWidth = 1280 });

            var video = await test.Model.GetVideoSettingsAsync().WithTimeoutAsync(Timeout);

            Assert.Equal((60000, 1001, 1920, 1080, 1280, 720), (video.FpsNumerator, video.FpsDenominator, video.BaseWidth, video.BaseHeight, video.OutputWidth, video.OutputHeight));
            Assert.Equal(60000d / 1001, video.FramesPerSecond, 9);
            Assert.Equal("Video 1920x1080 canvas, 1280x720 output, 59.94 fps (60000/1001)", video.ToString());
            Assert.True(Only(test, "GetVideoSettings").ValueKind is JsonValueKind.Undefined or JsonValueKind.Null, "The request has data, and it needs none.");
        }

        [Fact]
        public async Task GetVideoSettings_ReadsAFrameRateThatOBSStudioWritesAsAFloat()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetVideoSettings", 100, new { baseHeight = 1080, baseWidth = 1920, fpsDenominator = 1.0, fpsNumerator = 30.0, outputHeight = 720, outputWidth = 1280 });

            var video = await test.Model.GetVideoSettingsAsync().WithTimeoutAsync(Timeout);

            Assert.Equal((30, 1), (video.FpsNumerator, video.FpsDenominator));
            Assert.Equal(30d, video.FramesPerSecond);
        }

        [Fact]
        public async Task SetVideoSettings_SendsOnlyWhatIsGiven_AsWholeNumbers()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetVideoSettings");

            await test.Model.SetVideoSettingsAsync(baseResolution: (1280, 720)).WithTimeoutAsync(Timeout);
            await test.Model.SetVideoSettingsAsync(outputResolution: (640, 360), frameRate: (30000, 1001)).WithTimeoutAsync(Timeout);
            await test.Model.SetVideoSettingsAsync((3840, 2160), (1920, 1080), (60, 1)).WithTimeoutAsync(Timeout);

            var sent = test.AllSent("SetVideoSettings");
            Assert.Equal(3, sent.Length);
            Assert.Equal(["baseHeight", "baseWidth"], Members(sent[0]));
            Assert.Equal((720, 1280), (sent[0].GetProperty("baseHeight").GetInt32(), sent[0].GetProperty("baseWidth").GetInt32()));
            Assert.Equal(["fpsDenominator", "fpsNumerator", "outputHeight", "outputWidth"], Members(sent[1]));
            Assert.Equal((1001, 30000, 360, 640), (sent[1].GetProperty("fpsDenominator").GetInt32(), sent[1].GetProperty("fpsNumerator").GetInt32(), sent[1].GetProperty("outputHeight").GetInt32(), sent[1].GetProperty("outputWidth").GetInt32()));
            Assert.Equal(["baseHeight", "baseWidth", "fpsDenominator", "fpsNumerator", "outputHeight", "outputWidth"], Members(sent[2]));
            Assert.Equal((2160, 3840, 1080, 1920, 1, 60), (sent[2].GetProperty("baseHeight").GetInt32(), sent[2].GetProperty("baseWidth").GetInt32(), sent[2].GetProperty("outputHeight").GetInt32(), sent[2].GetProperty("outputWidth").GetInt32(), sent[2].GetProperty("fpsDenominator").GetInt32(), sent[2].GetProperty("fpsNumerator").GetInt32()));
        }

        [Fact]
        public async Task SetVideoSettings_ChecksWhatItIsGiven_BeforeItSendsAnything()
        {
            await using var test = await CallFixture.StartAsync();

            var nothing = Assert.Throws<ArgumentException>(() => { _ = test.Model.SetVideoSettingsAsync(); });
            var zero = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.SetVideoSettingsAsync(baseResolution: (0, 720)); });
            var tall = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.SetVideoSettingsAsync(outputResolution: (1280, 4097)); });
            var negative = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.SetVideoSettingsAsync(baseResolution: (-1, 720)); });
            var numerator = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.SetVideoSettingsAsync(frameRate: (0, 1)); });
            var denominator = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.SetVideoSettingsAsync(frameRate: (30, 0)); });

            Assert.Contains("at least one", nothing.Message, StringComparison.Ordinal);
            Assert.Equal("baseResolution", zero.ParamName);
            Assert.Equal("outputResolution", tall.ParamName);
            Assert.Equal("baseResolution", negative.ParamName);
            Assert.Equal("frameRate", numerator.ParamName);
            Assert.Equal("frameRate", denominator.ParamName);
            Assert.Equal(0, test.Count("SetVideoSettings"));
        }

        [Theory]
        [InlineData(1, 1, 1, 1)]
        [InlineData(4096, 4096, 4096, 4096)]
        public async Task SetVideoSettings_TakesTheSmallestAndTheLargestSizeAndFrameRatePartThatObsWebsocketDocuments(int width, int height, int numerator, int denominator)
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetVideoSettings");

            await test.Model.SetVideoSettingsAsync((width, height), (width, height), (numerator, denominator)).WithTimeoutAsync(Timeout);

            var sent = Only(test, "SetVideoSettings");
            Assert.Equal((width, height, numerator, denominator), (sent.GetProperty("baseWidth").GetInt32(), sent.GetProperty("outputHeight").GetInt32(), sent.GetProperty("fpsNumerator").GetInt32(), sent.GetProperty("fpsDenominator").GetInt32()));
        }

        [Theory]
        [InlineData(500, "Video settings cannot be changed while an output is active.", RequestStatusCode.OutputRunning)]
        [InlineData(402, "The field value of `baseWidth` is below the minimum of `8.000000`", RequestStatusCode.RequestFieldOutOfRange)]
        [InlineData(300, "You must specify at least one video-changing pair.", RequestStatusCode.MissingRequestField)]
        public async Task SetVideoSettings_ThatOBSStudioRefuses_ThrowsAResponseExceptionWithTheCodeAndTheWordsOfOBSStudio(int code, string comment, RequestStatusCode expected)
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetVideoSettings", code, comment);

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.SetVideoSettingsAsync(baseResolution: (4, 4)).WithTimeoutAsync(Timeout));

            Assert.Equal(expected, refused.ErrorCode);
            Assert.Equal(comment, refused.Message);
        }

        [Fact]
        public async Task GetStreamService_SaysTheTypeAndTheSettings_AndToStringKeepsTheKeyToItself()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetStreamServiceSettings", 100, new { streamServiceSettings = new { key = "SECRET-KEY", server = "rtmp://live.example.com/app" }, streamServiceType = "rtmp_custom" });

            var service = await test.Model.GetStreamServiceAsync().WithTimeoutAsync(Timeout);

            Assert.Equal("rtmp_custom", service.Type);
            Assert.Equal("SECRET-KEY", service.Settings.GetProperty("key").GetString());
            Assert.Equal("rtmp://live.example.com/app", service.Settings.GetProperty("server").GetString());
            Assert.Equal("Stream service 'rtmp_custom'", service.ToString());
            Assert.DoesNotContain("SECRET-KEY", service.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetStreamService_OfAServiceWithoutSettings_HasAnEmptyObject()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetStreamServiceSettings", 100, new { streamServiceSettings = new { }, streamServiceType = "rtmp_common" });

            var service = await test.Model.GetStreamServiceAsync().WithTimeoutAsync(Timeout);

            Assert.Equal("rtmp_common", service.Type);
            Assert.Equal(JsonValueKind.Object, service.Settings.ValueKind);
            Assert.Empty(Members(service.Settings));
        }

        [Fact]
        public async Task SetStreamService_SendsTheTypeAndTheSettings_WhetherAnObjectADictionaryOrAJsonElement()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetStreamServiceSettings");
            using var document = JsonDocument.Parse("""{"server":"rtmp://c/app","key":"k3"}""");

            await test.Model.SetStreamServiceAsync("rtmp_custom", new { server = "rtmp://a/app", key = "k1" }).WithTimeoutAsync(Timeout);
            await test.Model.SetStreamServiceAsync("rtmp_custom", new Dictionary<string, object> { ["server"] = "rtmp://b/app", ["key"] = "k2" }).WithTimeoutAsync(Timeout);
            await test.Model.SetStreamServiceAsync("rtmp_custom", document.RootElement).WithTimeoutAsync(Timeout);

            var sent = test.AllSent("SetStreamServiceSettings");
            Assert.Equal(3, sent.Length);
            Assert.All(sent, data => Assert.Equal("rtmp_custom", data.GetProperty("streamServiceType").GetString()));
            Assert.Equal(["k1", "k2", "k3"], sent.Select(data => data.GetProperty("streamServiceSettings").GetProperty("key").GetString()!).ToArray());
            Assert.Equal(["rtmp://a/app", "rtmp://b/app", "rtmp://c/app"], sent.Select(data => data.GetProperty("streamServiceSettings").GetProperty("server").GetString()!).ToArray());
        }

        [Fact]
        public async Task SetStreamService_ChecksWhatItIsGiven_BeforeItSendsAnything()
        {
            await using var test = await CallFixture.StartAsync();

            Assert.Throws<ArgumentException>(() => { _ = test.Model.SetStreamServiceAsync(string.Empty, new { server = "x" }); });
            Assert.Throws<ArgumentNullException>(() => { _ = test.Model.SetStreamServiceAsync(null!, new { server = "x" }); });
            Assert.Throws<ArgumentNullException>(() => { _ = test.Model.SetStreamServiceAsync("rtmp_custom", null!); });
            var notAnObject = Assert.Throws<ArgumentException>(() => { _ = test.Model.SetStreamServiceAsync("rtmp_custom", "server"); });

            Assert.Equal("settings", notAnObject.ParamName);
            Assert.Equal(0, test.Count("SetStreamServiceSettings"));
        }

        [Theory]
        [InlineData(500, "You cannot change stream service settings while streaming.", RequestStatusCode.OutputRunning)]
        [InlineData(700, "Failed to create the stream service with the requested streamServiceType. It may be an invalid type.", RequestStatusCode.ResourceCreationFailed)]
        [InlineData(403, "The field value of `streamServiceSettings` must not be empty.", RequestStatusCode.RequestFieldEmpty)]
        public async Task SetStreamService_ThatOBSStudioRefuses_ThrowsAResponseExceptionWithTheCodeAndTheWordsOfOBSStudio(int code, string comment, RequestStatusCode expected)
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetStreamServiceSettings", code, comment);

            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.SetStreamServiceAsync("rtmp_custom", new { }).WithTimeoutAsync(Timeout));

            Assert.Equal(expected, refused.ErrorCode);
            Assert.Equal(comment, refused.Message);
        }

        [Fact]
        public async Task TheRecordDirectory_IsAskedForAndSent_AsOBSStudioWritesIt()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetRecordDirectory", 100, new { recordDirectory = @"C:\Users\user\Videos" });
            test.Answer("SetRecordDirectory");

            var folder = await test.Model.GetRecordDirectoryAsync().WithTimeoutAsync(Timeout);
            await test.Model.SetRecordDirectoryAsync(@"D:\Recordings").WithTimeoutAsync(Timeout);

            Assert.Equal(@"C:\Users\user\Videos", folder);
            Assert.Equal(@"D:\Recordings", Only(test, "SetRecordDirectory").GetProperty("recordDirectory").GetString());
        }

        [Fact]
        public async Task SetRecordDirectory_ChecksTheFolder_AndSaysWhenOBSStudioRecords()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetRecordDirectory", 500);

            Assert.Throws<ArgumentException>(() => { _ = test.Model.SetRecordDirectoryAsync(string.Empty); });
            Assert.Throws<ArgumentNullException>(() => { _ = test.Model.SetRecordDirectoryAsync(null!); });
            var refused = await Assert.ThrowsAsync<ObsResponseException>(() => test.Model.SetRecordDirectoryAsync(@"D:\Recordings").WithTimeoutAsync(Timeout));

            Assert.Equal(RequestStatusCode.OutputRunning, refused.ErrorCode);
            Assert.Equal(1, test.Count("SetRecordDirectory"));
        }

        [Fact]
        public async Task GetParameter_SendsTheCategoryAndTheName_AndSaysTheValueAndTheDefault()
        {
            await using var test = await CallFixture.StartAsync();
            var answers = new Dictionary<string, Dictionary<string, string?>>
            {
                ["Mode"] = new() { ["defaultParameterValue"] = "Simple", ["parameterValue"] = "Advanced" },
                ["FilePath"] = new() { ["defaultParameterValue"] = null, ["parameterValue"] = @"D:\x" },
                ["a"] = new() { ["defaultParameterValue"] = null, ["parameterValue"] = null },
            };
            test.Answer("GetProfileParameter", data => (100, answers[data!.Value.GetProperty("parameterName").GetString()!]));

            var withDefault = await test.Model.Profiles.GetParameterAsync("Output", "Mode").WithTimeoutAsync(Timeout);
            var withoutDefault = await test.Model.Profiles.GetParameterAsync("SimpleOutput", "FilePath").WithTimeoutAsync(Timeout);
            var nobody = await test.Model.Profiles.GetParameterAsync("walk", "a").WithTimeoutAsync(Timeout);

            Assert.Equal(("Output", "Mode", "Advanced", "Simple", true), (withDefault.Category, withDefault.Name, withDefault.Value, withDefault.DefaultValue, withDefault.HasValue));
            Assert.Equal(("SimpleOutput", "FilePath", @"D:\x", null, true), (withoutDefault.Category, withoutDefault.Name, withoutDefault.Value, withoutDefault.DefaultValue, withoutDefault.HasValue));
            Assert.Equal(("walk", "a", null, null, false), (nobody.Category, nobody.Name, nobody.Value, nobody.DefaultValue, nobody.HasValue));
            Assert.Equal("Output/Mode = 'Advanced'", withDefault.ToString());
            Assert.Equal("walk/a = (none)", nobody.ToString());
            var sent = test.AllSent("GetProfileParameter");
            Assert.Equal(["Mode", "FilePath", "a"], sent.Select(data => data.GetProperty("parameterName").GetString()!).ToArray());
            Assert.Equal(["Output", "SimpleOutput", "walk"], sent.Select(data => data.GetProperty("parameterCategory").GetString()!).ToArray());
        }

        [Fact]
        public async Task SetParameter_SendsTheValue_AndNullForARemoval()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetProfileParameter");

            await test.Model.Profiles.SetParameterAsync("walk", "a", "1").WithTimeoutAsync(Timeout);
            await test.Model.Profiles.SetParameterAsync("walk", "a", null).WithTimeoutAsync(Timeout);

            var sent = test.AllSent("SetProfileParameter");
            Assert.Equal(2, sent.Length);
            Assert.Equal(["parameterCategory", "parameterName", "parameterValue"], Members(sent[0]));
            Assert.Equal("1", sent[0].GetProperty("parameterValue").GetString());
            Assert.Equal(["parameterCategory", "parameterName", "parameterValue"], Members(sent[1]));
            Assert.Equal(JsonValueKind.Null, sent[1].GetProperty("parameterValue").ValueKind);
        }

        [Fact]
        public async Task RemovingAParameterThatHasNoValue_IsANotFoundExceptionForTheParameter_NotARemovalOfAnObjectOfTheModel()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetProfileParameter", 600, "There are no existing instances of that profile parameter.");

            var missing = await Assert.ThrowsAsync<ObsNotFoundException>(() => test.Model.Profiles.SetParameterAsync("walk", "a", null).WithTimeoutAsync(Timeout));

            Assert.Equal("profile parameter", missing.Kind);
            Assert.Equal("walk/a", missing.RequestedName);
            Assert.Empty(missing.Suggestions);
            Assert.Equal("There is no profile parameter named \"walk/a\".", missing.Message);
        }

        [Fact]
        public async Task TheParameterCalls_CheckTheCategoryAndTheName_BeforeTheySendAnything()
        {
            await using var test = await CallFixture.StartAsync();

            Assert.Throws<ArgumentException>(() => { _ = test.Model.Profiles.GetParameterAsync(string.Empty, "a"); });
            Assert.Throws<ArgumentException>(() => { _ = test.Model.Profiles.GetParameterAsync("walk", string.Empty); });
            Assert.Throws<ArgumentNullException>(() => { _ = test.Model.Profiles.GetParameterAsync(null!, "a"); });
            Assert.Throws<ArgumentException>(() => { _ = test.Model.Profiles.SetParameterAsync(string.Empty, "a", "1"); });
            Assert.Throws<ArgumentException>(() => { _ = test.Model.Profiles.SetParameterAsync("walk", string.Empty, "1"); });

            Assert.Equal(0, test.Count("GetProfileParameter"));
            Assert.Equal(0, test.Count("SetProfileParameter"));
        }

        [Theory]
        [InlineData(ObsDataRealm.Global, "OBS_WEBSOCKET_DATA_REALM_GLOBAL")]
        [InlineData(ObsDataRealm.Profile, "OBS_WEBSOCKET_DATA_REALM_PROFILE")]
        public async Task GetPersistentData_SendsTheRealmAndTheSlot_AndSaysWhatTheSlotHolds(ObsDataRealm realm, string wire)
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("GetPersistentData", 100, new { slotValue = new { a = new[] { 1, 2 }, b = "two" } });

            var value = await test.Model.GetPersistentDataAsync(realm, "slot").WithTimeoutAsync(Timeout);

            Assert.NotNull(value);
            Assert.Equal(2, value.Value.GetProperty("a").GetArrayLength());
            Assert.Equal("two", value.Value.GetProperty("b").GetString());
            var sent = Only(test, "GetPersistentData");
            Assert.Equal(wire, sent.GetProperty("realm").GetString());
            Assert.Equal("slot", sent.GetProperty("slotName").GetString());
        }

        [Fact]
        public async Task GetPersistentData_OfASlotThatNobodyUsed_IsNull_AndOfEveryKindOfValueIsThatValue()
        {
            await using var test = await CallFixture.StartAsync();
            var answers = new Dictionary<string, object?>
            {
                ["unused"] = null,
                ["text"] = "hello",
                ["number"] = 3.5,
                ["flag"] = true,
                ["list"] = new[] { 1, 2, 3 },
                ["empty"] = string.Empty,
            };
            test.Answer("GetPersistentData", data => (100, new { slotValue = answers[data!.Value.GetProperty("slotName").GetString()!] }));

            Assert.Null(await test.Model.GetPersistentDataAsync(ObsDataRealm.Global, "unused").WithTimeoutAsync(Timeout));
            Assert.Equal("hello", (await test.Model.GetPersistentDataAsync(ObsDataRealm.Global, "text").WithTimeoutAsync(Timeout))!.Value.GetString());
            Assert.Equal(3.5, (await test.Model.GetPersistentDataAsync(ObsDataRealm.Global, "number").WithTimeoutAsync(Timeout))!.Value.GetDouble());
            Assert.True((await test.Model.GetPersistentDataAsync(ObsDataRealm.Global, "flag").WithTimeoutAsync(Timeout))!.Value.GetBoolean());
            Assert.Equal(3, (await test.Model.GetPersistentDataAsync(ObsDataRealm.Global, "list").WithTimeoutAsync(Timeout))!.Value.GetArrayLength());
            Assert.Equal(string.Empty, (await test.Model.GetPersistentDataAsync(ObsDataRealm.Global, "empty").WithTimeoutAsync(Timeout))!.Value.GetString());
        }

        [Fact]
        public async Task SetPersistentData_SendsTheRealmTheSlotAndTheValue_OfEveryKindThatJsonHas()
        {
            await using var test = await CallFixture.StartAsync();
            test.Answer("SetPersistentData");
            using var document = JsonDocument.Parse("""{"x":[true,null]}""");

            await test.Model.SetPersistentDataAsync(ObsDataRealm.Global, "text", "hello").WithTimeoutAsync(Timeout);
            await test.Model.SetPersistentDataAsync(ObsDataRealm.Profile, "number", 3.5).WithTimeoutAsync(Timeout);
            await test.Model.SetPersistentDataAsync(ObsDataRealm.Global, "flag", false).WithTimeoutAsync(Timeout);
            await test.Model.SetPersistentDataAsync(ObsDataRealm.Profile, "object", new { volume = 7 }).WithTimeoutAsync(Timeout);
            await test.Model.SetPersistentDataAsync(ObsDataRealm.Global, "list", new[] { 1, 2 }).WithTimeoutAsync(Timeout);
            await test.Model.SetPersistentDataAsync(ObsDataRealm.Profile, "element", document.RootElement).WithTimeoutAsync(Timeout);
            await test.Model.SetPersistentDataAsync(ObsDataRealm.Global, "empty", string.Empty).WithTimeoutAsync(Timeout);

            var sent = test.AllSent("SetPersistentData").ToDictionary(data => data.GetProperty("slotName").GetString()!);
            Assert.Equal(7, sent.Count);
            Assert.All(sent.Values, data => Assert.Equal(["realm", "slotName", "slotValue"], Members(data)));
            Assert.Equal("OBS_WEBSOCKET_DATA_REALM_GLOBAL", sent["text"].GetProperty("realm").GetString());
            Assert.Equal("OBS_WEBSOCKET_DATA_REALM_PROFILE", sent["number"].GetProperty("realm").GetString());
            Assert.Equal("hello", sent["text"].GetProperty("slotValue").GetString());
            Assert.Equal(3.5, sent["number"].GetProperty("slotValue").GetDouble());
            Assert.Equal(JsonValueKind.False, sent["flag"].GetProperty("slotValue").ValueKind);
            Assert.Equal(7, sent["object"].GetProperty("slotValue").GetProperty("volume").GetInt32());
            Assert.Equal(2, sent["list"].GetProperty("slotValue").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, sent["element"].GetProperty("slotValue").GetProperty("x")[1].ValueKind);
            Assert.Equal(string.Empty, sent["empty"].GetProperty("slotValue").GetString());
        }

        [Fact]
        public async Task SetPersistentData_ChecksWhatItIsGiven_BeforeItSendsAnything()
        {
            await using var test = await CallFixture.StartAsync();
            using var nullDocument = JsonDocument.Parse("null");

            Assert.Throws<ArgumentException>(() => { _ = test.Model.SetPersistentDataAsync(ObsDataRealm.Global, string.Empty, "x"); });
            Assert.Throws<ArgumentNullException>(() => { _ = test.Model.SetPersistentDataAsync(ObsDataRealm.Global, "slot", null!); });
            var jsonNull = Assert.Throws<ArgumentException>(() => { _ = test.Model.SetPersistentDataAsync(ObsDataRealm.Global, "slot", nullDocument.RootElement); });
            var undefined = Assert.Throws<ArgumentException>(() => { _ = test.Model.SetPersistentDataAsync(ObsDataRealm.Global, "slot", default(JsonElement)); });
            var unwritable = Assert.Throws<ArgumentException>(() => { _ = test.Model.SetPersistentDataAsync(ObsDataRealm.Global, "slot", new Func<int>(() => 1)); });
            var realm = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.SetPersistentDataAsync((ObsDataRealm)7, "slot", "x"); });
            var readRealm = Assert.Throws<ArgumentOutOfRangeException>(() => { _ = test.Model.GetPersistentDataAsync((ObsDataRealm)7, "slot"); });
            Assert.Throws<ArgumentException>(() => { _ = test.Model.GetPersistentDataAsync(ObsDataRealm.Global, string.Empty); });

            Assert.Equal("value", jsonNull.ParamName);
            Assert.Contains("null", jsonNull.Message, StringComparison.Ordinal);
            Assert.Equal("value", undefined.ParamName);
            Assert.Contains("can not be written as JSON", undefined.Message, StringComparison.Ordinal);
            Assert.Equal("value", unwritable.ParamName);
            Assert.Equal("realm", realm.ParamName);
            Assert.Equal("realm", readRealm.ParamName);
            Assert.Equal(0, test.Count("SetPersistentData"));
            Assert.Equal(0, test.Count("GetPersistentData"));
        }
    }
}
