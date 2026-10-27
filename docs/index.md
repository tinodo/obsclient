---
_layout: landing
title: A complete .NET client for OBS Studio
_description: A complete cross-platform .NET WebSocket client for OBS Studio 28 and up. Every request and event of obs-websocket 5.7.6 as typed, async C#.
---

<div class="oc-hero">
<div class="oc-hero-text">

<p class="oc-eyebrow">obs-websocket 5.7.6 &middot; OBS Studio 28 and up &middot; .NET 8 and 10</p>

# A complete .NET client for OBS Studio

Switch scenes, mute inputs, start the stream, react to whatever happens in OBS Studio: from any .NET application, on Windows,
Linux or macOS. Every request, event and enum of the obs-websocket protocol is a typed, async C# member, checked against the
protocol on every build.

<div class="oc-actions">
<a class="btn btn-primary btn-lg" href="articles/getting-started.md">Get started</a>
<a class="btn btn-outline-secondary btn-lg" href="api/index.md">API reference</a>
<a class="btn btn-outline-secondary btn-lg" href="https://github.com/tinodo/obsclient"><i class="bi bi-github"></i> GitHub</a>
</div>

```shell
dotnet add package OBSClient --prerelease
```

</div>
<div class="oc-hero-code">

```csharp
using OBSStudioClient;

using var client = new ObsClient();

if (await client.ConnectAsync(password: "secret"))
{
    client.CurrentProgramSceneChanged += (_, e) =>
        Console.WriteLine($"On air: {e.SceneName}");

    await client.SetCurrentProgramScene("Gaming Scene");
    await client.StartRecord();
}
```

</div>
</div>

<div class="oc-banner">
<i class="bi bi-info-circle"></i>
<div>
<strong>4.0 is in beta.</strong> This site documents OBSClient 4.0, a major update that brings the client to obs-websocket 5.7.6.
Using 3.x? <a href="articles/migrating-to-4.md">Moving from 3.x to 4.0</a> lists what changes.
</div>
</div>

<div class="oc-section">

## Why OBSClient

<div class="oc-cards">

<div class="oc-card">
<i class="bi bi-check2-circle"></i>
<h3>The whole protocol</h3>
<p>Every request, event, batch builder and enum of obs-websocket 5.7.6, including the multi-canvas support of 5.7.0.</p>
</div>

<div class="oc-card">
<i class="bi bi-braces"></i>
<h3>Typed and async</h3>
<p>Typed requests and responses, <code>async</code> and <code>await</code> throughout, nullable annotations, and XML documentation on every member.</p>
</div>

<div class="oc-card">
<i class="bi bi-link-45deg"></i>
<h3>Names or UUIDs</h3>
<p>Pass a scene or an input by name, by UUID, or as the object OBS Studio gave you. One method per request, no overload pairs.</p>
</div>

<div class="oc-card">
<i class="bi bi-layers"></i>
<h3>Request batches</h3>
<p>Send several requests in one round trip, serially or in parallel, in real time or per frame, and stop at the first failure.</p>
</div>

<div class="oc-card">
<i class="bi bi-arrow-repeat"></i>
<h3>A connection you can rely on</h3>
<p>Auto-reconnect, retries while OBS Studio is not ready, requests that fail fast, and a connection that tells you when it is lost.</p>
</div>

<div class="oc-card">
<i class="bi bi-shield-check"></i>
<h3>Tested against the real thing</h3>
<p>Conformance tests compare every request with the protocol definition, and captures of a real OBS Studio check the nested objects.</p>
</div>

</div>
</div>

<div class="oc-section">

## A quick tour

#### [Control OBS Studio](#tab/control)

```csharp
using OBSStudioClient;

using var client = new ObsClient();
if (!await client.ConnectAsync(password: "secret")) return;

await client.SetCurrentProgramScene("Gaming Scene");      // by name
await client.SetCurrentProgramScene(sceneUuid);           // by UUID: survives a rename
bool muted = await client.ToggleInputMute("Microphone");  // returns the new state
await client.StartStream();
```

#### [React to events](#tab/events)

```csharp
client.InputMuteStateChanged += (_, e) =>
    Console.WriteLine($"{e.InputName} is {(e.InputMuted ? "muted" : "live")}");

client.StreamStateChanged += (_, e) =>
    Console.WriteLine($"Streaming: {e.OutputActive}");
```

#### [Send a batch](#tab/batch)

```csharp
var batch = new RequestBatchMessage(haltOnFailure: true);
batch.AddSetCurrentProgramSceneRequest("Starting Soon");
batch.AddSleepRequest(5000, null);
batch.AddSetCurrentProgramSceneRequest("Gaming Scene");

RequestResponseMessage[] results =
    await client.SendRequestBatchAsync(batch, timeOutInMilliseconds: 6000);
```

#### [Move a scene item](#tab/transform)

```csharp
SceneItemTransform current =
    await client.GetSceneItemTransform("Gaming Scene", sceneItemId);

// Change what you need; the rest is left as it is.
await client.SetSceneItemTransform(
    "Gaming Scene", sceneItemId, current.ToUpdate() with { ScaleX = 2, ScaleY = 2 });
```

---

</div>

<div class="oc-section">

## Compatibility

| Area | Support |
| --- | --- |
| **OBS Studio** | 28 and up. obs-websocket 5 is built into OBS Studio since version 28. |
| **Protocol** | [obs-websocket 5.7.6](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md), including the multi-canvas requests and events of 5.7.0. |
| **Target frameworks** | `net8.0` and `net10.0`. |
| **Platforms** | Windows, Linux and macOS: the client only uses `System.Net.WebSockets`, and its tests run on all three in CI. |
| **Dependencies** | `Microsoft.Extensions.Logging`. |
| **License** | [MIT](https://github.com/tinodo/obsclient/blob/main/LICENSE) |

</div>

<div class="oc-section">

## Where next

<div class="oc-cards oc-cards-4">

<a class="oc-card oc-card-link" href="articles/getting-started.md">
<i class="bi bi-rocket-takeoff"></i>
<h3>Getting started</h3>
<p>Enable the WebSocket server, install the package and send your first request.</p>
</a>

<a class="oc-card oc-card-link" href="articles/requests.md">
<i class="bi bi-send"></i>
<h3>Calling requests</h3>
<p>Names and UUIDs, canvases, settings, errors and timeouts.</p>
</a>

<a class="oc-card oc-card-link" href="articles/test-app.md">
<i class="bi bi-window-stack"></i>
<h3>The test app</h3>
<p>A Windows app to try every request and event by hand against your own OBS Studio.</p>
</a>

<a class="oc-card oc-card-link" href="api/index.md">
<i class="bi bi-book"></i>
<h3>API reference</h3>
<p>Every type and member of the library, generated from the source.</p>
</a>

</div>
</div>
