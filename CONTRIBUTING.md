# Contributing to OBSClient

Thanks for your interest in improving OBSClient, a .NET client for OBS Studio's
[obs-websocket](https://github.com/obsproject/obs-websocket) protocol!

## Before you start

- For bugs or feature requests, please open an issue first using the provided templates so we can discuss the
  approach before you invest time in a PR.
- Check [obs-websocket's protocol docs](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md)
  for the authoritative definition of requests, events and enums. OBSClient aims to track the latest obs-websocket
  protocol version.

## Development setup

- .NET SDK 8.0 and 10.0 (the library multi-targets both; see `OBSClient/OBSClient.csproj`).
- `dotnet test OBSClient.Tests/OBSClient.Tests.csproj` restores and builds the library and runs the tests. It works on
  Windows, Linux and macOS, and is what CI runs on each of them.
- `dotnet build OBSClient.sln` builds everything, including the WinForms sample (`SampleWindowsAppliation`), so it needs
  Windows; on Linux and macOS it fails with NETSDK1100. CI builds the sample on its own Windows job.

The test suite runs against an in-process fake obs-websocket server (`OBSClient.Tests/TestSupport`), so you do not
need a running copy of OBS Studio to run or write most tests.

### Keeping up with obs-websocket

Most of the client is generated from obs-websocket's own protocol definition, so that it cannot drift from the protocol:

| Input (under `tools/protocol`) | What it is |
| --- | --- |
| `obs-websocket-protocol.json` | A copy of obs-websocket's `docs/generated/protocol.json`. |
| `overlay.toml` | The decisions that the definition cannot make for the client: the names of response and event types, the C# type of a field (the protocol only says `String`, `Number`, `Object`, ...), nullability, patches where upstream's documentation does not match what obs-websocket does (checked in its source), the ordinals of the `RequestType` and `EventType` members, and the few requests that are written by hand. |
| `generate.py` | Writes the C# under `OBSClient/Generated` (the method of `ObsClient` and the builder of `RequestBatchMessage` for each request, the response and event types, the registries and the two enums) and `OBSClient.Tests/Protocol/protocol.schema.json`, the snapshot the tests compare with. |

The generated files are committed and compiled into `OBSClient.dll`; **do not edit them**, change the inputs and run
`python tools/protocol/generate.py` (Python 3.11 or later, standard library only). CI runs it with `--check` and fails when a
generated file is not what the generator produces, and runs the tests of the conventions it follows
(`python -m unittest discover -s tools/protocol -p "test_*.py"`). The definition, the overlay and the generator are inputs of the build: none
of them is part of the NuGet package, which stays a single assembly (`tools/verify-package.py` and `PackagingTests` check that).

The tests in `OBSClient.Tests/Conformance` call every request method of `ObsClient` and every batch builder of
`RequestBatchMessage` (an object that is identified by name and by UUID is tried both ways), check that each sends the request it
is named after and compare what is sent with the snapshot, answer each request with the response the snapshot documents for it, and
send every event the way the snapshot documents it. A missing request, a field that cannot be sent, a mistyped field name, a wrongly
registered response, a method wired to another request, or a response or event the client cannot read fails the build; that
includes the parts that are written by hand.

When obs-websocket publishes a new version (a scheduled workflow opens an issue when it does, and tries the update):

1. `python tools/protocol/update.py --latest` fetches the definition, records the version, runs the generator and lists the differences.
   (`--dry-run` only reports; `--tag 5.8.0` picks a version.)
2. Build and run the tests. A new request, event or field is already in the client. What the generator cannot decide, it says
   (it stops with the line to add to `overlay.toml`: for example the C# type of a new kind of field, or a reference type for a
   new pair of `<x>Name` and `<x>Uuid` fields), and what it has to leave to you fails a test.
3. Commit the result, together with a line in the CHANGELOG.

How the methods follow from the definition (see `Model.request_params` in `protocol_model.py`): an `<x>Name` field and an `<x>Uuid` field
are one parameter `<x>` of a `SceneRef`, `InputRef` or `SourceRef`; `canvasUuid` is the last parameter; a field that is optional is
`T? x = null` and is left out of the request when null; a response with one field is returned as that value and one with
several as its type. A request that the client offers in another shape (`TriggerHotkeyByKeySequence`, `Sleep`) is listed under
`[manual]` in the overlay and written by hand in `ObsClient_ManualRequests.cs` and `RequestBatchMessage_ManualRequests.cs`;
convenience methods that are not requests of the protocol (`SetInputVolumeDb`, the projector shortcuts) are in `ObsClient_Shortcuts.cs`.

One rule that is easy to forget: the numeric values of the public enums are embedded in assemblies that use them. The generator
keeps the values of the members of `EventType` and `RequestType` (they are listed in `overlay.toml`; a new member gets the next
number) and `EnumStabilityTests` guards them; when you add a member to another public enum by hand, add it **at the end**.

## Making changes

- Keep changes focused; avoid unrelated refactors in the same PR.
- Follow the existing code style (see `.editorconfig`) and the patterns already used in neighboring files.
- Add or update XML doc comments for any public API you touch.
- Add tests for new behavior and for bug fixes (a regression test that fails before your fix and passes after it).
- Update `README.md` if you change public API surface or behavior.

## Pull requests

- CI (build, test, CodeQL) must pass before a PR can be merged.
- GitHub Copilot automatically reviews pull requests in this repository; please address or respond to its comments.
- Describe what changed and why; link the related issue if any.

## Releasing (maintainers)

1. Bump `<Version>` in `OBSClient/OBSClient.csproj`.
2. Merge to `main`.
3. `git tag vX.Y.Z && git push origin vX.Y.Z` — this triggers `.github/workflows/publish.yml`, which packs once
   and publishes that package to both nuget.org (via Trusted Publishing) and GitHub Packages.
