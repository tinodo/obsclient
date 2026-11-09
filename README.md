# OBSClient
[![NuGet](https://img.shields.io/nuget/vpre/OBSClient.svg?logo=nuget)](https://www.nuget.org/packages/OBSClient)
[![Downloads](https://img.shields.io/nuget/dt/OBSClient.svg)](https://www.nuget.org/packages/OBSClient)
[![Build and Test](https://github.com/tinodo/obsclient/actions/workflows/build.yml/badge.svg)](https://github.com/tinodo/obsclient/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/github/license/tinodo/obsclient.svg)](https://github.com/tinodo/obsclient/blob/main/LICENSE)
[![OpenSSF Scorecard](https://img.shields.io/ossf-scorecard/github.com/tinodo/obsclient?label=openssf%20scorecard)](https://scorecard.dev/viewer/?uri=github.com/tinodo/obsclient)

A complete .NET client for OBS Studio, with **two ways to work with it**:

- **`ObsClient` speaks the protocol.** Every request and every event of obs-websocket, as typed, async C#. Complete, direct and nothing hidden: the API of OBS Studio, as it is.
- **`ObsModel` speaks OBS Studio.** A live model of your scenes, inputs and filters, which you work with *by name*. Intuitive, and close to what you see in OBS Studio itself. New, and built on `ObsClient`.

One package, one connection: use either, or both together. We set out to make a client that is **complete** and **easy to use**. `ObsClient` is the first, and `ObsModel` is the second.

Works with OBS Studio 28 and up (`ObsModel` needs 30.1), on Windows, Linux and macOS. Implements [**obs-websocket 5.7.6**](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md) (including multi-canvas support added in 5.7.0).  
Targets `net10.0`, the current LTS release of .NET (see [Compatibility](https://tinodo.github.io/obsclient/#compatibility)).  
**Documentation, guides and the API reference: [tinodo.github.io/obsclient](https://tinodo.github.io/obsclient/)**

## Two ways to work with OBS Studio

OBS Studio is controlled through obs-websocket, a protocol of **requests** and **events**. A protocol client hands you those, and the bookkeeping is yours: asking for the state, looking up ids, and following the events to keep your own picture of OBS Studio up to date. That is `ObsClient`, and it is complete. `ObsModel` is the next level: it keeps the picture for you, so that you work with OBS Studio the way you see it in its window.

| | `ObsClient`: the protocol | `ObsModel`: OBS Studio by name |
| --- | --- | --- |
| **It is** | the obs-websocket protocol, as typed, async C# | a live model of OBS Studio in memory, built on `ObsClient` |
| **You think in** | requests, responses and events | scenes, inputs, scene items, filters and outputs |
| **You find things by** | a name or a UUID, and ids that you look up first | their name: `obs.Scenes["Gaming Scene"]` |
| **You read state** | with a request, each time | from a property, answered from memory, with no request |
| **A change** | is one request; the call returns when OBS Studio has answered | is a method on the object; the call returns when the model shows the change |
| **You hear about changes** | with one event for each kind of change, and you apply them | with `Changed` and `INotifyPropertyChanged`, after the model has applied them, and `Resynchronized` when it read everything again |
| **It covers** | the whole protocol | scenes, inputs, scene items, filters and outputs; the rest through `obs.Client` |
| **It needs** | OBS Studio 28 or newer | OBS Studio 30.1 or newer |
| **In 4.0** | the API is closed | new in 4.0.0-rc.2, provisional |

The same job at both levels: hide the webcam in the Gaming scene.

```csharp
// ObsClient: a scene item is a number, so you look it up first.
int webcam = await client.GetSceneItemId("Gaming Scene", "Webcam");
await client.SetSceneItemEnabled("Gaming Scene", webcam, false);
```

```csharp
// ObsModel: by name.
await obs.Scenes["Gaming Scene"].Items["Webcam"].SetEnabledAsync(false);
```

**Which one?** Use `ObsModel` to build something that shows or controls OBS Studio over time, such as a panel, a bot, an overlay or a user interface, when OBS Studio is 30.1 or newer. Use `ObsClient` for everything that the model does not cover yet, for request batches, for OBS Studio 28 to 30.0, and for a quick script that sends a few commands. Use both when you are in between: `obs.Client` is the `ObsClient`, on the same connection. [Two ways to work with OBS Studio](https://tinodo.github.io/obsclient/articles/two-ways.html) tells the whole story, with the same jobs done both ways.

## Installation
Install from the [NuGet Gallery](https://www.nuget.org/packages/OBSClient)   
Or through the NuGet CLI: `NuGet\Install-Package OBSClient -Version 4.0.0-rc.3`  
From the command line: `dotnet add package OBSClient --version 4.0.0-rc.3`  

**4.0.0-rc.3 is a prerelease** (a release candidate): it is meant to be tried. The public API of the client is closed, so what changes before 4.0.0 is fixes for what is found. `ObsModel` (OBS Studio by name) is new and provisional: its names and details can still change.
It targets `net10.0` only; an application on .NET 8 or 9 stays on 4.0.0-rc.1, which also has the `net8.0` library. A plain
`dotnet add package OBSClient` keeps installing the latest stable release, so name the version (or add `--prerelease`) to get it.
Please report what you find in an [issue](https://github.com/tinodo/obsclient/issues).

Upgrading from 3.x? See [Moving from 3.x to 4.0](https://tinodo.github.io/obsclient/articles/migrating-to-4.html), and the *Breaking changes* in the [CHANGELOG](https://github.com/tinodo/obsclient/blob/main/CHANGELOG.md).

## `ObsClient`: the protocol, as it is

Every request of obs-websocket is a method of `ObsClient` with the name of the request, every event is an event with the name of the event, and every response is typed. What you send is what the protocol defines, and what you get is what OBS Studio answers. [Calling requests](https://tinodo.github.io/obsclient/articles/requests.html), [Handling events](https://tinodo.github.io/obsclient/articles/events.html) and [Request batches](https://tinodo.github.io/obsclient/articles/batches.html) tell the rest.

### Simple

```csharp
ObsClient client = new();
bool isConnected = await client.ConnectAsync();
if (isConnected)
{
    await client.SetCurrentProgramScene("Gaming Scene");
    Monitor[] monitors = await client.GetMonitorList();
    client.Disconnect();
}
client.Dispose();
```

### Request Batch

```csharp
ObsClient client = new();
bool isConnected = await client.ConnectAsync(true, "P@ssw0rd", "localhost", 4455, EventSubscriptions.Filters | EventSubscriptions.Scenes);
if (isConnected)
{
    RequestBatchMessage batchRequest = new();
    batchRequest.AddToggleVirtualCamRequest();
    batchRequest.AddSetStudioModeEnabledRequest(true);
    batchRequest.AddSleepRequest(5000, null);
    batchRequest.AddToggleVirtualCamRequest();
    batchRequest.AddSetStudioModeEnabledRequest(false);
    var result = await client.SendRequestBatchAsync(batchRequest, 6000);
    client.Disconnect();
}
client.Dispose();
```

### Names and UUIDs

Whatever OBS Studio can look up by name or by UUID — a scene, an input, a source — is one argument of a type that holds
either: a `SceneRef`, an `InputRef` or a `SourceRef`. A name, a `Guid`, and the `Scene`, `Input` or `SceneItem` that OBS Studio
gave you convert implicitly, so there is nothing to wrap:

```csharp
await client.SetCurrentProgramScene("Gaming Scene");        // by name
await client.SetCurrentProgramScene(sceneUuid);             // by UUID, which survives a rename
await client.SetCurrentProgramScene(scene);                 // a Scene from GetSceneList (identified by its UUID)
bool muted = await client.GetInputMute("Microphone");
await client.SetSceneItemEnabled(SceneRef.FromName("Gaming Scene"), item.SceneItemId, true);
```

Optional arguments are optional (`await client.GetSceneItemList("Scene", canvasUuid: canvas.CanvasUuid)`), and an argument that
is missing or wrong is refused with an `ArgumentException` before anything is sent.

### Canvases (obs-websocket 5.7.0+)

```csharp
ObsClient client = new();
bool isConnected = await client.ConnectAsync();
if (isConnected)
{
    Canvas[] canvases = await client.GetCanvasList();
    foreach (var canvas in canvases)
    {
        SceneListResponse scenes = await client.GetSceneList(canvas.CanvasUuid);
    }
    client.Disconnect();
}
client.Dispose();
```

## `ObsModel`: OBS Studio, as you see it

*New in 4.0.0-rc.2, and provisional: its names and details can still change before 4.0.0.*

`ObsModel` reads your scenes, inputs, scene items, filters and outputs (stream, recording, virtual camera, replay buffer) into a model in memory, follows what happens in OBS Studio, also what the user does in its window, and lets you work by name. It is built on the `ObsClient` above, which stays available as `obs.Client`. It needs OBS Studio 30.1 or newer.

```csharp
await using var obs = new ObsModel();
await obs.ConnectAsync("localhost", 4455, "password");   // returns when the model is complete

await obs.Scenes["Gaming Scene"].SwitchToAsync();
await obs.Inputs["Microphone"].SetMutedAsync(true);
await obs.Scenes["Gaming Scene"].Items["Webcam"].SetEnabledAsync(false);
await obs.Inputs["Microphone"].Filters["Noise Gate"].SetEnabledAsync(true);
await obs.Outputs.Record.StartAsync();                   // returns when OBS Studio has begun to record

bool visible = obs.Scenes["Gaming Scene"].Items["Webcam"].IsEnabled;   // read from memory: no request is sent
obs.Changed += (_, e) => Console.WriteLine(e);           // what changes, also what the user does in OBS Studio
```

What it gives you:

- **Names, not plumbing.** `obs.Scenes["Gaming Scene"].Items["Webcam"]`: no UUIDs, no scene item ids, no indexes to look up first.
- **Reads without a round trip.** A property is answered from memory, from any thread, and sends no request.
- **Changes you can rely on.** A change is a method on the object. Changes are made one at a time, in the order in which you ask for them, and each call returns when the model shows the change (an output returns when OBS Studio has begun to start or stop). When something goes wrong the exception says whether the change was made. A change is never made twice.
- **Changes you can follow.** `Changed` says what changed, in order, and every object implements `INotifyPropertyChanged`, so a user interface can bind to it. When the model had to read everything again (a lost connection came back, OBS Studio switched its scene collection, or your handlers were too slow), `Resynchronized` says so: read again what you hold.
- **Made for the real OBS Studio.** It reconnects and reads again, follows a switch of the scene collection, and knows that OBS Studio keeps a removed scene or input alive for as long as a scene item shows it. It is checked against a simulation of OBS Studio that reproduces what the real one does that surprises, and in CI against a real one.
- **Nothing hidden.** `obs.Client` is the `ObsClient`.

**Not in the model yet:** studio mode and the preview scene (the model reads whether studio mode is on, and does not control it), transitions, profiles, scene collections, media inputs, and the settings of inputs and filters. Use `obs.Client` for those, in the same program, on the same connection; the model follows what they do:

```csharp
await obs.Client.SetCurrentSceneTransition("Fade");      // not in the model yet: the protocol
await obs.Scenes["Gaming Scene"].SwitchToAsync();        // the model
```

[Controlling OBS Studio by name](https://tinodo.github.io/obsclient/articles/model.html) has all of it, and `samples/ModelSample` is a small command prompt to try it against your own OBS Studio.

## Good to know about `ObsClient`

- **Events are raised by a dispatcher of the client**, one at a time and in the order in which they happened, on a thread that is not
  the one that reads from OBS Studio: a handler can wait for a response from OBS Studio, and a slow handler only delays the events
  behind it. `PropertyChanged` and the statistics events are raised on the thread that causes the change: keep those handlers quick.
- **Requests time out after 500 ms by default.** Change `ObsClient.RequestTimeout` if OBS Studio is busy (batches with long
  `Sleep` requests need a longer timeout; see `SendRequestBatchAsync`).
- **Every call can be cancelled.** Every request, `ConnectAsync`, `SendRequestBatchAsync` and `ReidentifyAsync` take a
  `CancellationToken`: cancelling it ends the wait with an `OperationCanceledException` and leaves the connection usable. A request
  that was already sent is not undone.
- **A lost connection is reported, not hidden.** `ConnectionState` and `ConnectionClosed` tell you when it ends, requests
  that were waiting fail at once with an `ObsClientException`, and `AutoReconnect` brings the connection back.
- **See what goes over the wire.** The client logs every message it sends and receives at the debug level (`Sending: …`,
  `Received: …`) to the `ILogger<ObsClient>` you give it. The answer to the authentication challenge is left out of what is sent.

## Try it: the test app

[`OBSClient.TestApp`](https://github.com/tinodo/obsclient/tree/main/OBSClient.TestApp) is a Windows (WinUI 3) application to try every request and event of this library by hand
against your own OBS Studio, and to compare what OBS Studio sends with what the client returns. Every
[release](https://github.com/tinodo/obsclient/releases) has it as a download (`OBSClient.TestApp-<version>-win-x64.zip`: unzip it, start
`OBSClient.TestApp.exe`), and every CI run publishes it as an artifact; see [CONTRIBUTING.md](https://github.com/tinodo/obsclient/blob/main/CONTRIBUTING.md#the-test-app).

## Staying in sync with obs-websocket

The requests, the responses, the events and the request and event enums of the client are **generated** from the
machine-readable protocol definition of obs-websocket
([`protocol.json`](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.json)) and the
few decisions that it cannot make for the client (`tools/protocol/overlay.toml`). The generated C# in `OBSClient/Generated`
is committed and compiled into the library like any other source; the definition, the overlay and the generator are
inputs of the build under `tools/` and are not part of the package, which is a single assembly.

A scheduled workflow opens an issue as soon as obs-websocket publishes a new version, with the differences and the outcome of
trying the update; `python tools/protocol/update.py --latest` applies it. The tests in `OBSClient.Tests/Conformance` call every
request method and batch builder and compare what is sent, field by field, with a snapshot of the definition, and verify the
events, responses and enum values too.

[![Build and Test](https://github.com/tinodo/obsclient/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/tinodo/obsclient/actions/workflows/build.yml)
[![CodeQL](https://github.com/tinodo/obsclient/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/tinodo/obsclient/actions/workflows/codeql.yml)
[![Docs](https://github.com/tinodo/obsclient/actions/workflows/docs.yml/badge.svg?branch=main)](https://tinodo.github.io/obsclient/)
[![NuGet](https://img.shields.io/nuget/v/obsclient.svg?style=flat)](https://www.nuget.org/packages/obsclient)  

See [CONTRIBUTING.md](https://github.com/tinodo/obsclient/blob/main/CONTRIBUTING.md) for how to build, test, and contribute, and [CHANGELOG.md](https://github.com/tinodo/obsclient/blob/main/CHANGELOG.md) for release history.
