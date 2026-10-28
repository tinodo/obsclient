---
title: Getting started
---

# Getting started

This page takes you from nothing to a small program that talks to your OBS Studio.

> [!NOTE]
> This site documents OBSClient **4.0**, which is in beta. Using 3.x? See [Moving from 3.x to 4.0](migrating-to-4.md).

## What you need

- **OBS Studio 28 or later.** The WebSocket server (obs-websocket 5) is built in.
- **The .NET 8 SDK or later.** The library targets `net8.0` and `net10.0`.

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
dotnet add package OBSClient --prerelease
```

`--prerelease` is needed while 4.0 is in beta: a plain `dotnet add package OBSClient` installs the latest stable version, which
is 3.x. Once 4.0 is released the flag is no longer needed.

## Connect and send your first requests

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

## What just happened

- `ObsClient` opens one WebSocket connection to OBS Studio. [`ConnectAsync`](xref:OBSStudioClient.ObsClient.ConnectAsync*) returns when
  the handshake, including the authentication, is done, so you can send requests as soon as it returns `true`. It does not
  throw when OBS Studio is not there: it returns `false`.
- Every request of the obs-websocket protocol is a method of `ObsClient` with the name of the request: `GetVersion`,
  `GetSceneList`, `SetCurrentProgramScene`, and so on. The result is a typed response, or the single value that the response holds.
- `using var` disposes the client at the end of the program, which closes the connection.

## Next steps

- [Connecting to OBS Studio](connecting.md): the result of `ConnectAsync`, reconnecting, logging, remote machines.
- [Calling requests](requests.md): scenes and inputs by name or by UUID, canvases, settings, errors and timeouts.
- [Handling events](events.md): react to what happens in OBS Studio.
- [Request batches](batches.md): several requests in one round trip.
- [The test app](test-app.md): try every request by hand before you write code.
- [A runnable sample](https://github.com/tinodo/obsclient/tree/main/samples/ConsoleSample) in the repository: it connects, lists the scenes and
  prints the events it receives (`dotnet run --project samples/ConsoleSample`). CI builds it against the package on every change.
