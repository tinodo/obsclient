# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
For history before 3.1.0, see [GitHub Releases](https://github.com/tinodo/obsclient/releases).

## [3.1.0] - Unreleased

### Added
- Support for the **Canvases** category added in obs-websocket 5.7.0 (multi-canvas support): `GetCanvasList`
  request, `Canvas`/`CanvasFlags`/`CanvasVideoSettings` classes, `CanvasCreated`/`CanvasRemoved`/`CanvasNameChanged`
  events, and the `EventSubscriptions.Canvases` flag (now included in `EventSubscriptions.All`).
- `canvasUuid` overloads on scene, scene item, filter and source requests that can be scoped to a non-main canvas
  (e.g. `GetSceneList(Guid canvasUuid)`, `CreateScene(string sceneName, Guid canvasUuid)`).
- `net10.0` as an additional target framework, alongside `net8.0`.
- A test project (`OBSClient.Tests`) with an in-process fake obs-websocket server, covering connection lifecycle,
  authentication, event dispatch, and request/response handling.
- CI: cross-platform build & test matrix, CodeQL analysis, and a tag-triggered publish pipeline to nuget.org
  (via Trusted Publishing) and GitHub Packages.

### Fixed
- `ConnectionState` could get permanently stuck at `Connected` after the underlying connection was lost without a
  clean WebSocket close handshake (e.g. OBS Studio closing abruptly, or a dropped network link). This also
  prevented `AutoReconnect` from ever triggering. ([#17](https://github.com/tinodo/obsclient/issues/17),
  related to the symptoms in [#15](https://github.com/tinodo/obsclient/issues/15))
- An OBS Studio event type not yet known to this client version (for example a newly introduced event from a
  future obs-websocket version) would throw during deserialization and could silently kill the receive loop.
  Unrecognized event types now safely map to `EventType.Unknown` and are ignored. This is the same class of bug
  previously reported in [#12](https://github.com/tinodo/obsclient/issues/12), for an event type that is missing
  rather than misnamed.
- `ConnectionClosed` did not fire after a deliberate `Disconnect()` call, because the event was (incorrectly)
  raised on the already-cancelled receiver `CancellationToken`.
- `Scene.SceneIndex` is now nullable (`int?`): scenes on a non-main canvas have no ordered index and OBS Studio
  returns `null` for them, which previously could not be deserialized.
- `Scene` now exposes `SceneUuid`, which OBS Studio has always returned but was not previously mapped.

### Changed
- `Scene`'s constructor gained a required `sceneUuid` parameter (see above). Code that deserializes `Scene` from
  OBS Studio is unaffected; code that constructs `Scene` instances directly will need to pass a UUID.
