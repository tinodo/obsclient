namespace OBSStudioClient.Tests.Simulation
{
    using System.Globalization;
    using System.Text.Json.Nodes;
    using static OBSStudioClient.Tests.Simulation.SimJson;

    /// <summary>What OBS Studio renders with: the frame rate as a fraction, the size of the canvas and the size of the output.</summary>
    internal readonly record struct SimVideo(int FpsNumerator, int FpsDenominator, int BaseWidth, int BaseHeight, int OutputWidth, int OutputHeight);

    /// <summary>
    /// What OBS Studio keeps for a profile, apart from the scenes: the video that it renders with, the service of the stream, the configuration of the profile (basic.ini: the values that were set) and the data that the programs
    /// that control it keep for the profile.
    /// </summary>
    internal sealed class SimProfileData
    {
        /// <summary>Gets the values that were set in the configuration of the profile, by category and name (both are case sensitive: measured with OBS Studio 33.0.0).</summary>
        public Dictionary<(string Category, string Name), string> Config { get; } = [];

        /// <summary>Gets or sets what OBS Studio renders with. A profile that is new has the video that OBS Studio gives it (measured with OBS Studio 33.0.0).</summary>
        public SimVideo Video { get; set; } = SimObs.FreshVideo;

        /// <summary>Gets or sets the type of the stream service. A profile that is new has <c>rtmp_common</c>, with no settings (measured with OBS Studio 33.0.0).</summary>
        public string ServiceType { get; set; } = "rtmp_common";

        /// <summary>Gets or sets the settings of the stream service.</summary>
        public JsonObject ServiceSettings { get; set; } = new();

        /// <summary>Gets the slots of the persistent data of the profile realm.</summary>
        public JsonObject PersistentData { get; } = new();
    }

    /// <summary>
    /// What the simulated OBS Studio keeps for the profile that is current (the video, the stream service, the configuration) and for the programs that control it (the persistent data): what the requests of the
    /// configuration read and write, as obs-websocket 5.7.6 does it (RequestHandler_Config.cpp). The simulation has no output, so the refusals of a request while an output runs (500) do not happen.
    /// </summary>
    // ASSUMPTION: OBS Studio reads the video from the configuration when it loads a profile, so a parameter of the video that was written to the configuration changes what it runs with after the next switch. The simulation keeps
    // the video of a profile apart from its configuration, and does not read the configuration back: a parameter that is written does not change the video (measured with OBS Studio 33.0.0: it does not change it at once).
    internal sealed partial class SimObs
    {
        /// <summary>The video of a profile that was made new: measured with OBS Studio 33.0.0 (the canvas of the screen, an output of 1280 by 720 at 30 frames per second).</summary>
        internal static readonly SimVideo FreshVideo = new(30, 1, 1920, 1080, 1280, 720);

        /// <summary>The types of service that the simulation can make.</summary>
        internal static readonly IReadOnlyList<string> ServiceTypes = ["rtmp_common", "rtmp_custom"];

        // The defaults that OBS Studio gives the parameters of a profile (config_set_default_*, in OBSBasic::InitBasicConfigDefaults): the mode of the outputs, and the folder for recordings of both modes. The canvas
        // of the screen is a default too (the capture of a real OBS Studio shows Video/BaseCX with a default of 1920): the simulation has the one of its own screen.
        private static readonly IReadOnlyDictionary<(string Category, string Name), string> ConfigDefaults = new Dictionary<(string Category, string Name), string>
        {
            [("Output", "Mode")] = "Simple",
            [("SimpleOutput", "FilePath")] = DefaultRecordDirectory,
            [("AdvOut", "RecFilePath")] = DefaultRecordDirectory,
            [("Video", "BaseCX")] = "1920",
        };

        private readonly Dictionary<string, SimProfileData> _profileData = new(StringComparer.Ordinal) { ["Untitled"] = new SimProfileData() };
        private readonly JsonObject _globalData = new();

        /// <summary>The folder for recordings that OBS Studio gives a profile: the videos folder of the user (the capture of a real OBS Studio has this one, with the name of the user taken out).</summary>
        public const string DefaultRecordDirectory = @"C:\Users\user\Videos";

        /// <summary>Gets what OBS Studio renders with now.</summary>
        public SimVideo Video => CurrentData.Video;

        /// <summary>Gets the type of the stream service of the profile that is current.</summary>
        public string ServiceType => CurrentData.ServiceType;

        /// <summary>Gets a copy of the settings of the stream service of the profile that is current.</summary>
        public JsonObject ServiceSettings => (JsonObject)CurrentData.ServiceSettings.DeepClone();

        /// <summary>Gets the folder for recordings that OBS Studio says: the one of the output mode of the profile that is current.</summary>
        public string RecordDirectory
        {
            get
            {
                var (mode, _) = Parameter("Output", "Mode");
                return (string.Equals(mode, "Advanced", StringComparison.Ordinal) ? Parameter("AdvOut", "RecFilePath").Value : Parameter("SimpleOutput", "FilePath").Value) ?? string.Empty;
            }
        }

        private SimProfileData CurrentData => _profileData[_currentProfile];

        /// <summary>
        /// Gets a parameter of the configuration of the profile that is current as GetProfileParameter says it: the value (what was set, or else the default) and the default; both are null for a parameter that
        /// has neither, and the default is null for one that was set and has none.
        /// </summary>
        public (string? Value, string? Default) Parameter(string category, string name)
        {
            var key = (category, name);
            var hasDefault = ConfigDefaults.TryGetValue(key, out var fallback);
            if (CurrentData.Config.TryGetValue(key, out var set))
            {
                return (set, hasDefault ? fallback : null);
            }

            return hasDefault ? (fallback, fallback) : (null, null);
        }

        /// <summary>
        /// Writes a parameter to the configuration of the profile that is current, or removes what was set (SetProfileParameter).
        /// </summary>
        /// <returns>False for a removal of a parameter that has no value that was set (a default does not count): there was nothing to remove.</returns>
        public bool SetParameter(string category, string name, string? value)
        {
            var config = CurrentData.Config;
            if (value is null)
            {
                return config.Remove((category, name));
            }

            config[(category, name)] = value;
            return true;
        }

        /// <summary>Changes the video of the profile that is current (SetVideoSettings): what is given is written to the configuration of the profile, and OBS Studio renders with it.</summary>
        public void SetVideo(SimVideo video, bool changeFps, bool changeBase, bool changeOutput)
        {
            var data = CurrentData;
            var current = data.Video;
            if (changeFps)
            {
                data.Config[("Video", "FPSType")] = "2";
                data.Config[("Video", "FPSNum")] = Text(video.FpsNumerator);
                data.Config[("Video", "FPSDen")] = Text(video.FpsDenominator);
                current = current with { FpsNumerator = video.FpsNumerator, FpsDenominator = video.FpsDenominator };
            }

            if (changeBase)
            {
                data.Config[("Video", "BaseCX")] = Text(video.BaseWidth);
                data.Config[("Video", "BaseCY")] = Text(video.BaseHeight);
                current = current with { BaseWidth = video.BaseWidth, BaseHeight = video.BaseHeight };
            }

            if (changeOutput)
            {
                data.Config[("Video", "OutputCX")] = Text(video.OutputWidth);
                data.Config[("Video", "OutputCY")] = Text(video.OutputHeight);
                current = current with { OutputWidth = video.OutputWidth, OutputHeight = video.OutputHeight };
            }

            data.Video = current;
        }

        /// <summary>Changes the folder for recordings of the profile that is current (SetRecordDirectory): both output modes get it.</summary>
        public void SetRecordDirectory(string directory)
        {
            var data = CurrentData;
            data.Config[("AdvOut", "RecFilePath")] = directory;
            data.Config[("SimpleOutput", "FilePath")] = directory;
        }

        /// <summary>
        /// Changes the stream service of the profile that is current (SetStreamServiceSettings): a service of the type that it has is given the settings laid over its own (a field that is an object replaces the object that was
        /// there, and a field that is null is ignored: obs_data_apply, as for the settings of an input), and another type makes a new service, with the settings that were given.
        /// </summary>
        /// <returns>False for a type that OBS Studio does not have a service of.</returns>
        public bool SetService(string type, JsonObject given)
        {
            if (!ServiceTypes.Contains(type, StringComparer.Ordinal))
            {
                return false;
            }

            var data = CurrentData;
            var merged = string.Equals(type, data.ServiceType, StringComparison.Ordinal) ? (JsonObject)data.ServiceSettings.DeepClone() : new JsonObject();
            foreach (var (name, value) in given)
            {
                if (SimWorld.WithoutNulls(value?.DeepClone()) is { } kept)
                {
                    merged[name] = kept;
                }
            }

            data.ServiceType = type;
            data.ServiceSettings = (JsonObject)Sorted(merged)!;
            return true;
        }

        /// <summary>Gets what a slot of the persistent data holds, or null for a slot that nobody put anything in.</summary>
        /// <param name="global">True for the global realm, false for the realm of the profile that is current.</param>
        /// <param name="slot">The name of the slot.</param>
        public JsonNode? PersistentData(bool global, string slot)
        {
            var slots = global ? _globalData : CurrentData.PersistentData;
            return slots.TryGetPropertyValue(slot, out var value) ? value?.DeepClone() : null;
        }

        /// <summary>Puts a value in a slot of the persistent data, in the order of the members that obs-websocket writes.</summary>
        /// <param name="global">True for the global realm, false for the realm of the profile that is current.</param>
        /// <param name="slot">The name of the slot.</param>
        /// <param name="value">The value, which is not null: obs-websocket takes a null for a field that is missing.</param>
        public void SetPersistentData(bool global, string slot, JsonNode value)
        {
            (global ? _globalData : CurrentData.PersistentData)[slot] = Sorted(value);
        }

        // What a profile that is made has (it is clean: not a copy of the one that was current).
        private void AddProfileData(string name)
        {
            _profileData[name] = new SimProfileData();
        }

        private void RemoveProfileData(string name)
        {
            _profileData.Remove(name);
        }

        // Every profile has data of its own, and nothing else has.
        private void FindProfileDataProblems(List<string> problems)
        {
            foreach (var name in _profileNames.Where(name => !_profileData.ContainsKey(name)))
            {
                problems.Add($"The profile \"{name}\" has no data.");
            }

            foreach (var name in _profileData.Keys.Where(name => !_profileNames.Contains(name, StringComparer.Ordinal)))
            {
                problems.Add($"There is data of the profile \"{name}\", which does not exist.");
            }
        }

        private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);
    }
}
