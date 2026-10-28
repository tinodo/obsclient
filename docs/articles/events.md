---
title: Handling events
---

# Handling events

OBS Studio tells you when something happens: the scene changed, an input was muted, the stream started. Each event of the protocol is a
.NET event of [`ObsClient`](xref:OBSStudioClient.ObsClient) with the name of the event, and its arguments carry the fields of the event:

```csharp
client.CurrentProgramSceneChanged += (_, e) =>
    Console.WriteLine($"On air: {e.SceneName} ({e.SceneUuid})");

client.InputMuteStateChanged += (_, e) =>
    Console.WriteLine($"{e.InputName} is {(e.InputMuted ? "muted" : "live")}");

client.StreamStateChanged += (_, e) =>
    Console.WriteLine($"Streaming: {e.OutputActive} ({e.OutputState})");
```

The argument types live in the `OBSStudioClient.Events` namespace. Events that carry the same fields share a type: `SceneNameEventArgs`
for `CurrentProgramSceneChanged` and `CurrentPreviewSceneChanged`, `OutputStateChangedEventArgs` for `StreamStateChanged`,
`ReplayBufferStateChanged` and `VirtualcamStateChanged`. An event without data, `ExitStarted`, is a plain `EventHandler`.

## Choosing the events you receive

OBS Studio only sends the events that a connection subscribed to. You choose them with an
[`EventSubscriptions`](xref:OBSStudioClient.Enums.EventSubscriptions) value, in `ConnectAsync`, and change the choice of an open
connection with `ReidentifyAsync`. The default is `All`.

```csharp
// Only what happens to scenes and inputs:
await client.ConnectAsync(
    password: "your-password",
    eventSubscription: EventSubscriptions.Scenes | EventSubscriptions.Inputs);

// Later, add the outputs (stream, recording, ...). The new value replaces the
// old one, so name everything you want:
await client.ReidentifyAsync(
    EventSubscriptions.Scenes | EventSubscriptions.Inputs | EventSubscriptions.Outputs);
```

| Subscription | Events |
| --- | --- |
| `General` | `ExitStarted`, `CustomEvent` |
| `Config` | `CurrentSceneCollectionChanging`, `CurrentSceneCollectionChanged`, `SceneCollectionListChanged`, `CurrentProfileChanging`, `CurrentProfileChanged`, `ProfileListChanged` |
| `Scenes` | `SceneCreated`, `SceneRemoved`, `SceneNameChanged`, `CurrentProgramSceneChanged`, `CurrentPreviewSceneChanged`, `SceneListChanged` |
| `Inputs` | `InputCreated`, `InputRemoved`, `InputNameChanged`, `InputSettingsChanged`, `InputMuteStateChanged`, `InputVolumeChanged`, `InputAudioBalanceChanged`, `InputAudioSyncOffsetChanged`, `InputAudioTracksChanged`, `InputAudioMonitorTypeChanged` |
| `Transitions` | `CurrentSceneTransitionChanged`, `CurrentSceneTransitionDurationChanged`, `SceneTransitionStarted`, `SceneTransitionEnded`, `SceneTransitionVideoEnded` |
| `Filters` | `SourceFilterListReindexed`, `SourceFilterCreated`, `SourceFilterRemoved`, `SourceFilterNameChanged`, `SourceFilterSettingsChanged`, `SourceFilterEnableStateChanged` |
| `Outputs` | `StreamStateChanged`, `RecordStateChanged`, `RecordFileChanged`, `ReplayBufferStateChanged`, `VirtualcamStateChanged`, `ReplayBufferSaved` |
| `SceneItems` | `SceneItemCreated`, `SceneItemRemoved`, `SceneItemListReindexed`, `SceneItemEnableStateChanged`, `SceneItemLockStateChanged`, `SceneItemSelected` |
| `MediaInputs` | `MediaInputPlaybackStarted`, `MediaInputPlaybackEnded`, `MediaInputActionTriggered` |
| `Vendors` | `VendorEvent` |
| `Ui` | `StudioModeStateChanged`, `ScreenshotSaved` |
| `Canvases` | `CanvasCreated`, `CanvasRemoved`, `CanvasNameChanged` |
| `InputVolumeMeters`, `InputActiveStateChanged`, `InputShowStateChanged`, `SceneItemTransformChanged` | The event with the same name: see [High-volume events](#high-volume-events). |

## High-volume events

OBS Studio sends four events so often that they are not part of `All`: `InputVolumeMeters`, `InputActiveStateChanged`,
`InputShowStateChanged` and `SceneItemTransformChanged`. Ask for them by name:

```csharp
await client.ReidentifyAsync(
    EventSubscriptions.All | EventSubscriptions.InputVolumeMeters);

client.InputVolumeMeters += (_, e) =>
{
    foreach (var input in e.Inputs)
    {
        Console.WriteLine($"{input.InputName}: {input.GetLevelDb(0):0.0} dB");
    }
};
```

`InputVolumeMeters` is a stream of samples of the levels of every input, so the client raises at most one every 100 milliseconds and
skips the ones in between. The other three events each describe a change of one input or scene item, so none of them is skipped.

## Rules for handlers

- **Handlers run one at a time, in the order in which the events arrived**, on a thread of the thread pool that is not the thread
  that reads from OBS Studio. A slow handler delays the events behind it, and nothing else: the responses to your requests are read
  all the same, so a handler can wait for a response (`client.GetSceneList().Result` does not time out), although an `async` handler
  that awaits it does not hold up the events behind it while it waits:

  ```csharp
  client.CurrentProgramSceneChanged += async (_, e) =>
  {
      try
      {
          SceneItem[] items = await client.GetSceneItemList(e.SceneUuid);
          Console.WriteLine($"{e.SceneName} has {items.Length} items.");
      }
      catch (Exception ex)
      {
          // An exception that leaves an async handler is not seen by
          // the client, and ends the process.
          Console.WriteLine($"Could not read the items: {ex.Message}");
      }
  };
  ```

- **Events queue up in memory while a handler runs.** There is no limit, because dropping an event would lose a change that your
  application has to know about; the client logs a warning when 1000 of them are waiting. Keep handlers short, and do the long work
  in a task of your own.
- **`ConnectionClosed` comes in the same order**: after the events that arrived before the connection ended. Events that are waiting
  when you call `Disconnect()` are still raised, before `ConnectionClosed`. When you call `Dispose()`, what is still waiting is dropped,
  and a notification that is being raised stops before its next handler. `Dispose()` does not wait for a handler that is running and does
  not interrupt it, so a handler can still be running after it returned: a handler that uses something that you dispose together with
  the client has to check for that itself.
- **An event is not guaranteed to be raised before the response of the request that caused it.** `await client.SetCurrentProgramScene("Game")`
  can return before `CurrentProgramSceneChanged` was raised for it; the two are on different threads.
- **Not everything is raised this way.** `PropertyChanged` (for `ConnectionState` and the other properties) and the statistics events
  (`TotalBytesSentChanged` and the others) are raised on the thread that causes the change, which can be the thread that reads from OBS
  Studio: keep those handlers quick, and do not wait for a response from OBS Studio in them.
- **An exception in a handler does not harm the connection.** The client logs it and goes on with the other handlers and the next
  event. (The `try`/`catch` above is for the part of an `async` handler after its first `await`, which the client cannot see.)
- **A user interface has its own thread.** Hand the work over to it (`Dispatcher.Invoke`, `DispatcherQueue.TryEnqueue`,
  `SynchronizationContext.Post`) before you touch a control.
- An event that this version of the library does not know, for example one of a newer obs-websocket, is ignored.

## What OBS Studio sends

The client raises the events that OBS Studio sends as it sends them: it does not combine them, and it drops none of them, with two exceptions
that are explained above: `InputVolumeMeters` is raised at most once every 100 milliseconds (see [High-volume events](#high-volume-events)),
and an event that this version of the library does not know is ignored. The live tests (see
[CONTRIBUTING](https://github.com/tinodo/obsclient/blob/main/CONTRIBUTING.md#live-tests-a-real-obs-studio)) compare what a real OBS Studio
sends with what the client raises, and found these things in OBS Studio 33.0.0 with obs-websocket 5.7.6 (on Windows) and in OBS Studio 32.2.0 with
obs-websocket 5.7.4 (on Linux), which are worth knowing:

- **`SceneCreated` arrives twice for every scene that is created** (in both versions), with the same data (the same `SceneUuid`) both times.
  The two do not follow each other: in the runs with OBS Studio 33.0.0 the `SceneListChanged` of the same change arrived in between, and the
  second `SceneCreated` came 0.7 to 25 milliseconds after the first, so ignoring an event that equals the previous one does not catch it. A
  handler that adds the scene to a list of yours has to check whether it is in the list already (go by the `SceneUuid`), and that check does
  no harm when a later OBS Studio sends the event once. Going by the source, this comes with obs-websocket 5.7 (OBS Studio 32.1.0 and later),
  and OBS Studio 32.0.x, with obs-websocket 5.6.3, is not affected. It is reported to obs-websocket as
  [obs-websocket#1364](https://github.com/obsproject/obs-websocket/issues/1364). No other event was sent twice for one change in the live
  tests.
- **`SceneCreated` can arrive before the scene is in the list that `GetSceneList` returns**: OBS Studio announces a scene a moment before its
  own list has it. Use the data of the event, and not a request that you send because of it, to learn what was created.
- **`SceneRemoved` can arrive while `GetSceneList` still lists the scene**, for a moment (it was seen once in the live tests, with OBS Studio
  32.2.0 on Linux, and not in the runs after it): OBS Studio announces the removal of a scene a moment before its own list has let go of it
  (in the same way as it announces a scene before its list has it). Go by the event.
- **`InputRemoved` can arrive while `GetInputList` still lists the input**, and it can stay in the list for a long time: in the tests an
  input of a scene that was not on an output was still listed 5 seconds later, and left the list when that scene was removed. Go by the event.
- **`SceneItemRemoved` is sent when an item is removed from a scene** (`RemoveSceneItem`), and not when the scene is removed (the
  protocol says so) or when the input is removed: you get `SceneRemoved` or `InputRemoved` then.

## Custom and vendor events

`BroadcastCustomEvent` sends your data to every client that subscribed to `General`, which receives it as a `CustomEvent`. The data is
what you gave to `BroadcastCustomEvent`, as it was sent:

```csharp
using System.Text.Json;

client.CustomEvent += (_, e) => Console.WriteLine($"Custom event: {e.EventData}");

var data = JsonSerializer.SerializeToElement(new { message = "hello" });
await client.BroadcastCustomEvent(data);
```

A plugin that registered with obs-websocket raises its own events as a `VendorEvent`, with the `VendorName`, the `EventType` and the
`EventData` of the plugin. It needs the `Vendors` subscription.
