namespace OBSStudioClient.Tests.Simulation
{
    using System.Text.Json.Nodes;

    /// <summary>
    /// A kind of input that the simulation can create.
    /// </summary>
    /// <param name="Id">The kind, with its version.</param>
    /// <param name="UnversionedId">The kind without its version.</param>
    /// <param name="Caps">The capability flags (obs_source_info.output_flags) of the kind.</param>
    /// <param name="DefaultSettings">The default settings of the kind, as JSON text.</param>
    internal sealed record SimInputKind(string Id, string UnversionedId, int Caps, string DefaultSettings)
    {
        /// <summary>The flag of a kind that has audio: it has a mute state and a volume (fact 18).</summary>
        public const int AudioCap = 2;

        /// <summary>Gets a value indicating whether the kind has audio.</summary>
        public bool HasAudio => (Caps & AudioCap) != 0;

        /// <summary>Makes a new object of the default settings.</summary>
        public JsonObject MakeDefaultSettings()
        {
            return (JsonObject)JsonNode.Parse(DefaultSettings)!;
        }
    }

    /// <summary>
    /// The kinds of input of the simulation. The capabilities and default settings are what a real OBS Studio 33.0.0 reports (the captures
    /// of the tests), except where noted.
    /// </summary>
    internal static class SimInputKinds
    {
        /// <summary>Gets the kinds in the order in which OBS Studio lists them.</summary>
        public static IReadOnlyList<SimInputKind> All { get; } =
        [
            new("image_source", "image_source", 32769, """{"linear_alpha":false,"unload":false}"""),
            new("color_source_v3", "color_source", 32777, """{"color":4291940817,"height":1080,"width":1920}"""),

            // ASSUMPTION: not in a capture. The flags are those that the source of obs-browser registers (VIDEO, AUDIO, CUSTOM_DRAW,
            // INTERACTION, DO_NOT_DUPLICATE and SRGB: 32939), so a browser source has audio. A measurement of GetInputList in a real OBS
            // Studio with a browser source would confirm it.
            new(
                "browser_source",
                "browser_source",
                32939,
                """{"css":"body { background-color: rgba(0, 0, 0, 0); margin: 0px auto; overflow: hidden; }","fps":30,"fps_custom":false,"height":600,"reroute_audio":false,"restart_when_active":false,"shutdown":false,"url":"https://obsproject.com/browser-source","webpage_control_level":1,"width":800}"""),
            new(
                "ffmpeg_source",
                "ffmpeg_source",
                8327,
                """{"buffering_mb":2,"clear_on_media_end":true,"is_local_file":true,"linear_alpha":false,"log_changes":true,"looping":false,"reconnect_delay_sec":10,"restart_on_activate":true,"speed_percent":100}"""),
            new(
                "text_gdiplus_v3",
                "text_gdiplus",
                32777,
                """{"align":"left","antialiasing":true,"bk_color":0,"bk_opacity":0,"chatlog_lines":6,"color":16777215,"extents_cx":100,"extents_cy":100,"extents_wrap":true,"font":{"face":"Arial","size":256},"gradient_color":16777215,"gradient_dir":90.0,"gradient_opacity":100,"opacity":100,"outline_color":16777215,"outline_opacity":100,"outline_size":2,"transform":0,"valign":"top"}"""),
            new("wasapi_input_capture", "wasapi_input_capture", 130, """{"device_id":"default","use_device_timing":false}"""),
            new("wasapi_output_capture", "wasapi_output_capture", 642, """{"device_id":"default","use_device_timing":true}"""),
        ];

        /// <summary>Finds a kind by its (versioned) name.</summary>
        public static SimInputKind? Find(string id)
        {
            return All.FirstOrDefault(kind => string.Equals(kind.Id, id, StringComparison.Ordinal));
        }
    }
}
