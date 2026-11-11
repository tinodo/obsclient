---
title: Two ways to work with OBS Studio
---

# Two ways to work with OBS Studio

OBSClient is one package with two ways in. [`ObsClient`](xref:OBSStudioClient.ObsClient) **speaks the protocol**: every request and every event of obs-websocket, as it is.
[`ObsModel`](xref:OBSStudioClient.Model.ObsModel) **speaks OBS Studio**: scenes, inputs and filters that you find by name, read at once and change with one call.
They share a library and a connection. You can use either, and you can use both in the same program.

> [!NOTE]
> `ObsClient` has been in the library from the start. `ObsModel` is new in 4.0. The API of both is closed: the versions of 4 that follow add to it, and do not break it.

## The idea, in a minute

OBS Studio is controlled through obs-websocket, a protocol of **requests** and **events**: a request asks for something or changes something, an event says that something changed.
The protocol names things the way OBS Studio keeps them inside: a scene or an input by its name or its UUID, a scene item by a number, a filter by its name within its source.

- **`ObsClient`** gives you that protocol, completely and as it is. Every request is a method and every event is an event, with the names of the protocol. What you know about OBS Studio
  is what you asked it, and what its events told you. Keeping a picture of OBS Studio up to date is yours to do. It is *close to the API*.
- **`ObsModel`** keeps that picture for you. It reads your scenes, inputs, scene items, filters and outputs when it connects, follows OBS Studio from then on, and lets you work with
  the picture: *"mute the Microphone"*, *"hide the Webcam in the Gaming scene"*. It is *close to OBS Studio*, to the way you look at it in its window.

| | `ObsClient`: the protocol | `ObsModel`: OBS Studio by name |
| --- | --- | --- |
| **It is** | the obs-websocket protocol, as typed, async C# | a live model of OBS Studio in memory, built on `ObsClient` |
| **You think in** | requests, responses and events | scenes, inputs, scene items, filters and outputs |
| **You find things by** | a name or a UUID, and ids that you look up first | their name: `obs.Scenes["Gaming Scene"]` |
| **You read state** | with a request, each time | from a property, answered from memory, with no request |
| **A change** | is one request; the call returns when OBS Studio has answered | is a method on the object; the call returns when the model shows the change |
| **You hear about changes** | with one event for each kind of change, and you apply them | with `Changed` and `INotifyPropertyChanged`, after the model has applied them, and `Resynchronized` when it read everything again |
| **It covers** | the whole protocol | the whole protocol too, by name: scenes, inputs (settings, audio, media), scene items (transforms), filters, outputs, studio mode and transitions, profiles and scene collections, canvases and the settings of OBS Studio. Request batches, and the scenes of canvases other than the main one, through `obs.Client` |
| **It needs** | OBS Studio 28 or newer | OBS Studio 30.1 or newer |
| **In 4.0** | the API is closed | new in 4.0, and its API is closed too |

<div class="oc-stack" role="img" aria-label="Your application uses ObsModel, which is built on ObsClient, or uses ObsClient directly. ObsClient talks to OBS Studio over a WebSocket.">
<div class="oc-stack-app">Your application</div>
<div class="oc-stack-model"><strong>ObsModel</strong><span>OBS Studio by name</span></div>
<div class="oc-stack-direct">or straight to the protocol</div>
<div class="oc-stack-client"><strong>ObsClient</strong><span>the protocol: every request, every event</span></div>
<div class="oc-stack-wire">WebSocket &middot; obs-websocket 5</div>
<div class="oc-stack-obs"><strong>OBS Studio</strong></div>
</div>

## `ObsClient`: the protocol, as it is

`ObsClient` is the obs-websocket protocol as typed, async C#, checked against the protocol definition on every build. It is the way to everything that obs-websocket offers.

- **Every request and every event.** Each request of the protocol is a method with its name, each event is a .NET event with its name, and the enumerations and the
  [request batches](batches.md) are there as well. When obs-websocket adds something, the client gets it from the protocol definition
  ([Staying in sync with obs-websocket](protocol-sync.md)).
- **Direct, and nothing hidden.** A call sends one request and returns when OBS Studio has answered it, with what OBS Studio sent, typed. No part of the protocol is left out.
- **Things are identified the way OBS Studio does.** One argument takes a name or a UUID (see [Calling requests](requests.md#scenes-inputs-and-sources-names-or-uuids)); a scene item is
  a number that you ask for first.
- **The state is yours.** The client does not keep a picture of OBS Studio. You ask when you want to know, and you listen to the events when you want to stay up to date.
- **No reading beforehand.** `ConnectAsync` returns when the handshake is done, so a request can go out at once and a program that sends a few commands is quick. It works with OBS Studio 28 and newer.

```csharp
using OBSStudioClient;

using var client = new ObsClient();
await client.ConnectAsync(password: "secret");

// A scene item is a number: look it up, then change it.
int webcam = await client.GetSceneItemId("Gaming Scene", "Webcam");
await client.SetSceneItemEnabled("Gaming Scene", webcam, false);
```

## `ObsModel`: OBS Studio, as you see it

`ObsModel` is a live model of OBS Studio, in memory, and it is built on `ObsClient`. When it connects it reads your scenes, inputs, scene items, filters and outputs. From then on it
listens to OBS Studio, also to what the user does in its window, and keeps the model right. You work with the model.

- **Names, not plumbing.** `obs.Scenes["Gaming Scene"].Items["Webcam"]`: no UUIDs, no scene item ids, no indexes to look up first.
- **Reads are instant.** A property is answered from memory, from any thread, and sends no request: `item.IsEnabled`, `input.IsMuted`, `scene.IsProgram`.
- **A change returns when the model shows it.** Changes are methods on the objects, made one at a time in the order in which you ask for them, and each returns **when the model shows the change**, so
  that what you read next is what you changed (an output returns when OBS Studio has *begun* to start or stop, because a stream takes seconds to connect). When something goes wrong the exception says exactly what is known, whether the change was made, and what to do. A change is never made twice.
- **You hear what changes.** `Changed` says what changed, in order, and every object implements `INotifyPropertyChanged`, so that a user interface can bind to them. When the model had to read
  everything again (a lost connection came back, OBS Studio switched its scene collection, or your handlers were too slow), `Resynchronized` says so, and you read again what you hold.
- **It copes with OBS Studio.** It reconnects and reads everything again, follows a switch of the scene collection, and knows that OBS Studio keeps a scene or an input alive after it was
  removed. It is checked against a simulation of OBS Studio that reproduces what the real one does that surprises, and in CI against a real one.
- **Nothing is hidden.** `obs.Client` is the `ObsClient`, for everything that the model does not offer (yet).

```csharp
using OBSStudioClient.Model;

await using var obs = new ObsModel();

// Returns when the model is complete.
await obs.ConnectAsync("localhost", 4455, "secret");

// Returns when the model shows the change.
await obs.Scenes["Gaming Scene"].Items["Webcam"].SetEnabledAsync(false);
```

What the model has: scenes, inputs (with their settings, audio, media and, when asked for, whether they are on the air), scene items (with their transform and blend mode, and copies of them), the program scene and the preview scene, studio mode and the scene transitions, the filters of scenes and inputs, the outputs (stream, recording, virtual camera, replay buffer), the profiles and scene collections (which one is current, the names of all, and the calls that switch and make them), the canvases (the list, with their flags, and the video of each one when you ask), and calls for the settings of OBS Studio itself (the video settings, the stream service, the folder for recordings, the parameters of a profile) and for the data that it keeps for an application.
What it does not have yet: the scenes of the canvases other than the main one, and request batches. Use `obs.Client` for those.
[Controlling OBS Studio by name](model.md) has all of it.

## The same job, both ways

The same four jobs, at both levels. `client` is an `ObsClient` that is connected, `obs` is an `ObsModel` that is.

### Change something

Hide the webcam in the Gaming scene.

**With `ObsClient`**

```csharp
int webcam = await client.GetSceneItemId("Gaming Scene", "Webcam");
await client.SetSceneItemEnabled("Gaming Scene", webcam, false);
```

**With `ObsModel`**

```csharp
await obs.Scenes["Gaming Scene"].Items["Webcam"].SetEnabledAsync(false);
```

For a single command, such as switching the scene or muting an input, the two are as short as each other. The model starts to pay off when you need an id first, and when you go on to read.

### Read something

Is the webcam shown?

**With `ObsClient`**

```csharp
int webcam = await client.GetSceneItemId("Gaming Scene", "Webcam");

// A request, each time you ask.
bool shown = await client.GetSceneItemEnabled("Gaming Scene", webcam);
```

**With `ObsModel`**

```csharp
// From memory: no request is sent.
bool shown = obs.Scenes["Gaming Scene"].Items["Webcam"].IsEnabled;
```

### Look at everything

List every scene item of every scene, and whether it is on.

**With `ObsClient`**

```csharp
var list = await client.GetSceneList();
foreach (var scene in list.Scenes)
{
    foreach (var item in await client.GetSceneItemList(scene))
    {
        var state = item.SceneItemEnabled ? "on" : "off";
        Console.WriteLine($"{scene.SceneName}: {item.SourceName} is {state}");
    }
}
```

That is one request for the scenes and one for each scene, and the list is out of date as soon as somebody clicks in OBS Studio.

**With `ObsModel`**

```csharp
foreach (var scene in obs.Scenes)
{
    foreach (var item in scene.Items)
    {
        var state = item.IsEnabled ? "on" : "off";
        Console.WriteLine($"{scene.Name}: {item.Source.Name} is {state}");
    }
}
```

No request is sent. The model follows OBS Studio, so looking again shows what changed in the meantime.

### Follow what happens

Hear about changes, also the ones that somebody makes in OBS Studio.

**With `ObsClient`**

```csharp
client.SceneItemEnableStateChanged += (_, e) =>
{
    var state = e.SceneItemEnabled ? "on" : "off";
    Console.WriteLine($"{e.SceneName}: scene item {e.SceneItemId} is {state}");
};

client.InputMuteStateChanged += (_, e) =>
    Console.WriteLine($"{e.InputName} is {(e.InputMuted ? "muted" : "live")}");
```

And one event for each other kind of change that you care about. To keep a picture of OBS Studio, you apply every one of them yourself.

**With `ObsModel`**

```csharp
obs.Changed += (_, e) => Console.WriteLine(e);

// The model read everything again: a lost connection came back, or the scene collection changed.
obs.Resynchronized += (_, _) => Console.WriteLine("Read again what you hold.");
```

```text
scene item 3 of 'Gaming Scene': IsEnabled True -> False
input 'Microphone': IsMuted False -> True
```

`Changed` says what changed, in order. It does not replace a look at the model after a resynchronization: what you held may belong to the scene collection that was left, so read again what you need.

## Which one do I use?

**Use `ObsModel`** to build something that shows or controls OBS Studio over time: a control panel, a bot, a deck, an overlay, a user interface with the live state of your scenes. It fits when you think in names,
when you read state more often than you change it, when you want to know that a change has landed before you go on, and when OBS Studio is 30.1 or newer.

**Use `ObsClient`** when you need exactly the protocol. That is: for every request and event that the model does not cover (yet), for a quick script that sends a few commands and leaves, for
[request batches](batches.md), for OBS Studio 28 to 30.0, and when you build your own abstraction on top of the protocol.

**Use both** when you are in between. They run on one connection, and nothing is lost by starting with either.

## Using both

The model is built on the client, and the client is one property away. Anything the model does not offer, you do with the protocol, in the same program and on the same connection, and the model
follows what it did:

```csharp
// Not in the model yet: the protocol (the scenes of another canvas).
var scenes = await obs.Client.GetSceneList(obs.Canvases["Vertical"].Uuid);

// The model: the call returns when the model shows the change.
await obs.Scenes["Gaming Scene"].SwitchToAsync();
```

To make the client yourself, for example with your own logger, give it to the model. The client has to be disconnected: from then on the model connects it, and you do not call `client.ConnectAsync`.

```csharp
using var client = new ObsClient(logger);   // your ILogger<ObsClient>
await using var obs = new ObsModel(client);
await obs.ConnectAsync("localhost", 4455, "secret");
```

## What the model costs

The model is not free, and it is as well to know where:

- **It reads before it works.** `ConnectAsync` returns when the model is complete, which takes a number of requests (more with many inputs, and two more for each input without audio before OBS Studio 31.1).
- **It holds a copy.** The copy is small, and it is kept right by the events. It is a copy: between two reads of it, it can have changed, and while the connection is down it shows what was known last (`IsStale` says so).
- **Changes go one at a time.** That is what makes them predictable and in order. A change that takes long makes the ones behind it wait.
- **What OBS Studio sends very often is not followed unless you ask.** The transform of every scene item (`HighVolume = Transforms`), whether each input is on the air (`Activity`) and how loud each input is (`AudioLevels`: twenty messages a second, for as long as the connection lasts) cost OBS Studio and the connection something for every client that listens, so the model
  subscribes to them only for what you ask for (see [Controlling OBS Studio by name](model.md)).
- **It needs OBS Studio 30.1 or newer**, the first version that gives scenes and inputs a UUID. Older versions work with the plain client alone.

## Next steps

- [Getting started](getting-started.md): enable the WebSocket server, install the package and run a first program at either level.
- [Controlling OBS Studio by name](model.md): everything about `ObsModel`.
- [Calling requests](requests.md), [Handling events](events.md) and [Request batches](batches.md): everything about `ObsClient`.
- [The API reference](../api/index.md): both, generated from the source.
