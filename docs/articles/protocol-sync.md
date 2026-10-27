---
title: Staying in sync with obs-websocket
---

# Staying in sync with obs-websocket

A client library is only as good as its match with the protocol. 3.x was written by hand and lagged behind: its README claimed
obs-websocket 5.5.2 while 5.7.x was out, and it had requests that could never succeed. 4.0 is built so that this cannot happen
unnoticed.

## The protocol, 5.5.2 to 5.7.6

| obs-websocket | Change | In OBSClient |
| --- | --- | --- |
| 5.5.3 – 5.5.6 | No changes to the protocol definition. | – |
| 5.6.0 | `Get/SetInputDeinterlaceMode`, `Get/SetInputDeinterlaceFieldOrder`; `inputKindCaps` on the `InputCreated` event. | The requests existed, but `GetInputDeinterlaceFieldOrder` was broken and had no batch builders; `InputKindCaps` is new. |
| 5.7.0 | Canvases: `GetCanvasList`, the `CanvasCreated`, `CanvasRemoved` and `CanvasNameChanged` events, the `Canvases` event subscription, and a `canvasUuid` on the requests that look up a scene or a source by name. | New. |
| 5.7.1 | `canvasName` was dropped again in favour of `canvasUuid`; `canvasUuid` was added to `CreateScene`. | Only `canvasUuid` is supported. |
| 5.7.2 – 5.7.6 | No changes to the protocol definition. | – |

## Generated from the protocol

The requests, the responses, the events and the request and event enums of the client are **generated** from obs-websocket's own
machine-readable definition,
[`protocol.json`](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.json), and from the few decisions
that the definition cannot make for a C# client (the names of response types, the C# type of a field, a place where upstream's
documentation does not match what obs-websocket does). The generated C# is committed and compiled into the library like any other
source. The definition, the decisions and the generator are inputs of the build and are **not** part of the package: OBSClient is one
assembly, with nothing to ship next to it.

Because the XML documentation of the generated members comes from the protocol definition too, the API reference on this site carries the
descriptions of the protocol itself.

## A test suite that checks the match

Every build runs *conformance tests* that:

- call **every request method** and every batch builder, and check that each sends the request it is named after;
- compare what is sent on the wire, field by field, with a snapshot of the protocol definition;
- answer each request with the response that the protocol documents for it, and send every event the way the protocol documents it;
- check the numeric values of the enums, which are embedded in the programs that use them;
- read captures of **what a real OBS Studio answered and sent**, and fail when a member that OBS Studio sent is not given back. This is
  how the nested objects are checked, which the protocol only describes in prose.

A missing request, a field that cannot be sent, a mistyped name, a wrongly registered response, or a response the client cannot read
fails the build.

## A new version of obs-websocket

A scheduled workflow compares, every week, the newest obs-websocket release with the version the client is generated from. When there
is a newer one it tries the update on a clean checkout (it regenerates the code, builds and runs the tests) and opens an issue with
the differences and the outcome.

Applying the update is one command, and the compiler and the tests say what, if anything, needs a decision:

```shell
python tools/protocol/update.py --latest
```

New requests, events and fields need no hand-written code. See [CONTRIBUTING](https://github.com/tinodo/obsclient/blob/main/CONTRIBUTING.md#keeping-up-with-obs-websocket)
for the details.

## Found a gap?

If a request, an event or a member of a response is missing or wrong, [open an issue](https://github.com/tinodo/obsclient/issues). The
Comparison view of [the test app](test-app.md) shows what OBS Studio sends next to what the client returns, which is the best evidence to
add.
