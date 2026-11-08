# Controlling OBS Studio by name

> [!NOTE]
> This part of the library is new, and **provisional**: it first appears in version 5.0, and the names and the shapes of its types can still change
> before it is called stable. Everything else in OBSClient is unaffected, and you can ignore this article if you only use the requests.

OBS Studio identifies most things by a UUID, a scene item id or an index. People think in names: *"mute the Mic"*, *"hide the Webcam in the Gaming scene"*.
[`ObsStudio`](xref:OBSStudioClient.Studio.ObsStudio) closes that gap. When it connects it reads your scenes, inputs and scene items, it listens to the events that
OBS Studio sends when they change, and it keeps a copy in memory. You ask the copy, by name, and you change things by name; the UUIDs, the ids and the indexes stay inside.

It is built **on** the plain `ObsClient`, and hides nothing: every request and event of OBS Studio is still available through [`obs.Client`](xref:OBSStudioClient.Studio.ObsStudio.Client).

## What you need

- OBS Studio **30.1 or newer** (obs-websocket 5.4 or newer): the first version that gives scenes and inputs a UUID. With an older OBS Studio, `ConnectAsync` throws an
  [`ObsVersionNotSupportedException`](xref:OBSStudioClient.Exceptions.ObsVersionNotSupportedException); use the plain client, which works from OBS Studio 28.
- From OBS Studio 31.1 (obs-websocket 5.6) OBS Studio says which inputs have audio. Before that the model asks for the audio state of every input and takes the refusal
  for an input without audio as the answer: the model is the same, and connecting takes two more requests for each such input.

## A first program

```csharp
using OBSStudioClient.Studio;

await using var obs = new ObsStudio();
await obs.ConnectAsync("localhost", 4455, "password");   // returns when the model is complete

var gaming = obs.Scenes["Gaming"];
Console.WriteLine($"{gaming.Name} shows {gaming.Items.Count} things; the webcam is {(gaming.Items["Webcam"].IsEnabled ? "on" : "off")}."); // no request is sent

await gaming.SwitchToAsync();                            // put it on the air
await obs.Inputs["Mic"].SetMutedAsync(true);             // mute the input
await gaming.Items["Webcam"].SetEnabledAsync(false);     // hide the webcam in this scene

obs.Changed += (_, e) => Console.WriteLine(e);           // and hear what changes, also what the user does in OBS Studio
```

A runnable version with a small command prompt (`scene "Gaming"`, `mute "Mic"`, `hide "Gaming" "Webcam"`, and more) is in the repository:
`samples/StudioSample`. Open the project in Visual Studio and press F5, or run `dotnet run --project samples/StudioSample`.

## Reading: from memory

Everything that is a property is answered from the model, at once, and can be read from any thread. The model is updated on a thread of its own, so what you see changes between two reads.

| Object | What you can read |
| --- | --- |
| [`ObsStudio`](xref:OBSStudioClient.Studio.ObsStudio) | `Scenes`, `Inputs`, `ProgramScene`, `IsStudioModeEnabled`, `Outputs`, `State`, `IsStale`, `Client` |
| [`ObsScene`](xref:OBSStudioClient.Studio.ObsScene) | `Name`, `Uuid`, `Index`, `IsProgram`, `IsGroup`, `Items`, `State` |
| [`ObsInput`](xref:OBSStudioClient.Studio.ObsInput) | `Name`, `Uuid`, `Kind`, `HasAudio`, `IsMuted`, `VolumeDb`, `VolumeMul`, `State`, `GetSceneItems()` (every scene item that shows it) |
| [`ObsSceneItem`](xref:OBSStudioClient.Studio.ObsSceneItem) | `Scene`, `Source` (the input or scene that it shows), `Id`, `Index`, `IsEnabled`, `IsLocked`, `IsRemoved` |
| [`ObsOutputs`](xref:OBSStudioClient.Studio.ObsOutputs) | `Stream`, `Record`, `VirtualCamera`, `ReplayBuffer`; also the four as a list, and by [`ObsOutputKind`](xref:OBSStudioClient.Studio.ObsOutputKind) |
| [`ObsOutput`](xref:OBSStudioClient.Studio.ObsOutput) | `Kind`, `Name`, `Status` ([`ObsOutputStatus`](xref:OBSStudioClient.Studio.ObsOutputStatus)), `IsActive`; the recording ([`ObsRecordOutput`](xref:OBSStudioClient.Studio.ObsRecordOutput)) also has `IsPaused` |

## Names

A name is looked up the way OBS Studio does it: exactly, and with case, against the names the objects have **now** (a renamed scene is found by its new name).

```csharp
var scene = obs.Scenes["Gaming"];            // throws ObsNotFoundException (it suggests close names) when there is none
if (obs.Inputs.TryGet("Mic", out var mic)) { }   // no exception when it is missing
var maybe = obs.Scenes.Find("Gaming");       // null when it is missing
bool there = obs.Scenes.Contains("Gaming");
var byUuid = obs.Scenes[uuid];               // a UUID works as well, and survives a rename
```

- With [`ObsStudioOptions.IgnoreCase`](xref:OBSStudioClient.Studio.ObsStudioOptions.IgnoreCase) a name that differs only by case matches too, when there is no exact match and it leaves
  one object. Two matches throw an [`ObsAmbiguousNameException`](xref:OBSStudioClient.Exceptions.ObsAmbiguousNameException) that lists them.
- A scene item is found by the name of the **source it shows**: `scene.Items["Webcam"]`. A scene can show the same source more than once: the indexer then throws
  an `ObsAmbiguousNameException`, `FindAll("Webcam")` returns all of them, and `ById(id)` finds one by its id.
- The collections (`Scenes`, `Inputs`, `Items`) are **snapshots**: they do not change while you look at them, and they are safe to use from any thread. Ask for them again to see what changed.

## Changing things

Changes are methods on the objects. They are made **one at a time, in the order in which you ask for them**, and each one returns **when the model shows the change**, so that what you read next is what you changed.

| On | Method | What it does |
| --- | --- | --- |
| `ObsStudio` | `CreateSceneAsync(name)` | makes a scene |
| `ObsScene` | `SwitchToAsync()` | puts the scene on the air |
| | `CreateInputAsync(name, kind, settings)` | makes an input, and a scene item that shows it in this scene |
| | `AddAsync(source)` | adds a scene item that shows an input or a scene |
| | `RenameAsync(name)`, `RemoveAsync()` | |
| `ObsInput` | `SetMutedAsync(bool)`, `ToggleMuteAsync()` | |
| | `SetVolumeDbAsync(double)`, `SetVolumeMulAsync(double)` | |
| | `SetEnabledInAllScenesAsync(bool)` | shows or hides every scene item that shows the input |
| | `RenameAsync(name)`, `RemoveAsync()` | |
| `ObsSceneItem` | `SetEnabledAsync(bool)`, `ToggleEnabledAsync()` | |
| | `SetLockedAsync(bool)`, `SetIndexAsync(int)`, `RemoveAsync()` | |
| `ObsOutput` | `StartAsync()`, `StopAsync()`, `ToggleAsync()` | return when OBS Studio has **begun** (see [Outputs](#outputs)) |
| `ObsRecordOutput` | `PauseAsync()`, `ResumeAsync()` | |
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

`obs.Outputs` has the four outputs of OBS Studio: `Stream`, `Record`, `VirtualCamera` and `ReplayBuffer`. They are the same objects for as long as the `ObsStudio` lives, also before it connects
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

`Status` is an [`ObsOutputStatus`](xref:OBSStudioClient.Studio.ObsOutputStatus): `Stopped`, `Starting`, `Started`, `Stopping`, `Reconnecting` (a stream that lost its connection to the service
and tries again) or `Paused` (the recording). `IsActive` is true for `Started`, `Reconnecting` and `Paused`.

- **A start or a stop returns when OBS Studio has *begun*, not when it is done.** A stream connects to the streaming service, which can take seconds and can fail: it is `Starting`, and then `Started`, or `Stopped` again.
  Watch `Status` (`PropertyChanged`, or `obs.Changed`) for how it goes.
- `ToggleAsync()` starts the output when it is not running and stops it when it is, and returns `true` when it was started. OBS Studio decides, so two toggles that you ask for one after the other make two toggles.
- When OBS Studio refuses (the output runs already, it does not run, there is no streaming service, the replay buffer is not enabled), the call throws an `ObsResponseException` whose `ErrorCode`
  says why (`OutputRunning`, `OutputNotRunning`, `OutputDisabled`, and others), and nothing was changed.
- **Going live is a decision.** `obs.Outputs.Stream.StartAsync()` starts the stream on the service that OBS Studio has set up. The sample asks you to type `yes` first.
- The model does not hold where a recording or a replay was written. The plain client says: `obs.Client.RecordStateChanged` (`OutputPath`, when the recording has stopped), `RecordFileChanged` (a recording that was split)
  and `ReplayBufferSaved` (`SavedReplayPath`). The model subscribes to the events of the outputs, so they come whatever you pass in `AdditionalEvents`.
- **OBS Studio does not always say that a start failed.** A recording whose encoder does not start shows an error in the window of OBS Studio and sends no event after `Starting` (seen with OBS Studio 33.0.0). The model reads
  an output that is still starting or stopping after [`OutputStepTimeout`](xref:OBSStudioClient.Studio.ObsStudioOptions.OutputStepTimeout) (30 seconds): when OBS Studio does not run it, `Status` is `Stopped`. A stream that needs
  longer than that to connect shows `Stopped` until OBS Studio says that it started: raise the setting when your streaming service is slow.
- **A recording can be answered "paused" and not pause.** A recording that uses the encoder of the stream (the default of the simple output) cannot be paused, and a recording that does not run is not refused: OBS Studio answers `PauseAsync()`
  with success and sends no event. The call returns with what OBS Studio holds, so look at `IsPaused` after it.
- Within a connection the events are what the model goes by: OBS Studio can go on saying that a recording runs for a moment after it said that it stopped, and that does not make the model show `Started` again.
- While there is no connection the outputs show what was known last (`IsStale` says so); when it is back they show what OBS Studio does.

## Hearing about changes

- [`obs.Changed`](xref:OBSStudioClient.Studio.ObsStudio.Changed) raises an [`ObsChangedEventArgs`](xref:OBSStudioClient.Studio.ObsChangedEventArgs) for each change, in the order in which it happened:
  `Added`, `Removed`, `PropertyChanged` (with the old and the new value), `StateChanged` and `Repaired` (see [`ObsChangeKind`](xref:OBSStudioClient.Studio.ObsChangeKind)). `Console.WriteLine(e)` prints it in words.
- Every object implements `INotifyPropertyChanged`, so you can bind a user interface to the objects directly.
- [`obs.Resynchronized`](xref:OBSStudioClient.Studio.ObsStudio.Resynchronized) says that the whole model was read again (after a connection that was lost came back, or after OBS Studio changed its scene collection): read again what you hold.
- The notifications are raised **one at a time, in order, on a thread of their own**. In a user interface application set
  [`ObsStudioOptions.NotificationContext`](xref:OBSStudioClient.Studio.ObsStudioOptions.NotificationContext) to the `SynchronizationContext` of the UI thread, and handlers run there.
  Add your handlers **before** you connect: the first synchronization is announced after `ConnectAsync` returns.
- A handler sees the model as it is when the handler runs, which can be newer than the change it is told about: the properties are always current, a change is a notification.

## Removed things: Live, Detached and Gone

OBS Studio keeps a scene or an input **alive after it is removed** for as long as a scene item still shows it. The model follows that, and says so with
[`ObsObjectState`](xref:OBSStudioClient.Studio.ObsObjectState) (`State`, and `IsRemoved` for "not Live"):

- `Live`: it exists.
- `Detached`: OBS Studio removed it and keeps it alive for the scene items that show it. It is not in `obs.Scenes` or `obs.Inputs` any more, its scene items stay in their scenes, and the **name of a removed input stays taken**
  until the last of them is gone (with OBS Studio 31.0 and older the name of a removed scene too; 31.1 and newer free it at once).
- `Gone`: it does not exist any more.

`RemoveAsync` removes the scene items too, so that OBS Studio destroys the object and the name is free. It returns when the object is removed and those scene items are gone; the model can call a scene `Gone` a moment later (watch `StateChanged`, or wait for `State`).

## The connection, and scene collections

- `obs.State` is `Disconnected`, `Connecting`, `Synchronizing`, `Synchronized` or `Reconnecting`. `ConnectAsync` returns when the model is complete. [`AutoReconnect`](xref:OBSStudioClient.Studio.ObsStudioOptions.AutoReconnect)
  (on by default) restores a lost connection and reads the model again; while it is down the model holds what was known last and `IsStale` is true.
- When OBS Studio switches to another **scene collection**, everything you hold belongs to the collection that was left: the scenes, inputs and scene items are `Gone`, and what is in the new one is new objects, also when a scene of the same name is in both. `Resynchronized` comes when the new model is read.
- `ConnectAsync` throws [`ObsNotConnectedException`](xref:OBSStudioClient.Exceptions.ObsNotConnectedException) when OBS Studio cannot be reached or refuses the password, and a `TimeoutException` when the model is not complete within `SynchronizationTimeout`.

## Settings

[`ObsStudioOptions`](xref:OBSStudioClient.Studio.ObsStudioOptions), all optional:

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
| `AdditionalEvents` | none | event subscriptions for your own handlers on `obs.Client`, on top of the ones that the model needs |

## What the model does not cover yet

The model has scenes, inputs, scene items, the program scene, the audio of inputs and the outputs (stream, recording, virtual camera, replay buffer). Filters, transitions and the control of studio mode, profiles and scene collections, media inputs and the rest of
the protocol are reached through the plain client, `obs.Client`, with the names and UUIDs that you know. The model sees what those requests do to scenes, inputs, scene items and outputs through the events, as it does for anything
another program does. Do not call `ConnectAsync`, `Disconnect` or `ReidentifyAsync` on that client yourself: use `ObsStudio.ConnectAsync` and `DisconnectAsync`.

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
