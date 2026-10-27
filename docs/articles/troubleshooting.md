---
title: Troubleshooting
---

# Troubleshooting

The quickest way to see what is going on is to turn on [debug logging](connecting.md#logging): the client logs every message that it
sends and receives. [The test app](test-app.md) shows the same traffic, and what OBS Studio answers to a request you try by hand.

## `ConnectAsync` returns `false`

It returns `false` when OBS Studio could not be reached, when it did not complete the handshake within 10 seconds, when it refused the
password, or when `Disconnect()` was called in the meantime. Check, in this order:

1. **OBS Studio is running**, and its WebSocket server is enabled: **Tools → WebSocket Server Settings → Enable WebSocket server**.
2. **The port** is the one that is shown there (4455 unless you changed it), and the `hostname` is the machine that runs OBS Studio.
3. **The password** is the server password. With the wrong one, OBS Studio closes the connection with `AuthenticationFailed`.
4. **Nothing else** answers on that port: a service that accepts the connection but is not obs-websocket never completes the handshake.
5. **A firewall** on the machine that runs OBS Studio lets the port through, when you connect from another machine.

To see why a connection ended, look at the close code:

```csharp
client.ConnectionClosed += (_, e) =>
    Console.WriteLine($"{e.WebSocketCloseCode}: {e.WebSocketCloseDescription}");
```

| Close code | Meaning |
| --- | --- |
| `NormalClosure` | The connection was closed on purpose, for example by `Disconnect()`. |
| `EndpointUnavailable` | OBS Studio is going away. |
| `AuthenticationFailed` | The password is wrong. |
| `SessionInvalidated` | The session was ended in OBS Studio (the **Kick** button of the session list). `AutoReconnect` is switched off: obs-websocket requires that a client does not reconnect automatically after this. |
| `NotIdentified`, `MessageDecodeError`, `InvalidDataFieldValue`, ... | OBS Studio refused a message of the client. This should not happen: please [open an issue](https://github.com/tinodo/obsclient/issues) with the debug log. |

## A request throws `TimeoutException`

OBS Studio did not answer within [`RequestTimeout`](xref:OBSStudioClient.ObsClient.RequestTimeout), which is 500 milliseconds by default.

- Some requests take OBS Studio longer, a screenshot of a large source for instance. Raise `RequestTimeout`.
- A batch that sleeps needs a timeout that is longer than all its sleeps: pass `timeOutInMilliseconds` to `SendRequestBatchAsync`.
- **Every request of your program times out after one in an event handler did.** A handler that waits for a response (`.Result`, `.Wait()`)
  blocks the thread that has to read it. See [the rules for handlers](events.md#rules-for-handlers).

## A request throws `ObsClientException`: "Not connected."

The client is not in the `Connected` state: `ConnectAsync` was not called, it returned `false`, or the connection was lost. Watch
[`ConnectionState` and `ConnectionClosed`](connecting.md#watching-the-connection), and use [`AutoReconnect`](connecting.md#reconnecting)
to have the client come back.

## A request throws `ObsResponseException`

OBS Studio refused the request. `ErrorCode` says why and `ErrorMessage` is the comment of OBS Studio. The common ones:

| `ErrorCode` | What it means |
| --- | --- |
| `ResourceNotFound` (600) | The scene, input, filter or other resource does not exist. A name has to match exactly, or use the UUID. |
| `ResourceAlreadyExists` (601) | You created something with a name that is taken. |
| `InvalidResourceState` (604) | The resource is in a state that does not allow the request. |
| `OutputRunning` (500), `OutputNotRunning` (501) | You started an output that already runs, or stopped one that does not. |
| `StudioModeActive` (505), `StudioModeNotActive` (506) | The request only works with Studio Mode off, or on. |
| `MissingRequestField` (300), `InvalidRequestField` (400), `InvalidRequestFieldType` (401), `RequestFieldOutOfRange` (402), `RequestFieldEmpty` (403) | An argument is missing or wrong. |
| `UnknownRequestType` (204) | The obs-websocket of your OBS Studio does not know the request: it is older than the request. |
| `RequestProcessingFailed` (702), `ResourceActionFailed` (701) | OBS Studio tried and failed. The comment tells what. |
| `NotReady` (207) | OBS Studio is not ready yet (for example just after it started). The client tries again by itself, see [timeouts and retries](requests.md#timeouts-and-retries). |

To find out whether your OBS Studio supports a request, ask it:

```csharp
VersionResponse version = await client.GetVersion();
bool canListCanvases = version.AvailableRequests.Contains("GetCanvasList");
```

## An event does not arrive

- Is the event part of what you subscribed to? `ConnectAsync` subscribes to `All`, and `All` does **not** include the four
  [high-volume events](events.md#high-volume-events). See [Choosing the events you receive](events.md#choosing-the-events-you-receive).
- Is it a `VendorEvent`? That needs the `Vendors` subscription, which `All` includes, but a narrower choice does not.
- The Events page of [the test app](test-app.md) lists every event that OBS Studio sends, so you can see whether OBS Studio sends it at all.

## A response is missing a member, or has a value you do not expect

The protocol only describes nested objects in prose, so the client may not know a member that your OBS Studio sends. The
**Comparison** view of [the test app](test-app.md) lists what OBS Studio sent and the client does not return. If you find one, please
[open an issue](https://github.com/tinodo/obsclient/issues).

## Asking for help

[Open an issue](https://github.com/tinodo/obsclient/issues) and tell us:

- the version of OBSClient, of OBS Studio and of obs-websocket (`GetVersion()` returns the last two),
- what you did, and what you expected to happen,
- the debug log of the messages around it. **Remove your password and stream key before you share a log.**
