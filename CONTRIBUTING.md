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

`OBSClient.Tests/Protocol/protocol.schema.json` is a compact snapshot of obs-websocket's own protocol definition
(`docs/generated/protocol.json`). The tests in `OBSClient.Tests/Conformance` call every request method of `ObsClient` and
every batch builder of `RequestBatchMessage`, check that each sends the request it is named after and compare what is
sent with that snapshot, answer each request with the response the snapshot documents for it, and send every event
the way the snapshot documents it. A missing request, a field that cannot be sent, a mistyped field name, a wrongly
registered response, a method wired to another request, or a response or event the client cannot read fails the build.

When obs-websocket publishes a new version (a scheduled workflow opens an issue when it does):

1. `python tools/protocol/update-protocol-snapshot.py --latest` refreshes the snapshot and lists the differences.
2. Run the tests; the conformance tests now fail for everything the client is missing.
3. Implement it, following the patterns of the neighbouring requests, until they pass.

Two rules that are easy to forget:

- The numeric values of the public enums are embedded in assemblies that use them: add new members **at the end**
  of `EventType`, `RequestType` and similar enums, never in the middle (`EnumStabilityTests` guards this).
- The request type of a request is derived from the name of the method that sends it (`[CallerMemberName]`), so a
  method that sends `GetInputMute` has to be called `GetInputMute`, and a batch builder `AddGetInputMuteRequest`.
  A private overload that all the public overloads call carries the same name for that reason.

## Making changes

- Keep changes focused; avoid unrelated refactors in the same PR.
- Follow the existing code style (see `.editorconfig`) and the patterns already used in neighboring files
  (e.g. the `string name` / `Guid uuid` overload pairs used throughout the request classes).
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
