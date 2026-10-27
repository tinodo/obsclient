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
- `dotnet build OBSClient.sln` builds everything, including the WinUI 3 test app (`OBSClient.TestApp`, see below), so it needs
  Windows and the .NET 10 SDK; on Linux and macOS it fails with NETSDK1100. CI builds the test app on its own Windows job.

The test suite runs against an in-process fake obs-websocket server (`OBSClient.Tests/TestSupport`), so you do not
need a running copy of OBS Studio to run or write most tests.

### Keeping up with obs-websocket

Most of the client is generated from obs-websocket's own protocol definition, so that it cannot drift from the protocol:

| Input (under `tools/protocol`) | What it is |
| --- | --- |
| `obs-websocket-protocol.json` | A copy of obs-websocket's `docs/generated/protocol.json`. |
| `overlay.toml` | The decisions that the definition cannot make for the client: the names of response and event types, the C# type of a field (the protocol only says `String`, `Number`, `Object`, ...), nullability, patches where upstream's documentation does not match what obs-websocket does (checked in its source), the ordinals of the `RequestType` and `EventType` members, and the few requests that are written by hand. |
| `generate.py` | Writes the C# under `OBSClient/Generated` (the method of `ObsClient` and the builder of `RequestBatchMessage` for each request, the response and event types, the registries and the two enums), `OBSClient.TestApp/Generated/ProtocolCatalog.g.cs` (every request and event as data, with the code that sends or observes it, for the test app) and `OBSClient.Tests/Protocol/protocol.schema.json`, the snapshot the tests compare with. |

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
`[manual]` in the overlay and written by hand in `ObsClient_ManualRequests.cs` and `RequestBatchMessage_ManualRequests.cs` (and
given a descriptor for the test app in `OBSClient.TestApp/Catalog/ManualCatalog.cs`; the generator checks that);
convenience methods that are not requests of the protocol (`SetInputVolumeDb`, the projector shortcuts) are in `ObsClient_Shortcuts.cs`.

One rule that is easy to forget: the numeric values of the public enums are embedded in assemblies that use them. The generator
keeps the values of the members of `EventType` and `RequestType` (they are listed in `overlay.toml`; a new member gets the next
number) and `EnumStabilityTests` guards them; when you add a member to another public enum by hand, add it **at the end**.

### The test app

`OBSClient.TestApp` is a WinUI 3 application (Fluent 2: Mica, a navigation pane, theme colors, the type ramp of Windows 11) whose
only purpose is to **try every request and event of the library by hand against a real OBS Studio**. It replaces the old WinForms
sample, and it is not part of the NuGet package.

| Page | What it does |
| --- | --- |
| Connection | Host, port, password and the events to receive (a Reidentify applies a change while connected). Shows the version of OBS Studio and the traffic of the session. |
| Explorer | Every request of the protocol (and the hand-written ones and the shortcuts), grouped like the protocol documentation, with an editor per parameter that suits its type (scenes and inputs are offered from OBS Studio, by name or by UUID). The result is shown three ways: as the client returned it, as OBS Studio sent it, and a **comparison** of the two that lists what OBS Studio sent and the client does not return (a nested member the client does not know, for example), what the client returns and OBS Studio did not send, and members that are another kind of value. A request can also be put in the batch (Ctrl+Enter sends it). |
| Batch | The requests that were put in the batch from the Explorer, in the order they will run (move, remove). Choose how OBS Studio runs them (serial in real time, serial per frame, parallel) and whether it stops at the first failure, send the batch, and see the response of each request the same three ways: as the client returned it, as OBS Studio sent it, and compared. |
| Events | Every event as the client raises it, with the raw message of OBS Studio next to it. |
| Log | Every message that went over the wire (the client logs them at the debug level) and everything else the client reports; export as JSON lines, and export the **observed shapes**: for each response and event, which members were seen and what kind of value each was. |

What it shows is a sample: a member that was not seen may exist, and a hardware dependent feature (the virtual camera, the replay
buffer, a streaming service) only answers when OBS Studio has it. Exports go to `Documents\OBSClient Test App`; passwords and stream
keys are hidden in them unless you say otherwise, but check a file before you share it. The log keeps every message whole, a screenshot
included (up to 40 million characters; the oldest entries go first): the screen leaves a very long string out and shows a note of its
length, the export and the comparison use the whole message. The events of the session are kept from the moment the app starts, so the
Events page has them when it is opened after they were raised.

Build and run it on Windows with the .NET 10 SDK: `dotnet run --project OBSClient.TestApp -p:Platform=x64`. Every release has it as a
download (`OBSClient.TestApp-<version>-win-x64.zip`), and every build of CI publishes it as the artifact `OBSClient.TestApp-win-x64`
(kept for two weeks). Both are unpackaged and self-contained: unzip it and start `OBSClient.TestApp.exe`; nothing else has to be
installed. CI also starts the app with `--smoke-test <log>`, because the XAML of an app is only read when it
runs: the app opens every page, builds, fills in and reads the editor of every request, and then goes the whole way against a
simulated OBS Studio (the fake server of the tests): it connects through its own Connection page, sends requests through the
Explorer, sends a batch, receives events and checks the Log. The pictures it takes of the pages are the artifact
`test-app-smoke-test`, which is how the design can be looked at without running the app.

The requests and events of the app come from `OBSClient.TestApp/Generated/ProtocolCatalog.g.cs`, which the generator writes (see
above), so a new request of a new protocol version shows up in the app by itself. The parts of the app that do not depend on WinUI
(the catalog, the arguments, the comparison of JSON, the export) are also compiled into `OBSClient.Tests`, so they are tested on
every platform.

### Captures of a real OBS Studio

The protocol documents the members of a response and of an event, but only describes nested objects in prose. `OBSClient.Tests/Fixtures/obs-capture-*.json`
hold what a real OBS Studio answered to the `Get` requests and sent as events, and `ObsStudioCaptureTests` reads each of them with the client
and fails when a member that OBS Studio sent is not given back, or became another kind of value. To add a capture (another version of OBS
Studio, another platform, a scene with more in it: scene items, filters, a media source):

1. Start OBS Studio with the WebSocket server on, and put in it what you want captured.
2. `pwsh tools/capture/obs-capture.ps1 -Password <password>` sends read-only requests (no stream key, no screenshots) and listens for events
   for a while. It writes `./obs-capture`, which holds your scenes and your machine and must not be committed (it is in `.gitignore`).
3. `python tools/capture/make-fixture.py obs-capture OBSClient.Tests/Fixtures/obs-capture-<obs version>-<platform>.json` keeps what was
   answered with success (and the events) and replaces what identifies the machine. Read the fixture before you commit it.

A scene with nothing in it has little to capture, so the script can fill one: with `-WithSampleContent` it creates a scene of its own
(`obs-capture-sample`) with scene items, filters, a media source and a transform, changes some of it (which causes events), and removes all
of it again at the end, also when something fails. It only touches what it created, never switches scenes (so nothing reaches a stream or a
recording), and it says what to remove by hand if OBS Studio stops answering before it is done. The events are subscribed to all, including
the high-volume ones; `-MaxEventsPerType` (5) limits how many of each type are kept.

### The documentation site

The site at <https://tinodo.github.io/obsclient/> is built with [DocFX](https://dotnet.github.io/docfx/), which is pinned in
`.config/dotnet-tools.json`.

| Where | What |
| --- | --- |
| `docs/index.md` | The home page. |
| `docs/articles/` | The guides, in Markdown; its `toc.yml` is the table of contents. |
| `docs/api/index.md` | The introduction of the API reference. The reference itself is **generated** from the XML documentation comments in the source of `OBSClient`; the generated YAML is not committed. |
| `docs/template/public/` | The theme: `main.css` and `main.js`, on top of the modern template of DocFX. |
| `CHANGELOG.md` | Copied to the site as its changelog page (the copy, `docs/changelog.md`, is not committed). |

Build it, and look at it, on any platform with the .NET SDKs (8.0 and 10.0) and Python 3:

```shell
python tools/docs/build.py --serve     # builds docs/_site, checks its links, and serves it at http://localhost:8080
```

The build fails on a DocFX warning (such as an `xref:` that cannot be resolved or a link to a file that does not exist) and on a link of the
result that leads nowhere. A pull request that changes `docs/`, the library, the
changelog or the tooling runs the **Docs** workflow, which builds the site and uploads it as the artifact `docs-site`; a push to `main`
publishes it to GitHub Pages (Settings → Pages is set to deploy from GitHub Actions).

Documentation comments (`///`) are documentation: they become the API reference. Say what a member does and returns, say when it throws,
and cross-reference with `<see cref="…"/>`. The comments of the generated code come from the protocol definition; do not edit them, change the
overlay or the generator. When a change alters how the library is used, update the article that explains it too.

## Making changes

- Keep changes focused; avoid unrelated refactors in the same PR.
- Follow the existing code style (see `.editorconfig`) and the patterns already used in neighboring files.
- Add or update XML doc comments for any public API you touch.
- Add tests for new behavior and for bug fixes (a regression test that fails before your fix and passes after it).
- Update `README.md` if you change public API surface or behavior, and the articles in `docs/` if you change how the library is used.

## Pull requests

- CI (build, test, CodeQL) must pass before a PR can be merged.
- GitHub Copilot automatically reviews pull requests in this repository; please address or respond to its comments.
- Describe what changed and why; link the related issue if any.

## Dependency updates

Dependabot (`.github/dependabot.yml`) looks at the GitHub Actions of the workflows and at the NuGet packages (the three projects of
`OBSClient.sln` and the DocFX tool of the documentation) every Monday morning. The routine updates of each ecosystem come in one pull
request: all the actions together, and all the minor and patch updates of the packages together. A major update of a package gets a pull
request of its own, so that it can be read before it is merged; the default limit of five open pull requests per ecosystem is not changed,
because a lower one would let an unmerged major update hold back the routine ones. A release has to be a week old before it is proposed
(the *cooldown*), which keeps a package that was published by mistake, or with malicious code, out of the repository in most cases;
security updates are not delayed. Dependabot alerts and security updates are on.

These are pull requests like any other: CI builds and tests them, and the Docs workflow builds the site when DocFX is updated. Read the
release notes of a major update before you merge it, and be careful with the actions of `publish.yml`: a change to that workflow is only
exercised when a version tag is pushed.

The one dependency of the library, `Microsoft.Extensions.Logging`, is part of what its users get with it. No pull request is opened for a
new major version of `Microsoft.Extensions.*`: moving to one is a decision about the target frameworks, made by hand.

## Releasing (maintainers)

Publishing is one workflow, `.github/workflows/publish.yml`, started by pushing a version tag. It packs **once**, checks that
the package is a single assembly, and pushes that same `.nupkg` to nuget.org and to GitHub Packages.

**Trusted Publishing, in short.** No NuGet API key is stored anywhere. When the nuget.org job runs, GitHub signs a token that says
"this is the workflow `publish.yml` of `tinodo/obsclient`, running in the environment `release`". nuget.org checks that against a
*policy* that the package owner created, and answers with an API key that is valid for one hour. Anything else (another
repository, another workflow, another environment) gets nothing.

### One-time setup

| Where | What | State |
| --- | --- | --- |
| GitHub: Settings, Environments, `release` | Required reviewer (you approve each publish), deployments only from `v*` tags and from `main` | done |
| GitHub: environment secret `NUGET_USER` | The nuget.org **profile name** of the package owner: `tinod`. Not the GitHub name `tinodo`, and not an e-mail address | done |
| nuget.org: Account, Trusted Publishing, Add policy | Repository Owner `tinodo`, Repository `obsclient`, Workflow File `publish.yml` (file name only, not the path), Environment `release`. Allow pushing new versions of existing packages, for the package `OBSClient` only | **only the package owner can do this** |

A policy for a public repository is active at once. The package ID `OBSClient` is already yours, so nothing has to be reserved.

### Each release

1. Bump `<Version>` in `OBSClient/OBSClient.csproj` (and the version in the *Installation* of the README), add the CHANGELOG entry, and merge to `main` with a pull request.
2. `git switch main && git pull && git tag vX.Y.Z && git push origin vX.Y.Z` (the tag has to equal `<Version>`).
3. Open the run in the Actions tab. The *Pack* job checks the tag, builds, packs and uploads the package as the artifact
   `nuget-package`; download it if you want to look at it. The *Publish to NuGet.org* job then waits for you: **Review
   deployments**, **Approve and deploy**. GitHub Packages does not wait.
4. nuget.org validates the package before it is listed; the version shows up after a few minutes.
5. Create the GitHub release. This step is manual. The release is titled with the tag, is a prerelease when the version has a suffix, and has
   the notes (what is new, what breaks, and the SHA-256 of both assets) and two assets: the package that was published (the artifact
   `nuget-package` of the Publish run) and the test app as `OBSClient.TestApp-X.Y.Z-win-x64.zip` (the artifact `OBSClient.TestApp-win-x64`
   of the Build and Test run of the tagged commit on `main`, which is kept for 14 days; the artifact has some 400 files in its root, so zip
   them again in a folder of that name). Create it as a draft, look at it, and publish it:

   ```shell
   gh release create vX.Y.Z --draft --prerelease --verify-tag --title vX.Y.Z --notes-file notes.md OBSClient.X.Y.Z.nupkg OBSClient.TestApp-X.Y.Z-win-x64.zip
   gh release view vX.Y.Z
   gh release edit vX.Y.Z --draft=false
   ```

   (`--prerelease` only when the version has a suffix.)

**Prereleases.** A version with a suffix (`4.0.0-beta1`, tag `v4.0.0-beta1`) goes through the same workflow. nuget.org lists it as a
prerelease, which `dotnet add package OBSClient` and the NuGet UI skip unless they are asked to include prereleases, so what people
get by default stays the latest stable version. A version cannot be reused: a fixed beta is `4.0.0-beta2`, and the stable release is `4.0.0`.

### When it fails

Re-run the failed jobs in the Actions tab; it runs the workflow as it was at the tag. Never delete and re-tag.

| Symptom | Cause |
| --- | --- |
| `NuGet/login` fails with 403 | The job lacks `id-token: write` (it has it), or `NUGET_USER` is wrong: it must be the profile name `tinod` |
| No matching policy | The policy on nuget.org differs from the workflow: owner, repository, file name (`publish.yml`) or environment (`release`) |
| Push is unauthorized | The policy is not owned by the account that owns the package, or its scope does not allow new versions of `OBSClient` |
| The job never starts | It is waiting for your approval |
| The pack job refuses the tag | The tag is not `vX.Y.Z` (or `vX.Y.Z-suffix`), differs from `<Version>`, or is not on `main` |

A published version is **permanent**: nuget.org lets you unlist or deprecate it, never delete or replace it. If a release is wrong,
publish a fixed one.
