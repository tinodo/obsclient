namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json;

    /// <summary>
    /// The settings that the simulated OBS Studio keeps for a profile (the video, the stream service, the folder for recordings, the configuration) and for the programs that control it (the persistent data), and what the requests
    /// of the configuration answer. What the simulation does here was measured with OBS Studio 33.0.0 (LiveProfileSettingsModelTests, LiveProfilesTests) and read from the source of obs-websocket 5.7.6 (RequestHandler_Config.cpp).
    /// </summary>
    public class SimSettingsTests
    {
        private static SimHarness NewHarness() => new(SimQuirks.Ideal);

        private static (int Fps, int Den, int BaseWidth, int BaseHeight, int OutputWidth, int OutputHeight) VideoOf(SimEntry entry)
        {
            return (entry.Int("fpsNumerator"), entry.Int("fpsDenominator"), entry.Int("baseWidth"), entry.Int("baseHeight"), entry.Int("outputWidth"), entry.Int("outputHeight"));
        }

        private static string? ValueOf(SimEntry entry, string member)
        {
            var value = entry.Data.GetProperty(member);
            return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
        }

        [Fact]
        public void AProfileThatIsNew_HasTheVideoThatOBSStudioGivesIt_AndAServiceWithoutSettings()
        {
            var h = NewHarness();

            var video = h.Ok("GetVideoSettings");
            var service = h.Ok("GetStreamServiceSettings");

            Assert.Equal((30, 1, 1920, 1080, 1280, 720), VideoOf(video));
            Assert.Equal("rtmp_common", service.String("streamServiceType"));
            Assert.Equal("{}", service.Data.GetProperty("streamServiceSettings").GetRawText());
            Assert.Equal(SimObs.DefaultRecordDirectory, h.Ok("GetRecordDirectory").String("recordDirectory"));
        }

        [Fact]
        public void SetVideoSettings_ChangesWhatIsGiven_AndWritesItToTheConfigurationOfTheProfile()
        {
            var h = NewHarness();

            h.Ok("SetVideoSettings", new { baseWidth = 1280, baseHeight = 720, outputWidth = 640, outputHeight = 360, fpsNumerator = 30000, fpsDenominator = 1001 });
            var video = h.Ok("GetVideoSettings");
            h.Ok("SetVideoSettings", new { fpsNumerator = 60, fpsDenominator = 1 });
            var fps = h.Ok("GetVideoSettings");

            Assert.Equal((30000, 1001, 1280, 720, 640, 360), VideoOf(video));
            Assert.Equal((60, 1, 1280, 720, 640, 360), VideoOf(fps));
            string? Parameter(string name) => ValueOf(h.Ok("GetProfileParameter", new { parameterCategory = "Video", parameterName = name }), "parameterValue");
            Assert.Equal("1280", Parameter("BaseCX"));
            Assert.Equal("720", Parameter("BaseCY"));
            Assert.Equal("640", Parameter("OutputCX"));
            Assert.Equal("360", Parameter("OutputCY"));
            Assert.Equal("2", Parameter("FPSType"));
            Assert.Equal("60", Parameter("FPSNum"));
            Assert.Equal("1", Parameter("FPSDen"));
        }

        [Fact]
        public void SetVideoSettings_IgnoresHalfOfAPair_AndIsRefusedWith300WhenThereIsNoPair()
        {
            var h = NewHarness();

            var half = h.Call("SetVideoSettings", new { baseWidth = 640 });
            var none = h.Call("SetVideoSettings", new { });
            var noData = h.Call("SetVideoSettings");
            h.Ok("SetVideoSettings", new { baseWidth = 640, fpsNumerator = 25, fpsDenominator = 1 });
            var video = h.Ok("GetVideoSettings");

            foreach (var refused in new[] { half, none, noData })
            {
                Assert.False(refused.Succeeded);
                Assert.Equal(SimStatus.MissingRequestField, refused.Code);
                Assert.Equal("You must specify at least one video-changing pair.", refused.Comment);
            }

            // The width without its height changed nothing; the pair that was complete did.
            Assert.Equal((25, 1, 1920, 1080, 1280, 720), VideoOf(video));
        }

        [Theory]
        [InlineData("baseWidth", 4, SimStatus.RequestFieldOutOfRange, "The field value of `baseWidth` is below the minimum of `8.000000`")]
        [InlineData("baseHeight", 4097, SimStatus.RequestFieldOutOfRange, "The field value of `baseHeight` is above the maximum of `4096.000000`")]
        [InlineData("outputWidth", 7, SimStatus.RequestFieldOutOfRange, "The field value of `outputWidth` is below the minimum of `8.000000`")]
        [InlineData("outputHeight", 5000, SimStatus.RequestFieldOutOfRange, "The field value of `outputHeight` is above the maximum of `4096.000000`")]
        [InlineData("fpsNumerator", 0, SimStatus.RequestFieldOutOfRange, "The field value of `fpsNumerator` is below the minimum of `1.000000`")]
        [InlineData("fpsDenominator", 0, SimStatus.RequestFieldOutOfRange, "The field value of `fpsDenominator` is below the minimum of `1.000000`")]
        public void SetVideoSettings_RefusesAValueThatIsOutOfRange_AndChangesNothing(string field, int value, int code, string comment)
        {
            var h = NewHarness();
            var pair = field.StartsWith("fps", StringComparison.Ordinal)
                ? new Dictionary<string, object> { ["fpsNumerator"] = 30, ["fpsDenominator"] = 1 }
                : new Dictionary<string, object> { [field.Replace("Height", "Width", StringComparison.Ordinal)] = 640, [field.Replace("Width", "Height", StringComparison.Ordinal)] = 480 };
            pair[field] = value;

            var refused = h.Call("SetVideoSettings", pair);

            Assert.False(refused.Succeeded);
            Assert.Equal(code, refused.Code);
            Assert.Equal(comment, refused.Comment);
            Assert.Equal((30, 1, 1920, 1080, 1280, 720), VideoOf(h.Ok("GetVideoSettings")));
        }

        [Fact]
        public void SetVideoSettings_RefusesANumberThatIsNoNumber_With401()
        {
            var h = NewHarness();

            var refused = h.Call("SetVideoSettings", new { fpsNumerator = "thirty", fpsDenominator = 1 });

            Assert.False(refused.Succeeded);
            Assert.Equal(SimStatus.InvalidRequestFieldType, refused.Code);
            Assert.Contains("`fpsNumerator`", refused.Comment, StringComparison.Ordinal);
        }

        [Fact]
        public void AParameterOfTheVideo_OnlyChangesTheConfiguration_NotWhatOBSStudioRendersWith()
        {
            var h = NewHarness();

            h.Ok("SetProfileParameter", new { parameterCategory = "Video", parameterName = "BaseCX", parameterValue = "1111" });

            Assert.Equal((30, 1, 1920, 1080, 1280, 720), VideoOf(h.Ok("GetVideoSettings")));
            Assert.Equal("1111", ValueOf(h.Ok("GetProfileParameter", new { parameterCategory = "Video", parameterName = "BaseCX" }), "parameterValue"));
        }

        [Fact]
        public void TheStreamService_OfTheSameType_GetsTheSettingsLaidOverItsOwn_AndAnotherTypeIsANewService()
        {
            var h = NewHarness();

            h.Ok("SetStreamServiceSettings", new { streamServiceType = "rtmp_custom", streamServiceSettings = new { server = "rtmp://a/app", key = "k1" } });
            var custom = h.Ok("GetStreamServiceSettings");
            h.Ok("SetStreamServiceSettings", new { streamServiceType = "rtmp_custom", streamServiceSettings = new { server = "rtmp://b/app", use_auth = (string?)null } });
            var merged = h.Ok("GetStreamServiceSettings");
            h.Ok("SetStreamServiceSettings", new { streamServiceType = "rtmp_common", streamServiceSettings = new { service = "Twitch" } });
            var common = h.Ok("GetStreamServiceSettings");

            Assert.Equal("rtmp_custom", custom.String("streamServiceType"));
            Assert.Equal("""{"key":"k1","server":"rtmp://a/app"}""", custom.Data.GetProperty("streamServiceSettings").GetRawText());
            Assert.Equal("""{"key":"k1","server":"rtmp://b/app"}""", merged.Data.GetProperty("streamServiceSettings").GetRawText());
            Assert.Equal("rtmp_common", common.String("streamServiceType"));
            Assert.Equal("""{"service":"Twitch"}""", common.Data.GetProperty("streamServiceSettings").GetRawText());
        }

        [Fact]
        public void TheStreamService_IsRefused_ForATypeThatDoesNotExist_ForEmptySettings_AndForAFieldThatIsMissingOrWrong()
        {
            var h = NewHarness();

            var unknown = h.Call("SetStreamServiceSettings", new { streamServiceType = "no_such_service", streamServiceSettings = new { server = "x" } });
            var empty = h.Call("SetStreamServiceSettings", new { streamServiceType = "rtmp_custom", streamServiceSettings = new { } });
            var wrong = h.Call("SetStreamServiceSettings", new { streamServiceType = "rtmp_custom", streamServiceSettings = "server" });
            var noType = h.Call("SetStreamServiceSettings", new { streamServiceSettings = new { server = "x" } });
            var noSettings = h.Call("SetStreamServiceSettings", new { streamServiceType = "rtmp_custom" });

            Assert.Equal(SimStatus.ResourceCreationFailed, unknown.Code);
            Assert.Equal("Failed to create the stream service with the requested streamServiceType. It may be an invalid type.", unknown.Comment);
            Assert.Equal(SimStatus.RequestFieldEmpty, empty.Code);
            Assert.Equal("The field value of `streamServiceSettings` must not be empty.", empty.Comment);
            Assert.Equal(SimStatus.InvalidRequestFieldType, wrong.Code);
            Assert.Equal(SimStatus.MissingRequestField, noType.Code);
            Assert.Equal(SimStatus.MissingRequestField, noSettings.Code);
            Assert.Equal("rtmp_common", h.Ok("GetStreamServiceSettings").String("streamServiceType"));
        }

        [Fact]
        public void SetRecordDirectory_SetsTheFolderOfBothOutputModes_AndGetRecordDirectoryReadsTheOneOfTheModeThatIsOn()
        {
            var h = NewHarness();

            h.Ok("SetRecordDirectory", new { recordDirectory = @"D:\Recordings" });
            h.Ok("SetProfileParameter", new { parameterCategory = "AdvOut", parameterName = "RecFilePath", parameterValue = @"E:\Advanced" });
            var simple = h.Ok("GetRecordDirectory").String("recordDirectory");
            h.Ok("SetProfileParameter", new { parameterCategory = "Output", parameterName = "Mode", parameterValue = "Advanced" });
            var advanced = h.Ok("GetRecordDirectory").String("recordDirectory");

            Assert.Equal(@"D:\Recordings", simple);
            Assert.Equal(@"E:\Advanced", advanced);
            Assert.Equal(@"D:\Recordings", ValueOf(h.Ok("GetProfileParameter", new { parameterCategory = "SimpleOutput", parameterName = "FilePath" }), "parameterValue"));
        }

        [Fact]
        public void SetRecordDirectory_IsRefusedForAFolderThatIsMissingOrEmpty()
        {
            var h = NewHarness();

            var missing = h.Call("SetRecordDirectory");
            var empty = h.Call("SetRecordDirectory", new { recordDirectory = string.Empty });

            Assert.False(missing.Succeeded);
            Assert.Equal(SimStatus.RequestFieldEmpty, empty.Code);
            Assert.Equal(SimObs.DefaultRecordDirectory, h.Ok("GetRecordDirectory").String("recordDirectory"));
        }

        [Fact]
        public void AParameter_SaysItsValueAndItsDefault_AndNoneForOneThatNobodyHas()
        {
            var h = NewHarness();
            SimEntry Get(string category, string name) => h.Ok("GetProfileParameter", new { parameterCategory = category, parameterName = name });

            var withDefault = Get("Output", "Mode");
            var nobody = Get("obsclient-live", "probe");
            h.Ok("SetProfileParameter", new { parameterCategory = "obsclient-live", parameterName = "probe", parameterValue = "1" });
            var set = Get("obsclient-live", "probe");
            h.Ok("SetProfileParameter", new { parameterCategory = "Output", parameterName = "Mode", parameterValue = "Advanced" });
            var overDefault = Get("Output", "Mode");

            Assert.Equal(("Simple", "Simple"), (ValueOf(withDefault, "parameterValue"), ValueOf(withDefault, "defaultParameterValue")));
            Assert.Equal((null, null), (ValueOf(nobody, "parameterValue"), ValueOf(nobody, "defaultParameterValue")));
            Assert.Equal(("1", null), (ValueOf(set, "parameterValue"), ValueOf(set, "defaultParameterValue")));
            Assert.Equal(("Advanced", "Simple"), (ValueOf(overDefault, "parameterValue"), ValueOf(overDefault, "defaultParameterValue")));
        }

        [Fact]
        public void ACategoryAndANameOfAParameter_AreCaseSensitive()
        {
            var h = NewHarness();
            h.Ok("SetProfileParameter", new { parameterCategory = "obsclient-live", parameterName = "probe", parameterValue = "1" });

            var other = h.Ok("GetProfileParameter", new { parameterCategory = "OBSCLIENT-LIVE", parameterName = "PROBE" });

            Assert.Null(ValueOf(other, "parameterValue"));
        }

        [Fact]
        public void RemovingAParameter_TakesItsValueAway_ButIsRefusedWith600ForOneThatHasNoValueThatWasSet()
        {
            var h = NewHarness();
            h.Ok("SetProfileParameter", new { parameterCategory = "Output", parameterName = "Mode", parameterValue = "Advanced" });
            h.Ok("SetProfileParameter", new { parameterCategory = "walk", parameterName = "a", parameterValue = "1" });

            h.Ok("SetProfileParameter", new { parameterCategory = "Output", parameterName = "Mode", parameterValue = (string?)null });
            h.Ok("SetProfileParameter", new { parameterCategory = "walk", parameterName = "a" });
            var back = h.Ok("GetProfileParameter", new { parameterCategory = "Output", parameterName = "Mode" });
            var gone = h.Ok("GetProfileParameter", new { parameterCategory = "walk", parameterName = "a" });
            var again = h.Call("SetProfileParameter", new { parameterCategory = "walk", parameterName = "a", parameterValue = (string?)null });
            var onlyDefault = h.Call("SetProfileParameter", new { parameterCategory = "Output", parameterName = "Mode", parameterValue = (string?)null });

            Assert.Equal("Simple", ValueOf(back, "parameterValue"));
            Assert.Null(ValueOf(gone, "parameterValue"));
            foreach (var refused in new[] { again, onlyDefault })
            {
                Assert.Equal(SimStatus.ResourceNotFound, refused.Code);
                Assert.Equal("There are no existing instances of that profile parameter.", refused.Comment);
            }
        }

        [Fact]
        public void AParameterValueThatIsNoString_IsRefusedWith401()
        {
            var h = NewHarness();

            var refused = h.Call("SetProfileParameter", new { parameterCategory = "walk", parameterName = "a", parameterValue = 42 });

            Assert.False(refused.Succeeded);
            Assert.Equal(SimStatus.InvalidRequestFieldType, refused.Code);
            Assert.Equal("The field `parameterValue` must be a string.", refused.Comment);
        }

        [Fact]
        public void ASlotOfThePersistentData_IsNullUntilSomethingIsPutInIt_AndHoldsWhatWasPutInItWithTheMembersSorted()
        {
            var h = NewHarness();
            SimEntry Get(string realm) => h.Ok("GetPersistentData", new { realm, slotName = "slot" });

            var before = Get("OBS_WEBSOCKET_DATA_REALM_GLOBAL");
            h.Ok("SetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_GLOBAL", slotName = "slot", slotValue = new { b = 2, a = new[] { 1, 2 } } });
            var global = Get("OBS_WEBSOCKET_DATA_REALM_GLOBAL");
            var profile = Get("OBS_WEBSOCKET_DATA_REALM_PROFILE");
            h.Ok("SetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_GLOBAL", slotName = "slot", slotValue = "text" });
            var replaced = Get("OBS_WEBSOCKET_DATA_REALM_GLOBAL");

            Assert.Equal(JsonValueKind.Null, before.Data.GetProperty("slotValue").ValueKind);
            Assert.Equal("""{"a":[1,2],"b":2}""", global.Data.GetProperty("slotValue").GetRawText());
            Assert.Equal(JsonValueKind.Null, profile.Data.GetProperty("slotValue").ValueKind);
            Assert.Equal("text", replaced.String("slotValue"));
        }

        [Fact]
        public void ASlotValueThatIsNull_CountsAsMissing_AndARealmThatIsNotOneIsRefusedWith600()
        {
            var h = NewHarness();

            var nothing = h.Call("SetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_GLOBAL", slotName = "slot", slotValue = (string?)null });
            var badRealm = h.Call("SetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_BOGUS", slotName = "slot", slotValue = 1 });
            var badRead = h.Call("GetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_BOGUS", slotName = "slot" });

            Assert.Equal(SimStatus.MissingRequestField, nothing.Code);
            Assert.Equal("Your request is missing the `slotValue` field.", nothing.Comment);
            foreach (var refused in new[] { badRealm, badRead })
            {
                Assert.Equal(SimStatus.ResourceNotFound, refused.Code);
                Assert.Equal("You have specified an invalid persistent data realm.", refused.Comment);
            }
        }

        [Fact]
        public void TheSettings_BelongToTheProfile_AndTheGlobalPersistentDataToNone()
        {
            var h = NewHarness();
            h.Ok("SetVideoSettings", new { fpsNumerator = 24, fpsDenominator = 1 });
            h.Ok("SetStreamServiceSettings", new { streamServiceType = "rtmp_custom", streamServiceSettings = new { server = "rtmp://untitled/app" } });
            h.Ok("SetRecordDirectory", new { recordDirectory = @"D:\Untitled" });
            h.Ok("SetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_PROFILE", slotName = "slot", slotValue = "untitled" });
            h.Ok("SetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_GLOBAL", slotName = "slot", slotValue = "everywhere" });

            h.Ok("CreateProfile", new { profileName = "Second" });
            h.Settle();

            // A profile that is made is clean: it has not got what the profile that was current had.
            Assert.Equal((30, 1, 1920, 1080, 1280, 720), VideoOf(h.Ok("GetVideoSettings")));
            Assert.Equal("rtmp_common", h.Ok("GetStreamServiceSettings").String("streamServiceType"));
            Assert.Equal(SimObs.DefaultRecordDirectory, h.Ok("GetRecordDirectory").String("recordDirectory"));
            Assert.Equal(JsonValueKind.Null, h.Ok("GetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_PROFILE", slotName = "slot" }).Data.GetProperty("slotValue").ValueKind);
            Assert.Equal("everywhere", h.Ok("GetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_GLOBAL", slotName = "slot" }).String("slotValue"));

            h.Ok("SetCurrentProfile", new { profileName = "Untitled" });

            Assert.Equal((24, 1, 1920, 1080, 1280, 720), VideoOf(h.Ok("GetVideoSettings")));
            Assert.Equal("""{"server":"rtmp://untitled/app"}""", h.Ok("GetStreamServiceSettings").Data.GetProperty("streamServiceSettings").GetRawText());
            Assert.Equal(@"D:\Untitled", h.Ok("GetRecordDirectory").String("recordDirectory"));
            Assert.Equal("untitled", h.Ok("GetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_PROFILE", slotName = "slot" }).String("slotValue"));
            Assert.Equal("everywhere", h.Ok("GetPersistentData", new { realm = "OBS_WEBSOCKET_DATA_REALM_GLOBAL", slotName = "slot" }).String("slotValue"));
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void AProfileThatIsRemoved_TakesItsSettingsWithIt_AndANewProfileOfTheSameNameStartsClean()
        {
            var h = NewHarness();
            h.Ok("CreateProfile", new { profileName = "Second" });
            h.Settle();
            h.Ok("SetVideoSettings", new { fpsNumerator = 24, fpsDenominator = 1 });
            h.Ok("SetCurrentProfile", new { profileName = "Untitled" });

            h.Ok("RemoveProfile", new { profileName = "Second" });
            h.Settle();
            h.Ok("CreateProfile", new { profileName = "Second" });
            h.Settle();

            Assert.Equal("Second", h.Sim.CurrentProfile);
            Assert.Equal((30, 1, 1920, 1080, 1280, 720), VideoOf(h.Ok("GetVideoSettings")));
            h.Sim.CheckInvariants();
        }

        [Fact]
        public void TheUserRemovingTheCurrentProfile_TakesItsSettingsWithIt_AndTheNextProfileHasItsOwn()
        {
            var h = NewHarness();
            h.Ok("SetVideoSettings", new { fpsNumerator = 24, fpsDenominator = 1 });
            h.Ok("CreateProfile", new { profileName = "Second" });
            h.Settle();
            h.Ok("SetVideoSettings", new { fpsNumerator = 50, fpsDenominator = 1 });
            h.Ok("SetCurrentProfile", new { profileName = "Untitled" });

            h.Sim.External.RemoveProfile("Untitled");
            h.Settle();

            Assert.Equal("Second", h.Sim.CurrentProfile);
            Assert.Equal((50, 1, 1920, 1080, 1280, 720), VideoOf(h.Ok("GetVideoSettings")));
            h.Sim.CheckInvariants();
        }
    }
}
