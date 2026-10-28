# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[Semantic Versioning](https://semver.org/).
For history before 4.0.0, see [GitHub Releases](https://github.com/tinodo/obsclient/releases).

## [4.0.0-beta1] - 2026-10-27

The first prerelease of 4.0.0, for trying out: it contains everything that is listed under 4.0.0 below, except the items that are
marked *after beta1*. Its public API can still change before 4.0.0 is released, so read the *Breaking changes* below to see what you
have to change, and report what you find in an [issue](https://github.com/tinodo/obsclient/issues). A prerelease is not installed by a
plain `dotnet add package OBSClient`: use `dotnet add package OBSClient --version 4.0.0-beta1` (or `--prerelease` for the newest
prerelease). The [GitHub release](https://github.com/tinodo/obsclient/releases/tag/v4.0.0-beta1) has the package and the test app as
downloads.

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
- **One parameter for a name or a UUID.** The pairs of overloads `Foo(string inputName, …)` / `Foo(Guid inputUuid, …)` (and
  their `canvasUuid` variants, which were never released) are replaced by one method per request that takes an `InputRef`,
  a `SceneRef` or a `SourceRef`: a small value that holds either a name or a UUID. A `string`, a `Guid`, and the `Input`,
  `Scene` or `SceneItem` that OBS Studio reported convert implicitly, so `SetCurrentProgramScene("Game")`,
  `SetCurrentProgramScene(sceneUuid)` and `SetCurrentProgramScene(scene)` still read the same, and an input or scene
  that is passed as an object is identified by its UUID, which does not change when it is renamed. Every public overload
  of 3.0.1 (221 methods of `ObsClient` and 141 builders of `RequestBatchMessage`) was compared with the new signature of
  its request, with a throwaway script that is not part of the repository: a call that compiled against 3.0.1 still
  compiles, except for seven overloads: `CreateInput` and `SetCurrentSceneTransitionSettings` and their builders, which took
  a wrong type for their settings, and `CreateRecordChapter`, whose return type changed (all three are listed below). What
  does break is **named arguments** (`inputName:` and `inputUuid:` are now `input:`, `sceneName:` and `sceneUuid:` are
  `scene:`, `sourceName:` and `sourceUuid:` are `source:`); a **method group** converted to a delegate type
  (`Func<string, Task<bool>> mute = client.GetInputMute;` no longer converts, because the implicit conversion from `string`
  to `InputRef` applies to a call, not to a method group); and, as for every signature change, code that was compiled
  against 3.x has to be recompiled. The same goes for the batch builders of `RequestBatchMessage`. `canvasUuid` is a
  trailing optional parameter (`GetSceneItemList("Scene", canvasUuid: id)`).
- `CreateInput` (and `RequestBatchMessage.AddCreateInputRequest`) take `Dictionary<string, object>? inputSettings` instead
  of `Input? inputSettings`, and `SetCurrentSceneTransitionSettings` (and its builder) takes `Dictionary<string, object>`
  instead of a `TransitionResponse?`. `Input` describes an entry of the input list (kind and name) and `TransitionResponse`
  is what `GetCurrentSceneTransition` returns; neither is a settings object, so there was no way to give a new input
  any settings (such as the URL of a browser source) or to change the settings of the current transition. Passing
  `null` to `CreateInput` still works.
- **Every request method takes a `CancellationToken`** *(after beta1)* as its last parameter, with a default (`GetSceneList(Guid? canvasUuid = null,
  CancellationToken cancellationToken = default)`), and so do `ConnectAsync`, `SendRequestBatchAsync` and `ReidentifyAsync` (see
  *Added*). The builders of `RequestBatchMessage` have none: they only collect requests. A call compiles unchanged, but the signature
  of each of these methods changed, so code that was compiled against 3.x or 4.0.0-beta1 has to be recompiled (against 3.x it had to
  be anyway, see above), and a method group that is converted to a delegate type (`Func<Task<bool>> f = client.GetStudioModeEnabled;`)
  no longer converts, because a method with an optional parameter does not fit a delegate type without it: use a lambda.
- `RequestBatchMessage.AddOpenSourceProjectorRequest` and `AddOpenVideoMixProjectorRequest` send the `monitorIndex` and
  `projectorGeometry` they are given and leave the check of the combination to obs-websocket, which answers a monitor
  index of 0 or more together with a geometry with an error. They used to throw an `ArgumentException` first, and the
  former defaulted `monitorIndex` to `-1`; leaving it out has the same effect now, because that is obs-websocket's default.
- `MonitorTypeResponse` was removed: no request returned it (`GetInputAudioMonitorType` returns the `MonitorType`).
- **The dependency is `Microsoft.Extensions.Logging.Abstractions` 8.0.0** *(after beta1)*, where it was `Microsoft.Extensions.Logging` 9.0.9. The client
  only uses the `ILogger<ObsClient>` interface (and `NullLogger`), which the abstractions have, so an application no longer gets the
  whole logging system with it, and one that stays on the 8.x packages of `Microsoft.Extensions` does not have to move to 9.0.9 (it could
  not restore: package downgrade, NU1605). An application that makes its own logger with `LoggerFactory` has to reference
  `Microsoft.Extensions.Logging` itself, as it does for `AddConsole` (`Microsoft.Extensions.Logging.Console` brings it along); one that
  gets its logger from dependency injection (a generic host, ASP.NET Core) has it already.
- The constructors of `RequestMessage` that derived the request type from the name of the calling method (`[CallerMemberName]`)
  and took a `dynamic` are gone. They were `protected internal`, so only a class that derived from `RequestMessage` could
  use them from outside the assembly.
- `Scene`'s constructor gained a required `sceneUuid` parameter and `Scene.SceneIndex` is now nullable (`int?`): OBS
  Studio has always returned a scene UUID, and scenes on a non-main canvas have no index. Code that only reads
  `Scene` instances needs to handle the nullable index; code that constructs `Scene` instances has to pass a UUID.
- `CreateRecordChapter(string? chapterName = null)` now returns `Task` instead of `Task<string?>`, and takes the optional
  chapter name that obs-websocket supports. OBS Studio returns no data for this request, so the old method could never
  succeed: it always threw an `InvalidCastException`. `CreateRecordChapterResponse` was removed.
- `ObsClient.TotalMessagesReceived` and `SessionMessagesReceived` now count messages; they counted WebSocket frames,
  which made a large message count several times.
- `SetSceneItemTransform` and `RequestBatchMessage.AddSetSceneItemTransformRequest` take a `SceneItemTransformUpdate`
  instead of a `SceneItemTransform` (see *Added*). A `SceneItemTransform` converts to it implicitly, so calls compile
  unchanged, but code that was compiled against 3.x has to be recompiled.
- Fields that obs-websocket documents as `null` in some situation are nullable now (found by reading the responses of a real
  OBS Studio, see *Added*): `SceneTransitionResponse.TransitionName` and `TransitionDuration` (`string?` and `long?`: the
  answer for a scene without a transition override, which the client could not read at all, see *Fixed*),
  `SceneTransitionListResponse.CurrentSceneTransitionKind` and `RecordStateChangedEventArgs.OutputPath` (`string?`), and the
  six names of `SpecialInputsResponse` (`Desktop1` to `Mic4`, `string?`): OBS Studio sends `null` for each one that is not
  configured, which is the second desktop and the second to fourth microphone by default. `InputVolumeMeterResponse.InputKind`
  and `UnversionedInputKind` are `string?` too: OBS Studio never sends them (see *Fixed*).
- `PropertyItem.ItemValue` (the items of `GetInputPropertiesListPropertyItems`) is a `JsonElement?` instead of a `string`, and so
  is the matching constructor parameter: OBS Studio sends a string, an integer, a floating point number or `null`, depending on
  the format of the list (see *Fixed*). `item.ItemValue!.Value.GetString()` is what `item.ItemValue` was for a list of strings.
- `SceneItemListReindexedEventArgs.SceneItems` is an array of the new `BasicSceneItem`, with the `SceneItemId` and the
  `SceneItemIndex` that OBS Studio sends for each item of a reindexed scene (see *Fixed*), instead of an array of `SceneItem`.
- **The events of OBS Studio and `ConnectionClosed` are raised off the receive thread** *(after beta1)*. The thread that reads from OBS Studio
  used to run the handlers itself, so a handler that waited for a response (`.Result`, `.Wait()`) kept the client from reading it, and the
  request timed out. The client now queues them, and a task of the thread pool raises them, one at a time and in the order in which they
  happened: a handler can wait for a response, and a slow handler delays only the events behind it. What that changes for code that
  worked: (1) an event is no longer raised before the response to the request that caused it is handled (`await
  client.SetCurrentProgramScene("Game")` can return before `CurrentProgramSceneChanged` was raised for it); (2) events queue up in memory
  while a handler runs, without a limit (dropping one would lose a change), where they used to hold up the receive loop and everything
  behind it, and the client logs a warning when 1000 of them are waiting; (3) `ConnectionClosed` is raised after the events that arrived
  before the connection ended, and once `Dispose()` was called nothing that was waiting is raised any more, and a notification that is under
  way stops before its next handler (a handler that is running is not waited for, so it can still be running when `Dispose()` returns).
  `ConnectionClosed` is now raised handler by handler, like the events, so a handler that throws no longer keeps the handlers behind it from
  being called. `PropertyChanged` and the statistics events are not changed: they are still raised on the thread that causes the change,
  which can be the receive thread.

### Added
- Support for the **Canvases** category of obs-websocket 5.7.0: `GetCanvasList`, the `Canvas`, `CanvasFlags` and
  `CanvasVideoSettings` classes, the `CanvasCreated`, `CanvasRemoved` and `CanvasNameChanged` events, and the
  `EventSubscriptions.Canvases` flag (included in `EventSubscriptions.All`).
- An optional `canvasUuid` parameter on every request that looks up a scene or a source by name, so it can address a
  non-main canvas (for example `GetSceneList(canvasUuid)` and `CreateScene("Scene", canvasUuid)`).
- `SceneRef`, `InputRef` and `SourceRef` (see *Breaking changes*). A `SourceRef` also converts from an `InputRef`, a
  `SceneRef`, an `Input`, a `Scene` and the `SceneItem` that shows the source, because an input and a scene are sources.
  A reference that identifies nothing (`default`), or an argument that is required and null, is refused with an
  `ArgumentException` before anything is sent.
- The requests of the protocol that had no method under their own name: `SetInputVolume`, `OpenSourceProjector` and
  `OpenVideoMixProjector`. `SetInputVolumeMul`, `SetInputVolumeDb` and the `OpenSourceProjectorOnMonitor`/`Window` and
  `OpenVideoMixProjectorOnMonitor`/`Window` methods stay as shortcuts for them.
- `InputCreatedEventArgs.InputKindCaps` (obs-websocket 5.6.0), `SceneItemTransform.CropToBounds`, `Input.InputUuid` and
  `Input.InputKindCaps` (entries of `GetInputList`), and `SceneItem.SourceUuid` (entries of `GetSceneItemList`): obs-websocket
  has sent all of these for a while, but the client did not expose them, so the UUIDs that the UUID based requests need
  could not be read from a list. They are `init` properties, so no constructor changed.
- Request batches can now address inputs, scenes, sources and scene items by UUID, like the individual requests
  always could, and the batch builders that were missing altogether:
  `AddGetInputDeinterlaceModeRequest`, `AddSetInputDeinterlaceModeRequest`, `AddGetInputDeinterlaceFieldOrderRequest`,
  `AddSetInputDeinterlaceFieldOrderRequest`, `AddGetSceneItemSourceRequest`, `AddGetSourceFilterKindListRequest`,
  `AddSplitRecordFileRequest` and `AddCreateRecordChapterRequest`. `AddTriggerHotkeyByNameRequest` accepts a context name.
- `SceneItemTransformUpdate`: change some members of the transform of a scene item (position, scale, rotation, crop,
  bounds, alignment) without having to send the others, which is what obs-websocket supports. `SceneItemTransform.ToUpdate()`
  turns what was read into a change that can be adjusted with a `with` expression:
  `await client.SetSceneItemTransform("Scene", id, current.ToUpdate() with { ScaleX = 2, ScaleY = 2 });`
- **Cancelling** *(after beta1)*: the `CancellationToken` of a request ends the wait for the response with an `OperationCanceledException`
  and leaves the connection usable. A request that was already sent is not undone, and its answer is ignored when it arrives; a token
  that is cancelled before the call stops it before anything is sent. A request that runs out of time still throws a
  `TimeoutException`, and the call ends with its token or its timeout even when OBS Studio does not read a large request: the write goes
  on in the background and keeps its turn, and a message that still waits for its turn is not sent. The token also ends the wait between
  the retries of a request that OBS Studio is not ready for. Cancelling
  `ConnectAsync` ends the attempt as `Disconnect()` does (it closes the connection that is being opened and switches `AutoReconnect`
  off, for that attempt only: a connection that a handler started in its place keeps its settings) and throws an
  `OperationCanceledException`, where a failed attempt returns `false`. A `ReidentifyAsync` that was cancelled, or that failed, leaves
  the events that a reconnect subscribes to as they were: they are remembered when the message has been written.
- `net10.0` as an additional target framework, next to `net8.0`.
- **A test suite** (`OBSClient.Tests`) with an in-process fake obs-websocket server. Besides the connection, request,
  event and failure-mode tests it contains *protocol conformance tests*: they call every request method and every batch
  builder, check that each sends the request it is named after and compare what is sent on the wire, field by field,
  with a snapshot of obs-websocket's own protocol definition (`OBSClient.Tests/Protocol`), answer each request with the
  response the protocol documents for it, send every event the way the protocol documents it, and check the numeric
  values of the enums. They also read **what a real OBS Studio answered and sent** (`OBSClient.Tests/Fixtures`, captured with
  `tools/capture/obs-capture.ps1`: the responses to the `Get` requests, and the events) and fail when a member that OBS Studio sent is
  not given back, which is how the nested objects, that the protocol only describes in prose, are checked.
- **The protocol-derived code is generated** from obs-websocket's own machine-readable definition
  (`tools/protocol/obs-websocket-protocol.json`) and `tools/protocol/overlay.toml`, which holds the decisions that the
  definition cannot make (type names, the C# type of a field, patches where upstream's documentation is wrong): the
  methods of `ObsClient` and the builders of `RequestBatchMessage` for every request, the response and event types,
  the registries and the `RequestType` and `EventType` enums. `python tools/protocol/update.py --latest` brings the
  client to a new obs-websocket version; a new request, event or field needs no code (the conformance tests say what
  needs a decision, if anything). The definition, the overlay and the generator are inputs of the build under `tools/`
  and are **not** part of the package, which stays one assembly (`tools/verify-package.py` checks the package in CI, and a
  test checks the assembly). A scheduled workflow opens an issue as soon as obs-websocket publishes a new version, with
  the differences and the outcome of trying the update.
- **A test app** (`OBSClient.TestApp`), a WinUI 3 application with a Fluent 2 design that replaces the WinForms sample. Its only
  purpose is to try every request and event of the library by hand against a real OBS Studio: it offers an editor for every
  parameter, shows each result as the client returned it, as OBS Studio sent it, and as a comparison of the two (which finds
  nested members that the client does not know), puts requests in a batch to send them together, lists every event, and keeps
  a log of everything that went over the wire that can be exported, together with the shapes of the responses and events that
  were observed. The catalog of requests and events is generated from the protocol like the library is, and CI publishes the
  app as a self-contained download and starts it to check every page, including a run against a simulated OBS Studio that
  connects, sends requests and a batch, receives events and checks the log (with screenshots as an artifact). Every release has it
  as a zip (from 4.0.0-beta1 on). It is not part of the NuGet package.
- The client logs every message it **sends** at the debug level (`Sending: …`), as it already logged what it receives
  (`Received: …`); the answer to the authentication challenge is left out. The two `Debug.WriteLine` calls that did the same
  (in debug builds only) are gone.
- CI: a cross-platform build and test matrix, CodeQL analysis, GitHub Copilot code review of every pull request (which
  replaces SonarCloud, now removed), and a tag-triggered publish pipeline to nuget.org (Trusted Publishing) and GitHub Packages.
  *After beta1*: Dependabot proposes updates of the GitHub Actions and of the NuGet packages every week (see CONTRIBUTING).
- **Documentation** *(after beta1)*: a site at <https://tinodo.github.io/obsclient/> with guides (getting started, connecting, requests, batches, events,
  moving from 3.x, troubleshooting), a page about the test app, and an API reference that is generated from the XML documentation of the
  library. The Docs workflow builds it, checks its links and publishes it on every change of `main`; `python tools/docs/build.py` builds it
  locally (see CONTRIBUTING). The project URL of the package is the site now. The XML documentation that the site exposes was corrected
  where it was wrong: `ConnectAsync` said that authentication was handled asynchronously, while it waits for the handshake; the `Connected`
  member of `ConnectionState` was described as "Connecting"; and the documentation of `AutoReconnect` ended in stray characters.
- **Symbols and Source Link** *(after beta1)*: each release comes with a symbol package (`.snupkg`) on nuget.org, so that a debugger can
  step into the library and show the source of the commit that was built (Visual Studio: tick *NuGet.org Symbol Server* in *Tools,
  Options, Debugging, Symbols*). The package itself stays one assembly with its documentation, and CI checks both. The release
  pipeline also builds the test app and creates the GitHub release, with the notes from this file, by itself (see CONTRIBUTING).
- **Live tests** *(after beta1)*: `OBSClient.Tests/Live` runs against a real OBS Studio. `tools/live/run-live-tests.ps1` starts a private copy of one
  (always with `--portable`, with a configuration and a WebSocket port of its own, never the port of another instance, and it stops only
  what it started); the tests that create and remove scenes only change an OBS Studio that has the scene collection of the run. They compare
  what OBS Studio answers to every `Get` request that needs no parameters with what the client returns, member by member and value by value,
  take a scene with an input through its life and check that every event is raised as OBS Studio sent it, and try a wrong password, a handler
  that waits for a response, a cancelled batch, a failing batch and `Reidentify`. They are skipped unless `OBS_LIVE_PORT` is set (see
  CONTRIBUTING). What they found about OBS Studio itself is in the guides: OBS Studio 32.2.0 and 33.0.0 send `SceneCreated` twice for every
  scene, a scene or an input that was removed can still be in the list of OBS Studio when its event arrives, and the interface thread of
  OBS Studio can be away for seconds, so that a request times out and is carried out later all the same.
  The one difference that the comparison of values found is on purpose, and is documented now: OBS Studio sends `null` as the
  `transitionSettings` of a transition that cannot be configured, and `TransitionResponse.TransitionSettings` is an empty dictionary then.
- **A package icon, better tags and a sample** *(after beta1)*: the package has an icon (the logo of the site) and the tags `obs`, `obs-studio`,
  `obs-websocket`, `websocket`, `streaming`, `dotnet` and `client`, so that it is easier to find and to recognise on nuget.org (CI checks that the
  icon is a whole PNG, with its chunks in order, a valid header, palette and transparency and image data that is as long as its header says,
  of at least 128 by 128 pixels). `samples/ConsoleSample`
  is a small program that connects, says which OBS Studio it reached, lists the scenes and prints the events (`dotnet run --project samples/ConsoleSample`); CI builds it
  against the package that was just packed, for both target frameworks, which shows that the package can be used. The target frameworks
  are `net8.0` and `net10.0`, the LTS releases: .NET 8 reaches the end of its support on 10 November 2026, and its target stays in the 4.x packages (see
  the compatibility table of the documentation).

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
    combination; only a real monitor index (0 or more) and a geometry exclude each other, as on the server. The builders
    now send what they are given and leave that check to obs-websocket.
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
- **`AutoReconnect` reconnected a client that was kicked** *(after beta1)*. obs-websocket ends a session with the close code `SessionInvalidated` (4011)
  when a user presses the Kick button of its session list, and documents that a client must not reconnect automatically after it. The client
  now sets `AutoReconnect` to `false` and stays `Disconnected` (`ConnectionClosed` is raised as before); call `ConnectAsync` to connect again.
  Found while writing the documentation of the reconnect behavior.
- Authentication hashed the password, salt and challenge as ASCII, so a password with a non-ASCII character (for
  example `pässword`) was silently changed to `?` and could not authenticate. obs-websocket hashes the UTF-8 bytes
  (`Utils::Crypto::GenerateSecret`/`CheckAuthenticationString`), and the client now does the same.
- `GetSceneList()` keeps sending no request data; the canvas overload sends the `canvasUuid`.
- The transform of a scene item that has no bounds could not be given back to it: `SetSceneItemTransform` sent the whole
  transform as it was read, including a bounds width and height of 0, which obs-websocket refuses (they must be at
  least 1; checked in the source of 5.7.6). Only the members that are meant to change are sent now, and a
  `SceneItemTransform` is converted so that the bounds of an item without bounds are left out.
- **`haltOnFailure` of a request batch never took effect.** `SendRequestBatchAsync` sent the field as `HaltOnFailure`;
  obs-websocket reads `haltOnFailure` and ignores a field it does not know, so a batch always ran on after a failed request
  (found with the Batch page of the test app, and checked in the source of 5.7.6).
- `ConnectAsync` refused port 65535, which is a valid port; it accepts 1 to 65535.
- **`GetSceneSceneTransitionOverride` failed for every scene without an override**, which is every scene by default: OBS Studio
  answers with `transitionName` and `transitionDuration` set to `null`, and the client could not convert the null to a number.
  Found by reading the responses of a real OBS Studio (`ObsStudioCaptureTests`).
- **State changes of inputs and scene items were dropped** (this was so in 3.0.1 as well): the client passed on one event per
  100 ms for each of the four events that obs-websocket counts as high-volume, whichever input or scene item it was about, so a
  scene switch, which changes the active state of many inputs at the same moment, reached the application as one change
  (`InputActiveStateChanged`, `InputShowStateChanged`, `SceneItemTransformChanged`). Only `InputVolumeMeters`, a stream of samples of
  which the latest replaces the others, is throttled now (one every 100 ms, as before). Found by sending the events that were
  captured from a real OBS Studio one after the other (`ObsStudioCaptureTests`).
- **The data of a `CustomEvent` never arrived** (this was so in 3.0.1 as well): obs-websocket sends the data that another client
  gave to `BroadcastCustomEvent` as the data of the event itself, and not as a member `eventData` of it (checked in the source of
  5.7.6, and in the events of a real OBS Studio), which is what its documentation of the event says, so `CustomEventEventArgs.EventData`
  was always `null`. It is the whole payload now.
- The entries of the `InputVolumeMeters` event have an `InputUuid` now (`InputVolumeMeterResponse`, an `init` property so that the
  constructor stays what it was). OBS Studio does not send `inputKind` and `unversionedInputKind` with them, so those two are
  `string?` now (they were always `null`).
- **`GetInputPropertiesListPropertyItems` failed for every list of numbers** (this was so in 3.0.1 as well): the value of an item is
  a string, an integer, a floating point number or `null`, depending on the format of the list (checked in the source of 5.7.6,
  `ArrayHelper::GetListPropertyItems`), and the client read it as a string. It keeps the kind that OBS Studio sent now (see
  *Breaking changes*), which matters because a setting that is set from it has to be set as the same kind.
- The items of a `SceneItemListReindexed` event were read as `SceneItem`, but OBS Studio sends only the `sceneItemId` and the
  `sceneItemIndex` of each (the *basic* list of obs-websocket), so every other member of the item was a default that looked like
  data: an item was disabled, locked, and had no name. They are `BasicSceneItem` now (see *Breaking changes*).
- `KeyModifier` is marked `[Flags]`: the client always treated it as a combination of keys (`KeyModifier.Control | KeyModifier.Shift`
  is the modifiers of Ctrl+Shift, which `TriggerHotkeyByKeySequence` and its batch builder send as one flag for each key), but the
  type did not say so, so it printed and parsed a combination as a number.

### Compatibility
- The numeric values of `EventType` and `RequestType` are unchanged: new members are appended (and `EventType.Unknown`
  is `-1`), so assemblies compiled against 3.0.x keep working with respect to those enums.
- The response and event types are what they were in 3.0.1 (the same constructors and properties, checked field by field),
  except for `CreateRecordChapterResponse` and `MonitorTypeResponse` (removed, see above) and the new canvas types; a list in
  a response is empty instead of null when OBS Studio sends none. The obsolete `CurrentProgramSceneName`,
  `CurrentProgramSceneUuid`, `CurrentPreviewSceneName` and `CurrentPreviewSceneUuid` properties of the two current-scene
  responses are still there.
- **Checked against the released package.** .NET's package validation (ApiCompat) of this build against the published 3.0.1
  package reports 224 differences, and each one is described in this file: the two removed response types, the constructor and
  `SceneIndex` of `Scene`, the removed constructors of `RequestMessage`, the value of the constant `EventSubscriptions.All`
  (below), and 217 request methods and builders whose signature takes a `SceneRef`, an `InputRef` or a `SourceRef` where it
  took a name or a `Guid`. Those are binary breaks, and source breaks only for the seven overloads named above (and for named
  arguments and method groups, as described there). Nothing else of the public API was removed or changed.
- The new `InputKindCaps`, `CropToBounds`, `InputUuid` and `SourceUuid` are `init` properties, so no existing constructor changed.
- Optional arguments that are not given are left out of the request instead of being sent as `null`. obs-websocket treats
  the two alike (`Request::Contains` ignores a null), except for the three fields where it gives a null a meaning: it
  removes the value. The client always sends those, so that `SetSceneSceneTransitionOverride("Scene", null, 500)` clears
  the transition that is overridden for the scene and sets the duration, and `SetProfileParameter(category, name, null)`
  deletes the parameter.
- The `Canvases` event subscription is part of `EventSubscriptions.All`. Older obs-websocket versions only require the
  value to be an unsigned number and ignore the bits they do not know (checked in the source of 5.5.2), so that is harmless there.
  `All` is a constant, so code that was compiled against 3.x keeps the old value (without `Canvases`) until it is recompiled.
- The events of OBS Studio and `ConnectionClosed` are raised by a dispatcher of the client and not by the receive thread, since 4.0.0
  (see *Breaking changes*): up to and including 4.0.0-beta1 a handler that blocked waiting for a response of OBS Studio (for example with
  `.Result`) kept the client from receiving that response, and the request timed out.
