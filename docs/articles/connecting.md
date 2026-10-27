---
title: Connecting to OBS Studio
---

# Connecting to OBS Studio

## Connect

```csharp
using OBSStudioClient;
using OBSStudioClient.Enums;

using var client = new ObsClient();

bool connected = await client.ConnectAsync(
    autoReconnect: true,
    password: "your-password",
    hostname: "localhost",
    port: 4455,
    eventSubscription: EventSubscriptions.All);
```

| Parameter | Default | Meaning |
| --- | --- | --- |
| `autoReconnect` | `false` | Keep trying to connect again when the connection is lost, or could not be made. See [Reconnecting](#reconnecting). |
| `password` | `""` | The password of the WebSocket server. Empty connects without authentication. |
| `hostname` | `"localhost"` | The machine that runs OBS Studio. |
| `port` | `4455` | The port of the WebSocket server, 1 to 65535. |
| `eventSubscription` | `All` | The events to receive. See [Handling events](events.md#choosing-the-events-you-receive). |

The client connects over a plain `ws://` connection.

### What the result means

[`ConnectAsync`](xref:OBSStudioClient.ObsClient.ConnectAsync*) completes when the handshake with OBS Studio is done, so you can send a
request as soon as it returns.

| Result | When |
| --- | --- |
| `true` | OBS Studio accepted the session. `ConnectionState` is `Connected`. |
| `false` | OBS Studio could not be reached (an attempt is given up after 2 seconds), accepted the connection but did not complete the handshake within 10 seconds, refused the password, or `Disconnect()` was called in the meantime. |

Nothing is thrown for these: check the result. An exception means that an argument cannot be right: an
`ArgumentOutOfRangeException` when `port` is not between 1 and 65535, an `ArgumentException` when `hostname` is not a valid host name.

> [!NOTE]
> When the client is not `Disconnected`, `ConnectAsync` does nothing and returns `true`. To connect with other settings,
> call `Disconnect()` and wait until `ConnectionState` is `Disconnected` (it is closed asynchronously) before you connect again.

## The state of the connection

[`ObsClient.ConnectionState`](xref:OBSStudioClient.ObsClient.ConnectionState) is one of:

| State | Meaning |
| --- | --- |
| `Disconnected` | There is no connection. |
| `Connecting` | The connection is being opened. |
| `Authenticating` | The connection is open and the handshake, which includes the authentication, is in progress. |
| `Connected` | OBS Studio has accepted the session: requests can be sent and events are received. |
| `Disconnecting` | The connection is being closed. |

Requests are only sent in the `Connected` state. At any other time a request fails with an
[`ObsClientException`](xref:OBSStudioClient.Exceptions.ObsClientException) (`Not connected.`).

## Watching the connection

`ObsClient` implements `INotifyPropertyChanged`, and raises [`ConnectionClosed`](xref:OBSStudioClient.ObsClient.ConnectionClosed)
when a connection ends, for whatever reason, including a `Disconnect()` of your own.

```csharp
client.PropertyChanged += (_, e) =>
{
    if (e.PropertyName == nameof(ObsClient.ConnectionState))
    {
        Console.WriteLine($"State: {client.ConnectionState}");
    }
};

client.ConnectionClosed += (_, e) =>
    Console.WriteLine($"Closed: {e.WebSocketCloseCode} {e.WebSocketCloseDescription}");
```

A request that is waiting for its response when the connection ends fails at once with an `ObsClientException`; it does not
wait for its timeout. `ConnectionClosed` is raised by the dispatcher of the client, after the events that arrived before the
connection ended (see [Handling events](events.md#rules-for-handlers)). `PropertyChanged` is raised on the thread that causes the
change, which is often the one that reads from the socket, so keep that handler short and do not wait for a response from OBS Studio in it.

## Reconnecting

With `AutoReconnect` set to `true` the client tries to connect again, every 3 seconds, as long as it is `Disconnected`:

- when OBS Studio closed the connection, or OBS Studio itself closed,
- when a message of yours was refused, or the password was wrong,
- when the first attempt of `ConnectAsync` did not succeed.

```csharp
client.AutoReconnect = true;    // can be switched on at any time
```

The events you chose are subscribed to again each time. `Disconnect()` sets `AutoReconnect` to `false`; set it to `true` again to
have the client reconnect.

An OBS Studio user can end your session with the **Kick** button of the session list. The connection then closes with
`WebSocketCloseCode.SessionInvalidated`, and obs-websocket requires that a client does not reconnect automatically after that, so the
client sets `AutoReconnect` to `false` and stays `Disconnected`. Connect again yourself when you want to. (Versions up to and including
4.0.0-beta1 reconnected anyway; that was fixed after beta1.)

## Choosing the events

The `eventSubscription` argument of `ConnectAsync` selects the events that OBS Studio sends. To change the selection of a
connection that is open, call [`ReidentifyAsync`](xref:OBSStudioClient.ObsClient.ReidentifyAsync*). See
[Handling events](events.md#choosing-the-events-you-receive).

## Disconnecting

```csharp
client.Disconnect();   // closes the connection and switches AutoReconnect off
client.Dispose();      // does the same, and releases the client
```

## Logging

The constructor takes an optional `ILogger<ObsClient>`. At the debug level the client logs every message that it sends
(`Sending: …`) and receives (`Received: …`), which is the quickest way to see what goes over the wire. The answer to the
authentication challenge is left out of what is sent.

```csharp
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder
    .AddConsole()
    .SetMinimumLevel(LogLevel.Debug));

using var client = new ObsClient(loggerFactory.CreateLogger<ObsClient>());
```

(`AddConsole` comes from the `Microsoft.Extensions.Logging.Console` package.) With dependency injection, register the client and
the container supplies the logger:

```csharp
builder.Services.AddSingleton<ObsClient>();
```

## Connecting to another machine

Use the host name or the IP address of the machine that runs OBS Studio, and make sure the port of the WebSocket server (4455 by
default) is open in the firewall of that machine.

```csharp
await client.ConnectAsync(
    password: "your-password", hostname: "streaming-pc.local", port: 4455);
```

There is no encryption between the client and OBS Studio, so connect across a network that you trust, or through a tunnel.

## Threads

Requests can be sent from any thread, and at the same time: they take turns writing to the socket, and each response is matched
to its request. The events are raised by a dispatcher of the client, on a thread of the thread pool that is not the one that reads
from the socket; see [Handling events](events.md#rules-for-handlers).
