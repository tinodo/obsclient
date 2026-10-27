# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[Semantic Versioning](https://semver.org/).
For history before 4.0.0, see [GitHub Releases](https://github.com/tinodo/obsclient/releases).

## [4.0.0] - Unreleased

Brings the client up to **obs-websocket 5.7.6** (the README claimed 5.5.2) and fixes a number of long-standing
problems that were found by checking the client, request by request and field by field, against the machine-readable
protocol definition. A new test suite keeps it that way (see *Added*).

What changed in the protocol between 5.5.2 and 5.7.6, and where it landed:

| obs-websocket | Change | In this client |
| --- | --- | --- |
| 5.5.3 – 5.5.6 | No changes to the protocol definition. | – |
| 5.6.0 | `Get/SetInputDeinterlaceMode`, `Get/SetInputDeinterlaceFieldOrder`; `inputKindCaps` on the `InputCreated` event. | The requests existed but `GetInputDeinterlaceFieldOrder` was broken (see *Fixed*) and had no batch builders; `InputKindCaps` is new. |
| 5.7.0 | Canvases: `GetCanvasList`, `CanvasCreated`/`CanvasRemoved`/`CanvasNameChanged`, `EventSubscription.Canvases`, `canvasUuid` on the requests that look up a scene or source by name. | New (see *Added*). |
| 5.7.1 | `canvasName` was dropped again in favour of `canvasUuid`; `canvasUuid` added to `CreateScene`. | Only `canvasUuid` is supported. |
| 5.7.2 – 5.7.6 | No changes to the protocol definition. | – |

### Breaking changes
- `Scene`'s constructor gained a required `sceneUuid` parameter and `Scene.SceneIndex` is now nullable (`int?`): OBS
  Studio has always returned a scene UUID, and scenes on a non-main canvas have no index. Code that only reads
  `Scene` instances needs to handle the nullable index; code that constructs `Scene` instances has to pass a UUID.
- `CreateRecordChapter(string? chapterName = null)` now returns `Task` instead of `Task<string?>`, and takes the optional
  chapter name that obs-websocket supports. OBS Studio returns no data for this request, so the old method could never
  succeed: it always threw an `InvalidCastException`. `CreateRecordChapterResponse` was removed.
- `CreateInput` (and `RequestBatchMessage.AddCreateInputRequest`) take `Dictionary<string, object>? inputSettings` instead
  of `Input? inputSettings`. `Input` describes an entry of the input list (kind and name), not the settings of an input,
  so there was no way to give a new input any settings (such as the URL of a browser source). Passing `null` still works.
- `ObsClient.TotalMessagesReceived` and `SessionMessagesReceived` now count messages; they counted WebSocket frames,
  which made a large message count several times.
- `SetSceneItemTransform` and `RequestBatchMessage.AddSetSceneItemTransformRequest` take a `SceneItemTransformUpdate`
  instead of a `SceneItemTransform` (see *Added*). A `SceneItemTransform` converts to it implicitly, so calls compile
  unchanged, but code that was compiled against 3.x has to be recompiled.

### Added
- Support for the **Canvases** category of obs-websocket 5.7.0: `GetCanvasList`, the `Canvas`, `CanvasFlags` and
  `CanvasVideoSettings` classes, the `CanvasCreated`, `CanvasRemoved` and `CanvasNameChanged` events, and the
  `EventSubscriptions.Canvases` flag (included in `EventSubscriptions.All`).
- `canvasUuid` overloads for every request that identifies a scene or a source by name, so they can address a
  non-main canvas (for example `GetSceneList(Guid canvasUuid)` and `CreateScene(string sceneName, Guid canvasUuid)`).
- `InputCreatedEventArgs.InputKindCaps` (obs-websocket 5.6.0), `SceneItemTransform.CropToBounds`, `Input.InputUuid` and
  `Input.InputKindCaps` (entries of `GetInputList`), and `SceneItem.SourceUuid` (entries of `GetSceneItemList`): obs-websocket
  has sent all of these for a while, but the client did not expose them, so the UUIDs that the UUID based requests need
  could not be read from a list. They are `init` properties, so no constructor changed.
- Request batches can now address inputs, scenes, sources and scene items by UUID, like the individual requests
  always could (about sixty new `Add…Request(Guid …)` overloads), and the batch builders that were missing altogether:
  `AddGetInputDeinterlaceModeRequest`, `AddSetInputDeinterlaceModeRequest`, `AddGetInputDeinterlaceFieldOrderRequest`,
  `AddSetInputDeinterlaceFieldOrderRequest`, `AddGetSceneItemSourceRequest`, `AddGetSourceFilterKindListRequest`,
  `AddSplitRecordFileRequest` and `AddCreateRecordChapterRequest`. `AddTriggerHotkeyByNameRequest` accepts a context name.
- `SceneItemTransformUpdate`: change some members of the transform of a scene item (position, scale, rotation, crop,
  bounds, alignment) without having to send the others, which is what obs-websocket supports. `SceneItemTransform.ToUpdate()`
  turns what was read into a change that can be adjusted with a `with` expression:
  `await client.SetSceneItemTransform("Scene", id, current.ToUpdate() with { ScaleX = 2, ScaleY = 2 });`
- `net10.0` as an additional target framework, next to `net8.0`.
- **A test suite** (`OBSClient.Tests`) with an in-process fake obs-websocket server. Besides the connection, request,
  event and failure-mode tests it contains *protocol conformance tests*: they call every request method and every batch
  builder, check that each sends the request it is named after and compare what is sent on the wire, field by field,
  with a snapshot of obs-websocket's own protocol definition (`OBSClient.Tests/Protocol`), answer each request with the
  response the protocol documents for it, send every event the way the protocol documents it, and check the numeric
  values of the enums.
- `tools/protocol/update-protocol-snapshot.py` to refresh that snapshot, and a scheduled workflow that opens an issue
  as soon as obs-websocket publishes a new version, with the differences.
- CI: a cross-platform build and test matrix, CodeQL analysis, and a tag-triggered publish pipeline to nuget.org
  (Trusted Publishing) and GitHub Packages.

### Fixed
- Request fields that could not be sent, or were sent wrongly:
  - `SetInputMute(Guid inputUuid, bool)` sent the UUID as `inputGuid`, which obs-websocket ignores, so it always failed.
  - `GetInputDeinterlaceFieldOrder` was registered with the response type of `GetInputDeinterlaceMode` and always
    threw an `InvalidCastException`.
  - `CreateInput` and `OpenSourceProjector…` had no way to give a `canvasUuid`; `CreateRecordChapter` could not send a
    chapter name.
  - The projector request builders (`AddOpenSourceProjectorRequest`, `AddOpenVideoMixProjectorRequest`) refused a
    `projectorGeometry` next to a `monitorIndex` of `-1`, which is the default of the former, so a windowed projector
    could only be requested by passing `null` for the index. obs-websocket treats `-1` as "no monitor" and accepts that
    combination; only a real monitor index (0 or more) and a geometry exclude each other, as on the server.
- Responses and events that were not wired up:
  - **No response type was registered for `SetInputSettings`**, so reading its response threw inside the receive loop,
    which ended without cleaning up: the connection was dead while `ConnectionState` kept saying `Connected`, and
    `AutoReconnect` never kicked in. This is the root cause of [#17](https://github.com/tinodo/obsclient/issues/17)
    (the reporter called `SetInputSettings` on a browser source) and produces exactly the symptoms in
    [#15](https://github.com/tinodo/obsclient/issues/15): requests that time out, then "not connected".
  - `GetInputAudioMonitorType` expected another response type than the one registered for it and always threw an
    `InvalidCastException`.
  - The `RecordFileChanged` event (obs-websocket 5.5.0) was parsed but the client had no event for it, so it never
    reached anyone.
- A receive loop that ends for any reason now always cleans up (`ConnectionState` reaches `Disconnected`,
  `ConnectionClosed` fires, `AutoReconnect` works), and a response that cannot be mapped or read fails only its own request.
- The connection lifecycle was reworked so that each connection owns its own socket, pending requests and handshake:
  - A response that arrived just after its request had timed out could throw inside the receive loop and take the
    whole connection down. The default request timeout is only 500 ms, which makes that realistic.
  - One message that cannot be read no longer closes the connection. A response that cannot be read fails only its own
    request, with a descriptive exception (it used to be a timeout, at best); an event that cannot be read is skipped.
  - Requests that are waiting for a response when the connection is lost now fail immediately with an
    `ObsClientException` instead of waiting for their timeout.
  - `ConnectAsync` returned never (it hung) when the connection was lost before OBS Studio had accepted the handshake; it
    now returns `false`, and it gives up after 10 seconds when OBS Studio never completes the handshake.
  - A handler of `PropertyChanged` that reconnects as soon as the state becomes `Disconnected` could have the new
    connection disposed by the receive loop of the old one.
  - Multi-byte characters (scene names, settings) that straddled the 4096-byte receive buffer were corrupted.
  - Concurrent requests now take turns writing to the socket.
  - An exception of a handler of `ConnectionClosed`, of the byte and message statistics events, or of `PropertyChanged`
    for `ConnectionState` and `AutoReconnect` (the notifications the client raises from its own flow) is logged instead
    of disturbing the connection, and those two `PropertyChanged` notifications are no longer raised while the client holds
    a lock (a handler that waited for a UI thread could deadlock it). `RequestTimeout`, `MaxRequestRetries` and
    `RequestRetryInterval` are only changed by you: they raise on the thread that sets them, and an exception of a handler
    reaches that caller.
- An event type that this client version does not know (for example one introduced by a newer obs-websocket version)
  no longer throws during deserialization; it maps to `EventType.Unknown` and is ignored
  ([#12](https://github.com/tinodo/obsclient/issues/12) was the same class of bug for an event type that was misnamed).
- `ConnectionClosed` did not fire after a deliberate `Disconnect()`.
- Authentication hashed the password, salt and challenge as ASCII, so a password with a non-ASCII character (for
  example `pässword`) was silently changed to `?` and could not authenticate. obs-websocket hashes the UTF-8 bytes
  (`Utils::Crypto::GenerateSecret`/`CheckAuthenticationString`), and the client now does the same.
- `GetSceneList()` keeps sending no request data; the canvas overload sends the `canvasUuid`.
- The transform of a scene item that has no bounds could not be given back to it: `SetSceneItemTransform` sent the whole
  transform as it was read, including a bounds width and height of 0, which obs-websocket refuses (they must be at
  least 1; checked in the source of 5.7.6). Only the members that are meant to change are sent now, and a
  `SceneItemTransform` is converted so that the bounds of an item without bounds are left out.

### Compatibility
- The numeric values of `EventType` and `RequestType` are unchanged: new members are appended (and `EventType.Unknown`
  is `-1`), so assemblies compiled against 3.0.x keep working with respect to those enums.
- The new `InputKindCaps`, `CropToBounds`, `InputUuid` and `SourceUuid` are `init` properties, so no existing constructor changed.
- The `Canvases` event subscription is part of `EventSubscriptions.All`. Older obs-websocket versions only require the
  value to be an unsigned number and ignore the bits they do not know (checked in the source of 5.5.2), so that is harmless there.
- Event handlers run on the receive thread. A handler must not block waiting for a response of OBS Studio (for example
  with `.Result`); it would keep the client from receiving that response.
