---
title: Getting started
---

# Getting started

This page takes you from nothing to a small program that talks to your OBS Studio.

> [!NOTE]
> This site documents OBSClient **4.0**: the client, and [`ObsModel`](model.md), OBS Studio by name. Using 3.x? See [Moving from 3.x to 4.0](migrating-to-4.md).

## What you need

- **OBS Studio 28 or later.** The WebSocket server (obs-websocket 5) is built in. The UUIDs of scenes and inputs came later, with OBS
  Studio 30.1 (obs-websocket 5.4): an older OBS Studio sends none, so `Scene.SceneUuid` is empty (`Guid.Empty`), `Input.InputUuid` and
  `SceneItem.SourceUuid` are `null`, `CreateScene` returns `Guid.Empty`, and a `Scene`, an `Input` or a `SceneItem` that it gave you
  still converts to a reference, by name (see [Calling requests](requests.md#scenes-inputs-and-sources-names-or-uuids)). The live tests
  in CI run against OBS Studio 30.0.2 (obs-websocket 5.3.4) and against the newest release; 28 and 29 are not tested. `ObsModel` needs OBS Studio 30.1 or later.
- **The .NET 10 SDK or later.** The library targets `net10.0` only (4.0.0-beta1, 4.0.0-beta2 and 4.0.0-rc.1 also had `net8.0`).

## Enable the WebSocket server in OBS Studio

1. In OBS Studio, open **Tools → WebSocket Server Settings**.
2. Tick **Enable WebSocket server**.
3. Keep the default **Server Port** (4455), or choose your own.
4. Tick **Enable Authentication** and set a **Server Password**. **Show Connect Info** displays the port and the password.
5. Click **Apply**.

> [!TIP]
> Without authentication anyone who can reach the port can control OBS Studio. That may be fine for a quick try on your own
> machine, but not on a network you share.

## Create a project and install the package

```shell
dotnet new console -n ObsHello
cd ObsHello
dotnet add package OBSClient
```

## Two ways in

OBSClient has two ways to work with OBS Studio, and you can start with either. [`ObsClient`](xref:OBSStudioClient.ObsClient) is the protocol, as it is: you send requests, you
read what OBS Studio answers, and you listen to its events. [`ObsModel`](xref:OBSStudioClient.Model.ObsModel) keeps a live model of OBS Studio in memory, and you work with
your scenes, inputs and filters by name. [Two ways to work with OBS Studio](two-ways.md) tells how they differ and which one to use. In short: start with `ObsModel` when you
build something around your scenes and inputs, and OBS Studio is 30.1 or newer; start with `ObsClient` for everything else. They run on one connection, so you can use both in one program.

Below is a first program for each.

## First program with `ObsClient`: send requests

Replace the content of `Program.cs`:

```csharp
using OBSStudioClient;

using var client = new ObsClient();

// True when OBS Studio has accepted the connection and the password.
if (!await client.ConnectAsync(password: "your-password"))
{
    Console.WriteLine("Could not connect. Is the WebSocket server enabled?");
    return;
}

var version = await client.GetVersion();
Console.WriteLine($"OBS Studio {version.ObsVersion}");
Console.WriteLine($"obs-websocket {version.ObsWebSocketVersion}");

var current = await client.GetCurrentProgramScene();
Console.WriteLine($"On air: {current.SceneName}");

var scenes = await client.GetSceneList();
foreach (var scene in scenes.Scenes)
{
    Console.WriteLine($"- {scene.SceneName}");
}
```

Run it with `dotnet run`, while OBS Studio is running:

```text
OBS Studio 33.0.0
obs-websocket 5.7.6
On air: Gaming Scene
- Gaming Scene
- Starting Soon
- Be Right Back
```

These requests only read. When you are ready to change something, remember that OBS Studio may be live:

```csharp
await client.SetCurrentProgramScene("Starting Soon");   // switches the scene
bool muted = await client.ToggleInputMute("Microphone");  // returns the new state
```

## First program with `ObsModel`: work by name

The same program, with a model. When it connects, the model reads your scenes, inputs, scene items, filters and outputs. Replace the content of `Program.cs`:

```csharp
using OBSStudioClient.Exceptions;
using OBSStudioClient.Model;

await using var obs = new ObsModel();

try
{
    // Returns when the model is complete.
    await obs.ConnectAsync("localhost", 4455, "your-password");
}
catch (ObsNotConnectedException)
{
    Console.WriteLine("Could not connect. Is the WebSocket server enabled?");
    return;
}

// The protocol is one property away.
var version = await obs.Client.GetVersion();
Console.WriteLine($"OBS Studio {version.ObsVersion}");

// From memory: no request is sent.
Console.WriteLine($"On air: {obs.ProgramScene?.Name}");
foreach (var scene in obs.Scenes)
{
    Console.WriteLine($"- {scene.Name} ({scene.Items.Count} items)");
}
```

Run it with `dotnet run`, while OBS Studio is running:

```text
OBS Studio 33.0.0
On air: Gaming Scene
- Gaming Scene (4 items)
- Starting Soon (2 items)
- Be Right Back (2 items)
```

Again these only read. To change something, name it:

```csharp
await obs.Scenes["Starting Soon"].SwitchToAsync();       // switches the scene
await obs.Inputs["Microphone"].SetMutedAsync(true);      // mutes the input
```

Each call returns when the model shows the change. `ConnectAsync` throws an [`ObsNotConnectedException`](xref:OBSStudioClient.Exceptions.ObsNotConnectedException) when OBS Studio cannot be reached
or refuses the password, and an [`ObsVersionNotSupportedException`](xref:OBSStudioClient.Exceptions.ObsVersionNotSupportedException) for an OBS Studio older than 30.1. A runnable command prompt
that does much more is in the repository: [`samples/ModelSample`](https://github.com/tinodo/obsclient/tree/main/samples/ModelSample).

## What just happened

With `ObsClient`:

- `ObsClient` opens one WebSocket connection to OBS Studio. [`ConnectAsync`](xref:OBSStudioClient.ObsClient.ConnectAsync*) returns when
  the handshake, including the authentication, is done, so you can send requests as soon as it returns `true`. It does not
  throw when OBS Studio is not there: it returns `false`.
- Every request of the obs-websocket protocol is a method of `ObsClient` with the name of the request: `GetVersion`,
  `GetSceneList`, `SetCurrentProgramScene`, and so on. The result is a typed response, or the single value that the response holds.
- `using var` disposes the client at the end of the program, which closes the connection.

With `ObsModel`:

- `ObsModel` makes an `ObsClient` of its own and connects it. [`ConnectAsync`](xref:OBSStudioClient.Model.ObsModel.ConnectAsync*) returns when it has read your scenes, inputs, scene items, filters and
  outputs. From then on every property is answered from memory, and kept right by the events of OBS Studio. It throws when OBS Studio is not there, where the client returns `false`.
- You find things by name (`obs.Scenes["Starting Soon"]`) and change them with a method on the object, which returns when the model shows the change.
- `obs.Client` is the `ObsClient` that the model is built on: everything that the model does not offer yet is one property away.
- `await using var` disposes the model at the end of the program, which closes the connection.

## Next steps

- [Two ways to work with OBS Studio](two-ways.md): how `ObsClient` and `ObsModel` differ, the same jobs done both ways, and which one to use.
- [Connecting to OBS Studio](connecting.md): the result of `ConnectAsync`, reconnecting, logging, remote machines.
- [Calling requests](requests.md): scenes and inputs by name or by UUID, canvases, settings, errors and timeouts.
- [Handling events](events.md): react to what happens in OBS Studio.
- [Request batches](batches.md): several requests in one round trip.
- [ObsModel: controlling OBS Studio by name](model.md): reading, changing and watching your scenes, inputs, filters and outputs.
- [The test app](test-app.md): try every request by hand before you write code.
- [A runnable sample](https://github.com/tinodo/obsclient/tree/main/samples/ConsoleSample) in the repository: it connects, lists the scenes and
  prints the events it receives (`dotnet run --project samples/ConsoleSample`). CI builds it against the package on every change.
