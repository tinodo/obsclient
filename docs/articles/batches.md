---
title: Request batches
---

# Request batches

A batch sends several requests to OBS Studio in one message, and OBS Studio runs them one after the other (or all at once) and
answers once. That is faster than awaiting each request, and it is the way to run requests that must follow each other without a
gap, or in step with the video frames.

## Build and send a batch

[`RequestBatchMessage`](xref:OBSStudioClient.Messages.RequestBatchMessage) has an `Add…Request` method for every request of the
protocol, with the parameters of the request. [`SendRequestBatchAsync`](xref:OBSStudioClient.ObsClient.SendRequestBatchAsync*) sends
it and returns the result of each request:

```csharp
var batch = new RequestBatchMessage(haltOnFailure: true);
batch.AddSetCurrentProgramSceneRequest("Starting Soon");
batch.AddSetInputMuteRequest("Microphone", true);
batch.AddSleepRequest(5000, null);                   // OBS Studio waits 5 seconds
batch.AddSetInputMuteRequest("Microphone", false);
batch.AddSetCurrentProgramSceneRequest("Gaming Scene");

RequestResponseMessage[] results =
    await client.SendRequestBatchAsync(batch, timeOutInMilliseconds: 7000);
```

The scenes and inputs of a batch are given like those of a [request](requests.md#scenes-inputs-and-sources-names-or-uuids): by name,
by UUID, or as the object OBS Studio gave you.

## The results

A request of a batch that fails **does not throw**: the batch itself was answered. Each request has its own result in the array, in the
order of the requests:

```csharp
foreach (RequestResponseMessage result in results)
{
    RequestStatus status = result.RequestStatus;
    Console.WriteLine(status.Result
        ? $"{result.RequestType}: ok"
        : $"{result.RequestType}: failed, {status.Code} ({status.Comment})");
}
```

| Member | Meaning |
| --- | --- |
| `RequestType` | The request that this is the result of. |
| `RequestStatus` | `Result` (did it succeed), `Code` (a [`RequestStatusCode`](xref:OBSStudioClient.Enums.RequestStatusCode)) and `Comment` (what OBS Studio says about a failure). |
| `ResponseData` | The response as the type that belongs to the request (`VersionResponse` for `GetVersion`, and so on). It says nothing about success: for a request that returns no data it is an `EmptyResponse` even when the request failed, and for a request that returns data it is `null` when the request failed. Check `RequestStatus.Result` first. |
| `RawResponseData` | The data of the response as OBS Studio sent it. |

```csharp
var batch = new RequestBatchMessage();
batch.AddGetVersionRequest();
batch.AddGetStatsRequest();

RequestResponseMessage[] results = await client.SendRequestBatchAsync(batch);

if (results[0].RequestStatus.Result && results[0].ResponseData is VersionResponse version)
{
    Console.WriteLine($"OBS Studio {version.ObsVersion}");
}

if (results[1].RequestStatus.Result && results[1].ResponseData is StatsResponse stats)
{
    Console.WriteLine($"{stats.ActiveFps} frames per second");
}
```

## Stopping at a failure

With `haltOnFailure: true` OBS Studio stops at the first request that fails, so the array holds the results up to and including that
one, and it can be shorter than the batch. Without it, every request runs. `haltOnFailure` applies to the serial execution types.

## How the requests run

The second argument of the constructor is the [execution type](xref:OBSStudioClient.Enums.RequestBatchExecutionType):

| Execution type | How the requests run | Delay with |
| --- | --- | --- |
| `SerialRealtime` (default) | One after the other, as fast as possible. | `AddSleepRequest(sleepMillis, null)` |
| `SerialFrame` | One after the other, in step with the video frames of OBS Studio. Meant for accurate animations. | `AddSleepRequest(null, sleepFrames)` |
| `Parallel` | All at once, on the thread pool of OBS Studio. Experimental, and only of use for requests that need a lot of work, such as a screenshot. | |

An animation that moves a scene item by 10 pixels per frame:

```csharp
var batch = new RequestBatchMessage(
    requestBatchExecutionType: RequestBatchExecutionType.SerialFrame);

for (int step = 1; step <= 30; step++)
{
    var move = new SceneItemTransformUpdate { PositionX = step * 10 };
    batch.AddSetSceneItemTransformRequest("Gaming Scene", sceneItemId, move);
    batch.AddSleepRequest(null, 1);                      // wait one frame
}

await client.SendRequestBatchAsync(batch, timeOutInMilliseconds: 5000);
```

## Timeouts

`SendRequestBatchAsync` waits [`RequestTimeout`](xref:OBSStudioClient.ObsClient.RequestTimeout) milliseconds for the answer unless you give
`timeOutInMilliseconds`. The answer only comes when the whole batch has run, so a batch that sleeps needs a timeout that is longer
than all its sleeps together, or the call throws a `TimeoutException` (OBS Studio still runs the batch).

`SendRequestBatchAsync` also takes a `CancellationToken`, as the last parameter: cancelling it ends the wait and the call throws an
`OperationCanceledException`. A batch that was already sent is not undone, so OBS Studio still runs it.
