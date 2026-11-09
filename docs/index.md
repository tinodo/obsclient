---
_layout: landing
title: A complete .NET client for OBS Studio, with two ways to work with it
_description: A complete cross-platform .NET client for OBS Studio 28 and up. ObsClient speaks the protocol, every request and event of obs-websocket 5.7.6 as typed, async C#. ObsModel speaks OBS Studio, a live model of your scenes, inputs and filters that you work with by name.
---

<div class="oc-hero">
<div class="oc-hero-text">

<p class="oc-eyebrow">obs-websocket 5.7.6 &middot; OBS Studio 28 and up &middot; .NET 10</p>

# A complete .NET client for OBS Studio

Two ways to work with it, in one package: **`ObsClient`** speaks the protocol, and **`ObsModel`** speaks OBS Studio. Use the protocol when you
want every request and event of obs-websocket as it is, typed and async. Use the model when you want your scenes, inputs and filters by
name, kept up to date for you. Switch scenes, mute inputs, start the stream, react to whatever happens in OBS Studio: from any .NET application,
on Windows, Linux or macOS.

<div class="oc-actions">
<a class="btn btn-primary btn-lg" href="articles/getting-started.md">Get started</a>
<a class="btn btn-outline-secondary btn-lg" href="articles/two-ways.md">The two ways</a>
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
using OBSStudioClient.Model;

// ObsClient: the protocol, as it is.
using var client = new ObsClient();
await client.ConnectAsync(password: "secret");

int webcam = await client.GetSceneItemId("Gaming Scene", "Webcam");
await client.SetSceneItemEnabled("Gaming Scene", webcam, false);

// ObsModel: OBS Studio, by name.
await using var obs = new ObsModel();
await obs.ConnectAsync("localhost", 4455, "secret");

await obs.Scenes["Gaming Scene"].Items["Webcam"].SetEnabledAsync(false);
obs.Changed += (_, e) => Console.WriteLine(e);
```

</div>
</div>

<div class="oc-banner">
<i class="bi bi-info-circle"></i>
<div>
<strong>4.0 is a release candidate.</strong> This site documents OBSClient 4.0, a major update that brings the client to obs-websocket 5.7.6, and <a href="articles/model.md">ObsModel</a>, OBS Studio by name, which is new and provisional.
Using 3.x? <a href="articles/migrating-to-4.md">Moving from 3.x to 4.0</a> lists what changes.
</div>
</div>

<div class="oc-section" id="two-ways">

## Two ways to work with OBS Studio

OBS Studio is controlled through obs-websocket, a protocol of requests and events. A protocol client hands you those, and the bookkeeping is yours:
asking for the state, looking up ids, following the events to keep your own picture of OBS Studio. OBSClient has that client, complete, and one level
above it a model that keeps the picture for you.

<div class="oc-ways">

<div class="oc-way">
<p class="oc-way-label">The protocol, as it is</p>
<h3><code>ObsClient</code></h3>
<p>Every request and every event of obs-websocket, as typed, async C#. Complete, direct and nothing hidden: close to the API, and the way to everything that obs-websocket offers.</p>

```csharp
using var client = new ObsClient();
await client.ConnectAsync(password: "secret");

int webcam = await client.GetSceneItemId("Gaming Scene", "Webcam");
await client.SetSceneItemEnabled("Gaming Scene", webcam, false);
```

<p class="oc-way-note"><strong>Reach for it</strong> when you want every request and event, when you need request batches or OBS Studio 28 to 30.0, or when you send a few commands and leave.</p>
<p class="oc-way-links"><a href="articles/requests.md">Calling requests</a><a href="articles/events.md">Handling events</a></p>
</div>

<div class="oc-way oc-way-model">
<p class="oc-way-label">OBS Studio, as you see it <span class="oc-way-tag">New</span></p>
<h3><code>ObsModel</code></h3>
<p>A live model of OBS Studio that you work with by name. Read from memory, change with one call, hear what changes: intuitive, and close to OBS Studio itself.</p>

```csharp
await using var obs = new ObsModel();
await obs.ConnectAsync("localhost", 4455, "secret");

await obs.Scenes["Gaming Scene"]
         .Items["Webcam"]
         .SetEnabledAsync(false);
```

<p class="oc-way-note"><strong>Reach for it</strong> to build a panel, a bot, an overlay or a user interface that shows or controls scenes, inputs, filters and outputs, with OBS Studio 30.1 or newer.</p>
<p class="oc-way-links"><a href="articles/model.md">Controlling OBS Studio by name</a></p>
</div>

</div>

<p class="oc-ways-foot">Both are in one package and share a connection: <code>obs.Client</code> is the <code>ObsClient</code>, so you can use both in one program. <a href="articles/two-ways.md">Read the whole story: two ways to work with OBS Studio &rarr;</a></p>

</div>

<div class="oc-section">

## Why OBSClient

### `ObsClient`: the protocol, complete

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

### `ObsModel`: OBS Studio, by name

<div class="oc-cards">

<div class="oc-card">
<i class="bi bi-tag"></i>
<h3>By name</h3>
<p><code>obs.Scenes["Gaming Scene"].Items["Webcam"]</code>: no UUIDs, no scene item ids, no indexes to look up first.</p>
</div>

<div class="oc-card">
<i class="bi bi-lightning-charge"></i>
<h3>Reads without a round trip</h3>
<p>A property is answered from memory, from any thread. The model follows OBS Studio, also what the user does in its window.</p>
</div>

<div class="oc-card">
<i class="bi bi-check2-all"></i>
<h3>Changes you can rely on</h3>
<p>A call returns when the model shows the change, one change at a time and in order. When one fails, the exception says whether anything was changed.</p>
</div>

<div class="oc-card">
<i class="bi bi-broadcast"></i>
<h3>Hear what changes</h3>
<p><code>Changed</code> says what changed, in order, and every object implements <code>INotifyPropertyChanged</code>, so that a user interface can bind to it. <code>Resynchronized</code> says when everything was read again.</p>
</div>

<div class="oc-card">
<i class="bi bi-arrow-repeat"></i>
<h3>Made for the real OBS Studio</h3>
<p>It reconnects and reads again, follows a switch of the scene collection, and copes with scenes that OBS Studio keeps alive after they were removed. Checked against a simulation and a real OBS Studio.</p>
</div>

<div class="oc-card">
<i class="bi bi-diagram-3"></i>
<h3>Nothing hidden</h3>
<p><code>obs.Client</code> is the <code>ObsClient</code>: whatever the model does not offer yet is one property away.</p>
</div>

</div>
</div>

<div class="oc-section">

## A quick tour

#### [ObsClient: control](#tab/control)

```csharp
using OBSStudioClient;

using var client = new ObsClient();
if (!await client.ConnectAsync(password: "secret")) return;

await client.SetCurrentProgramScene("Gaming Scene");      // by name
await client.SetCurrentProgramScene(sceneUuid);           // by UUID: survives a rename
bool muted = await client.ToggleInputMute("Microphone");  // returns the new state
await client.StartStream();
```

#### [ObsModel: by name](#tab/model)

```csharp
using OBSStudioClient.Model;

await using var obs = new ObsModel();
await obs.ConnectAsync("localhost", 4455, "secret");       // reads your scenes and inputs, then follows OBS Studio

await obs.Scenes["Gaming Scene"].SwitchToAsync();          // no UUIDs, no ids, no indexes
await obs.Inputs["Microphone"].SetMutedAsync(true);
await obs.Scenes["Gaming Scene"].Items["Webcam"].SetEnabledAsync(false);
await obs.Inputs["Microphone"].Filters["Noise Gate"].SetEnabledAsync(true);

bool shown = obs.Scenes["Gaming Scene"].Items["Webcam"].IsEnabled;   // from memory: no request is sent
```

#### [ObsClient: events](#tab/events)

```csharp
client.InputMuteStateChanged += (_, e) =>
    Console.WriteLine($"{e.InputName} is {(e.InputMuted ? "muted" : "live")}");

client.StreamStateChanged += (_, e) =>
    Console.WriteLine($"Streaming: {e.OutputActive}");
```

#### [ObsModel: changes](#tab/changes)

```csharp
obs.Changed += (_, e) => Console.WriteLine(e);    // what changes, in order
// input 'Microphone': IsMuted False -> True
// scene item 3 of 'Gaming Scene': IsEnabled True -> False

// The model read everything again: a lost connection came back, or the scene collection changed.
obs.Resynchronized += (_, _) => Console.WriteLine("Read again what you hold.");

// Every object is an INotifyPropertyChanged, so a user interface can bind to it.
obs.Inputs["Microphone"].PropertyChanged += (_, e) => Console.WriteLine(e.PropertyName);
```

#### [ObsClient: batches](#tab/batch)

```csharp
var batch = new RequestBatchMessage(haltOnFailure: true);
batch.AddSetCurrentProgramSceneRequest("Starting Soon");
batch.AddSleepRequest(5000, null);
batch.AddSetCurrentProgramSceneRequest("Gaming Scene");

RequestResponseMessage[] results =
    await client.SendRequestBatchAsync(batch, timeOutInMilliseconds: 6000);
```

#### [ObsClient: transforms](#tab/transform)

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
| **OBS Studio** | `ObsClient`: 28 and up. obs-websocket 5 is built into OBS Studio since version 28. `ObsModel`: 30.1 and up, the first version that gives scenes and inputs a UUID (obs-websocket 5.4). An older OBS Studio sends none, so name the scene or the input, and use `ObsClient`. CI runs the live tests against OBS Studio 30.0.2 and against the newest release. |
| **Protocol** | [obs-websocket 5.7.6](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md), including the multi-canvas requests and events of 5.7.0. |
| **Target frameworks** | `net10.0`, the current LTS release of .NET. An application on .NET 11 uses it too. Microsoft ends the support of .NET 8 and .NET 9 on 10 November 2026, so 4.0.0 has no `net8.0` library: only 4.0.0-beta1, 4.0.0-beta2 and 4.0.0-rc.1 had one, and an application on .NET 8 or 9 stays on one of those. |
| **Platforms** | Windows, Linux and macOS: the client only uses `System.Net.WebSockets`, and its tests run on all three in CI. |
| **Dependencies** | `Microsoft.Extensions.Logging.Abstractions` (8.0.0 and up), which has the `ILogger` that the client logs to. |
| **License** | [MIT](https://github.com/tinodo/obsclient/blob/main/LICENSE) |

</div>

<div class="oc-section">

## Where next

<div class="oc-cards">

<a class="oc-card oc-card-link" href="articles/two-ways.md">
<i class="bi bi-signpost-split"></i>
<h3>Two ways to work with OBS Studio</h3>
<p>The protocol or the model: what each is, the same jobs done both ways, and which one to use.</p>
</a>

<a class="oc-card oc-card-link" href="articles/getting-started.md">
<i class="bi bi-rocket-takeoff"></i>
<h3>Getting started</h3>
<p>Enable the WebSocket server, install the package and run your first program, at either level.</p>
</a>

<a class="oc-card oc-card-link" href="articles/requests.md">
<i class="bi bi-send"></i>
<h3>Calling requests</h3>
<p><code>ObsClient</code>: names and UUIDs, canvases, settings, errors and timeouts.</p>
</a>

<a class="oc-card oc-card-link" href="articles/model.md">
<i class="bi bi-diagram-3"></i>
<h3>Controlling OBS Studio by name</h3>
<p><code>ObsModel</code>: reading, changing and watching your scenes, inputs, filters and outputs.</p>
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
