---
title: API reference
---

# API reference

This reference is generated from the XML documentation in the source of the library. The documentation of the requests, the responses
and the events comes from the obs-websocket protocol definition, so it says what the protocol says.

The library has [two ways to work with OBS Studio](../articles/two-ways.md): [`ObsClient`](xref:OBSStudioClient.ObsClient), the protocol as it is, and
[`ObsModel`](xref:OBSStudioClient.Model.ObsModel), OBS Studio by name, which is built on it.

## Start here

| Type | What it is |
| --- | --- |
| [`ObsClient`](xref:OBSStudioClient.ObsClient) | The protocol: the connection, every request, every event. |
| [`ObsModel`](xref:OBSStudioClient.Model.ObsModel) | OBS Studio by name: a live model of its scenes, inputs, scene items, filters and outputs, built on `ObsClient`. New, and provisional. |
| [`ObsScene`](xref:OBSStudioClient.Model.ObsScene), [`ObsInput`](xref:OBSStudioClient.Model.ObsInput), [`ObsSceneItem`](xref:OBSStudioClient.Model.ObsSceneItem), [`ObsFilter`](xref:OBSStudioClient.Model.ObsFilter), [`ObsOutputs`](xref:OBSStudioClient.Model.ObsOutputs) | What the model holds, and the methods that change it. |
| [`RequestBatchMessage`](xref:OBSStudioClient.Messages.RequestBatchMessage) | Builds a [request batch](../articles/batches.md). |
| [`EventSubscriptions`](xref:OBSStudioClient.Enums.EventSubscriptions) | Chooses the events to receive. |
| [`SceneRef`](xref:OBSStudioClient.Classes.SceneRef), [`InputRef`](xref:OBSStudioClient.Classes.InputRef), [`SourceRef`](xref:OBSStudioClient.Classes.SourceRef) | A scene, an input or a source, by name or by UUID. |
| [`SceneItemTransformUpdate`](xref:OBSStudioClient.Classes.SceneItemTransformUpdate) | A change to part of the transform of a scene item. |
| [`ObsResponseException`](xref:OBSStudioClient.Exceptions.ObsResponseException), [`ObsClientException`](xref:OBSStudioClient.Exceptions.ObsClientException) | What a failed request throws. |

## Namespaces

| Namespace | Contains |
| --- | --- |
| [`OBSStudioClient`](xref:OBSStudioClient) | `ObsClient`, and the arguments of its `ConnectionClosed` event. |
| [`OBSStudioClient.Model`](xref:OBSStudioClient.Model) | `ObsModel`, the live model of OBS Studio, and what it holds: `ObsScene`, `ObsInput`, `ObsSceneItem`, `ObsFilter`, `ObsOutput` and their collections. |
| [`OBSStudioClient.Classes`](xref:OBSStudioClient.Classes) | The objects of the protocol: scenes, inputs, scene items, canvases, filters, transitions, monitors, and the references (`SceneRef`, `InputRef`, `SourceRef`). |
| [`OBSStudioClient.Enums`](xref:OBSStudioClient.Enums) | The enumerations: `ConnectionState`, `EventSubscriptions`, `RequestStatusCode`, `RequestBatchExecutionType`, `ObsOutputState`, and more. |
| [`OBSStudioClient.Events`](xref:OBSStudioClient.Events) | The arguments of the events of OBS Studio. |
| [`OBSStudioClient.Exceptions`](xref:OBSStudioClient.Exceptions) | `ObsClientException` and `ObsResponseException`, and the exceptions of the model: `ObsNotFoundException`, `ObsNameInUseException`, `ObsRemovedException`, and more. |
| [`OBSStudioClient.Messages`](xref:OBSStudioClient.Messages) | `RequestBatchMessage`, and the messages that are exchanged with OBS Studio. |
| [`OBSStudioClient.Responses`](xref:OBSStudioClient.Responses) | The responses that requests return. |
| [`OBSStudioClient.Interfaces`](xref:OBSStudioClient.Interfaces) | The interfaces of messages and responses. |

## Looking for a request?

Every request of the protocol is a method of `ObsClient` with the name of the request, for example
[`GetSceneList`](xref:OBSStudioClient.ObsClient.GetSceneList*) and [`SetCurrentProgramScene`](xref:OBSStudioClient.ObsClient.SetCurrentProgramScene*).
Use the search box at the top of the page, or open `ObsClient` and look through its methods. [The articles](../articles/getting-started.md)
explain the concepts, and [the test app](../articles/test-app.md) lets you try a request by hand.

## Looking for scenes, inputs and filters by name?

Start with [`ObsModel`](xref:OBSStudioClient.Model.ObsModel): its `Scenes`, `Inputs`, `Outputs` and `ProgramScene` hold the objects, and the objects have the methods that change
them. [Controlling OBS Studio by name](../articles/model.md) explains them.
