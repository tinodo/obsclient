using OBSStudioClient.Enums;

namespace OBSStudioClient.TestApp.Catalog;

/// <summary>
/// The requests that tools/protocol/generate.py does not write ([manual] in tools/protocol/overlay.toml), and the
/// shortcuts of the client, which are methods of their own rather than requests of the protocol. The generator checks
/// that every manual request has a descriptor here.
/// </summary>
internal static class ManualCatalog
{
    private const string ProjectorNote = "Note: this request serves to provide feature parity with 4.x. It is very likely to be changed or deprecated in a future release of obs-websocket.";

    /// <summary>The manual requests and the shortcuts.</summary>
    public static IReadOnlyList<RequestDescriptor> Requests { get; } =
    [
        new(
            "Sleep",
            "General",
            "Sleeps for a time duration or number of frames. Only available in request batches with execution type SerialRealtime or SerialFrame: OBS Studio refuses it as a request of its own, so it can only be added to a batch.",
            "5.0.0",
            [
                new("sleepMillis", ParameterKind.Integer, "int?", false, "Number of milliseconds to sleep for (SerialRealtime, between 0 and 50000). Give this or sleepFrames, not both."),
                new("sleepFrames", ParameterKind.Integer, "int?", false, "Number of frames to sleep for (SerialFrame, between 0 and 10000). Give this or sleepMillis, not both."),
            ],
            null,
            (batch, args) => batch.AddSleepRequest(args.OptionalInt("sleepMillis"), args.OptionalInt("sleepFrames"))),
        new(
            "TriggerHotkeyByKeySequence",
            "General",
            "Triggers a hotkey using a sequence of keys.",
            "5.0.0",
            [
                new("keyId", ParameterKind.Enum, "ObsKey?", false, "The OBS key ID to use. See https://github.com/obsproject/obs-studio/blob/master/libobs/obs-hotkeys.h", EnumType: typeof(ObsKey)),
                new("keyModifier", ParameterKind.Enum, "KeyModifier?", false, "Key modifier to apply.", EnumType: typeof(KeyModifier)),
            ],
            (client, args) => Invocation.Of(client.TriggerHotkeyByKeySequence(args.OptionalEnumValue<ObsKey>("keyId"), args.OptionalEnumValue<KeyModifier>("keyModifier"))),
            (batch, args) => batch.AddTriggerHotkeyByKeySequenceRequest(args.OptionalEnumValue<ObsKey>("keyId"), args.OptionalEnumValue<KeyModifier>("keyModifier"))),
        new(
            "SetInputVolumeMul",
            "Shortcuts",
            "Sets the volume setting of an input, in mul. A shortcut of the client for SetInputVolume.",
            "5.0.0",
            [
                new("input", ParameterKind.Input, "InputRef", true, "The input to set the volume of, by name or by UUID."),
                new("inputVolumeMul", ParameterKind.Number, "float", true, "Volume setting in mul (between 0 and 20)."),
            ],
            (client, args) => Invocation.Of(client.SetInputVolumeMul(args.Input("input"), args.Float("inputVolumeMul"))),
            null,
            WireType: "SetInputVolume"),
        new(
            "SetInputVolumeDb",
            "Shortcuts",
            "Sets the volume setting of an input, in dB. A shortcut of the client for SetInputVolume.",
            "5.0.0",
            [
                new("input", ParameterKind.Input, "InputRef", true, "The input to set the volume of, by name or by UUID."),
                new("inputVolumeDb", ParameterKind.Number, "float", true, "Volume setting in dB (between -100 and 26)."),
            ],
            (client, args) => Invocation.Of(client.SetInputVolumeDb(args.Input("input"), args.Float("inputVolumeDb"))),
            null,
            WireType: "SetInputVolume"),
        new(
            "OpenVideoMixProjectorOnMonitor",
            "Shortcuts",
            "Opens a projector for a specific output video mix on a monitor. A shortcut of the client for OpenVideoMixProjector. " + ProjectorNote,
            "5.0.0",
            [
                new("videoMixType", ParameterKind.Enum, "MixType", true, "Type of mix to open.", EnumType: typeof(MixType)),
                new("monitorIndex", ParameterKind.Integer, "int", true, "Monitor index, use GetMonitorList to obtain it. Use -1 for windowed mode."),
            ],
            (client, args) => Invocation.Of(client.OpenVideoMixProjectorOnMonitor(args.EnumValue<MixType>("videoMixType"), args.Int("monitorIndex"))),
            null,
            WireType: "OpenVideoMixProjector"),
        new(
            "OpenVideoMixProjectorWindow",
            "Shortcuts",
            "Opens a windowed projector for a specific output video mix. A shortcut of the client for OpenVideoMixProjector. " + ProjectorNote,
            "5.0.0",
            [
                new("videoMixType", ParameterKind.Enum, "MixType", true, "Type of mix to open.", EnumType: typeof(MixType)),
                new("projectorGeometry", ParameterKind.Text, "string", true, "Size and position data for a windowed projector, in Qt Base64 encoded format. See ObsClient.GetGeometry."),
            ],
            (client, args) => Invocation.Of(client.OpenVideoMixProjectorWindow(args.EnumValue<MixType>("videoMixType"), args.Text("projectorGeometry"))),
            null,
            WireType: "OpenVideoMixProjector"),
        new(
            "OpenSourceProjectorOnMonitor",
            "Shortcuts",
            "Opens a projector for a source on a monitor. A shortcut of the client for OpenSourceProjector. " + ProjectorNote,
            "5.0.0",
            [
                new("source", ParameterKind.Source, "SourceRef", true, "The source to open a projector for, by name or by UUID."),
                new("monitorIndex", ParameterKind.Integer, "int", true, "Monitor index, use GetMonitorList to obtain it. Use -1 for windowed mode."),
                new("canvasUuid", ParameterKind.Guid, "Guid?", false, "UUID of the canvas the source is in, if the source is given by name."),
            ],
            (client, args) => Invocation.Of(client.OpenSourceProjectorOnMonitor(args.Source("source"), args.Int("monitorIndex"), args.OptionalUuid("canvasUuid"))),
            null,
            WireType: "OpenSourceProjector"),
        new(
            "OpenSourceProjectorWindow",
            "Shortcuts",
            "Opens a windowed projector for a source. A shortcut of the client for OpenSourceProjector. " + ProjectorNote,
            "5.0.0",
            [
                new("source", ParameterKind.Source, "SourceRef", true, "The source to open a projector for, by name or by UUID."),
                new("projectorGeometry", ParameterKind.Text, "string", true, "Size and position data for a windowed projector, in Qt Base64 encoded format. See ObsClient.GetGeometry."),
                new("canvasUuid", ParameterKind.Guid, "Guid?", false, "UUID of the canvas the source is in, if the source is given by name."),
            ],
            (client, args) => Invocation.Of(client.OpenSourceProjectorWindow(args.Source("source"), args.Text("projectorGeometry"), args.OptionalUuid("canvasUuid"))),
            null,
            WireType: "OpenSourceProjector"),
    ];
}
