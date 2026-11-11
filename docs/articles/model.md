# ObsModel: controlling OBS Studio by name

> [!NOTE]
> This part of the library is new in 4.0 (it first appeared in 4.0.0-rc.2). Its API is closed like the rest of OBSClient: the versions of 4 that follow add to it, and do not break it.
> You can ignore this article if you only use the requests.
>
> `ObsModel` is one of the [two ways to work with OBS Studio](two-ways.md). The other is `ObsClient`, the protocol as it is, which the model is built on.

OBS Studio identifies most things by a UUID, a scene item id or an index. People think in names: *"mute the Mic"*, *"hide the Webcam in the Gaming scene"*.
[`ObsModel`](xref:OBSStudioClient.Model.ObsModel) closes that gap. When it connects it reads your scenes, inputs and scene items, it listens to the events that
OBS Studio sends when they change, and it keeps a copy in memory. You ask the copy, by name, and you change things by name; the UUIDs, the ids and the indexes stay inside.

It is built **on** the plain `ObsClient`, and hides nothing: every request and event of OBS Studio is still available through [`obs.Client`](xref:OBSStudioClient.Model.ObsModel.Client).

## What you need

- OBS Studio **30.1 or newer** (obs-websocket 5.4 or newer): the first version that gives scenes and inputs a UUID. With an older OBS Studio, `ConnectAsync` throws an
  [`ObsVersionNotSupportedException`](xref:OBSStudioClient.Exceptions.ObsVersionNotSupportedException); use the plain client, which works from OBS Studio 28.
- From OBS Studio 31.1 (obs-websocket 5.6) OBS Studio says which inputs have audio. Before that the model asks for the audio state of every input and takes the refusal
  for an input without audio as the answer: the model is the same, and connecting takes two more requests for each such input.

## A first program

```csharp
using OBSStudioClient.Model;

await using var obs = new ObsModel();
await obs.ConnectAsync("localhost", 4455, "password");   // returns when the model is complete

var gaming = obs.Scenes["Gaming"];
Console.WriteLine($"{gaming.Name} shows {gaming.Items.Count} things; the webcam is {(gaming.Items["Webcam"].IsEnabled ? "on" : "off")}."); // no request is sent

await gaming.SwitchToAsync();                            // put it on the air
await obs.Inputs["Mic"].SetMutedAsync(true);             // mute the input
await gaming.Items["Webcam"].SetEnabledAsync(false);     // hide the webcam in this scene

obs.Changed += (_, e) => Console.WriteLine(e);           // and hear what changes, also what the user does in OBS Studio
```

A runnable version with a small command prompt (`scene "Gaming"`, `mute "Mic"`, `hide "Gaming" "Webcam"`, and more) is in the repository:
`samples/ModelSample`. Open the project in Visual Studio and press F5, or run `dotnet run --project samples/ModelSample`.

## Reading: from memory

Everything that is a property is answered from the model, at once, and can be read from any thread. The model is updated on a thread of its own, so what you see changes between two reads.

| Object | What you can read |
| --- | --- |
| [`ObsModel`](xref:OBSStudioClient.Model.ObsModel) | `Scenes`, `Inputs`, `ProgramScene`, `PreviewScene`, `IsStudioModeEnabled`, `Transitions`, `Outputs`, `State`, `IsStale`, `IsExiting`, `ObsVersion`, `WebSocketVersion`, `Platform`, `SupportedImageFormats`, `Client` |
| [`ObsScene`](xref:OBSStudioClient.Model.ObsScene) | `Name`, `Uuid`, `Index`, `IsProgram`, `IsPreview`, `IsGroup`, `Items`, `Filters`, `State` |
| [`ObsInput`](xref:OBSStudioClient.Model.ObsInput) | `Name`, `Uuid`, `Kind`, `HasAudio`, `IsMuted`, `VolumeDb`, `VolumeMul`, `AudioBalance`, `SyncOffset`, `MonitorType`, `AudioTracks`, `Settings`, `Filters`, `State`, `GetSceneItems()` (every scene item that shows it); `IsActive` and `IsShowing` when the model is asked to follow them (see [Is an input on the air? Activity](#is-an-input-on-the-air-activity)), and `Levels` when it is asked to follow the levels of the audio (see [How loud is it? Audio levels](#how-loud-is-it-audio-levels)) |
| [`ObsSceneItem`](xref:OBSStudioClient.Model.ObsSceneItem) | `Scene`, `Source` (the input or scene that it shows), `Id`, `Index`, `IsEnabled`, `IsLocked`, `IsRemoved` |
| [`ObsFilters`](xref:OBSStudioClient.Model.ObsFilters) | the filters of a scene or an input, from the first: `Filters["Gain"]` by name, by position, `TryGet`, `Find`, `Contains` |
| [`ObsFilter`](xref:OBSStudioClient.Model.ObsFilter) | `Source`, `Name`, `Kind`, `Index`, `IsEnabled`, `IsRemoved` |
| [`ObsOutputs`](xref:OBSStudioClient.Model.ObsOutputs) | `Stream`, `Record`, `VirtualCamera`, `ReplayBuffer`; also the four as a list, and by [`ObsOutputKind`](xref:OBSStudioClient.Model.ObsOutputKind) |
| [`ObsOutput`](xref:OBSStudioClient.Model.ObsOutput) | `Kind`, `Name`, `Status` ([`ObsOutputStatus`](xref:OBSStudioClient.Model.ObsOutputStatus)), `IsActive`; the recording ([`ObsRecordOutput`](xref:OBSStudioClient.Model.ObsRecordOutput)) also has `IsPaused` and `CurrentFile`, and the replay buffer ([`ObsReplayBufferOutput`](xref:OBSStudioClient.Model.ObsReplayBufferOutput)) `LastSavedPath` |
| [`ObsTransitions`](xref:OBSStudioClient.Model.ObsTransitions) | `Current` ([`ObsTransition`](xref:OBSStudioClient.Model.ObsTransition): `Name`, `Uuid`, `Kind`, `IsFixed`, `IsConfigurable`), `Duration`, `IsRunning` (see [Studio mode, the preview scene and transitions](#studio-mode-the-preview-scene-and-transitions)) |

## Names

A name is looked up the way OBS Studio does it: exactly, and with case, against the names the objects have **now** (a renamed scene is found by its new name).

```csharp
var scene = obs.Scenes["Gaming"];            // throws ObsNotFoundException (it suggests close names) when there is none
if (obs.Inputs.TryGet("Mic", out var mic)) { }   // no exception when it is missing
var maybe = obs.Scenes.Find("Gaming");       // null when it is missing
bool there = obs.Scenes.Contains("Gaming");
var byUuid = obs.Scenes[uuid];               // a UUID works as well, and survives a rename
```

- With [`ObsModelOptions.IgnoreCase`](xref:OBSStudioClient.Model.ObsModelOptions.IgnoreCase) a name that differs only by case matches too, when there is no exact match and it leaves
  one object. Two matches throw an [`ObsAmbiguousNameException`](xref:OBSStudioClient.Exceptions.ObsAmbiguousNameException) that lists them.
- A scene item is found by the name of the **source it shows**: `scene.Items["Webcam"]`. A scene can show the same source more than once: the indexer then throws
  an `ObsAmbiguousNameException`, `FindAll("Webcam")` returns all of them, and `ById(id)` finds one by its id.
- The collections (`Scenes`, `Inputs`, `Items`) are **snapshots**: they do not change while you look at them, and they are safe to use from any thread. Ask for them again to see what changed.

## Changing things

Changes are methods on the objects. They are made **one at a time, in the order in which you ask for them**, and each one returns **when the model shows the change**, so that what you read next is what you changed.

| On | Method | What it does |
| --- | --- | --- |
| `ObsModel` | `CreateSceneAsync(name)` | makes a scene |
| | `SetStudioModeEnabledAsync(bool)` | switches studio mode on or off (see [Studio mode, the preview scene and transitions](#studio-mode-the-preview-scene-and-transitions)) |
| `ObsScene` | `SwitchToAsync()` | puts the scene on the air |
| | `SwitchToPreviewAsync()` | makes the scene the preview scene of studio mode |
| | `SetTransitionOverrideAsync(name, duration)`, `ClearTransitionOverrideAsync()` | gives the scene a transition and a duration of its own, or takes them away |
| | `CreateInputAsync(name, kind, settings)` | makes an input, and a scene item that shows it in this scene |
| | `AddAsync(source)` | adds a scene item that shows an input or a scene |
| | `RenameAsync(name)`, `RemoveAsync()` | |
| `ObsInput` | `SetMutedAsync(bool)`, `ToggleMuteAsync()` | |
| | `SetVolumeDbAsync(double)`, `SetVolumeMulAsync(double)` | |
| | `SetSettingsAsync(settings, overlay)` | changes the settings of the input (see [Input settings, audio and kinds](#input-settings-audio-and-kinds)) |
| | `SetAudioBalanceAsync(float)`, `SetSyncOffsetAsync(TimeSpan)`, `SetMonitorTypeAsync(type)`, `SetAudioTracksAsync(tracks)` | the audio details of the input |
| | `SetEnabledInAllScenesAsync(bool)` | shows or hides every scene item that shows the input |
| | `RenameAsync(name)`, `RemoveAsync()` | |
| `ObsSceneItem` | `SetEnabledAsync(bool)`, `ToggleEnabledAsync()` | |
| | `SetLockedAsync(bool)`, `SetIndexAsync(int)`, `RemoveAsync()` | |
| | `SetTransformAsync(update)`, `SetBlendModeAsync(mode)` | position, size and crop, and the blend mode (see [Position, size and crop: transforms](#position-size-and-crop-transforms)) |
| | `DuplicateAsync(destination)` | makes a copy of the scene item, in its scene or in another one |
| `ObsScene`, `ObsInput` | `CreateFilterAsync(name, kind, settings)` | makes a filter, on top of the others (see [Filters](#filters)) |
| `ObsFilter` | `SetEnabledAsync(bool)`, `ToggleEnabledAsync()` | |
| | `RenameAsync(name)`, `SetIndexAsync(int)`, `RemoveAsync()` | |
| `ObsOutput` | `StartAsync()`, `StopAsync()`, `ToggleAsync()` | return when OBS Studio has **begun** (see [Outputs](#outputs)) |
| `ObsRecordOutput` | `PauseAsync()`, `ResumeAsync()`, `TogglePauseAsync()` | |
| `ObsReplayBufferOutput` | `SaveAsync()` | OBS Studio writes the file a moment later |
| `ObsTransitions` | `SetCurrentAsync(name)`, `SetDurationAsync(duration)` | choose the transition, and how long the transitions take |
| | `TriggerAsync()` | the Transition button of studio mode: returns when the transition has started |

Every method takes a `CancellationToken`. A token that ends before the request is sent keeps it from being sent; one that was sent is not undone.

**A change is never made twice.** When something goes wrong the exception says exactly what is known:

| Exception | What happened | Was it changed? |
| --- | --- | --- |
| [`ObsNotConnectedException`](xref:OBSStudioClient.Exceptions.ObsNotConnectedException) | there is no connection (or it ended and another was made) | no |
| [`ObsNotReadyException`](xref:OBSStudioClient.Exceptions.ObsNotReadyException) | OBS Studio is changing its scene collection | no |
| [`ObsRemovedException`](xref:OBSStudioClient.Exceptions.ObsRemovedException) | what you changed was removed | no |
| [`ObsNameInUseException`](xref:OBSStudioClient.Exceptions.ObsNameInUseException) | the name is taken; the exception says what holds it | no |
| [`ObsResponseException`](xref:OBSStudioClient.Exceptions.ObsResponseException) | OBS Studio refused the change (it says why) | no |
| [`ObsUnknownOutcomeException`](xref:OBSStudioClient.Exceptions.ObsUnknownOutcomeException) | OBS Studio did not answer in time, or the connection ended | **not known**; the model reads again what it was about |
| [`ObsNotConfirmedException`](xref:OBSStudioClient.Exceptions.ObsNotConfirmedException) | OBS Studio made it, and the model could not show it in time | **yes**: do not send it again |

The messages are meant to be read: they name the object, say what was changed and what was not, and say what to do.

## Outputs

`obs.Outputs` has the four outputs of OBS Studio: `Stream`, `Record`, `VirtualCamera` and `ReplayBuffer`. They are the same objects for as long as the `ObsModel` lives, also before it connects
(they are `Stopped` until the model has read them): an output does not belong to a scene collection, so switching the collection does not replace them.

```csharp
var record = obs.Outputs.Record;
record.PropertyChanged += (_, e) => Console.WriteLine($"{record.Name}: {record.Status}");

if (!record.IsActive)
{
    await record.StartAsync();                  // returns when OBS Studio has begun, not when the recording runs
}

await record.PauseAsync();                      // only the recording can be paused
await obs.Outputs.ReplayBuffer.SaveAsync();     // the replay buffer has to be running (and enabled in OBS Studio)
```

`Status` is an [`ObsOutputStatus`](xref:OBSStudioClient.Model.ObsOutputStatus): `Stopped`, `Starting`, `Started`, `Stopping`, `Reconnecting` (a stream that lost its connection to the service
and tries again) or `Paused` (the recording). `IsActive` is true for `Started`, `Reconnecting` and `Paused`.

- **A start or a stop returns when OBS Studio has *begun*, not when it is done.** A stream connects to the streaming service, which can take seconds and can fail: it is `Starting`, and then `Started`, or `Stopped` again.
  Watch `Status` (`PropertyChanged`, or `obs.Changed`) for how it goes.
- `ToggleAsync()` starts the output when it is not running and stops it when it is, and returns `true` when it was started. OBS Studio decides, so two toggles that you ask for one after the other make two toggles.
- When OBS Studio refuses (the output runs already, it does not run, there is no streaming service, the replay buffer is not enabled), the call throws an `ObsResponseException` whose `ErrorCode`
  says why (`OutputRunning`, `OutputNotRunning`, `OutputDisabled`, and others), and nothing was changed.
- **Going live is a decision.** `obs.Outputs.Stream.StartAsync()` starts the stream on the service that OBS Studio has set up. The sample asks you to type `yes` first.
- **The files.** `record.CurrentFile` is the file that the recording writes, or wrote last, and `replayBuffer.LastSavedPath` is the replay that was saved last. OBS Studio says them in events and in no request, so the model
  knows the files of what it saw happen: a recording that it saw start (obs-websocket 5.5.0 and newer say the file then; older ones say it when the recording stops), go on in a new file (`record.FileChanged`) and stop, and the replays
  that were saved while it was connected (`replayBuffer.Saved`). A recording that was split says, when it stops, the file that it started with: `CurrentFile` keeps the file that the split announced, which is the one that
  was written last. `await replayBuffer.GetLastReplayFileAsync()` asks OBS Studio for the newest replay, also one that was saved before the model connected (the replay buffer has to run). OBS Studio writes a path with
  forward slashes on every platform (`C:/Videos/2026-11-10 21-59-29.mp4`): the model hands it on as it is. A property changes before the event that says why, so a handler of `FileChanged` or `Saved` finds the new value.
- **OBS Studio does not always say that a start failed.** A recording whose encoder does not start shows an error in the window of OBS Studio and sends no event after `Starting` (seen with OBS Studio 33.0.0). The model reads
  an output that is still starting or stopping after [`OutputStepTimeout`](xref:OBSStudioClient.Model.ObsModelOptions.OutputStepTimeout) (30 seconds): when OBS Studio does not run it, `Status` is `Stopped`. A stream that needs
  longer than that to connect shows `Stopped` until OBS Studio says that it started: raise the setting when your streaming service is slow.
- **A recording can be answered "paused" and not pause.** A recording that uses the encoder of the stream (the default of the simple output) cannot be paused, and a recording that does not run is not refused: OBS Studio answers `PauseAsync()`
  with success and sends no event. The call returns with what OBS Studio holds, so look at `IsPaused` after it.
- Within a connection the events are what the model goes by: OBS Studio can go on saying that a recording runs for a moment after it said that it stopped, and that does not make the model show `Started` again.
- **`Started` also says that an output came back.** A stream that lost its connection is `Reconnecting`, and is `Started` again when OBS Studio says that it reconnected (`OBS_WEBSOCKET_OUTPUT_RECONNECTED`); a recording that was paused is `Paused`,
  and is `Started` again when OBS Studio says that it resumed (`OBS_WEBSOCKET_OUTPUT_RESUMED`). The status does not tell a start from a come-back, but the change does: `Reconnecting` to `Started` and `Paused` to `Started` are come-backs. A handler
  that has to react to a start or a stop only (to say that the stream is live, say) looks at the change, or uses the events of the plain client, which say what OBS Studio sent (`obs.Client.StreamStateChanged`, `RecordStateChanged`,
  `VirtualcamStateChanged` and `ReplayBufferStateChanged`).
- While there is no connection the outputs show what was known last (`IsStale` says so); when it is back they show what OBS Studio does.

## Asking and telling

Some things that you ask OBS Studio, or tell it, change nothing that the model holds: its statistics, a screenshot, a hotkey, a window to open. They are methods too, and they take their turn in the same line as the
changes, so a question that you ask after a change is asked after it.

| On | Method | What it does |
| --- | --- | --- |
| `ObsModel` | `GetStatsAsync()` | CPU, memory, free disk space, frame rate, skipped frames, and the traffic of the WebSocket connection |
| | `GetHotkeysAsync()` | the names of the hotkeys of OBS Studio |
| | `TriggerHotkeyAsync(name, context)`, `TriggerHotkeyAsync(key, modifiers)` | triggers a hotkey by its name (`context`: the name of the source that has it), or presses a key sequence |
| | `BroadcastCustomEventAsync(data)` | sends a custom event to every client of OBS Studio, this model too (see [Things that happen](#things-that-happen)) |
| | `CallVendorRequestAsync(vendor, request, data)` | calls a request that a plugin or a script registered |
| | `GetMonitorsAsync()`, `OpenVideoMixProjectorAsync(mix, monitor, geometry)` | the monitors that OBS Studio sees, and a projector of the preview, the program or the multiview |
| | `GetInputKindsAsync(unversioned)`, `GetFilterKindsAsync()` | the kinds of inputs and of filters that OBS Studio has |
| | `GetDefaultInputSettingsAsync(kind)`, `GetDefaultFilterSettingsAsync(kind)` | the settings that a new input or filter of a kind starts with |
| | `GetSpecialInputsAsync()` | the inputs that OBS Studio uses for the desktop audio and the microphones, as [`ObsSpecialInputs`](xref:OBSStudioClient.Model.ObsSpecialInputs) |
| `ObsScene`, `ObsInput` | `GetActivityAsync()` | whether it is on the air (`VideoActive`), and whether it is shown anywhere (`VideoShowing`) |
| | `GetScreenshotAsync(format, width, height, quality)` | a picture of it, as an [`ObsScreenshot`](xref:OBSStudioClient.Model.ObsScreenshot): the bytes, `ToDataUri()`, `SaveAsync(path)` |
| | `SaveScreenshotAsync(path, format, ...)` | has OBS Studio write the picture to a file, on its own computer |
| | `OpenProjectorAsync(monitor, geometry)`, `TriggerHotkeyAsync(name)` | a projector of it; a hotkey that it has |
| `ObsInput` | `OpenPropertiesDialogAsync()`, `OpenFiltersDialogAsync()`, `OpenInteractDialogAsync()` | opens that window in OBS Studio, for the person at its computer |
| | `GetPropertyItemsAsync(name)`, `PressPropertyButtonAsync(name)` | the choices of a dropdown of its properties window; presses a button of it |
| | `GetDeinterlaceModeAsync()`, `SetDeinterlaceModeAsync(mode)`, `GetDeinterlaceFieldOrderAsync()`, `SetDeinterlaceFieldOrderAsync(order)` | how an input that has its own video (a media source, a capture device) is deinterlaced |
| `ObsFilter` | `GetSettingsAsync()`, `SetSettingsAsync(settings, overlay)` | the settings of a filter: not kept in the model (see [Filters](#filters)) |
| `ObsOutputs` | `GetAllAsync()`, `Named(name)` | every output that OBS Studio has, the outputs that plugins add included; and one of them by its name |
| [`ObsNamedOutput`](xref:OBSStudioClient.Model.ObsNamedOutput) | `StartAsync()`, `StopAsync()`, `ToggleAsync()`, `GetStatusAsync()`, `GetSettingsAsync()`, `SetSettingsAsync(settings)` | an output that is addressed by its name |
| `ObsStreamOutput` | `SendCaptionAsync(text)`, `GetStatisticsAsync()` | a caption over the stream; the figures of the stream: duration, bytes, congestion, skipped frames |
| `ObsRecordOutput` | `SplitFileAsync()`, `CreateChapterAsync(name)`, `GetStatisticsAsync()` | the recording goes on in a new file; a chapter marker; duration and size |
| `ObsReplayBufferOutput` | `GetLastReplayFileAsync()` | the path of the newest replay |

```csharp
var stats = await obs.GetStatsAsync();                                  // CpuUsage, ActiveFps, OutputSkippedFrames, ...
await obs.Inputs["Mic"].TriggerHotkeyAsync("libobs.mute");              // a hotkey that the source has
var picture = await obs.Scenes["Gaming"].GetScreenshotAsync("png", width: 640, height: 360);
await picture.SaveAsync(@"C:\Temp\gaming.png");                         // the application writes the file
await obs.Scenes["Gaming"].SaveScreenshotAsync(@"C:\Temp\gaming.jpg", "jpg");   // or OBS Studio does
await obs.Outputs.Record.SplitFileAsync();                              // the recording goes on in a new file
```

- **A question returns when OBS Studio has answered**, and does not wait for the model: nothing in the model changes. A question that is not answered ends with a `TimeoutException`, and a connection that ends
  with an `ObsNotConnectedException`: nothing was changed, so there is no `ObsUnknownOutcomeException` for a question. A call that does something (a hotkey, a custom event, a projector, a split) throws what a change throws.
- **OBS Studio can refuse**, and the `ObsResponseException` says why. `ErrorCode` is `ResourceNotFound` for a hotkey, a vendor or an output that does not exist (and for a folder that does not exist in `SaveScreenshotAsync`: that is
  not the source being removed), `OutputNotRunning` for a caption, a split or a chapter while nothing runs, and `RequestProcessingFailed` when the output does not allow splitting or its format has no chapters.
- **Splitting a recording** needs obs-websocket 5.5.0 or newer, and file splitting set in the output: only the *advanced* output has it (*Recording*, *Automatic File Splitting*, type *Manual*). Hybrid MP4, the default format of OBS Studio 33,
  has chapters. A request that the obs-websocket of your OBS Studio does not have is refused with a message that says so (`obs.WebSocketVersion` is what it runs).
- **A screenshot** can be taken of a scene or an input. `format` is one of `obs.SupportedImageFormats` (`png` and `jpg` are always there), `width` and `height` are from 8 to 4096 (with both, the picture is scaled to fit inside),
  `quality` is from 0 to 100, or -1 for the default of the format. The picture travels over the connection as text: a large one takes time and memory, and `SaveScreenshotAsync` has OBS Studio write the file instead.
- **The windows open in OBS Studio**, for the person at its computer; the call returns when OBS Studio has opened it. obs-websocket opens them for inputs only: a scene has none. The interaction window is refused (`InvalidResourceState`) for an input that cannot be interacted with.
- **An output by its name** is for the outputs that plugins add (a second stream, a recording to another place), which send no events: the model keeps no state of them, and the calls return when OBS Studio has answered. `GetAllAsync()` lists
  the four of `obs.Outputs` too, under the names that OBS Studio gave them (they differ between its simple and its advanced output mode), and does not say which one is which. Settings are a JSON object: `GetSettingsAsync()` returns a `JsonElement`,
  `SetSettingsAsync(new { max_size_mb = 200 })` takes an object or a `JsonElement`.
- **A hotkey is a last resort**: obs-websocket advises to use another request wherever there is one, and OBS Studio does not say whether a hotkey did anything. The model learns what it did through the events, like anything else.

## Filters

Every scene and every input has `Filters`: the filters of OBS Studio (a color correction, a noise gate), from the first, which is applied first. They are found by name, in the source that has them.

```csharp
var mic = obs.Inputs["Mic"];
var gate = mic.Filters["Noise Gate"];                 // throws ObsNotFoundException (it suggests close names) when there is none
Console.WriteLine($"{gate.Name} ({gate.Kind}) is {(gate.IsEnabled ? "on" : "off")}, at position {gate.Index}");   // no request is sent

await gate.SetEnabledAsync(false);                    // or ToggleEnabledAsync()
await gate.SetIndexAsync(0);                          // make it the first
await gate.RenameAsync("Gate");                       // the same object, with another name
var gain = await mic.CreateFilterAsync("Boost", "gain_filter", new { db = 6.0 });   // the kinds: obs.Client.GetSourceFilterKindList()
await gain.RemoveAsync();
```

- **A filter has no UUID.** OBS Studio knows a filter by its name within its source, and the name can change. An `ObsFilter` stays the same object for as long as the filter exists, also when it is renamed or moved.
  The exception: a filter that is renamed while there is no connection to hear it is a new object when the connection is back (the old one is `IsRemoved`).
- **A change that you ask for right after a rename names the filter by its new name**, also when the model does not show the new name yet.
- `IsEnabled`, `Name`, `Index` and the list itself are current: OBS Studio announces every change of them (`PropertyChanged`, and `obs.Changed` with `Added` and `Removed` for filters).
- **The settings of a filter are not in the model**, and OBS Studio is the reason. The protocol has an event for a change of them (`SourceFilterSettingsChanged`), and OBS Studio does not send it: libobs tells of the
  updates of public sources, and a filter is a private one (a test with OBS Studio 33.0.0 saw no event, for a filter of a scene, of an input or of audio). A copy in the model would go out of date without a sign, so there is none.
  The model does pass the event on when a server does send it (`obs.FilterSettingsChanged`, with `e.Filter` and `e.Settings`; see [Things that happen](#things-that-happen)): do not wait for it with OBS Studio.
  Ask when you need them, and change them when you want: `await gate.GetSettingsAsync()` is what OBS Studio holds at that moment, and `await gate.SetSettingsAsync(new { open_threshold = -30 })` returns when OBS Studio has made the
  change (nothing in the model shows it, so there is nothing else to wait for). Settings that you pass to `CreateFilterAsync` start the filter with them, and `GetDefaultFilterSettingsAsync(kind)` says what the defaults of a kind are.
- The filters of a source that was announced a moment ago can be read a moment later: `await obs.WaitUntilSettledAsync()` waits for them (and for the rest of the model).
- `RenameAsync` and `CreateFilterAsync` throw an [`ObsNameInUseException`](xref:OBSStudioClient.Exceptions.ObsNameInUseException) when the source has a filter with that name already.

## Input settings, audio and kinds

An input has **settings**: what you set in its properties window (the color and the size of a color source, the text of a text source, the file of a media source). The model keeps them, and they are current: OBS Studio announces every change.

```csharp
var title = obs.Inputs["Title"];
Console.WriteLine(title.Settings.GetProperty("text").GetString());        // a JsonElement; no request is sent
await title.SetSettingsAsync(new { text = "Hello" });                     // lays the given fields over the ones that it has
await title.SetSettingsAsync(new { text = "Hello" }, overlay: false);     // or: back to the defaults of the kind first, so that these fields are all it has
var defaults = await obs.GetDefaultInputSettingsAsync(title.Kind);        // what a new input of the kind starts with
```

- **`Settings` has what was set**, and leaves out what still has the default of the kind: ask `GetDefaultInputSettingsAsync(kind)` for those (`GetInputKindsAsync()` lists the kinds). It is a snapshot: a change makes a new `JsonElement`, and one that you hold does not change.
  What the fields are called depends on the kind: look at the `Settings` of an input that is set up as you want, and send what you see.
- **OBS Studio does not check settings.** It keeps every field that it is given, also one that the kind does not know, and a value of the wrong kind makes the input do what it does with a value that it cannot use. A field that is an object replaces the
  object that was there **as a whole**, not field by field, and a field that is `null` is **ignored**: it keeps what it was (both were checked with OBS Studio 33.0.0). `SetSettingsAsync` returns when the model shows the given fields.
- **The choices of a dropdown** in the properties window of an input are asked for: `await camera.GetPropertyItemsAsync("device_id")` returns an [`ObsPropertyItem`](xref:OBSStudioClient.Model.ObsPropertyItem) for each (`Name`, `Value`, `IsEnabled`), and the `Value`
  is what to set the setting to. `PressPropertyButtonAsync("refreshnocache")` presses a button of it. OBS Studio answers `ResourceNotFound` for a property that the input does not have, and `InvalidResourceType` for one that is not a list or not a button.

**Audio.** An input with audio (`HasAudio`) has more than a mute and a volume: `AudioBalance` (0 is all left, 0.5 the middle, 1 all right), `SyncOffset` (a `TimeSpan`, from -950 ms to 20 s), `MonitorType` and `AudioTracks`. A new input has
the middle, no offset, no monitoring and all six tracks. They are properties like the others, current and with `PropertyChanged`, and they are changed with `SetAudioBalanceAsync`, `SetSyncOffsetAsync`, `SetMonitorTypeAsync` and `SetAudioTracksAsync`.

```csharp
await mic.SetAudioBalanceAsync(0.25f);                                    // a little to the left
await mic.SetSyncOffsetAsync(TimeSpan.FromMilliseconds(150));             // delay it, to match the video
await mic.SetAudioTracksAsync(ObsAudioTracks.Track1 | ObsAudioTracks.Track3);    // exactly these tracks
await mic.SetAudioTracksAsync(mic.AudioTracks | ObsAudioTracks.Track6);          // one more, and the others stay
```

- A value that OBS Studio does not take (a balance beyond 0 to 1, an offset beyond -950 ms to 20 s) is an `ArgumentOutOfRangeException` at once, and nothing is sent. The audio calls of an input without audio are an `InvalidOperationException`.
- **OBS Studio 33 has no "monitor only".** Its libobs replaced the three monitor types by a switch that is on or off, and turns `ObsMonitorType.MonitorOnly` into `MonitorAndOutput`: after `SetMonitorTypeAsync(MonitorOnly)` the input holds
  `MonitorAndOutput` (checked with OBS Studio 33.0.0). The call returns after the model has been read again, with `MonitorType` showing what OBS Studio holds, and no exception: compare it with what you asked for if that matters. A computer that cannot monitor refuses with `InvalidResourceState`.

**Deinterlacing** is asked, and not kept in the model: `await camera.GetDeinterlaceModeAsync()` and `SetDeinterlaceModeAsync(ObsDeinterlaceMode.Yadif2x)`, with `GetDeinterlaceFieldOrderAsync()` and `SetDeinterlaceFieldOrderAsync(...)` for the order of the fields. It is for the inputs
that have their own video (a media source, a capture device): OBS Studio refuses another input with `InvalidResourceState` (*"The specified input is not async."*). It needs obs-websocket 5.6.0 or newer.

**The audio devices.** `await obs.GetSpecialInputsAsync()` says which inputs OBS Studio uses for the desktop audio (`Desktop1`, `Desktop2`) and the microphones (`Mic1` to `Mic4`), as the `ObsInput` objects of the model. A slot that is not set up, or whose input the
model does not have, is `null`.

## Media inputs

An input that plays a file or a stream (a media source, a VLC video source) has a **media**: `input.Media` is an [`ObsMedia`](xref:OBSStudioClient.Model.ObsMedia) for it, and `null` for every other input. It stays the same object for as long as the input exists.

```csharp
var intro = obs.Inputs["Intro"].Media;                  // null when "Intro" is no media input
Console.WriteLine(intro.State);                         // Playing, Paused, Stopped, Ended ...: from memory, no request is sent

await intro.PlayAsync();                                // returns when the model shows Playing
await intro.PauseAsync();
var asked = await intro.TogglePlayPauseAsync();         // plays what is not playing and pauses what plays: returns the state that it asked for
await intro.SeekAsync(TimeSpan.FromSeconds(30));
await intro.SkipAsync(TimeSpan.FromSeconds(-10));

ObsMediaStatus status = await intro.GetStatusAsync();   // asks OBS Studio: the state, the length and the position
Console.WriteLine($"{status.Position} of {status.Duration}");
```

| | |
| --- | --- |
| `State` | what the media is doing: `Playing`, `Paused`, `Stopped`, `Ended`, or `None` (OBS Studio has not opened it). Current, because OBS Studio announces when a playback starts and ends and when it is told to play, pause, stop or restart. `PropertyChanged` and `Changed` say it (the entity of the change is the `ObsMedia`) |
| `PlayAsync()`, `PauseAsync()`, `StopAsync()`, `RestartAsync()` | return when the model shows `Playing`, `Paused`, `Stopped` and `Playing`. A paused media goes on; one that was stopped or ended plays from the start, and so does a restart |
| `TogglePlayPauseAsync()` | OBS Studio has no toggle, so the model asks what the media does in the turn of the change, when the changes that were asked for before it are answered: two toggles one after the other make two toggles, whatever `State` shows in between |
| `NextAsync()`, `PreviousAsync()` | the next and the previous item of the playlist of a VLC source. A single file has none: OBS Studio says that it did it, and nothing changes. The calls return when OBS Studio has answered |
| `SeekAsync(position)`, `SkipAsync(offset)` | move the media to a position, or by an offset (whole milliseconds, which is what OBS Studio takes). They return when OBS Studio has answered. They are refused with `InvalidResourceState` when the media neither plays nor is paused |
| `GetStatusAsync()` | asks OBS Studio, in the turn of the changes: the state (including `Opening`, `Buffering` and `Error`, which are never announced), the length and the position (`null` when OBS Studio does not say: it only knows while the media plays or is paused). The answer is a snapshot, and is not applied to `State` |

The length and the position are **not kept in the model**: they move all the time, and OBS Studio sends no event for them. Ask `GetStatusAsync()` when you need them.

What OBS Studio does, which the model follows (all checked with OBS Studio 33.0.0, with the media source):

- **A media source whose scene is not on the program output is not opened.** With the default setting ("restart playback when the source becomes active") its state is `None`, a play or a pause opens it, and it does not move: its position stays where it was.
  When the scene is shown the media plays from the start (`Playing`), and when the scene is no longer shown it ends (`Ended`), unless the setting of the source says otherwise. A source that does not restart when it is shown is opened and plays as soon as it is made.
- **A change of the position is carried out for a media that plays and is shown**, and it plays on from there; a position beyond the end ends the media (`Ended`). A media that is not shown takes the change and does not move. A media that is paused takes it too, and then says position zero and plays from the start.
- **`Ended` is also what OBS Studio says for a file that it cannot open** (and a play of it then answers that it plays, with a length of zero), and `Opening`, `Buffering` and `Error` only show in `GetStatusAsync()`.
- **Which inputs have a media.** The model goes by what OBS Studio says an input can do (obs-websocket 5.6.0 and newer), and by the kind (`ffmpeg_source` and `vlc_source`) when it does not say. It reads the state of each media input once when it reads the inputs, so a connect sends one request more for each of them.
- An input that was removed has no calls: they throw an `ObsRemovedException`, as for any other removed object.

## Studio mode, the preview scene and transitions

With **studio mode** on there is a **preview scene** next to the program scene: you choose in the preview what goes on the air next, and trigger a transition that puts it on the air. `obs.IsStudioModeEnabled` says whether it is on,
`obs.PreviewScene` is the preview scene (`null` while studio mode is off), and `scene.IsPreview` says it from the side of the scene. The scene transitions are in [`obs.Transitions`](xref:OBSStudioClient.Model.ObsTransitions), which is the same object for as long as the model lives.

```csharp
await obs.SetStudioModeEnabledAsync(true);                // returns when the model shows it
await obs.Scenes["Interview"].SwitchToPreviewAsync();     // choose what comes next
await obs.Transitions.TriggerAsync();                     // the Transition button: returns when the transition has started

Console.WriteLine(obs.Transitions.Current);               // the current transition: Fade, 300 ms
await obs.Transitions.SetCurrentAsync("Cut");             // by the name that OBS Studio shows
await obs.Transitions.SetDurationAsync(TimeSpan.FromSeconds(1));
obs.Transitions.VideoEnded += (_, e) => Console.WriteLine($"{e.TransitionName} is over");
```

| On `obs.Transitions` | |
| --- | --- |
| `Current` | the current transition ([`ObsTransition`](xref:OBSStudioClient.Model.ObsTransition): `Name`, `Uuid`, `Kind`, `IsFixed` for a cut, which takes no time, `IsConfigurable` for one that has settings); `null` when there is none. `PropertyChanged` and `Changed` say it |
| `Duration` | how long the transitions take (a `TimeSpan?`). It is the same for all of them, and `null` until OBS Studio has said: it does not say while the current transition is a cut, until it is told or the duration changes |
| `IsRunning` | whether a transition is running: from the moment that OBS Studio says that it started until it says that its video ended. A transition that was running when the model connected is not known to run |
| `Started`, `Ended`, `VideoEnded` | .NET events, raised like the ones in [Things that happen](#things-that-happen). `VideoEnded` is the end of the transition for every kind of transition; `Ended` is the same moment, except for a stinger, which says `Ended` when it reaches its cut point and goes on until `VideoEnded` |
| `SetCurrentAsync(name)`, `SetDurationAsync(duration)` | return when the model shows the change. A transition that OBS Studio does not have is refused with `ResourceNotFound`; a duration from 50 ms to 20 seconds is what OBS Studio takes (`ArgumentOutOfRangeException` before anything is sent) |
| `TriggerAsync()` | the Transition button: the preview scene goes on the air. It returns when the model shows that a transition has started, not when it has ended, and is refused with `StudioModeNotActive` while studio mode is off |
| `GetAllAsync()`, `GetKindsAsync()` | ask OBS Studio for the transitions it has, and for the kinds it can make (the list is not kept: OBS Studio does not announce a change of it). obs-websocket cannot make a transition: the user does that in the window of OBS Studio |
| `GetSettingsAsync()`, `SetSettingsAsync(settings, overlay)` | the settings of the current transition, as a JSON object. A cut and a fade have none: OBS Studio refuses a change with `ResourceNotConfigurable` |
| `GetProgressAsync()` | how far the transition that runs has come, from 0.0 to 1.0 (1.0 when none runs): it moves all the time, so it is asked and not kept |
| `SetTBarPositionAsync(position, release)` | moves the T-bar of studio mode, which moves a transition by hand. obs-websocket says that it will be replaced, and the T-bar has a direction of its own: use `TriggerAsync()` to switch from the preview scene to the program scene |

A scene can bring a **transition of its own**: `await scene.SetTransitionOverrideAsync("Fade", TimeSpan.FromSeconds(2))`, `await scene.GetTransitionOverrideAsync()` (an [`ObsTransitionOverride`](xref:OBSStudioClient.Model.ObsTransitionOverride): `Name`, `Duration` and `IsSet`) and
`await scene.ClearTransitionOverrideAsync()`. The name and the duration are independent (a scene can have only a duration), and both `null` takes the override away. When OBS Studio switches to the scene it uses the override, and `Current` does not change for it.

What OBS Studio does, which the model follows (all checked with OBS Studio 33.0.0):

- **The program scene is the new scene from the start of a transition.** OBS Studio answers a question about the program scene with the scene that it goes to at once, and sends the event that says so when the transition has ended, which can be seconds later. The model reads the program scene when a
  transition starts, so `obs.ProgramScene` and `scene.IsProgram` show the new scene from the start, as a question to OBS Studio does, and `SwitchToAsync()` returns then: **while `Transitions.IsRunning` is still true**. Wait for it to become false (or for `VideoEnded`) when you need the scene to be completely on the air.
- **Studio mode on** makes the program scene the preview scene, and OBS Studio sends no event for that: the model reads it, so `PreviewScene` follows `IsStudioModeEnabled` a moment later. **Studio mode off** takes the preview scene away.
- **The Transition button** puts the preview scene on the air at once as far as a question goes, and when the transition has ended the preview scene is the scene that was on the air. A trigger while a transition runs does nothing, except in the first moment of a transition (while its position is still 0), when it starts
  another: the first one then ends without its video ending, which the model takes for the end of it.
- **The duration belongs to the transitions, not to the current one.** OBS Studio takes it while a cut is the current transition too (the fade has it afterwards), and sends an event only when it changed.
- **A removed preview scene** is replaced by another scene, and the model reads which. **Do not remove the program scene while studio mode is on:** OBS Studio 33.0.0 then stopped answering the requests that need its window for seconds (up to half a minute was seen), and everything that waits for it waits too. Switch to another scene first.
- **The model reads the current transition when it connects** (and the preview scene when studio mode is on), so a connect sends one or two requests more. The events of the transitions (`Transitions`) are part of what the model subscribes to.

## Position, size and crop: transforms

Where a scene item is, how large it is shown, how it is rotated, bounded and cropped is its **transform** ([`SceneItemTransform`](xref:OBSStudioClient.Classes.SceneItemTransform)). OBS Studio sends an event for every step of a drag
in the preview (sixty a second), and every program that listens pays for it, so the model **does not follow the transforms unless you ask it to**:

```csharp
using var obs = new ObsModel(new ObsModelOptions { HighVolume = ObsHighVolume.Transforms });   // follow the transforms of all the scene items
// ...
var webcam = obs.Scenes["Gaming"].Items["Webcam"];
Console.WriteLine(webcam.Transform);                                     // null without HighVolume; PropertyChanged and Changed tell when it changes

await webcam.SetTransformAsync(webcam.Transform!.ToUpdate() with { PositionX = 100, ScaleX = 0.5f, ScaleY = 0.5f });   // returns when the model shows it
await webcam.SetTransformAsync(new SceneItemTransformUpdate { CropLeft = 20 });          // only the members that are set are changed
var current = await webcam.GetTransformAsync();                          // asks OBS Studio: works with and without HighVolume
```

| On `ObsSceneItem` | |
| --- | --- |
| `Transform` | the transform, as a copy that does not change (a change gives another object): `null` unless the model follows the transforms ([`ObsModelOptions.HighVolume`](xref:OBSStudioClient.Model.ObsModelOptions.HighVolume) with [`ObsHighVolume.Transforms`](xref:OBSStudioClient.Model.ObsHighVolume)). OBS Studio sends the transform again when something else about the item changes (a blend mode, a source that is the same size): the model tells only when it is not the one that it held |
| `GetTransformAsync()` | asks OBS Studio for the transform, whatever the options are |
| `SetTransformAsync(update)` | changes the members of the [`SceneItemTransformUpdate`](xref:OBSStudioClient.Classes.SceneItemTransformUpdate) that are set, and leaves the others as they are. It returns when the model shows the change if it follows the transforms, and when OBS Studio has answered if it does not. OBS Studio refuses a rotation beyond 360 degrees, a crop below 0 and bounds below 1 (`ObsResponseException`, and nothing is changed) |
| `GetBlendModeAsync()`, `SetBlendModeAsync(mode)` | how the item is blended with what is behind it. It is asked and not kept: OBS Studio announces no change of it |
| `DuplicateAsync(destination)` | makes a copy and returns it when the model shows it: on top of its scene, or of the `destination` scene. It has the transform, the crop and whether the item is shown, and not whether it is locked, nor the blend mode |

`obs.SceneItemSelected` says that an item was selected in OBS Studio (also the one that OBS Studio selects when an item is made); it is something that happened, and the model does not keep which item is selected, because OBS Studio does not say when it is not.

What OBS Studio does (checked with OBS Studio 33.0.0, obs-websocket 5.7.6):

- **A crop and the size of a source are announced only for a scene that OBS Studio renders**: the program scene, the preview scene of studio mode, a scene that a projector shows. For another scene they are announced when it is rendered, so the transform that a model that follows
  the transforms shows of an item in such a scene has the crop and the size from before until then. The position, the scale, the rotation, the alignment and the bounds are announced at once. `SetTransformAsync` and `DuplicateAsync` read the scene again and do not depend on it, and
  `GetTransformAsync` says what OBS Studio holds.
- **A change with a crop is two events** (the crop comes last), and the call waits until the model has all of it.
- **The size of an item is the size of its source times the scale**, whatever the bounds are, and a change of the size of the source gives the items that show it a new transform.
- **A copy is made in the scene that OBS Studio is given by name.** `DuplicateSceneItem` with only the UUID of the destination makes the copy in the scene of the original (obs-websocket reads the destination only when the request has its name), so the model gives both. With the plain client
  give the destination by name.
- **When several programs change one item at the same time**, events can come in another order than the changes were made (two of fifty changes sent at once were swapped): the model shows the last event, and `GetTransformAsync` says what OBS Studio holds.

## Is an input on the air? Activity

Whether an input is **on the air** (it is in the output that goes to the stream and the recording) and whether it is **shown** (on the air, or in the preview of studio mode, a projector, or the window of its properties) is its **activity**.
OBS Studio sends an event for every input that a change reaches: a program scene that changes, a transition, a scene item that is hidden, a scene that is nested in another. Every program that listens pays for it, so the model **does not follow the activity unless you ask it to**:

```csharp
using var obs = new ObsModel(new ObsModelOptions { HighVolume = ObsHighVolume.Activity });   // follow whether the inputs are on the air
// ...
var webcam = obs.Inputs["Webcam"];
Console.WriteLine($"{webcam.IsActive} {webcam.IsShowing}");        // null without HighVolume; PropertyChanged and Changed tell when it changes
var asked = await webcam.GetActivityAsync();                       // asks OBS Studio: works with and without HighVolume, and for scenes too
```

[`ObsHighVolume.All`](xref:OBSStudioClient.Model.ObsHighVolume) follows everything that is sent often (the transforms of the scene items, the activity of the inputs and the levels of the audio).

| On `ObsInput` | |
| --- | --- |
| `IsActive` | `true` while the input is on the air; `null` unless the model follows the activity ([`ObsModelOptions.HighVolume`](xref:OBSStudioClient.Model.ObsModelOptions.HighVolume) with [`ObsHighVolume.Activity`](xref:OBSStudioClient.Model.ObsHighVolume)) |
| `IsShowing` | `true` while the input is shown: on the air, or in the preview scene, a projector or the window of its properties; `null` unless the model follows the activity |
| `GetActivityAsync()` | asks OBS Studio (`GetSourceActive`), whatever the options are; also on scenes (the model keeps no activity for a scene: OBS Studio announces none) |

With the activity the model reads it for every input when it connects (the reads go in the batches of the model, one request for each input), and for every input that is made. A model that does not follow the activity sends none of that.

What OBS Studio does (checked with OBS Studio 33.0.0, obs-websocket 5.7.6):

- **An input is on the air when the program scene shows it**, directly or in a scene that it nests, as long as the scene items that lead to it are enabled. Hiding the item of a nested scene takes its inputs off the air; an input that is reached in two ways stays on the air when one of them ends.
- **A scene that goes on the air puts its inputs on the air when the transition starts, and the scene that goes off the air takes its inputs off the air after the transition has ended.** A fade keeps the inputs of both scenes on the air while it runs. `SwitchToAsync()` returns while the transition runs
  (see [Studio mode, the preview scene and transitions](#studio-mode-the-preview-scene-and-transitions)), so the inputs of the old scene are still on the air then.
- **Studio mode shows the preview scene, and does not put it on the air.** The inputs of the preview scene are shown (`IsShowing`) and not on the air, unless the program scene shows them too. The Transition button puts the inputs of the preview scene on the air when the transition starts, and the inputs of the scene that was on the air
  go off the air when it has ended: they stay shown, because that scene is the preview scene then.
- **In studio mode what is on the air is a copy of the program scene**, made when the scene went on the air (or when studio mode was switched on). What you do to the scene itself, such as hiding a scene item or making an input in it, does not reach the copy: the input of a hidden item stays on the air, an input that is made is shown in the preview
  and not on the air. They change when the next scene goes on the air, or when studio mode is switched off and the scene itself is on the air again.
- **The events come in no fixed order.** An input is told that it is shown and that it is on the air in either order, and relative to the other events of the same change (the scene item that was made, the end of a transition) anywhere. The model does not depend on it, and `Changed` tells each of the two properties by itself.
- **An input that OBS Studio destroys is not told to go off the air**, and neither is one that is removed: there is no event, and the model shows an input that was removed as removed. A scene that is removed while it is on the air puts the scene that takes its place on the air.
- **The model reads the activity again after a connection that was lost came back**, and tells what changed while it was down once for each property, as for any other value (see [Hearing about changes](#hearing-about-changes)).

## How loud is it? Audio levels

OBS Studio measures how loud each input is, and tells every client that asks **twenty times a second**, whether anything plays or not, and it has every input that is on the air measure its audio for as long as somebody asks. It is a **stream** (no request says how loud an input is now) and a busy one, so the model
**does not follow the levels unless you ask it to**:

```csharp
using var obs = new ObsModel(new ObsModelOptions { HighVolume = ObsHighVolume.AudioLevels });   // follow the levels of the audio
obs.AudioLevels += (_, e) =>                                           // about twenty times a second, as long as the levels change
{
    foreach (var levels in e.Levels)                                   // every input that OBS Studio listed, in its order
    {
        Console.WriteLine($"{levels.Input.Name}: {string.Join(" / ", levels.Channels.Select(channel => $"{channel.PeakDb:0} dB"))}");
    }
};
// ...
var mic = obs.Inputs["Microphone"];
Console.WriteLine(mic.Levels?.Channels[0].PeakDb);                     // null while OBS Studio does not list the input; PropertyChanged says when it changes
```

[`ObsHighVolume.All`](xref:OBSStudioClient.Model.ObsHighVolume) follows everything that is sent often (the transforms of the scene items, the activity of the inputs and the levels of the audio).

| | |
| --- | --- |
| `ObsInput.Levels` | the [`ObsAudioLevels`](xref:OBSStudioClient.Model.ObsAudioLevels) of the last message of OBS Studio about the input: its `Input` and its `Channels`, the left one first. `null` unless the model follows the levels ([`ObsModelOptions.HighVolume`](xref:OBSStudioClient.Model.ObsModelOptions.HighVolume) with [`ObsHighVolume.AudioLevels`](xref:OBSStudioClient.Model.ObsHighVolume)) and OBS Studio lists the input. It is a snapshot: a new message makes a new object |
| [`ObsChannelLevel`](xref:OBSStudioClient.Model.ObsChannelLevel) | `Magnitude` (how loud it sounds: the root mean square of the last chunk of audio), `Peak` (the highest sample) and `InputPeak` (the peak as the input made it), as multipliers of the full scale (1 is 0 dB), and `MagnitudeDb`, `PeakDb` and `InputPeakDb` in decibels (-100 is silence) |
| `ObsModel.AudioLevels` | an [`ObsAudioLevelsEventArgs`](xref:OBSStudioClient.Model.ObsAudioLevelsEventArgs): `Levels` of every input that OBS Studio listed in its last message, and `Ended`, the inputs that it stopped listing since the last time the event was raised (their `Levels` is `null` now) |

What OBS Studio does (checked with OBS Studio 33.0.0, obs-websocket 5.7.6):

- **It sends a message every 50 milliseconds**, with an empty list when nothing is on the air.
- **It lists the inputs that are on the air and have audio, and no others**, in the order in which they came on the air: an input that is only shown in the preview of studio mode, an input whose scene item is hidden, a color source or any other input without audio, and an input of a scene that is not on the air are not in it.
  The inputs of a scene that goes off the air are listed until the transition has ended (a fade keeps both scenes on the air). It is the same set of inputs as `IsActive` with `HasAudio` (see [Is an input on the air? Activity](#is-an-input-on-the-air-activity)).
- **A muted input is listed.** The magnitude and the peak are what the volume and the mute make of them (0 while it is muted), and the peak of the input is the peak as the input made it: a meter that wants to show whether the source is too loud for whatever the fader says looks at `InputPeak`.
- **An input that plays nothing is listed too**, with levels of 0 after a moment, and so is one that is paused or stopped, for as long as it is on the air.
- **The first message that lists an input has no channels** (OBS Studio has no audio of it yet): `Levels` is there, and `Channels` is empty. A mono file arrives as two channels with the same numbers.
- **The first message that has audio of an input can say a peak that is far too high** (1.9 was measured often, and 2.7e20 once): a meter should limit it, or ignore the first message of an input.
- **An input that was just removed can be listed once more**, in the message that was on its way.

What the model does with it:

- **The levels are in the inputs when the message arrives**, from any thread; nothing is read, and nothing waits for another message.
- **You are told, but not of every message.** At most one notification waits in the queue of the notifications, and it tells what changed since the last one that was raised, with the newest levels: a handler that is slower than OBS Studio (or a user interface thread that is busy) gets the newest levels, and not all of them.
  The levels never fill the queue ([`MaxPendingNotifications`](xref:OBSStudioClient.Model.ObsModelOptions.MaxPendingNotifications)), and never hold up what the model reads from OBS Studio. A handler that does not return holds up the notifications behind it, as for any notification.
- **A message that says what the one before it said is not told**: a silence that stays silent is not twenty notifications a second. `PropertyChanged` of `Levels` is raised for each input whose levels changed, and when OBS Studio stops listing it. [`obs.Changed`](xref:OBSStudioClient.Model.ObsModel.Changed) does not tell the levels.
- **The levels end with the connection**: when it is lost, `Levels` of every input is `null` and `Ended` says so; they come back with the first message after the model is connected again.
- **An input that the model does not have yet is skipped**: the next message has it. The plain client passes on one `InputVolumeMeters` event every 100 ms (`obs.Client.InputVolumeMeters`, with `AdditionalEvents = EventSubscriptions.InputVolumeMeters`); the model uses every message.

## Profiles and scene collections

OBS Studio keeps its settings in **profiles** (the video settings, the stream service, the folder for recordings, the encoders) and what is in the scenes in **scene collections**. The model shows which one of each is current, and the names of all, in
[`obs.Profiles`](xref:OBSStudioClient.Model.ObsProfiles) and [`obs.SceneCollections`](xref:OBSStudioClient.Model.ObsSceneCollections). They are the same objects for as long as the model lives, so you can hold them from the start and bind to them.

```csharp
Console.WriteLine($"{obs.Profiles.Current} of {string.Join(", ", obs.Profiles.Names)}");
await obs.Profiles.CreateAsync("Podcast");                   // OBS Studio makes it and switches to it: returns when the model shows that
await obs.Profiles.SetCurrentAsync("Gaming");
await obs.Profiles.RemoveAsync("Podcast");                   // not the current one: OBS Studio would ask the user

await obs.SceneCollections.SetCurrentAsync("Stream");        // returns when the model has read the collection that OBS Studio loaded
obs.SceneCollections.PropertyChanged += (_, e) => Console.WriteLine(e.PropertyName);   // Names, Current
```

| On `obs.Profiles` and `obs.SceneCollections` | |
| --- | --- |
| `Current` | the name of the profile that is current (of the scene collection that is loaded); `null` until OBS Studio has said. `PropertyChanged` and `Changed` say when it changes, also when the user (or another program) changes it |
| `Names` | the names, in the order in which OBS Studio lists them (it sorts them by name, ignoring case). It is another list when they change: `Names` is told before `Current`, so the current one is among the names when you hear of it |
| `SetCurrentAsync(name)` | makes it current, and returns when the model shows it. OBS Studio does nothing when it is current already, and the call returns at once. A name that OBS Studio does not have throws an `ObsNotFoundException` that suggests names that are close |
| `CreateAsync(name)` | makes one, which OBS Studio makes current. For a profile it returns when the list has it and OBS Studio has switched to it; for a scene collection when the model has read it. A name that is taken throws an `ObsNameInUseException`. A profile is a clean one (the settings that OBS Studio gives a new profile); a scene collection has one scene, named `Scene` |
| `RemoveAsync(name)` (profiles only) | removes a profile and its files, and returns when the list no longer has it. **Not the current one**: it throws an `InvalidOperationException` before anything is sent (see below). There is no request that removes or renames a scene collection, or renames a profile |

What OBS Studio does, which the model follows (checked with OBS Studio 33.0.0, and read in its source):

- **The model never takes a name from an event.** Each of `ProfileListChanged`, `CurrentProfileChanging`, `CurrentProfileChanged` and `SceneCollectionListChanged` makes the model read the list again, and what the model shows is what that read says: OBS Studio does not send `ProfileListChanged` when the
  user removes the current profile in its window (it says that the current profile changed, which the model also answers with a read).
- **Switching a profile** is done when OBS Studio answers, and `CurrentProfileChanging` (with the profile that is left) and `CurrentProfileChanged` come at the same moment. **Making a profile** is answered at once, and OBS Studio lists the profile about 40 ms later and says that it is current about 90 ms after that (`CurrentProfileChanging` is not sent for it).
  Profiles that are made right after one another are made in that order, and the last one is current.
- **Removing the current profile** is not done by obs-websocket: OBS Studio asks the user in its own window ("are you sure?"), and answers at once as if it had removed it. The profile stays until somebody answers, and a question is left on the screen of the user. `RemoveAsync` refuses it for that reason.
  A switch to a profile with another sample rate or speaker setup makes OBS Studio ask the user, in its window, whether it may restart; the profile is the current one all the same.
- **A profile does not change what the model holds** (scenes, inputs, scene items, outputs): it only changes the settings of OBS Studio itself. A **scene collection** does: while OBS Studio loads another one it answers every request with 207 and sends no event, and the model is `Synchronizing`; everything that you hold belongs to the collection that was left
  (see [The connection, and scene collections](#the-connection-and-scene-collections)). The call returns when the model is `Synchronized` with the new collection. OBS Studio answers `SetCurrentSceneCollection` when it has loaded the collection, so the call waits for the answer as long as `SynchronizationTimeout` when that is more than `WriteTimeout`.
- **The model reads the two lists when it connects**, and again when it reads everything because OBS Studio switched its scene collection, so a connect sends two requests more.

## The settings of OBS Studio itself

A profile holds what OBS Studio keeps apart from the scenes, and the model has a call for each part that obs-websocket gives: the **video settings**, the **stream service**, the **folder for recordings**, the **parameters** of the profile (what OBS Studio keeps in the configuration file of the profile),
and the **data that OBS Studio keeps for an application**. OBS Studio does not announce a change of any of them, so the model keeps none of them: **a call asks, or tells, and ends when OBS Studio has answered**. They are the settings of the profile that is current when the request is made,
and the requests are made in the order of the calls: a read that you ask for before a switch of the profile is about the profile that was current then.

```csharp
var video = await obs.GetVideoSettingsAsync();                           // Video 1920x1080 canvas, 1280x720 output, 30 fps (30/1)
await obs.SetVideoSettingsAsync(outputResolution: (1280, 720), frameRate: (60000, 1001));   // what is given changes, the rest stays

await obs.SetStreamServiceAsync("rtmp_custom", new { server = "rtmp://live.example.com/app", key = secret });
ObsStreamService service = await obs.GetStreamServiceAsync();            // the type and the settings: the stream key is among them

await obs.SetRecordDirectoryAsync(@"D:\Recordings");
ObsProfileParameter mode = await obs.Profiles.GetParameterAsync("Output", "Mode");   // mode.Value is "Simple" or "Advanced"

await obs.SetPersistentDataAsync(ObsDataRealm.Global, "my-app", new { lastScene = "Gaming" });
JsonElement? mine = await obs.GetPersistentDataAsync(ObsDataRealm.Global, "my-app");   // null for a slot that nobody used
```

| Call | |
| --- | --- |
| `GetVideoSettingsAsync()` | an [`ObsVideoSettings`](xref:OBSStudioClient.Model.ObsVideoSettings): the base (canvas) and the output resolution, and the frame rate as a fraction (`FpsNumerator` and `FpsDenominator`; `FramesPerSecond` divides them) |
| `SetVideoSettingsAsync(baseResolution, outputResolution, frameRate)` | changes what is given (at least one of the three), with one request. A size is from 1 to 4096 pixels (OBS Studio takes 8 and more), and the frame rate is a numerator and a denominator, so that 59.94 is `(60000, 1001)` |
| `GetStreamServiceAsync()` | an [`ObsStreamService`](xref:OBSStudioClient.Model.ObsStreamService): `Type` (`rtmp_common`, `rtmp_custom`, or the type of a plugin) and `Settings` (a JSON object). **The settings hold the stream key**: do not log them (`ToString()` does not show them) |
| `SetStreamServiceAsync(type, settings)` | settings of the type that is current are laid over its own (a field that you leave out stays), another type makes a new service. The settings can not be empty |
| `GetRecordDirectoryAsync()`, `SetRecordDirectoryAsync(path)` | the folder for recordings, of the output mode that the profile is in. Setting it sets the folder of both modes, and OBS Studio does not check that it exists |
| `obs.Profiles.GetParameterAsync(category, name)` | an [`ObsProfileParameter`](xref:OBSStudioClient.Model.ObsProfileParameter): `Value` and `DefaultValue` (text), and `HasValue`. A parameter that nobody set and that has no default has no value, and that is no error |
| `obs.Profiles.SetParameterAsync(category, name, value)` | writes a parameter to the configuration of the profile. `null` removes it; removing a parameter that has no value that was set throws an `ObsNotFoundException` (`Kind` is `profile parameter`) |
| `GetPersistentDataAsync(realm, slot)`, `SetPersistentDataAsync(realm, slot, value)` | a slot that OBS Studio keeps for the programs that control it, in files of its own: for [`ObsDataRealm.Global`](xref:OBSStudioClient.Model.ObsDataRealm) the same for every profile, for `ObsDataRealm.Profile` the one of the profile that is current. A value of any kind that JSON has, **but not null** |

What OBS Studio does, which the calls follow (checked with OBS Studio 33.0.0, and read in the source of obs-websocket 5.7.6):

- **OBS Studio refuses a change while an output runs**: the video settings while an output is active (measured while recording), the folder for recordings while it records, and the stream service while it streams. The call throws an `ObsResponseException` with the code (`OutputRunning`) and the words of OBS Studio.
  A refusal of a value says so the same way: a canvas of 4 by 4 is refused with `RequestFieldOutOfRange`.
- **A parameter is only written to the configuration**: OBS Studio does not apply it to what it runs (`Video`/`BaseCX` written as a parameter did not change the canvas). `SetVideoSettingsAsync` changes what OBS Studio runs with, and writes the parameters of the video too. The category and the name of a parameter
  are case sensitive, and a value is text whatever it means (`"1920"`).
- **A profile that is made is a clean one**: its video settings, its service (`rtmp_common`, with no settings) and its folder are what OBS Studio gives a new profile, not a copy of the profile that was current. What a profile holds goes away with it.
- **A null is not kept**: OBS Studio takes a null for a value that is missing, and refuses it ("Your request is missing the `slotValue` field"); the call refuses it before it sends anything. OBS Studio has no request that removes a slot: keep an empty value (an empty string, an empty object) to say that there is nothing.
- **Nothing of it is in the model**, so `Changed` does not tell it: ask again when you need it, and after a switch of the profile. The model does not read any of it when it connects.

## Canvases

OBS Studio can have more than one **canvas** (obs-websocket 5.7.0 and newer): the main canvas, which libobs makes itself, and the canvases that a plugin or a script makes (the live tests of this library use a script). A scene collection saves the canvases that are not ephemeral. The model keeps the list in
[`obs.Canvases`](xref:OBSStudioClient.Model.ObsCanvases), with one [`ObsCanvas`](xref:OBSStudioClient.Model.ObsCanvas) for each canvas:

```csharp
foreach (ObsCanvas canvas in obs.Canvases.All)                     // the main canvas first, then the others in the order in which OBS Studio lists them
{
    Console.WriteLine($"{canvas.Name} {canvas.Uuid} {canvas.Flags}"); // Main 6c69626f-6273-4c00-9d88-c5136d61696e Main, Activate, MixAudio, SceneRef
}

ObsVideoSettings? video = await obs.Canvases.Main!.GetVideoSettingsAsync();   // 1920x1080 canvas, 1280x720 output, 30 fps
obs.Canvases.PropertyChanged += (_, e) => Console.WriteLine(e.PropertyName);  // All (and Main when the main canvas is another object)
IReadOnlyList<ObsScene> scenes = obs.Scenes;                                   // the scenes of the main canvas
```

| On `obs.Canvases` | |
| --- | --- |
| `All` | the canvases, in the order in which OBS Studio lists them. It is another list when the canvases change, and it is empty until OBS Studio has said |
| `Main` | the main canvas, the one that has the `Main` flag; `null` until OBS Studio has said |
| `this[name]`, `this[uuid]`, `TryGet(name, out canvas)`, `TryGet(uuid, out canvas)` | find a canvas. Names are case sensitive, as they are in OBS Studio, unless [`IgnoreCase`](xref:OBSStudioClient.Model.ObsModelOptions.IgnoreCase) is set (see [Names](#names)); a name that is not there throws an `ObsNotFoundException` that suggests names that are close, and a UUID that is not there throws one too |

| On an `ObsCanvas` | |
| --- | --- |
| `Name`, `Uuid`, `Flags`, `IsMain` | what the model keeps of a canvas: the name, the UUID (it never changes), and the [`ObsCanvasFlags`](xref:OBSStudioClient.Model.ObsCanvasFlags) (`Main`, `Activate`, `MixAudio`, `SceneRef` and `Ephemeral`, set when the canvas is made) |
| `GetVideoSettingsAsync()` | asks OBS Studio for the video of the canvas, as an `ObsVideoSettings` (the base and the output resolution, and the frame rate), or `null` for a canvas that has no video. It throws an `ObsRemovedException` when the canvas is not there any more. For the main canvas it says what `obs.GetVideoSettingsAsync()` says |

What OBS Studio does, which the model follows (checked with OBS Studio 33.0.0, obs-websocket 5.7.6, with a script that makes canvases, and read in the source of OBS Studio and obs-websocket):

- **A canvas is a snapshot.** An `ObsCanvas` does not change. A canvas that was renamed is another object with the same `Uuid`, and the canvases that did not change stay the same objects, so `Main` is told (`PropertyChanged`) only when the main canvas is another object.
- **An event says that something happened, and a read says what.** `CanvasCreated` and `CanvasNameChanged` make the model read the list again, and what it shows is what that read says. `Changed` says it with a record for `All` (`the canvases: All [Main] -> [Main, Overlay]`).
- **OBS Studio makes the names unique, and the list is in the order in which the canvases were added.** A canvas that is made or renamed to a name that is taken gets the next free one (`Overlay 2`, and for a rename `Renamed 2`: measured), and a canvas that is renamed is last in the list (measured). The main canvas can be neither renamed nor removed.
- **A canvas that is removed leaves the model at once, however long OBS Studio goes on listing it.** OBS Studio keeps a removed canvas for as long as something holds on to it, lists it all that time (measured: with a script that held it, `GetCanvasList` still listed the canvas, and no longer did when the script let go),
  and says that it was removed once (`CanvasRemoved`), and nothing when it lets go. The model leaves the canvas out of every read after it, until the connection is made again, or OBS Studio loads another scene collection (a collection saves its canvases with their UUIDs, so one that comes back is listed again).
- **Canvases belong to the scene collection.** A collection saves its canvases (not the ephemeral ones), and OBS Studio unloads them with the collection. While OBS Studio loads another collection the model keeps what it knew, and when it has loaded it the model reads the list again with the rest
  (see [The connection, and scene collections](#the-connection-and-scene-collections)). A canvas that a collection loads keeps the UUID that the collection saved (measured), and has no video (see below). An `ObsCanvas` that you hold is of the collection that was loaded when you got it: after a switch `GetVideoSettingsAsync` on it throws `ObsRemovedException`, also when the collection that is loaded has a canvas with the same UUID (the main canvas has the same in every collection), so take the canvas from `obs.Canvases` again.
- **OBS Studio does not announce the video of a canvas**, so the model does not keep it: `GetVideoSettingsAsync` asks, and answers at the moment that OBS Studio does. A canvas without a video says nothing of it (the six values are null; measured for a canvas that a collection loads and for one that a script makes without a video),
  and the call returns `null`. The main canvas has the video of the profile that is current.
- **The scenes of the other canvases are not in the model.** `obs.Scenes` holds the scenes of the main canvas, because that is what OBS Studio announces: ask for the others with the plain client (`await obs.Client.GetSceneList(canvas.Uuid)`).
- **An OBS Studio before obs-websocket 5.7.0 has no canvases.** The model asks, is refused, and does not believe the first refusal; then `All` stays empty and `Main` is `null`, and the model is `Synchronized` all the same.
- **The model reads the list when it connects** (one request more), and subscribes to the `Canvases` events whatever you pass in `AdditionalEvents`.

## Hearing about changes

- [`obs.Changed`](xref:OBSStudioClient.Model.ObsModel.Changed) raises an [`ObsChangedEventArgs`](xref:OBSStudioClient.Model.ObsChangedEventArgs) for each change, in the order in which it happened:
  `Added`, `Removed`, `PropertyChanged` (with the old and the new value), `StateChanged` and `Repaired` (see [`ObsChangeKind`](xref:OBSStudioClient.Model.ObsChangeKind)). `Console.WriteLine(e)` prints it in words.
- Every object implements `INotifyPropertyChanged`, so you can bind a user interface to the objects directly.
- [`obs.Resynchronized`](xref:OBSStudioClient.Model.ObsModel.Resynchronized) says that the whole model was read again (after a connection that was lost came back, or after OBS Studio changed its scene collection): read again what you hold.
- The notifications are raised **one at a time, in order, on a thread of their own**. In a user interface application set
  [`ObsModelOptions.NotificationContext`](xref:OBSStudioClient.Model.ObsModelOptions.NotificationContext) to the `SynchronizationContext` of the UI thread, and handlers run there.
  Add your handlers **before** you connect: the first synchronization is announced after `ConnectAsync` returns.
- A handler sees the model as it is when the handler runs, which can be newer than the change it is told about: the properties are always current, a change is a notification.

## Things that happen

Some events say that something happened, and are no state of the model: a custom event that a program sent, a file that was written, OBS Studio that is shutting down. The model raises them as .NET events, in the order in which they arrived,
one at a time, like the notifications above (on `NotificationContext` when you set it), and **also while the model is being read**. Add the handlers before you connect.

| Event | On | When |
| --- | --- | --- |
| `CustomEvent` | `ObsModel` | a program called `BroadcastCustomEventAsync` (this one included), or a script of OBS Studio sent a custom event; `e.EventData` is what was sent |
| `VendorEvent` | `ObsModel` | a plugin or a script sent an event of its own. **You opt in**: pass `AdditionalEvents = EventSubscriptions.Vendors` (a plugin can send a great many) |
| `ScreenshotSaved` | `ObsModel` | the user took a screenshot in OBS Studio (its hotkey or its menu); not for `SaveScreenshotAsync`, which returns when the file is written |
| `FilterSettingsChanged` | `ObsModel` | a server said that the settings of a filter changed (`e.Filter`, `e.Settings`). **OBS Studio does not** (a filter is a private source, and OBS Studio 33.0.0 sent none): ask with `filter.GetSettingsAsync()` |
| `ExitStarted` | `ObsModel` | OBS Studio begins to shut down. `IsExiting` is true from then until the model is connected to an OBS Studio that was started again: it tells a closed OBS Studio from a lost connection |
| `AudioLevels` | `ObsModel` | OBS Studio sent new levels of the audio of the inputs that are on the air. **You opt in**: `HighVolume = ObsHighVolume.AudioLevels` (twenty messages a second): a slow handler gets the newest levels, see [How loud is it? Audio levels](#how-loud-is-it-audio-levels) |
| `FileChanged` | `ObsRecordOutput` | the recording goes on in a new file |
| `Saved` | `ObsReplayBufferOutput` | the replay buffer was saved to a file |
| `Started`, `Ended`, `VideoEnded` | `ObsTransitions` | a scene transition started, ended, and its video ended (see [Studio mode, the preview scene and transitions](#studio-mode-the-preview-scene-and-transitions)); `e.TransitionName` and `e.TransitionUuid` say which |

A handler that throws is logged, and does not keep the other handlers, or the next notification, from running. The same events are available on the plain client (`obs.Client.CustomEvent`, `RecordFileChanged`, ...), which raises them from a dispatcher of its own.

## Removed things: Live, Detached and Gone

OBS Studio keeps a scene or an input **alive after it is removed** for as long as a scene item still shows it. The model follows that, and says so with
[`ObsObjectState`](xref:OBSStudioClient.Model.ObsObjectState) (`State`, and `IsRemoved` for "not Live"):

- `Live`: it exists.
- `Detached`: OBS Studio removed it and keeps it alive for the scene items that show it. It is not in `obs.Scenes` or `obs.Inputs` any more, its scene items stay in their scenes, and the **name of a removed input stays taken**
  until the last of them is gone (with OBS Studio 31.0 and older the name of a removed scene too; 31.1 and newer free it at once).
- `Gone`: it does not exist any more.

`RemoveAsync` removes the scene items too, so that OBS Studio destroys the object and the name is free. It returns when the object is removed and those scene items are gone; the model can call a scene `Gone` a moment later (watch `StateChanged`, or wait for `State`).

OBS Studio frees the name a moment after the model can tell (it destroys the object when the last reference to it is released, up to a frame later, and does not announce it: about 1 time in 10, for up to 25 ms, with OBS Studio 33.0.0). The calls that
take a name, `CreateSceneAsync`, `CreateInputAsync` and `RenameAsync`, allow for that: a name that OBS Studio refuses and that nothing in the model holds is asked for again every 25 ms, for as long as
`ConfirmationTimeout`, before the `ObsNameInUseException` is thrown. A name that the model shows a holder for is refused at once.

One thing that the model cannot be told is what OBS Studio removed a moment **before** the connection was made. OBS Studio goes on listing a scene or an input that it removed for as long as it holds on to it (a frame, or until the transition that shows it has ended),
says nothing when it lets go, and has nothing in the list that tells a removed object from a live one. A model that reads the lists in that time shows the object as `Live`, until a list is read again: when a scene or an input is created or the
model is read again (a new connection, another scene collection). That takes a connection that is made within that time, usually a frame or the length of a transition, so it is rare; the live tests wait for OBS Studio to let go of what a test removed before the next one connects.

## The connection, and scene collections

- `obs.State` is `Disconnected`, `Connecting`, `Synchronizing`, `Synchronized` or `Reconnecting`. `ConnectAsync` returns when the model is complete. [`AutoReconnect`](xref:OBSStudioClient.Model.ObsModelOptions.AutoReconnect)
  (on by default) restores a lost connection and reads the model again; while it is down the model holds what was known last and `IsStale` is true.
- When OBS Studio switches to another **scene collection**, everything you hold belongs to the collection that was left: the scenes, inputs and scene items are `Gone`, and what is in the new one is new objects, also when a scene of the same name is in both. `Resynchronized` comes when the new model is read. The model does not depend on the events for it: a read of the list of the scene collections that names another collection than the one that the model is of (the events were lost, or are late) is a switch too, and the events of that switch that come after the read are not a second one. A call that makes OBS Studio switch returns when the model has read the collection that is loaded, and not when the list says its name. Which collection is loaded, and the names of all, are in [`obs.SceneCollections`](#profiles-and-scene-collections).
- `ConnectAsync` throws [`ObsNotConnectedException`](xref:OBSStudioClient.Exceptions.ObsNotConnectedException) when OBS Studio cannot be reached or refuses the password, and a `TimeoutException` when the model is not complete within `SynchronizationTimeout`.

## Settings

[`ObsModelOptions`](xref:OBSStudioClient.Model.ObsModelOptions), all optional:

| Setting | Default | |
| --- | --- | --- |
| `IgnoreCase` | `false` | a name that differs only by case matches when it is the only match |
| `AutoReconnect` | `true` | restore a lost connection, and read the model again when it is back |
| `SynchronizationTimeout` | 30 s | how long `ConnectAsync` waits for the model to be complete |
| `ReadTimeout` | 10 s | how long one read of the model may take |
| `WriteTimeout` | 10 s | how long OBS Studio gets to answer a change (a call that makes OBS Studio load a scene collection waits as long as `SynchronizationTimeout` when that is more, because OBS Studio answers when it has loaded it) |
| `OutputStepTimeout` | 30 s | how long an output may be starting or stopping before the model reads it again to see whether the step is over (see [Outputs](#outputs)) |
| `ConfirmationTimeout` | 1 s | how long a change waits for the event that shows it before the model is read again |
| `NotificationContext` | none | where the notifications run (for example the UI thread) |
| `MaxPendingNotifications` | 10,000 | how many notifications may wait for slow handlers; beyond that new ones are dropped until the handlers catch up, and then every object raises `PropertyChanged` with an empty name and `Resynchronized` is raised (the model itself is always current) |
| `AdditionalEvents` | none | event subscriptions for your own handlers on `obs.Client`, on top of the ones that the model needs; `VendorEvent` needs `Vendors` here. The events that arrive very often (the volume meters of the inputs, the active and showing state of inputs, a changed transform of a scene item) are not subscribed to for the model unless `HighVolume` asks for them |
| `HighVolume` | none | what the model follows of what OBS Studio sends very often: `Transforms` (the transform of every scene item, see [Position, size and crop: transforms](#position-size-and-crop-transforms)), `Activity` (whether the inputs are on the air and shown, see [Is an input on the air? Activity](#is-an-input-on-the-air-activity)), `AudioLevels` (how loud the inputs are, see [How loud is it? Audio levels](#how-loud-is-it-audio-levels)), or `All` |

## What the model does not cover yet

The model has scenes, inputs (with their settings, audio, media and, when asked for, their activity and levels), scene items (with their transform and blend mode, and copies of them), the program scene and the preview scene, studio mode and the scene transitions, the filters of scenes and inputs, the outputs (stream, recording, virtual camera, replay buffer), the profiles and scene collections (which one is current, the names of all, and the calls that switch and make them), the canvases (the list, with their flags, and the video of each one when you ask), and calls for the settings of OBS Studio itself and for the data that it keeps for an application (see [The settings of OBS Studio itself](#the-settings-of-obs-studio-itself)). What it does not cover yet: the scenes of the canvases other than the main one, and request batches (the model makes its changes one at a time, in order; see [Request batches](batches.md)).
Those are reached through the plain client, `obs.Client`, with the names and UUIDs that you know. The model sees what those requests do to scenes, inputs, scene items and outputs through the events, as it does for anything
another program does. Do not call `ConnectAsync`, `Disconnect` or `ReidentifyAsync` on that client yourself: use `ObsModel.ConnectAsync` and `DisconnectAsync`.

```csharp
var scenes = await obs.Client.GetSceneList(obs.Canvases["Vertical"].Uuid);   // the scenes of another canvas: not modelled yet, the plain client
await obs.Scenes["Gaming"].Items["Webcam"].SetEnabledAsync(true);            // and the model follows
```

## How it is checked

The model is checked against a simulation of OBS Studio that reproduces what the real one does that surprises (a list of scenes that comes late, an input that stays alive and listed after it was removed,
a program scene that changes a frame after the answer), with random and hostile orders of events, and, in CI, against a real OBS Studio: after every step of the live tests the model has to equal what OBS Studio holds.

## Next steps

- [Handling events](events.md): the plain events that the model is built on.
- [Calling requests](requests.md): everything the model does not cover yet.
- [Troubleshooting](troubleshooting.md).
