---
title: Calling requests
---

# Calling requests

Every request of the obs-websocket protocol is a method of [`ObsClient`](xref:OBSStudioClient.ObsClient), named after the request.

```csharp
VersionResponse version = await client.GetVersion();
bool muted = await client.GetInputMute("Microphone");
await client.SetCurrentProgramScene("Gaming Scene");
string recordingPath = await client.StopRecord();
```

What a method returns follows the response of the request:

- nothing, when the response has no fields (`Task`);
- the value itself, when the response has one field (`Task<bool>`, `Task<string>`, `Task<Guid>`, ...);
- a response type with a property for each field, when it has several (`Task<VersionResponse>`).

The parameters, the responses and their members carry the names and the descriptions of the
[protocol documentation](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md), in PascalCase.

## Finding the request you need

The protocol groups its requests by category, and the library follows it: General, Config, Sources, Scenes, Inputs, Transitions,
Filters, Scene Items, Outputs, Stream, Record, Media Inputs, Ui and Canvases. The members of
[`ObsClient`](xref:OBSStudioClient.ObsClient) in the API reference list them all, and [the test app](test-app.md) shows them by
category with an editor for each parameter.

## Scenes, inputs and sources: names or UUIDs

OBS Studio can look up a scene, an input or a source by its name or by its UUID. A name can be changed by the user, a UUID cannot,
so a program that keeps a reference should keep the UUID. Whichever you use, there is one argument for it, of type
[`SceneRef`](xref:OBSStudioClient.Classes.SceneRef), [`InputRef`](xref:OBSStudioClient.Classes.InputRef) or
[`SourceRef`](xref:OBSStudioClient.Classes.SourceRef). A name, a `Guid`, and the `Scene`, `Input` or `SceneItem` that OBS Studio
gave you convert to it implicitly, so there is nothing to wrap:

```csharp
await client.SetCurrentProgramScene("Gaming Scene");   // by name
await client.SetCurrentProgramScene(sceneUuid);        // by UUID: survives a rename
await client.SetCurrentProgramScene(scene);            // a Scene from GetSceneList

bool muted = await client.GetInputMute("Microphone");

// When you need to say which it is, or the type cannot be inferred:
await client.SetSceneItemEnabled(
    SceneRef.FromName("Gaming Scene"), item.SceneItemId, true);
```

A reference that identifies nothing (`default`), and a required argument that is `null`, are refused with an `ArgumentException`
before anything is sent.

> [!NOTE]
> An OBS Studio before 30.1 (obs-websocket before 5.4) has no UUIDs. What it reports has empty ones (`Guid.Empty`), and `CreateScene`
> returns `Guid.Empty` for the scene that it made, so a `Scene`, an `Input` or a `SceneItem` that it gave you converts to a reference by
> *name* (that is what the conversion does when there is no UUID). Give such an OBS Studio names: it ignores a UUID that you made up.

## Canvases

OBS Studio can have more than one canvas (obs-websocket 5.7.0 and later). A request that looks up a scene or a source *by name*
takes an optional `canvasUuid`, so it can address a canvas other than the main one. Use [`GetCanvasList`](xref:OBSStudioClient.ObsClient.GetCanvasList*)
to find the canvases:

```csharp
Canvas[] canvases = await client.GetCanvasList();

foreach (Canvas canvas in canvases)
{
    SceneListResponse scenes = await client.GetSceneList(canvas.CanvasUuid);
}

Guid canvasUuid = canvases[0].CanvasUuid;
SceneItem[] items = await client.GetSceneItemList("Scene", canvasUuid: canvasUuid);
```

A UUID is unique to the whole of OBS Studio, so it needs no canvas.

## Settings

The settings of an input, a filter or a transition are free-form: they differ per kind. They are a
`Dictionary<string, object>`, and what comes back are `JsonElement` values:

```csharp
using System.Text.Json;

var settings = await client.GetInputSettings("Browser");
string? url = ((JsonElement)settings.InputSettings["url"]).GetString();

// overlay: true changes the settings you give and keeps the others.
await client.SetInputSettings(
    "Browser",
    new Dictionary<string, object> { ["url"] = "https://example.com" },
    overlay: true);
```

## Moving and scaling a scene item

OBS Studio accepts a part of a transform: only what you send is changed. [`SceneItemTransformUpdate`](xref:OBSStudioClient.Classes.SceneItemTransformUpdate)
has a nullable member for each part of the transform, and a member that is `null` is left out of the request.
`ToUpdate()` turns what you read into an update that you adjust with a `with` expression:

```csharp
SceneItemTransform current =
    await client.GetSceneItemTransform("Gaming Scene", sceneItemId);

await client.SetSceneItemTransform(
    "Gaming Scene", sceneItemId, current.ToUpdate() with { ScaleX = 2, ScaleY = 2 });

// Or only what you want to change:
var move = new SceneItemTransformUpdate { PositionX = 100, PositionY = 50 };
await client.SetSceneItemTransform("Gaming Scene", sceneItemId, move);
```

A `SceneItemTransform` also converts to an update implicitly.

## Errors

| Exception | Meaning |
| --- | --- |
| [`ObsResponseException`](xref:OBSStudioClient.Exceptions.ObsResponseException) | OBS Studio answered with an error. `ErrorCode` is the [`RequestStatusCode`](xref:OBSStudioClient.Enums.RequestStatusCode) and `ErrorMessage` the comment of OBS Studio. |
| [`ObsClientException`](xref:OBSStudioClient.Exceptions.ObsClientException) | The client could not complete the request: it is not connected (`Not connected.`), the connection ended while it waited, or the response could not be read. `ObsResponseException` derives from it. |
| `TimeoutException` | OBS Studio did not answer within `RequestTimeout`. |
| `OperationCanceledException` | The `CancellationToken` that you passed was cancelled, see [cancelling a request](#cancelling-a-request). |
| `ArgumentException` | An argument is refused before anything is sent (see above). |

```csharp
try
{
    await client.SetCurrentProgramScene("No such scene");
}
catch (ObsResponseException ex)
    when (ex.ErrorCode == RequestStatusCode.ResourceNotFound)
{
    Console.WriteLine("That scene does not exist.");
}
catch (ObsResponseException ex)
{
    Console.WriteLine($"Refused: {ex.ErrorCode}, {ex.ErrorMessage}");
}
catch (TimeoutException)
{
    Console.WriteLine("OBS Studio did not answer in time.");
}
catch (ObsClientException ex)
{
    Console.WriteLine($"The request could not be completed: {ex.Message}");
}
```

## Timeouts and retries

- [`RequestTimeout`](xref:OBSStudioClient.ObsClient.RequestTimeout) is how long the client waits for an answer, in milliseconds. The default is
  500 and the minimum 150. Raise it when a request takes OBS Studio longer to answer, for example a screenshot of a large source.
- OBS Studio answers `NotReady` (`RequestStatusCode.NotReady`) to a request that arrives while it is not ready, for example just after
  it started. The client then tries again, up to [`MaxRequestRetries`](xref:OBSStudioClient.ObsClient.MaxRequestRetries) times (10 by default),
  [`RequestRetryInterval`](xref:OBSStudioClient.ObsClient.RequestRetryInterval) milliseconds apart (500 by default), before it throws the
  `ObsResponseException`. Each attempt has its own timeout.

## Cancelling a request

Every request method takes a `CancellationToken` as its last parameter, and so do `SendRequestBatchAsync`, `ReidentifyAsync` and
[`ConnectAsync`](connecting.md#cancelling-a-connection-attempt). Cancelling it ends the wait for the response, and the method throws an
`OperationCanceledException`:

```csharp
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

try
{
    SceneListResponse scenes = await client.GetSceneList(cancellationToken: cancellation.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Stopped waiting.");
}
```

- A token that is cancelled before the call stops it before anything is sent.
- A request that was already sent is **not undone**: OBS Studio may still carry it out. When its answer arrives, it is ignored, and the
  connection stays usable.
- The call ends when the token is cancelled even if OBS Studio is not reading what you send. A message that is being written cannot be
  cancelled without breaking the connection, so it is written to the end in the background, and the requests that follow wait for their
  turn behind it. A message that was still waiting for its turn is not sent at all.
- The timeout is a different thing: a request that runs out of `RequestTimeout` throws a `TimeoutException`, whatever the token is.
- The wait between the retries of a request that OBS Studio is not ready for ends when the token is cancelled, too.

## Requests of other programs and your own events

`CallVendorRequest` calls a request that another program registered with obs-websocket (a plugin), and `BroadcastCustomEvent`
sends data to every client that listens for [custom events](events.md#custom-and-vendor-events):

```csharp
using System.Text.Json;

CallVendorResponse response = await client.CallVendorRequest(
    "my-plugin", "my-request", JsonSerializer.SerializeToElement(new { value = 1 }));

var data = JsonSerializer.SerializeToElement(new { message = "hello" });
await client.BroadcastCustomEvent(data);
```

## Shortcuts

A few methods are conveniences and not requests of the protocol: `SetInputVolumeDb` and `SetInputVolumeMul` (the protocol has the
one request `SetInputVolume`), `OpenSourceProjectorOnMonitor` and `OpenSourceProjectorWindow`, `OpenVideoMixProjectorOnMonitor` and
`OpenVideoMixProjectorWindow`, and `GetGeometry`, which builds the geometry that a windowed projector needs.
