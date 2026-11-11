# ObsModel: controlling OBS Studio by name

> [!NOTE]
> This part of the library is new, and **provisional**: it first appears in 4.0.0-rc.2, and the names and the shapes of its types can still change
> before 4.0.0 is released. Everything else in OBSClient is unaffected, and you can ignore this article if you only use the requests.
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
| [`ObsModel`](xref:OBSStudioClient.Model.ObsModel) | `Scenes`, `Inputs`, `ProgramScene`, `IsStudioModeEnabled`, `Outputs`, `State`, `IsStale`, `IsExiting`, `ObsVersion`, `WebSocketVersion`, `Platform`, `SupportedImageFormats`, `Client` |
| [`ObsScene`](xref:OBSStudioClient.Model.ObsScene) | `Name`, `Uuid`, `Index`, `IsProgram`, `IsGroup`, `Items`, `Filters`, `State` |
| [`ObsInput`](xref:OBSStudioClient.Model.ObsInput) | `Name`, `Uuid`, `Kind`, `HasAudio`, `IsMuted`, `VolumeDb`, `VolumeMul`, `AudioBalance`, `SyncOffset`, `MonitorType`, `AudioTracks`, `Settings`, `Filters`, `State`, `GetSceneItems()` (every scene item that shows it) |
| [`ObsSceneItem`](xref:OBSStudioClient.Model.ObsSceneItem) | `Scene`, `Source` (the input or scene that it shows), `Id`, `Index`, `IsEnabled`, `IsLocked`, `IsRemoved` |
| [`ObsFilters`](xref:OBSStudioClient.Model.ObsFilters) | the filters of a scene or an input, from the first: `Filters["Gain"]` by name, by position, `TryGet`, `Find`, `Contains` |
| [`ObsFilter`](xref:OBSStudioClient.Model.ObsFilter) | `Source`, `Name`, `Kind`, `Index`, `IsEnabled`, `IsRemoved` |
| [`ObsOutputs`](xref:OBSStudioClient.Model.ObsOutputs) | `Stream`, `Record`, `VirtualCamera`, `ReplayBuffer`; also the four as a list, and by [`ObsOutputKind`](xref:OBSStudioClient.Model.ObsOutputKind) |
| [`ObsOutput`](xref:OBSStudioClient.Model.ObsOutput) | `Kind`, `Name`, `Status` ([`ObsOutputStatus`](xref:OBSStudioClient.Model.ObsOutputStatus)), `IsActive`; the recording ([`ObsRecordOutput`](xref:OBSStudioClient.Model.ObsRecordOutput)) also has `IsPaused` and `CurrentFile`, and the replay buffer ([`ObsReplayBufferOutput`](xref:OBSStudioClient.Model.ObsReplayBufferOutput)) `LastSavedPath` |

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
| `ObsScene` | `SwitchToAsync()` | puts the scene on the air |
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
| `ObsScene`, `ObsInput` | `CreateFilterAsync(name, kind, settings)` | makes a filter, on top of the others (see [Filters](#filters)) |
| `ObsFilter` | `SetEnabledAsync(bool)`, `ToggleEnabledAsync()` | |
| | `RenameAsync(name)`, `SetIndexAsync(int)`, `RemoveAsync()` | |
| `ObsOutput` | `StartAsync()`, `StopAsync()`, `ToggleAsync()` | return when OBS Studio has **begun** (see [Outputs](#outputs)) |
| `ObsRecordOutput` | `PauseAsync()`, `ResumeAsync()`, `TogglePauseAsync()` | |
| `ObsReplayBufferOutput` | `SaveAsync()` | OBS Studio writes the file a moment later |

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
| `ExitStarted` | `ObsModel` | OBS Studio begins to shut down. `IsExiting` is true from then until the model is connected to an OBS Studio that was started again: it tells a closed OBS Studio from a lost connection |
| `FileChanged` | `ObsRecordOutput` | the recording goes on in a new file |
| `Saved` | `ObsReplayBufferOutput` | the replay buffer was saved to a file |

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

## The connection, and scene collections

- `obs.State` is `Disconnected`, `Connecting`, `Synchronizing`, `Synchronized` or `Reconnecting`. `ConnectAsync` returns when the model is complete. [`AutoReconnect`](xref:OBSStudioClient.Model.ObsModelOptions.AutoReconnect)
  (on by default) restores a lost connection and reads the model again; while it is down the model holds what was known last and `IsStale` is true.
- When OBS Studio switches to another **scene collection**, everything you hold belongs to the collection that was left: the scenes, inputs and scene items are `Gone`, and what is in the new one is new objects, also when a scene of the same name is in both. `Resynchronized` comes when the new model is read.
- `ConnectAsync` throws [`ObsNotConnectedException`](xref:OBSStudioClient.Exceptions.ObsNotConnectedException) when OBS Studio cannot be reached or refuses the password, and a `TimeoutException` when the model is not complete within `SynchronizationTimeout`.

## Settings

[`ObsModelOptions`](xref:OBSStudioClient.Model.ObsModelOptions), all optional:

| Setting | Default | |
| --- | --- | --- |
| `IgnoreCase` | `false` | a name that differs only by case matches when it is the only match |
| `AutoReconnect` | `true` | restore a lost connection, and read the model again when it is back |
| `SynchronizationTimeout` | 30 s | how long `ConnectAsync` waits for the model to be complete |
| `ReadTimeout` | 10 s | how long one read of the model may take |
| `WriteTimeout` | 10 s | how long OBS Studio gets to answer a change |
| `OutputStepTimeout` | 30 s | how long an output may be starting or stopping before the model reads it again to see whether the step is over (see [Outputs](#outputs)) |
| `ConfirmationTimeout` | 1 s | how long a change waits for the event that shows it before the model is read again |
| `NotificationContext` | none | where the notifications run (for example the UI thread) |
| `MaxPendingNotifications` | 10,000 | how many notifications may wait for slow handlers; beyond that new ones are dropped until the handlers catch up, and then every object raises `PropertyChanged` with an empty name and `Resynchronized` is raised (the model itself is always current) |
| `AdditionalEvents` | none | event subscriptions for your own handlers on `obs.Client`, on top of the ones that the model needs; `VendorEvent` needs `Vendors` here. The events that arrive very often (the volume meters of the inputs, the active and showing state of inputs, a changed transform of a scene item) are never subscribed to for the model |

## What the model does not cover yet

The model has scenes, inputs (with their settings, audio and media), scene items, the program scene, the filters of scenes and inputs, and the outputs (stream, recording, virtual camera, replay buffer). Transitions and the control of studio mode (the model only reads whether it is on), profiles and scene collections, and the rest of
the protocol are reached through the plain client, `obs.Client`, with the names and UUIDs that you know. The model sees what those requests do to scenes, inputs, scene items and outputs through the events, as it does for anything
another program does. Do not call `ConnectAsync`, `Disconnect` or `ReidentifyAsync` on that client yourself: use `ObsModel.ConnectAsync` and `DisconnectAsync`.

```csharp
await obs.Client.SetCurrentSceneTransition("Fade");              // not modelled yet: the plain client
await obs.Scenes["Gaming"].Items["Webcam"].SetEnabledAsync(true); // and the model follows
```

## How it is checked

The model is checked against a simulation of OBS Studio that reproduces what the real one does that surprises (a list of scenes that comes late, an input that stays alive and listed after it was removed,
a program scene that changes a frame after the answer), with random and hostile orders of events, and, in CI, against a real OBS Studio: after every step of the live tests the model has to equal what OBS Studio holds.

## Next steps

- [Handling events](events.md): the plain events that the model is built on.
- [Calling requests](requests.md): everything the model does not cover yet.
- [Troubleshooting](troubleshooting.md).
