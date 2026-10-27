# ObsClient
A Complete cross platform .NET WebSocket Client for OBS Studio version 28 and up.  
Currently implementing: [**obs-websocket 5.7.6 Protocol**](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md) (including multi-canvas support added in 5.7.0)  
Targets `net8.0` and `net10.0`.  
Our intent is to create a <ins>complete</ins> and <ins>easy to use</ins> client for OBS Studio.

## Installation
Install from the [NuGet Gallery](https://www.nuget.org/packages/OBSClient)   
Or through the NuGet CLI: `NuGet\Install-Package OBSClient -Version 4.0.0`  
From the command line: `dotnet add package OBSClient --version 4.0.0`  

Upgrading from 3.x? See the *Breaking changes* in the [CHANGELOG](CHANGELOG.md).

## Sample usage

### Simple

```
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

```
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

```
await client.SetCurrentProgramScene("Gaming Scene");        // by name
await client.SetCurrentProgramScene(sceneUuid);             // by UUID, which survives a rename
await client.SetCurrentProgramScene(scene);                 // a Scene from GetSceneList (identified by its UUID)
bool muted = await client.GetInputMute("Microphone");
await client.SetSceneItemEnabled(SceneRef.FromName("Gaming Scene"), item.SceneItemId, true);
```

Optional arguments are optional (`await client.GetSceneItemList("Scene", canvasUuid: canvas.CanvasUuid)`), and an argument that
is missing or wrong is refused with an `ArgumentException` before anything is sent.

### Canvases (obs-websocket 5.7.0+)

```
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

## Good to know

- **Events run on the receive thread.** A handler must not block waiting for a response from OBS Studio (for example
  `client.GetSceneList().Result`): the client could not receive that response while the handler is waiting for it.
- **Requests time out after 500 ms by default.** Change `ObsClient.RequestTimeout` if OBS Studio is busy (batches with long
  `Sleep` requests need a longer timeout; see `SendRequestBatchAsync`).
- **A lost connection is reported, not hidden.** `ConnectionState` and `ConnectionClosed` tell you when it ends, requests
  that were waiting fail at once with an `ObsClientException`, and `AutoReconnect` brings the connection back.
- **See what goes over the wire.** The client logs every message it sends and receives at the debug level (`Sending: …`,
  `Received: …`) to the `ILogger<ObsClient>` you give it. The answer to the authentication challenge is left out of what is sent.

## Try it: the test app

[`OBSClient.TestApp`](OBSClient.TestApp) is a Windows (WinUI 3) application to try every request and event of this library by hand
against your own OBS Studio, and to compare what OBS Studio sends with what the client returns. Every CI run publishes it as an
artifact (`OBSClient.TestApp-win-x64`: unzip it, start `OBSClient.TestApp.exe`); see [CONTRIBUTING.md](CONTRIBUTING.md#the-test-app).

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
[![NuGet](https://img.shields.io/nuget/v/obsclient.svg?style=flat)](https://www.nuget.org/packages/obsclient)  

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to build, test, and contribute, and [CHANGELOG.md](CHANGELOG.md) for release history.
