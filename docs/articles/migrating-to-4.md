---
title: Moving from 3.x to 4.0
---

# Moving from 3.x to 4.0

OBSClient 4.0 brings the client up to **obs-websocket 5.7.6** (3.x implemented 5.5.2) and fixes a number of long-standing problems that came
to light when every request and field was checked against the protocol definition. Most code that compiled against 3.x still
compiles. This page lists what does not, what behaves differently, and what is new.

> [!NOTE]
> 4.0 is in beta. Install it with `dotnet add package OBSClient --prerelease`, and report what you find in an
> [issue](https://github.com/tinodo/obsclient/issues). The public API can still change before 4.0.0. The
> [changelog](../changelog.md) has the complete list.

Code that was compiled against 3.x **has to be recompiled**: a signature changed, even where the call looks the same.

## What breaks

### One parameter for a name or a UUID

3.x had a pair of overloads for everything that OBS Studio looks up by name or by UUID: `GetInputMute(string inputName)` and
`GetInputMute(Guid inputUuid)`. 4.0 has one method that takes a [`SceneRef`](xref:OBSStudioClient.Classes.SceneRef),
[`InputRef`](xref:OBSStudioClient.Classes.InputRef) or [`SourceRef`](xref:OBSStudioClient.Classes.SourceRef): a small value that holds a name or a
UUID. A `string`, a `Guid`, and the `Scene`, `Input` or `SceneItem` that OBS Studio reported convert to it implicitly, so calls read as
before:

```csharp
await client.SetCurrentProgramScene("Gaming Scene");
await client.SetCurrentProgramScene(sceneUuid);
await client.SetCurrentProgramScene(scene);        // identified by its UUID
```

What does break:

- **Named arguments.** `inputName:` and `inputUuid:` are now `input:`, `sceneName:` and `sceneUuid:` are `scene:`, `sourceName:` and
  `sourceUuid:` are `source:`.

  ```csharp
  // 3.x
  bool muted = await client.GetInputMute(inputName: "Microphone");
  // 4.0
  bool muted = await client.GetInputMute(input: "Microphone");
  ```

- **A method group converted to a delegate.** The implicit conversion from `string` to `InputRef` applies to a call, not to a method group.

  ```csharp
  // 3.x
  Func<string, Task<bool>> mute = client.GetInputMute;
  // 4.0
  Func<string, Task<bool>> mute = name => client.GetInputMute(name);
  ```

The same goes for the builders of [`RequestBatchMessage`](xref:OBSStudioClient.Messages.RequestBatchMessage). The canvas of a request is a trailing optional
parameter: `GetSceneItemList("Scene", canvasUuid: id)`.

### Settings of `CreateInput` and `SetCurrentSceneTransitionSettings`

`CreateInput` (and `AddCreateInputRequest`) take a `Dictionary<string, object>?` for the settings of the new input instead of an `Input?`, and
`SetCurrentSceneTransitionSettings` (and its builder) takes a `Dictionary<string, object>` instead of a `TransitionResponse?`. Neither
of the old types is a settings object, so 3.x had no way to give a new input its settings, such as the URL of a browser source.

```csharp
await client.CreateInput(
    "Gaming Scene", "Overlay", "browser_source",
    new Dictionary<string, object>
    {
        ["url"] = "https://example.com",
        ["width"] = 800,
        ["height"] = 600
    });
```

Passing `null` to `CreateInput` still works.

### Events are raised off the receive thread

The events of OBS Studio and `ConnectionClosed` used to be raised by the thread that reads from OBS Studio. They are queued now, and raised
by a task of the thread pool: one at a time, in the order in which they happened. See [the rules for handlers](events.md#rules-for-handlers).
Most code does not notice, but:

- **A handler can wait for a response** (`client.GetSceneList().Result`). In 3.x and 4.0.0-beta1 that made the request time out, because the
  handler held the thread that has to read the response. A handler that was written around that (with `async`/`await` or `Task.Run`) keeps
  working.
- **An event is not guaranteed to be raised before the response of the request that caused it.** `await
  client.SetCurrentProgramScene("Game")` can return before the `CurrentProgramSceneChanged` for it was raised. Code that read something
  that the handler sets, right after the `await`, has to wait for the event instead.
- **Events queue up while a handler runs**, in memory and without a limit; the client logs a warning when 1000 are waiting. Keep handlers
  short.
- **`ConnectionClosed` comes after the events that arrived before the connection ended**, and once `Dispose()` was called nothing that was
  waiting is raised any more (a handler that is running is not waited for). It is raised handler by handler, so a handler that throws no
  longer keeps the others from being called.
- **`PropertyChanged` and the statistics events are not part of this**: they are raised on the thread that causes the change, which can
  still be the thread that reads from OBS Studio.

### `SetSceneItemTransform` takes an update

[`SetSceneItemTransform`](xref:OBSStudioClient.ObsClient.SetSceneItemTransform*) and its builder take a
[`SceneItemTransformUpdate`](xref:OBSStudioClient.Classes.SceneItemTransformUpdate) instead of a `SceneItemTransform`. A `SceneItemTransform` converts to it
implicitly, so a call compiles unchanged. The update has a nullable member for each part of the transform, and sends only
what you set, which is what obs-websocket supports. See [Calling requests](requests.md#moving-and-scaling-a-scene-item).

### A `CancellationToken` at the end of every request method

Every request method, `ConnectAsync`, `SendRequestBatchAsync` and `ReidentifyAsync` have a new last parameter,
`CancellationToken cancellationToken = default`: see [cancelling a request](requests.md#cancelling-a-request). A call compiles unchanged,
but there are two things to know:

- **Code that was compiled against 3.x or 4.0.0-beta1 has to be recompiled**, because the signature of each of these methods changed.
  (Everything that was compiled against 3.x had to be recompiled anyway, see above.)
- **A method group converted to a delegate** no longer converts: `Func<Task<bool>> enabled = client.GetStudioModeEnabled;` does not
  compile, because a method with an optional parameter does not fit a delegate type without it. Use a lambda:
  `Func<Task<bool>> enabled = () => client.GetStudioModeEnabled();`.

### Other signatures

| What | 3.x | 4.0 |
| --- | --- | --- |
| `Scene` | `Scene(int sceneIndex, string sceneName)`, `SceneIndex` is an `int` | A `sceneUuid` is required, and `SceneIndex` is an `int?`: a scene of a canvas other than the main one has no index. |
| `CreateRecordChapter` | `Task<string?> CreateRecordChapter()`, which always threw an `InvalidCastException` | `Task CreateRecordChapter(string? chapterName = null)`. `CreateRecordChapterResponse` is gone. |
| `SceneTransitionResponse` | `TransitionName` and `TransitionDuration` could not be null | `string?` and `long?`: a scene without a transition override has neither. |
| `SceneTransitionListResponse.CurrentSceneTransitionKind`, `RecordStateChangedEventArgs.OutputPath` | `string` | `string?` |
| `SpecialInputsResponse` (`Desktop1` to `Mic4`) | `string` | `string?`: OBS Studio sends `null` for an input that is not configured. |
| `InputVolumeMeterResponse.InputKind`, `UnversionedInputKind` | `string` | `string?`: OBS Studio never sends them. |
| `PropertyItem.ItemValue` | `string` | `JsonElement?`: OBS Studio sends a string, a number or `null`. `item.ItemValue!.Value.GetString()` is what `ItemValue` was for a list of strings. |
| `SceneItemListReindexedEventArgs.SceneItems` | `SceneItem[]` | `BasicSceneItem[]`: the `SceneItemId` and `SceneItemIndex` that OBS Studio sends for each item. |
| `TotalMessagesReceived`, `SessionMessagesReceived` | Counted WebSocket frames, so a large message counted several times | They count messages. |
| `AddOpenSourceProjectorRequest`, `AddOpenVideoMixProjectorRequest` | Threw an `ArgumentException` for an invalid combination of monitor and geometry | Send what they are given and leave the check to obs-websocket, which answers an error. |
| `MonitorTypeResponse` | Existed, but no request returned it | Removed (`GetInputAudioMonitorType` returns the `MonitorType`). |
| The constructors of `RequestMessage` that took a `dynamic` | `protected internal` | Removed. |
| The package dependency | `Microsoft.Extensions.Logging` 9.0.9 | `Microsoft.Extensions.Logging.Abstractions` 8.0.0, which has the `ILogger` that the client uses. An application that makes its own logger with `LoggerFactory` references `Microsoft.Extensions.Logging` itself; one that gets its logger from dependency injection has it already. |

## What behaves differently

These were bugs. Code that worked around them may not need the workaround any more.

- **`SetInputSettings` no longer kills the connection.** No response type was registered for it, so reading the response threw in the
  receive loop, which ended without cleaning up: `ConnectionState` kept saying `Connected` while requests timed out.
  This was [#17](https://github.com/tinodo/obsclient/issues/17) and the symptoms of [#15](https://github.com/tinodo/obsclient/issues/15).
  A receive loop that ends for any reason now always cleans up, so `ConnectionState` reaches `Disconnected`, `ConnectionClosed` is raised and
  `AutoReconnect` works.
- **`ConnectAsync`** returns `false`, instead of hanging, when the connection is lost before OBS Studio accepted the handshake, gives
  up after 10 seconds when the handshake is never completed, and accepts port 65535.
- **Requests that are waiting when the connection is lost** fail at once with an `ObsClientException` instead of after their timeout.
- **`haltOnFailure` of a batch** takes effect. The client sent it under the wrong name, so a batch always ran on after a failure.
- **State changes are no longer dropped.** 3.x passed on one event per 100 ms for each of `InputActiveStateChanged`,
  `InputShowStateChanged`, `SceneItemTransformChanged` and `InputVolumeMeters`, whichever input or scene item it was about, so a scene switch
  reached you as one change. Only `InputVolumeMeters` is throttled now.
- **The data of a `CustomEvent` arrives.** 3.x read a member that obs-websocket never sends.
- **A password with a character outside ASCII** works: the client hashed it as ASCII, obs-websocket hashes UTF-8.
- **`GetSceneSceneTransitionOverride`** works for a scene without an override, which is every scene by default.
- **`SetInputMute` by UUID, `GetInputDeinterlaceFieldOrder` and `GetInputAudioMonitorType`** work; each always failed.
- **`SetSceneItemTransform`** no longer fails for an item without bounds.
- **The `RecordFileChanged` event** (obs-websocket 5.5.0) reaches you.
- **An event type that the client does not know** is ignored instead of throwing.
- **Multi-byte characters** in scene names and settings are no longer damaged at the boundary of the receive buffer.

## What is new

- **Canvases** (obs-websocket 5.7.0): `GetCanvasList`, the `Canvas` classes, `CanvasCreated`, `CanvasRemoved` and `CanvasNameChanged`, the
  `EventSubscriptions.Canvases` flag, and an optional `canvasUuid` on every request that looks up a scene or a source by name.
- The requests that had no method under their own name: `SetInputVolume`, `OpenSourceProjector` and `OpenVideoMixProjector`.
  `SetInputVolumeMul`, `SetInputVolumeDb` and the projector helpers stay as shortcuts.
- Batch builders that were missing, and **batches that address inputs, scenes and sources by UUID**.
- `SceneItemTransformUpdate` and `SceneItemTransform.ToUpdate()`.
- Members that obs-websocket sends and 3.x did not expose, among them `InputCreatedEventArgs.InputKindCaps`, `Input.InputUuid` and
  `SceneItem.SourceUuid`.
- Every message the client **sends** is logged at the debug level, as what it receives already was.
- A `net10.0` target next to `net8.0`.
- [A test app](test-app.md), a WinUI 3 application to try every request and event by hand.
- A test suite with protocol conformance tests, and code that is generated from the protocol definition: see
  [Staying in sync with obs-websocket](protocol-sync.md).

## Upgrading step by step

1. Update the package: `dotnet add package OBSClient --prerelease` (or `--version 4.0.0-beta2`).
2. Build. The compiler points to what has to change: named arguments, method groups, the types in the table above.
3. Read the list of behavior changes. If you kept a workaround for a bug that is listed there, try without it.
4. Run your program against OBS Studio. The [test app](test-app.md) is a quick way to see what a response looks like.
