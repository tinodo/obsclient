# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[Semantic Versioning](https://semver.org/).
For history before 4.0.0, see [GitHub Releases](https://github.com/tinodo/obsclient/releases).

## [4.0.0-rc.3] - 2026-11-09

The third release candidate of 4.0.0, for trying out. It has everything that 4.0.0-rc.2 has, and what is marked *after rc.2* under 4.0.0 in the
[changelog](https://github.com/tinodo/obsclient/blob/main/CHANGELOG.md). **The library is the same as in 4.0.0-rc.2: no code of the package changed**, so there is nothing
in it that you have to update for. What is new is what the package says about itself, and the test app. The public API of the client is as closed as it was in rc.1, and
`ObsModel` is still **provisional**: its names and its details can still change before 4.0.0 is released. Read the *Breaking changes* there to see what you have to change,
and report what you find in an [issue](https://github.com/tinodo/obsclient/issues). A prerelease is not installed by a plain `dotnet add package OBSClient`: use
`dotnet add package OBSClient --version 4.0.0-rc.3` (or `--prerelease` for the newest prerelease). The GitHub release of this version has the package, the test app and the
attestation of where the files come from as downloads.

What is new since 4.0.0-rc.2, in short:

- **The two ways to work with OBS Studio, told in the README and in the description of the package.** OBSClient has two levels. `ObsClient` is the protocol as it is: every
  request and event of obs-websocket, complete, close to the API. `ObsModel` is OBS Studio by name: scenes, inputs and filters that you work with the way you think of them,
  close to OBS Studio. The README (the page of the package on nuget.org) says how the two differ, shows the same jobs done both ways, and says which one to use and how to use
  both; the description of the package says it in one sentence. The documentation site tells the same story in a new article, *Two ways to work with OBS Studio*, and has a
  first program for each level in the getting started guide.
- **The test app** (`OBSClient.TestApp`, for Windows, a download of the release) is built with Windows App SDK 2.6.4, where it was 2.5.1. It looks and works the same.

## [4.0.0-rc.2] - 2026-11-08

The second release candidate of 4.0.0, for trying out. It has everything that 4.0.0-rc.1 has, and what is marked *after rc.1* under 4.0.0 in the
[changelog](https://github.com/tinodo/obsclient/blob/main/CHANGELOG.md). The public API of the client is as closed as it was in rc.1. The new
`ObsModel` is the exception: it is **provisional**, and its names and its details can still change before 4.0.0 is released. Read the
*Breaking changes* there to see what you have to change, and report what you find in an
[issue](https://github.com/tinodo/obsclient/issues). A prerelease is not installed by a plain `dotnet add package OBSClient`: use
`dotnet add package OBSClient --version 4.0.0-rc.2` (or `--prerelease` for the newest prerelease). The GitHub release of this version has the
package, the test app and the attestation of where the files come from as downloads.

What is new since 4.0.0-rc.1, in short:

- **`ObsModel`: OBS Studio by name** *(new, and provisional)*. It reads the scenes, the inputs, the scene items, the filters and the outputs of OBS Studio into a
  model in memory, follows what OBS Studio and the user in it do, and tells you what changed, so that you work with names: `obs.Scenes["Gaming Scene"]`,
  `obs.Inputs["Microphone"]`. It makes, renames and removes scenes, inputs and filters by name, switches the program scene, shows, hides, locks, moves and removes
  scene items, mutes an input and sets its volume, enables, disables, moves and renames filters, and starts, stops and toggles the stream, the recording (which can
  be paused), the virtual camera and the replay buffer (which can be saved). A call returns when the model shows the change. It extends the client and hides
  nothing of it: `obs.Client` is the `ObsClient`. It does not have yet the control of studio mode and of the preview scene, transitions, profiles, scene collections,
  media inputs, and the settings of inputs and filters: use `obs.Client` for those. The article
  [Controlling OBS Studio by name](https://tinodo.github.io/obsclient/articles/model.html) tells the rest, and `samples/ModelSample` is a small command prompt to
  try it against your own OBS Studio (OBS Studio 30.1 or newer).
- **The library targets `net10.0` only**: the `net8.0` target is gone, because Microsoft ends the support of .NET 8 and .NET 9 on 10 November 2026. An application
  that uses 4.0.0-beta1, 4.0.0-beta2 or 4.0.0-rc.1 on .NET 8 or 9 stays on that version. Nothing in the API changed because of it.

## [4.0.0-rc.1] - 2026-11-03

The first release candidate of 4.0.0, for trying out. The public API is closed now, so what changes between this version and 4.0.0 is fixes
for what is found with it. That includes a mistake in the API itself, if somebody finds one: after 4.0.0 it could no longer be corrected
without a new major version. This version contains everything that is listed under 4.0.0 in the
[changelog](https://github.com/tinodo/obsclient/blob/main/CHANGELOG.md), where what is new since 4.0.0-beta2 is marked *after beta2*. Read the
*Breaking changes* there to see what you have to change, and report what you find in an
[issue](https://github.com/tinodo/obsclient/issues). A prerelease is not installed by a plain `dotnet add package OBSClient`: use
`dotnet add package OBSClient --version 4.0.0-rc.1` (or `--prerelease` for the newest prerelease). The GitHub release of this version has the
package, the test app and the attestation of where the files come from as downloads.

What is new since 4.0.0-beta2, in short:

- **The public API is closed**, so that it can grow but never has to shrink. 17 types and two members left it: 13 types are internal now
  (what the client and OBS Studio exchange to connect, to deliver an event and to answer a batch, the two JSON converters, and what the
  messages are made of), and four that nothing used are gone (`Crop`, `Coordinates`, `BalanceType` and `IconType`). 130 classes are sealed,
  `ObsClient` among them. Code that only calls the methods of `ObsClient` and handles its events is not affected, whether it was compiled
  against 4.0.0-beta2 or not. Code that derives from one of the sealed classes no longer compiles, so hold the object in a class of your own
  instead, and code that used one of the types that left has to stop using it.
- **Fixed**: a message from OBS Studio that is not as it should be could end the connection or throw (an `op` that is not a number or is
  missing, `null` data, a response that names no request, an event type that is not valid text, a list of levels that is `null`); found by new
  fuzz tests. `CreateScene` threw for a scene that an OBS Studio before 30.1.0 had made (it sends no UUID), and `GetLevelDb` threw for a channel
  that is not there.
- **The package**: the license is the SPDX expression `MIT` (the `LICENSE` file is in the package as well), the package says whose copyright
  it is, and its release notes are the GitHub release of its version. The README has absolute links and no raw HTML: nuget.org drops what it
  cannot show, and a published version cannot be changed.
- **Tested more**: against a server that misbehaves, a connection that fails again and again (a soak test against a real OBS Studio, every
  week), input that nobody wrote an example for (property-based and fuzz tests), and an older OBS Studio (30.0.2) next to the newest one.
- **Where the files come from**: the package, its symbols, the test app and the libraries in the package have a signed build provenance
  attestation, which is also a file of the release (`OBSClient.4.0.0-rc.1.intoto.jsonl`); the notes of the release say how to check it.
- **The project**: a Code of Conduct, a weekly OpenSSF Scorecard, and a review of the dependencies that a pull request changes.

## [4.0.0-beta2] - 2026-10-29

The second prerelease of 4.0.0, for trying out: it contains everything that is listed under 4.0.0 in the
[changelog](https://github.com/tinodo/obsclient/blob/main/CHANGELOG.md), where what is new since 4.0.0-beta1 is marked *after beta1*.
Its public API can still change before 4.0.0 is released, so read the *Breaking changes* there to see what you have to change, and report
what you find in an [issue](https://github.com/tinodo/obsclient/issues). A prerelease is not installed by a plain
`dotnet add package OBSClient`: use `dotnet add package OBSClient --version 4.0.0-beta2` (or `--prerelease` for the newest prerelease).
The GitHub release of this version has the package and the test app as downloads.

What is new since 4.0.0-beta1, in short:

- **Code that was compiled against 4.0.0-beta1 has to be recompiled.** Every request method, `ConnectAsync`, `SendRequestBatchAsync` and
  `ReidentifyAsync` takes a `CancellationToken` as its last parameter now, so that a request can be cancelled. A call compiles unchanged;
  a method group that is converted to a delegate type does not, so use a lambda.
- **The events are raised off the receive thread**, one at a time and in the order in which they happened, so a handler can wait for a
  response (`.Result`) without the request timing out. An event is no longer guaranteed to be raised before the response to the request
  that caused it is handled: `await client.SetCurrentProgramScene("Game")` can return before `CurrentProgramSceneChanged` was raised.
- **The dependency is `Microsoft.Extensions.Logging.Abstractions` 8.0.0**, where it was `Microsoft.Extensions.Logging` 9.0.9. An
  application that makes its own logger with `LoggerFactory` has to reference `Microsoft.Extensions.Logging` itself.
- **Fixed**: `AutoReconnect` reconnected a client that was kicked in OBS Studio. It stays disconnected now, as obs-websocket requires.
- **New**: a symbol package with Source Link, so that a debugger can step into the library; a package icon and better tags; a
  [documentation site](https://tinodo.github.io/obsclient/); a console sample; and live tests against a real OBS Studio, which found that
  OBS Studio 32.2.0 and 33.0.0 send `SceneCreated` twice for every scene (reported to obs-websocket; the guide on events says what to do
  about it).

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
- **The library targets `net10.0` only** *(after rc.1)*: the `net8.0` target that 4.0.0-beta1, 4.0.0-beta2 and 4.0.0-rc.1 had is gone, because Microsoft ends the
  support of .NET 8 and .NET 9 on 10 November 2026. An application that uses one of those prereleases on .NET 8 or 9 stays on it. Nothing in the API changed because of it.
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
  worked: (1) an event is no longer guaranteed to be raised before the response to the request that caused it is handled (`await
  client.SetCurrentProgramScene("Game")` can return before `CurrentProgramSceneChanged` was raised for it); (2) events queue up in memory
  while a handler runs, without a limit (dropping one would lose a change), where they used to hold up the receive loop and everything
  behind it, and the client logs a warning when 1000 of them are waiting; (3) `ConnectionClosed` is raised after the events that arrived
  before the connection ended, and once `Dispose()` was called nothing that was waiting is raised any more, and a notification that is under
  way stops before its next handler (a handler that is running is not waited for, so it can still be running when `Dispose()` returns).
  `ConnectionClosed` is now raised handler by handler, like the events, so a handler that throws no longer keeps the handlers behind it from
  being called. `PropertyChanged` and the statistics events are not changed: they are still raised on the thread that causes the change,
  which can be the receive thread.

- **The wire types are internal, types that nothing used are gone, and the classes are sealed** *(after beta2)*, so that 4.0.0 freezes an
  API that can grow but never has to shrink: a type that is public cannot be made internal, and a class that others can derive from cannot
  be sealed, without breaking the code that uses it, whereas the other way round breaks nobody. Compared with 4.0.0-beta2 (ApiCompat on the
  `net8.0` assemblies), 17 types left the public API, two members did, and 130 classes that could be derived from are sealed. A new test
  (`PublicApiTests`) keeps it so.
  - What the client and OBS Studio exchange to connect, to deliver an event and to answer a batch is internal: `HelloMessage`,
    `IdentifyMessage`, `IdentifiedMessage`, `ReidentifyMessage`, `EventMessage`, `RequestBatchResponseMessage`, `Authentication` and
    `IMessage`, and so are the two JSON converters, `EventTypeConverter` and `NullableNumberToNumberConverter`, which were public by
    accident (the documentation site already left them out). An application gets what the messages carry from the methods and the events
    of `ObsClient`. `RequestBatchMessage`, `RequestMessage` and `RequestResponseMessage` stay public, because they are how a batch is built
    and read, and so does `IResponse`, the type of `RequestResponseMessage.ResponseData`.
  - What the messages are made of is internal too, because nothing in the API takes, returns or raises it: the enums `OpCode` and
    `EventType` (the events are raised as `ObsClient` events, with their own arguments) and the class `KeyModifiers`
    (`TriggerHotkeyByKeySequence` takes the `KeyModifier` flags). `Crop`, `Coordinates`, `BalanceType` and `IconType` are removed: they were
    left over from the protocol of obs-websocket 4, and no request returned them, no event carried them and nothing took them (like
    `MonitorTypeResponse`, see above).
  - Every other class is sealed: `ObsClient`, the classes of the responses, of the events and of `OBSStudioClient.Classes`, and the batch
    messages that stay public. Not sealed are the exceptions (as in the framework) and `Transition`, which `TransitionResponse` extends.
    Code that derives from a sealed class no longer compiles: hold the object in a class of your own instead. `ObsClient` had nothing
    virtual to override but `Dispose(bool)`, which is gone: `Dispose()` does it all.
  - `RequestResponseMessage.OnDeserialized()`, which `System.Text.Json` calls, implements `IJsonOnDeserialized` explicitly now, so it is no
    longer a public method.

### Added
- **`ObsModel`: OBS Studio by name** *(after rc.1; new, and provisional: its names and details can change until 4.0.0 is released)*. `ObsModel` keeps a model of the
  scenes, the inputs, the scene items, the filters and the outputs of OBS Studio in memory, so that you work with names and objects and not with UUIDs and ids that you have to
  look up first: `obs.Scenes["Gaming"]`, `obs.Inputs["Webcam"]`, `scene.Items`, `obs.ProgramScene`, `input.Filters["Noise Gate"]`, `obs.Outputs.Stream`. It follows OBS Studio (and
  the user in it) with the events, reads again where the events cannot say, and tells you what changed (`Changed`, `INotifyPropertyChanged`, `Resynchronized`).
  - You can make, rename and remove scenes and inputs by name (`CreateSceneAsync`, `CreateInputAsync`, `AddAsync`, `RenameAsync`, `RemoveAsync`), switch the program scene
    (`SwitchToAsync`), show, hide, lock, move and remove scene items, mute an input and set its volume, and show or hide an input in every scene.
  - The filters of scenes and inputs can be enabled, disabled, toggled, renamed, moved, removed and made (`CreateFilterAsync`), and stay the same object when they are renamed or
    moved. Their settings stay with the plain client, because OBS Studio announces few changes of them.
  - The outputs, `obs.Outputs.Stream`, `.Record` (which can be paused), `.VirtualCamera` and `.ReplayBuffer` (which can be saved), show their `Status` and `IsActive`, and can be started,
    stopped and toggled. A start or a stop returns when OBS Studio has begun, because a stream takes seconds to connect and can fail, and OBS Studio does not always say that a start
    failed (an output that stays starting is read again after `ObsModelOptions.OutputStepTimeout`).
  - The changes are made one at a time, in the order of the calls, and a call returns when the model shows the change. What can go wrong is told by exceptions that say what to do
    (`ObsNameInUseException`, `ObsRemovedException`, `ObsNotConfirmedException`, `ObsUnknownOutcomeException`, `ObsNotReadyException`).
  - It extends the client and hides nothing of it: `ObsModel.Client` is the `ObsClient`. It is tested against a simulation of OBS Studio that lags and reorders events the way the real
    one does, and against a real OBS Studio. There is an article, *Controlling OBS Studio by name*, and a runnable sample, `samples/ModelSample` (a small command prompt: `scene "Gaming"`,
    `mute "Mic"`, `hide "Gaming" "Webcam"`). The text of a change in `Changed` (`Console.WriteLine(e)`) shows a list by the names in it.
- **The two ways to work with OBS Studio, told in the README and on the site** *(after rc.2)*: the README, the home page of the site and a new article, *Two ways to work with OBS Studio*, tell that OBSClient has
  two levels: `ObsClient`, the protocol as it is (every request and event, complete, close to the API), and `ObsModel`, OBS Studio by name (intuitive, close to OBS Studio). They say how the two differ, show the
  same jobs done both ways, and say which one to use and how to use both. The getting started guide has a first program for each, the guides of the client say that they are about `ObsClient`, and the API
  reference starts with both (it did not mention the `OBSStudioClient.Model` namespace). The description of the package says it too. The README is the page of the package on nuget.org, which shows what was packed: the new text appears there with the next version.
- **`ObsModel` does more than scenes, inputs, filters and outputs: the calls that change nothing, the files of the outputs and the events that say that something happened** *(after rc.3; the first step of making
  `ObsModel` complete, which goes on until 4.0.0)*.
  - **Asking and telling** (nothing is kept in the model, and each call takes its turn in the line of the changes): `GetStatsAsync()`, `GetHotkeysAsync()`, `TriggerHotkeyAsync(name, context)` and
    `TriggerHotkeyAsync(key, modifiers)` (and `TriggerHotkeyAsync(name)` on a scene or an input), `BroadcastCustomEventAsync(data)`, `CallVendorRequestAsync(vendor, request, data)`, `GetMonitorsAsync()` and
    `OpenVideoMixProjectorAsync(...)`; on scenes and inputs `GetActivityAsync()`, `GetScreenshotAsync(...)` (an `ObsScreenshot`), `SaveScreenshotAsync(...)` and `OpenProjectorAsync(...)`; on inputs
    `OpenPropertiesDialogAsync()`, `OpenFiltersDialogAsync()` and `OpenInteractDialogAsync()`.
  - **Outputs**: `obs.Outputs.GetAllAsync()` lists every output of OBS Studio, and `obs.Outputs.Named(name)` gives one by its name (an `ObsNamedOutput`: start, stop, toggle, status, settings), for the outputs
    that plugins add. The stream sends captions (`SendCaptionAsync`); the recording toggles its pause (`TogglePauseAsync`), splits (`SplitFileAsync`) and takes chapters (`CreateChapterAsync`); the replay buffer tells its
    newest replay (`GetLastReplayFileAsync`); the stream and the recording tell their figures (`GetStatisticsAsync`: duration, bytes, congestion, skipped frames).
  - **The files**: `record.CurrentFile` and `replayBuffer.LastSavedPath`, with the events `record.FileChanged` and `replayBuffer.Saved`: where OBS Studio writes the recording (obs-websocket 5.5.0 and newer say it
    when the recording starts) and which replay was saved last.
  - **Events that say that something happened**: `obs.CustomEvent`, `obs.VendorEvent` (you opt in with `AdditionalEvents = EventSubscriptions.Vendors`), `obs.ScreenshotSaved`, and `obs.ExitStarted` with
    `obs.IsExiting` (a closed OBS Studio is not a lost connection). They are raised in the order of arrival, one at a time, also while the model is read. The model subscribes to the General events for them.
  - **What OBS Studio says about itself**: `obs.ObsVersion`, `obs.WebSocketVersion`, `obs.Platform` and `obs.SupportedImageFormats`. A request that the obs-websocket of that OBS Studio does not have (splitting a
    recording and chapters need 5.5.0) is refused with a message that says so.
  - `obs.Outputs.Stream` has the type `ObsStreamOutput` (a subclass of `ObsOutput`) and no longer `ObsOutput`: code that was compiled against rc.3 has to be compiled again. `ObsModel` is provisional.
- **`ObsModel` has the settings of inputs, the details of their audio, and the calls around them** *(after rc.3; the second step of making `ObsModel` complete, which goes on until 4.0.0)*.
  - **The settings of an input**: `input.Settings` (a `JsonElement`: what was set, without the defaults of the kind; current, because OBS Studio announces every change) and
    `input.SetSettingsAsync(settings, overlay)`, which returns when the model shows them. `CreateInputAsync(name, kind, settings)` starts an input with them. Checked with OBS Studio 33.0.0: a field that is an object
    replaces the object that was there as a whole, and a null field is ignored.
  - **The audio of an input**: `AudioBalance`, `SyncOffset` (a `TimeSpan`), `MonitorType` (`ObsMonitorType`) and `AudioTracks` (`ObsAudioTracks`, flags), current like the volume, with `SetAudioBalanceAsync`, `SetSyncOffsetAsync`,
    `SetMonitorTypeAsync` and `SetAudioTracksAsync`. A value that OBS Studio does not take is an `ArgumentOutOfRangeException` before anything is sent. OBS Studio 33 has no "monitor only": it holds
    `MonitorAndOutput` when it is asked for `MonitorOnly`, and the property shows what it holds.
  - **Asked, and not kept in the model** (each call takes its turn in the line of the changes): `input.GetPropertyItemsAsync(name)` (the choices of a dropdown of the properties window, as `ObsPropertyItem`s) and
    `input.PressPropertyButtonAsync(name)`; `GetDeinterlaceModeAsync()`, `SetDeinterlaceModeAsync(mode)`, `GetDeinterlaceFieldOrderAsync()` and `SetDeinterlaceFieldOrderAsync(order)` (`ObsDeinterlaceMode`,
    `ObsDeinterlaceFieldOrder`; for inputs that have their own video); on the model `GetInputKindsAsync(unversioned)`, `GetFilterKindsAsync()`, `GetDefaultInputSettingsAsync(kind)`,
    `GetDefaultFilterSettingsAsync(kind)` and `GetSpecialInputsAsync()` (the desktop audio and the microphones, as `ObsInput` objects: `ObsSpecialInputs`).
  - **The settings of a filter are asked, and not kept**: `filter.GetSettingsAsync()` and `filter.SetSettingsAsync(settings, overlay)`, and `CreateFilterAsync` takes settings. The protocol has an event for a change of
    them, and OBS Studio does not send it (OBS Studio 33.0.0 sent none for any kind of filter: libobs tells of the updates of public sources only, and a filter is a private one), so a copy in the model would go out of date without a sign.
- **`ObsModel` has media inputs: what a media is doing, and the calls that play, pause, stop, restart and move it** *(after rc.3; the third step of making `ObsModel` complete, which goes on until 4.0.0)*.
  - **`input.Media`** is an `ObsMedia` for an input that is a media input (a media source, a VLC video source), and `null` for any other input. Its `State` (`ObsMediaState`: `None`, `Playing`, `Opening`, `Buffering`,
    `Paused`, `Stopped`, `Ended`, `Error`) is current, because OBS Studio announces when a playback starts and ends and when it is told to play, pause, stop or restart; `PropertyChanged` and `Changed` say it. The length and
    the position are not kept (they move all the time, and OBS Studio sends no event for them): `GetStatusAsync()` asks for them (an `ObsMediaStatus`), and for the states that no event announces.
  - **The calls** (each takes its turn in the line of the changes): `PlayAsync()`, `PauseAsync()`, `StopAsync()` and `RestartAsync()` return when the model shows the new state; `TogglePlayPauseAsync()` returns the state
    that it asked for (OBS Studio has no toggle, so the model decides in the turn of the change); `NextAsync()` and `PreviousAsync()` (the playlist of a VLC source); `SeekAsync(position)` and `SkipAsync(offset)`.
  - **Checked with OBS Studio 33.0.0** (the article has the details): a media source whose scene is not on the program output is not opened (state `None`) and does not move; showing the scene plays it from the start,
    and when it is no longer shown it ends. A change of the position is carried out for a media that plays and is shown, a position beyond the end ends the media, and a media that neither plays nor is paused refuses
    a change of its position with `InvalidResourceState`.
  - The model subscribes to the media input events now (`EventSubscriptions.MediaInputs`), and reads the state of each media input once, when it reads the inputs: a connect sends one request more for each media input.
- **`ObsModel` has studio mode, the preview scene and the scene transitions** *(after rc.3; the fourth step of making `ObsModel` complete, which goes on until 4.0.0)*.
  - **Studio mode and the preview scene**: `SetStudioModeEnabledAsync(bool)` (`IsStudioModeEnabled` was there already), `obs.PreviewScene` (`null` while studio mode is off), `scene.IsPreview` and `scene.SwitchToPreviewAsync()`.
    OBS Studio makes the program scene the preview scene when studio mode is switched on and sends no event for it, so the model reads it then (and when it connects with studio mode on).
  - **`obs.Transitions`** (`ObsTransitions`) has `Current` (an `ObsTransition`: `Name`, `Uuid`, `Kind`, `IsFixed`, `IsConfigurable`), `Duration` and `IsRunning` as properties of the model (`PropertyChanged` and `Changed` say them), and the
    events `Started`, `Ended` and `VideoEnded`. Its calls: `SetCurrentAsync(name)` and `SetDurationAsync(duration)` (return when the model shows the change), `TriggerAsync()` (the Transition button; returns when the transition has started),
    `GetAllAsync()`, `GetKindsAsync()`, `GetSettingsAsync()`, `SetSettingsAsync(settings, overlay)`, `GetProgressAsync()` and `SetTBarPositionAsync(position, release)`. The list of transitions, their settings, the position and
    the overrides are asked and not kept, because OBS Studio does not announce a change of them. A scene can bring a transition of its own: `scene.GetTransitionOverrideAsync()` (an `ObsTransitionOverride`),
    `SetTransitionOverrideAsync(name, duration)` and `ClearTransitionOverrideAsync()`.
  - **The program scene is the new scene from the start of a transition.** OBS Studio answers a question about the program scene with the scene that it goes to at once, and sends the event that says so when the transition has
    ended (seconds later for a long one). The model reads the program scene when a transition starts, so `obs.ProgramScene` shows the new scene from the start, and `scene.SwitchToAsync()` returns then, while `Transitions.IsRunning`
    is still true; before, it returned when the event came or, for a transition longer than `ConfirmationTimeout`, after the model had read the program scene because the event did not come in time.
  - **Checked with OBS Studio 33.0.0** (the article has the details): the Transition button puts the preview scene on the air at once and swaps the two scenes when the transition has ended; a trigger while a transition runs does
    nothing, except in the first moment of a transition; the duration belongs to the transitions and is taken while a cut is current; a scene that has a transition of its own is shown with it and `Current` does not change.
    **Removing the program scene while studio mode is on makes OBS Studio stop answering for seconds**: switch to another scene first.
  - The model subscribes to the transition events now (`EventSubscriptions.Transitions`), and reads the current transition when it connects, and the preview scene while studio mode is on: a connect sends one or two requests more.
- **`ObsModel` has the transform of a scene item, its blend mode, copies of scene items, and a choice of what it follows of what OBS Studio sends very often** *(after rc.3; the fifth step of making `ObsModel` complete, which goes on until 4.0.0)*.
  - **`ObsModelOptions.HighVolume`** (`ObsHighVolume`, `None` by default) says what the model follows of what OBS Studio sends many times a second, which every client that listens pays for (OBS Studio does not include these events in
    `EventSubscriptions.All` either). `ObsHighVolume.Transforms` makes the model follow the transform of every scene item, and subscribe to the events for it.
  - **The transform of a scene item**: `item.Transform` (a `SceneItemTransform?`, `null` unless the model follows the transforms; it is a copy that does not change, `PropertyChanged` and `Changed` tell when it is another), `GetTransformAsync()` (asks OBS Studio, with and
    without `HighVolume`) and `SetTransformAsync(update)` (a `SceneItemTransformUpdate`, for example `item.Transform.ToUpdate() with { ScaleX = 2 }`: it returns when the model shows the change, or when OBS Studio has answered if the model does not follow the transforms).
    `SceneItemTransform` has value equality now (`Equals` and `GetHashCode`; the numbers are 32 bit numbers, as OBS Studio keeps them).
  - **The blend mode** (`GetBlendModeAsync()`, `SetBlendModeAsync(mode)`): asked and not kept, because OBS Studio announces no change of it.
  - **A copy of a scene item**: `DuplicateAsync(destination)` makes it on top of its scene or of another one, and returns it when the model shows it. It has the transform, the crop and whether the item is shown, and is not locked and has the normal blend mode.
    `obs.SceneItemSelected` (`ObsSceneItemEventArgs`) says that an item was selected in OBS Studio (also the one that OBS Studio selects when an item is made); the model does not keep which item is selected, because OBS Studio says nothing when it is not.
  - **Checked with OBS Studio 33.0.0 (obs-websocket 5.7.6)**: a change of the position, the scale, the rotation, the alignment and the bounds is announced at once, a crop and the size of a source **only for a scene that OBS Studio renders** (the program scene, the preview scene of
    studio mode, a scene that a projector shows; for another scene when it is rendered), so the transform that the model shows of an item in such a scene can be behind until then: `SetTransformAsync` and `DuplicateAsync` read the scene again and do not depend on it. A change
    with a crop is two events (the crop comes last). A blend mode that is set makes OBS Studio send the transform again, which is no change. The size of an item is the size of its source times the scale, whatever the bounds are.
  - **`DuplicateSceneItem` with the UUID of the destination alone makes the copy in the scene of the original** (obs-websocket reads the destination only when the request has its name, and then the UUID that is there wins). `DuplicateAsync` gives both. With the plain client give the
    destination by name; the article about requests says so.
  - The simulation has transforms, blend modes and copies, and the checks that obs-websocket 5.7.6 makes of a transform (read from its source and checked against OBS Studio 33.0.0); the model is compared with it, with and without following the transforms, and against a real OBS Studio.
- **`ObsModel` follows whether the inputs are on the air and shown** *(after rc.3; step 5b of making `ObsModel` complete, which goes on until 4.0.0)*.
  - **`ObsHighVolume.Activity`** (`ObsHighVolume.All` has it too) makes the model read `GetSourceActive` for every input and subscribe to `InputActiveStateChanged` and `InputShowStateChanged`, which OBS Studio sends for every input that a change of the program scene,
    a transition, the preview scene or a scene item reaches (`ObsModelOptions.HighVolume`; the default is still none, so a model that does not ask sends no request for it and does not subscribe).
  - **`input.IsActive`** (on the air: shown in the program output) and **`input.IsShowing`** (shown: on the air, or in the preview of studio mode, a projector or the window of its properties) are `bool?`: `null` unless the model follows the activity. `PropertyChanged` and `Changed` tell each by itself.
    The activity of a scene is asked and not kept (`scene.GetActivityAsync()`, on every source), because OBS Studio announces none for a scene.
  - **Checked with OBS Studio 33.0.0 (obs-websocket 5.7.6)**: a scene that goes on the air puts its inputs on the air when the transition starts, and the scene that goes off takes them off after it has ended (a fade keeps both on the air while it runs, and `SwitchToAsync()` returns before that, as it does for the program scene);
    a scene that is nested puts its inputs on the air as long as the scene items that lead to it are enabled; the preview scene of studio mode shows its inputs and does not put them on the air; **in studio mode a copy of the program scene is on the air**, so hiding a scene item of the program scene, or making an input in it,
    changes nothing of what is on the air until the next scene goes on the air or studio mode is switched off; the two events come in no fixed order, and none comes for an input that OBS Studio destroys.
  - The simulation has the activity (with the copy of studio mode), and the model is compared with it, with and without following it, and against a real OBS Studio, where the model has to equal what `GetSourceActive` says of every input after every step.
    The simulation also announces the preview scene first when studio mode is switched off, as OBS Studio does (it makes the preview scene the program scene for a moment, and then the scene that was), and does not make a scene that was removed during a transition the preview scene.
- **`ObsModel` follows the levels of the audio** *(after rc.3; step 5c of making `ObsModel` complete, which goes on until 4.0.0)*.
  - **`ObsHighVolume.AudioLevels`** (and `ObsHighVolume.All`, which is `Transforms | Activity | AudioLevels`) makes the model subscribe to `InputVolumeMeters`, which OBS Studio sends every 50 milliseconds to every client that asks, also when nothing plays, and for which it has every input that is on the
    air measure its audio for as long as someone is subscribed (`ObsModelOptions.HighVolume`; the default is still none, so a model that does not ask does not subscribe).
  - **`input.Levels`** (an `ObsAudioLevels?`: the `Input` and its `Channels`, each an `ObsChannelLevel` with `Magnitude`, `Peak` and `InputPeak` as multipliers of the full scale, and `MagnitudeDb`, `PeakDb` and `InputPeakDb` with -100 for silence) is what OBS Studio said of the input in its last message: `null` unless the model follows the levels and
    OBS Studio lists the input. `PropertyChanged` tells when the levels change, when OBS Studio stops listing the input, and when the connection ends. **`obs.AudioLevels`** (`ObsAudioLevelsEventArgs`: `Levels` of every listed input, and `Ended` with the inputs that stopped being listed) says that new levels came.
  - **A stream and not a history.** The levels do not go through the engine of the model: they are in the inputs when the message arrives. The application is not told of every message: at most one notification waits in the queue of the notifications, and a handler that is slower than OBS Studio gets the newest
    levels (`Ended` has every input that stopped being listed meanwhile). The levels never fill the queue (`MaxPendingNotifications`), never hold up what the model reads, and `Changed` does not tell them. A message that says what the previous one said is not told. The plain client passes on one `InputVolumeMeters` event every 100 ms,
    the model uses every message.
  - **Checked with OBS Studio 33.0.0 (obs-websocket 5.7.6)**: a message every 50 milliseconds, with an empty list when nothing is on the air; it lists the inputs that are on the air and have audio, in the order in which they came on the air, so not an input that is only shown in the preview of studio mode, one whose scene item is hidden, or one without audio, and the
    inputs of the scene that goes off the air until the transition has ended; a muted input is listed, with a magnitude and a peak of 0 and the peak of the input as it is, and the volume scales the magnitude and the peak and not the peak of the input; an input that plays nothing is listed with zeros; the first message that lists an input has no channels; a mono file arrives as two channels
    with the same levels, a stereo file with the left channel first; **the first message that has audio of an input can say a peak that is far too high** (1.9 was measured often, and 2.7e20 once), which `ObsChannelLevel.Peak` says.
  - The simulation has the levels (a tick sends a message, and the random walks make some), and the model is compared with it over the whole stack, for the inputs that are on the air and those that go off it, a lost connection and a switch of the scene collection, and against a real OBS Studio with a tone of a known loudness.
- **`ObsModel` has the profiles and the scene collections** *(after rc.3; step 6a of making `ObsModel` complete, which goes on until 4.0.0)*.
  - **`obs.Profiles`** (`ObsProfiles`) and **`obs.SceneCollections`** (`ObsSceneCollections`) are objects that stay the same for as long as the model lives, with `Current` (the name of the profile that is current, of the scene collection that is loaded; `null` until OBS Studio has said) and
    `Names` (in the order of OBS Studio), both told by `PropertyChanged` and `Changed` (`Names` first, so that a new current one is among the names), also for what the user does in OBS Studio. The model reads both lists when it connects and again when OBS Studio
    says that one changed; it never takes a name from the payload of an event, because OBS Studio does not send `ProfileListChanged` when the user removes the current profile (it sends `CurrentProfileChanging` and `CurrentProfileChanged`).
  - **`SetCurrentAsync(name)`** and **`CreateAsync(name)`** (both), **`RemoveAsync(name)`** (profiles) return when the model shows the change; for a scene collection when the model has read the one that OBS Studio loaded (`State` is `Synchronized`: what you held of the collection that was left is `Gone`).
    A name that OBS Studio does not have throws an `ObsNotFoundException` (`Kind` is `profile` or `scene collection`, with names that are close), a name that is taken an `ObsNameInUseException` (which now says it for profiles and scene collections too). **`RemoveAsync` of the current profile throws an `InvalidOperationException` before it sends anything**:
    OBS Studio answers that it removed the current profile and opens a question in its own window instead (read in its source, and seen: the profile stays).
  - **A change that makes OBS Studio load a scene collection waits for the answer as long as `SynchronizationTimeout`** when that is more than `WriteTimeout` (OBS Studio answers when it has loaded the collection), and the wait for the model to show it is as long as `SynchronizationTimeout` more; it ends when the model is `Synchronized`, not when the first read of the list of collections is done.
  - **The model now says its first synchronization to the long-lived objects too**: `Transitions`, `Profiles` and `SceneCollections` raise `PropertyChanged` for what the first read set (the outputs and the model itself did; the transitions did not, though the application can hold `obs.Transitions` from the start).
  - **Checked with OBS Studio 33.0.0 (obs-websocket 5.7.6)**: a switch of the profile is answered when it is done, and `CurrentProfileChanging` (with the profile that is left) and `CurrentProfileChanged` come together; a new profile is answered at once, listed about 40 ms later and current about 90 ms after that, with no `CurrentProfileChanging`, and it is a clean one;
    the same name is refused with 601 and an unknown one with 600; a scene collection that is made is loaded at once, with one scene named `Scene`, and `CurrentSceneCollectionChanging` and `CurrentSceneCollectionChanged` come together after the answer; `SceneCollectionListChanged` was not sent for it.
  - The simulation has profiles (with the events that OBS Studio sends, the user interface that makes a profile a moment after the answer, in order and not while a scene collection loads, and what the user does when he answers the question for the current profile), the random walks of the engine and of the whole stack compare the model with the lists, and a real OBS Studio shows the same in the live tests.
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
- `net10.0` as an additional target framework, next to `net8.0` (4.0.0-beta1 to 4.0.0-rc.1; from 4.0.0-rc.2 on it is the only one, see *Breaking changes*).
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
  scene (reported to obs-websocket as [obs-websocket#1364](https://github.com/obsproject/obs-websocket/issues/1364)), a scene or an input
  that was removed can still be in the list of OBS Studio when its event arrives, and the interface thread of
  OBS Studio can be away for seconds, so that a request times out and is carried out later all the same.
  The one difference that the comparison of values found is on purpose, and is documented now: OBS Studio sends `null` as the
  `transitionSettings` of a transition that cannot be configured, and `TransitionResponse.TransitionSettings` is an empty dictionary then.
- **A package icon, better tags and a sample** *(after beta1)*: the package has an icon (the logo of the site) and the tags `obs`, `obs-studio`,
  `obs-websocket`, `websocket`, `streaming`, `dotnet` and `client`, so that it is easier to find and to recognise on nuget.org (CI checks that the
  icon is a whole PNG, with its chunks in order, a valid header, palette and transparency and image data that is as long as its header says,
  of at least 128 by 128 pixels). `samples/ConsoleSample`
  is a small program that connects, says which OBS Studio it reached, lists the scenes and prints the events (`dotnet run --project samples/ConsoleSample`); CI builds it
  against the package that was just packed, which shows that the package can be used. The target frameworks were `net8.0` and `net10.0`, the LTS releases, up to
  4.0.0-rc.1; from 4.0.0-rc.2 on `net10.0` is the only one (see *Breaking changes*).
- **The README of the package** *(after beta2)* has absolute links and no raw HTML now: nuget.org drops a relative link (the page of 4.0.0-beta2
  shows five of them without a target) and shows HTML as text (it showed `<ins>` around two phrases), and a published version cannot be
  changed. CI parses the README with Markdig, the parser of nuget.org (`ReadmeTests`), and fails on either, and on an image from a domain that
  nuget.org does not render images from (it shows none), so that the next version cannot be published with them. The README has the badge of
  the OpenSSF Scorecard now, drawn by shields.io, which nuget.org renders.
- **The license of the package is an SPDX expression** *(after beta2)*: `MIT`, where the package pointed at its `LICENSE` file. nuget.org and the
  tools that scan the licenses of the packages of an application read the expression and show the license by its name; the `LICENSE` file is
  in the package as well, because the license asks for its notice to be included with copies. The package also says whose copyright it is,
  and its release notes are the GitHub release of its version. CI checks the expression and the file (`tools/verify-package.py`).
- **A Code of Conduct** *(after beta2)*: [`CODE_OF_CONDUCT.md`](https://github.com/tinodo/obsclient/blob/main/CODE_OF_CONDUCT.md) is the Contributor Covenant 2.1, word for word, with a private
  report on GitHub as the way to report behavior that breaks it.
- **Tests of what goes wrong** *(after beta2)*: the client is tested against a server that misbehaves and a connection that fails, not
  only against one that behaves. Tests with the fake server cover the counters of the traffic, a Hello or an Identified that cannot be
  read, a handshake that is never completed, a request that OBS Studio is not ready for (asked again as often as `MaxRequestRetries`
  says, and no more), a client that was never connected or that was disposed, and 1100 events held up by a slow handler (none is lost,
  and they stay in order). A **soak test** runs against a real OBS Studio every week, and on request for as long as you ask: a proxy
  cuts, stalls and refuses the connection again and again while the client asks OBS Studio things all the time, and the client has to
  reconnect every time, get through the calls that it can, and not leak memory, threads or file descriptors (see CONTRIBUTING, *Live
  tests*). Nothing of this is in the package.
- **Property-based tests and fuzz tests** *(after beta2)*: besides the examples, the client is tested with input that nobody wrote an
  example for. [FsCheck](https://fscheck.github.io/FsCheck/) makes it (in the tests only: nothing of this is in the package): properties
  of the converters, of the conversion of levels to decibels and of the references to scenes and inputs, and three fuzz tests that send a
  connected client frames of every shape (members of the wrong type, members that are missing, JSON of another shape, text that is not
  JSON, a message that is cut off, the response to a request and the data of an event with the fields of the protocol wrong or missing),
  and check after each that the client goes on and answers the next request. They run from a fixed seed (300 cases each, a second or
  two); `OBS_FUZZ_CASES` and `OBS_FUZZ_SEED` look further (see CONTRIBUTING). Their first runs found the defects listed under *Fixed*.
- **OpenSSF Scorecard** *(after beta2)*: a workflow runs the [OpenSSF Scorecard](https://scorecard.dev/viewer/?uri=github.com/tinodo/obsclient) of the
  repository on every push to `main` and every week, puts what it finds in the code scanning alerts, and publishes the results to scorecard.dev (see
  CONTRIBUTING). The CodeQL and protocol watch workflows ask for their write permission (`security-events`, `issues`) on their job now, and not on
  the workflow as a whole, so that every workflow only reads at the top.
- **Dependency review** *(after beta2)*: a workflow reviews the dependencies that a pull request changes (the NuGet packages of the projects and
  the actions of the workflows, whatever their scope) and fails when the change brings in a package with a known vulnerability of moderate
  severity or worse; it is advisory, not a required check. It does not see the DocFX tool of the documentation, which the dependency graph of
  GitHub does not list, and it does not find a vulnerability in an action, because GitHub's alerts for actions only cover semantic versions and
  not the commit SHAs that the actions here are pinned to (see CONTRIBUTING, *Dependency updates*).
- **Build provenance** *(after beta2)*: the package, its symbols, the test app and the libraries inside the package have a signed
  [build provenance attestation](https://docs.github.com/en/actions/concepts/security/artifact-attestations) from now on: GitHub's statement
  that the `publish.yml` workflow of this repository built them, from the commit of the release, on a GitHub-hosted runner. The workflow
  makes the attestations and checks them before the release waits for approval, and `gh attestation verify` checks them for anyone (the
  notes of every release say how, and `SECURITY.md` has the command). nuget.org signs the package that it serves, so the `.nupkg` from
  nuget.org does not verify, but the libraries in it do (the DLL in your NuGet cache), and so does the `.nupkg` of the release page.
  The attestation is also a file of every release, `OBSClient.<version>.intoto.jsonl` (a Sigstore bundle for all the files):
  `gh attestation verify <file> --bundle ...` checks a file against it without asking GitHub for the attestation.
- **Live tests on an older OBS Studio** *(after beta2)*: the live workflow runs against two OBS Studios, the newest and the one of
  Ubuntu 24.04 (OBS Studio 30.0.2, with obs-websocket 5.3.4), so that "OBS Studio 28 and up" is not only said of the newest. Everything
  that does not need UUIDs passes against 30.0.2 (including the comparison of the answers to every request that needs no parameters,
  and a connection that is cut again and again); the lifecycle test of a scene and an input follows them by UUID, which obs-websocket has
  from 5.4.0 (OBS Studio 30.1.0), and is skipped there. 28 and 29 are not tested: Ubuntu 24.04 has no packages of them. See *Compatibility*.

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
  - `Disconnect()` did nothing to a connection that was still being opened: the attempt went on until its own 2 second limit, so
    after a `Disconnect()` the client stayed `Connecting` for almost 2 seconds, for example while OBS Studio accepted the connection and
    never completed the handshake (measured with a server that does that: 1.8 to 1.9 seconds with 3.0.1). It ends the attempt at once
    now: `ConnectionState` is `Disconnected` a moment later (0.4 to 0.8 milliseconds in the same measurement), the connection that was
    being opened is closed, `ConnectAsync` returns `false`, and the client does not connect again, also with `AutoReconnect`. Code
    that relied on the wait (a test that keeps a stop open by holding a handshake, for one) has to bring its own.
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
- **`InputVolumeMeterResponse.GetLevelDb` threw an `IndexOutOfRangeException` for a negative channel** (this was so in 3.0.1 as well),
  while any other channel that is not there, such as an index that is too high, or every channel of an input for which OBS Studio sent an
  empty list of levels (it does for some), gave `float.NegativeInfinity`. A negative index gives it too now, and the documentation says
  what the method returns (a silent channel gives -100 dB, the floor). Found by the report of what the tests do not reach, which showed
  that no test had called it.
- **`CreateScene` threw "OBS Studio returned an empty response" for a scene that OBS Studio had made**, on an OBS Studio before 30.1.0
  (the 4.0 betas had this too): the whole of its response is the UUID of the scene, obs-websocket before 5.4.0 has no UUIDs, and so
  it answers without data. The request did not fail, so the call does not: it returns `Guid.Empty`, and the name that was asked for
  identifies the scene. Any other response without data is still an error. Found by running the live tests against OBS Studio 30.0.2.
- **A message from OBS Studio that is not as it should be could end the connection**, where the client promises to skip it: a message
  whose `op` is not a number (a string, a boolean, `null`, an array, an object), a message without an `op` (it was read as op code 0, which
  is a Hello), a message of which the data is `null`, and a response or a batch response that names no request (the `requestId` is missing
  or `null`, which reached a dictionary as a null key). OBS Studio does not send such messages, but a plugin or a proxy between it and the
  client might, and a client that is to run next to a stream should not be taken down by one. Each is logged and skipped now. Found by the
  fuzz tests, which are new (see *Added*).
- `InputVolumeMeterResponse.GetLevelDb` threw a `NullReferenceException` for a channel of which the list of levels is `null` (in a message
  that is not as it should be), and `EventTypeConverter` threw for an event type that is not valid text (half of a surrogate pair,
  escaped), where it promises `EventType.Unknown`. Both do what they promise now. Found by the property tests.

### Compatibility
- **OBS Studio before 30.1.0 (obs-websocket before 5.4.0) has no UUIDs** for scenes and inputs. What it reports has none: `Scene.SceneUuid`
  is empty (`Guid.Empty`), and `Input.InputUuid` and `SceneItem.SourceUuid` are `null`; `CreateScene` returns `Guid.Empty`; and a `Scene`,
  an `Input` or a `SceneItem` that it gave you converts to a reference by name. Give such an OBS Studio names (it refuses a scene or an
  input that is given by its UUID alone). Everything else is tested against OBS Studio 30.0.2 (obs-websocket 5.3.4) and against the
  newest release (32.2.0 in CI, 33.0.0 on a workstation); 28 and 29 are not tested.
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
- **Making or renaming something with the name of an input that was just removed no longer fails for a moment** *(after rc.3)*. OBS Studio destroys a source that it removed a moment after the model
  can tell (when the last scene item that kept it alive is gone, the last reference to it is released a frame later, and OBS Studio does not announce it), and keeps its name taken until then: measured with
  OBS Studio 33.0.0, in about 1 round of 10, for up to 25 ms or so. A `CreateSceneAsync`, `CreateInputAsync` or `RenameAsync` right after the `RemoveAsync` of the object that had the name was refused in that moment with an
  `ObsNameInUseException` that says that the model does not show who holds the name (a live test that does exactly that failed in about 1 run of 10 because of it). A refusal for a name that nothing in the model holds is now sent
  again every 25 ms, in the same turn of the line of changes, for as long as `ObsModelOptions.ConfirmationTimeout` (1 s by default); a name that the model shows a holder for is refused at once, as before.
  A change that was refused made nothing, so sending it again is safe.
