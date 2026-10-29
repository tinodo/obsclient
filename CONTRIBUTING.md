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
- `samples/ConsoleSample` is a small program that uses the library (`dotnet run --project samples/ConsoleSample`, with OBS Studio running).
  CI also builds it against the package that was just packed, for both target frameworks, the way a consumer of the package does: that is
  what shows that the package can be used.
- The icon of the package, `OBSClient/icon.png`, is drawn by `python tools/make-icon.py` (it needs Pillow) from the same drawing as the logo of
  the site, `docs/images/logo.svg`: change them together. `tools/verify-package.py` checks that the package has it, and that it is a PNG that
  nuget.org accepts: a whole file (the checksums of its chunks, chunks in the order, the number and the size that PNG asks for, a valid header, palette
  and transparency, and image data that is as long as its header says, row by row), of at most 1 MB and at least 128 by 128 pixels. It has to be
  a plain image: besides the chunks that every PNG has it may only have `tRNS`, `sRGB`, `gAMA` and `pHYs`, and no metadata (text, colour profiles,
  times, ...), so save it without metadata; `tools/make-icon.py` writes none. The pixels are not decoded: look at the icon when you change it.
- The README is also the page of the package on nuget.org, which renders it with Markdig and with less than GitHub does: it drops a relative link
  (the page shows the text without a target) and shows raw HTML as text. So the README uses absolute links
  (`https://github.com/tinodo/obsclient/blob/main/CONTRIBUTING.md`) and Markdown only (`**bold**`, not `<b>`). `ReadmeTests` parse it with Markdig as
  well, so the parser decides what is a link, what is HTML and what is code, and fail on a relative link or image, a link without a target and raw
  HTML; `tools/verify-package.py` checks that the package has the README. A published version cannot be changed, so a mistake in the README stays
  on nuget.org until the next version.

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

### Live tests (a real OBS Studio)

The tests in `OBSClient.Tests/Live` talk to a real OBS Studio, which is what the other tests can only imitate. For every `Get` request that needs
no parameters they compare what OBS Studio sent with what the client gives back, member by member and value by value; they create, change, rename, switch to and remove a scene with
an input and check that every event is raised as OBS Studio sent it; and they try what only a real server shows: a wrong password, a handler
that waits for the answer of another request, a batch that is cancelled or that fails, and `Reidentify`. They are **skipped, quietly,** unless
`OBS_LIVE_PORT` is set, so `dotnet test` and CI do not need OBS Studio.

`pwsh tools/live/run-live-tests.ps1 -ObsPath <the folder of an OBS Studio>` runs them. Several versions of OBS Studio can be installed (and
running) on one machine, and the arguments of a start decide which configuration, with the scenes in it, an instance uses, so the script is strict:

- It copies the programs of that OBS Studio to `%LOCALAPPDATA%\obsclient-live` (without its configuration: the original is only read) and starts
  **the copy, always with `--portable`**, with a new configuration for every run. It never starts the virtual camera.
- The copy gets a WebSocket port of its own: a free one, never the default 4455 that another instance may use. The script checks that the copy
  is the only process that listens on it, and it stops only the process that it started.
- One run at a time uses that folder: the script holds a lock on it (`.lock`), and a second run says so and ends, instead of stopping the
  OBS Studio of the first one and replacing its files. A crash leaves nothing to clean up; give a second run another `-CacheDirectory` to
  have two at once.
- The tests that create and remove scenes and inputs need `OBS_LIVE_ALLOW_CHANGES=1` **and** `OBS_LIVE_COLLECTION`, the name of the scene
  collection of the OBS Studio of the run, and refuse to change an OBS Studio that has another scene collection as its current one (a test checks
  that). The script names the scene collection after the run. What a test creates is named `obsclient-live-…` and removed again.

To point the read-only tests at an OBS Studio of your own, set `OBS_LIVE_PORT` and `OBS_LIVE_PASSWORD` (and `OBS_LIVE_HOST`); the tests that
change something stay skipped. `-WebSocketDebug` has the copy of OBS Studio log every message of its WebSocket server, to see what it did.

`.github/workflows/live-obs.yml` runs the same tests on Linux in CI: OBS Studio from the PPA of the OBS project, in a virtual display (Xvfb),
with its own configuration and port (the runner is thrown away, so nothing is at risk there). It is experimental and **not a required
check**. It runs when the client, its tests, the live tests or the workflow change, every week, and on request (*Actions, Live OBS tests, Run workflow*); a scheduled run
that fails opens an issue (or adds to the open one). The log of OBS Studio is an artifact of the run.

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
- Start closed: a new class is `sealed`, and a new type is `internal` unless an application needs it. Opening one later breaks nobody, but a
  type that is public cannot be made internal, and a class that others can derive from cannot be sealed, without breaking someone. The
  tests in `OBSClient.Tests/Unit/PublicApiTests.cs` fail for a class that is not sealed, for a public message or interface that is not
  on their list, and for a public type that nothing in the API uses; one that should be public or open on purpose is added to them in the
  same pull request, with the reason.

## Pull requests

- The checks of `main` must pass before a PR can be merged: *Build & Test* on Ubuntu and Windows, *Build Test App (WinUI 3)*, *CodeQL* and
  *Pack (single assembly)*, on a branch that is up to date with `main`. Every conversation on the pull request, the comments of the Copilot
  review included, has to be resolved. The other checks (macOS, the docs build, the live tests) are not required, but read them: they are
  there for a reason.
- GitHub Copilot automatically reviews pull requests in this repository; please address or respond to its comments.
- Describe what changed and why; link the related issue if any.

## Dependency updates

Dependabot (`.github/dependabot.yml`) looks at the GitHub Actions of the workflows and at the NuGet packages (the three projects of
`OBSClient.sln` and the DocFX tool of the documentation) every Monday morning. The routine updates of each ecosystem come in one pull
request: all the actions together, and all the minor and patch updates of the packages together. A major update of a package gets a pull
request of its own, so that it can be read before it is merged; the default limit of five open pull requests per ecosystem is not changed,
because a lower one would let an unmerged major update hold back the routine ones. A package or action release has to be a week old before it
is proposed (the *cooldown*), which keeps one that was published by mistake, or with malicious code, out of the repository in most cases.
Security updates are not delayed. Dependabot alerts and security updates are on.

**The actions are pinned.** Every `uses:` in `.github/workflows` is the full commit SHA of a release, with the version in a comment
(`uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1`), and the repository setting *Require actions to be pinned to a
full-length commit SHA* makes a workflow fail when one is not. A tag of an action can be moved to other code, a commit cannot, which matters most
for `publish.yml`: its jobs hold the token that becomes a nuget.org API key. When you add or change a step, look the SHA up with
`gh api repos/OWNER/REPO/commits/vX.Y.Z --jq .sha` (the tag in the comment has to point at it); Dependabot keeps both the SHA and the comment
current, so every release of an action, also within a major version, comes as a pull request.

These are pull requests like any other: CI builds and tests them, and the Docs workflow builds the site when DocFX is updated. Read the
release notes of a major update before you merge it, and be careful with the actions of `publish.yml`: a change to that workflow is only
exercised when a version tag is pushed, or by a dry run (see *Releasing*), so do a dry run after you merge an update of one of its actions.

The one dependency of the library, `Microsoft.Extensions.Logging.Abstractions`, is part of what its users get with it, so it is asked for at
the oldest version that has what the library uses (8.0.0): a library that asks for a newer one makes every application that uses it
update too. No version update is proposed for it, whatever the version (a security update still is, see `.github/dependabot.yml`);
raise it by hand when the library needs something that a newer version has.

## Repository settings (maintainers)

These settings are not in the repository, so this table is where they are written down: if you change one, change the table too (`gh api
repos/tinodo/obsclient/rulesets` and `gh api repos/tinodo/obsclient/actions/permissions/workflow` show what is set).

| Where | What | Why |
| --- | --- | --- |
| Settings, Rules: *Require CI and Copilot review on main* | A pull request is required (no approval: there is one maintainer). The checks listed under *Pull requests* must pass on a branch that is up to date, and the conversations must be resolved. Copilot reviews every push. `main` cannot be deleted or force-pushed. The owner can bypass the rules | `main` only gets what CI has built, tested and scanned. The bypass is for a broken pipeline, not for a pull request whose checks have not passed |
| Settings, Rules: *Release tags are immutable* | The tags `v*` cannot be deleted or moved. The owner can bypass, which the recovery under *When it fails* needs | A tag names what was published and what the release was built from: the publish workflow checks that it did not move |
| Settings, Actions, General | The token of a workflow is read-only by default (each workflow and job asks for what it needs with `permissions:`), a workflow cannot approve a pull request, and an action must be pinned to a full-length commit SHA | A workflow, or an action in it, only gets what it asks for, and cannot approve its own pull request |
| Settings, General | The head branch of a pull request is deleted when it is merged. Squash, merge and rebase are all allowed | No stale branches |
| Settings, Code security | Dependabot alerts and security updates, secret scanning with push protection, and private vulnerability reporting are on | See `SECURITY.md` and *Dependency updates* |
| Settings, Pages | Source: GitHub Actions | The *Docs* workflow deploys the site |
| Settings, Environments | `release` (see *Releasing*) and `github-pages` | |

## Releasing (maintainers)

Publishing is one workflow, `.github/workflows/publish.yml`, started by pushing a version tag. It checks the version and the
changelog, packs **once** (the package, and next to it its symbols), checks that the package is a single assembly, builds the test app
and writes the notes of the release. Then it waits for your approval, pushes that same `.nupkg` to nuget.org and, once that lists it, to
GitHub Packages, and creates the GitHub release with the notes, the package and the test app.

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

1. Bump `<Version>` in `OBSClient/OBSClient.csproj` (and the version in the *Installation* of the README), write the section of that
   version in `CHANGELOG.md` and give it its date (`## [4.0.0-beta2] - 2026-11-02`), and merge to `main` with a pull request. That
   section is the text of the GitHub release: what is new, what breaks, what is fixed. A version without a section that has a date is
   refused by the first job of the workflow, before anything is built.
2. Optional, and worth it after a change of the workflow or of one of its actions: a **dry run**. Actions, *Publish Package*, *Run
   workflow*, pick the branch (or `main`), tick *Dry run*. It does everything up to the approval, for what is on that branch: it checks
   the version and the changelog, packs, builds the test app, shows the release as it will be made in the summary of the run, and makes a
   draft release to prove that one can be made (which is deleted again). It publishes nothing and needs no tag.
3. `git switch main && git pull && git tag vX.Y.Z && git push origin vX.Y.Z` (the tag has to equal `<Version>`).
4. Open the run in the Actions tab. The *Check the version*, *Pack*, *Build the test app* and *Prepare the release* jobs run first (the
   package and the test app are artifacts of the run; download them if you want to look). The *Publish to NuGet.org* job then waits for
   you: **Review deployments**, **Approve and deploy**. That one approval is the gate of every channel: GitHub Packages and the release
   follow once nuget.org lists the version.
5. The *Publish to NuGet.org* job checks that the tag still points at the commit that was packed, pushes the package, **waits until
   nuget.org lists the version** (it validates a package first, which takes a few minutes), **checks that the package that nuget.org
   serves is the one that was packed** (`tools/compare-published.py`: file by file, nuget.org adds only its signature), and then pushes
   the symbols. *Publish to GitHub Packages* follows. When both are done, the *Create the GitHub release* job makes the release: titled
   with the tag, a prerelease when the version has a suffix, with the notes, two files, `OBSClient.X.Y.Z.nupkg` (the package that was
   published) and `OBSClient.TestApp-X.Y.Z-win-x64.zip` (the test app), and the SHA-256 of both in its text. Have a look at it.

Only one run for a version is active at a time (the workflow has a concurrency group per tag): a second run for the same tag waits for
the first to end, and then stops at the first job, because nuget.org lists the version by then. A run that waits for your approval holds
its turn: cancel it before you start over.

**Prereleases.** A version with a suffix (`4.0.0-beta1`, tag `v4.0.0-beta1`) goes through the same workflow. nuget.org lists it as a
prerelease, which `dotnet add package OBSClient` and the NuGet UI skip unless they are asked to include prereleases, so what people
get by default stays the latest stable version. A version cannot be reused: a fixed beta is `4.0.0-beta2`, and the stable release is `4.0.0`.

**Symbols.** Next to the package the workflow builds `OBSClient.X.Y.Z.snupkg`: the debug symbols (PDB) of each target framework, with
[Source Link](https://learn.microsoft.com/dotnet/standard/library-guidance/sourcelink), so that a debugger can step into the library and
show the source of the commit that was built. nuget.org serves them from its symbol server (Visual Studio: *Tools, Options, Debugging,
Symbols*, tick *NuGet.org Symbol Server*). `tools/verify-package.py` makes sure that the symbol package is there, that it holds nothing
but the PDBs, and that they carry Source Link (and that the package itself has no PDB). The symbols are pushed once the package is
listed, and a failure to push them is a warning, not a failed release.

### When it fails

Re-run the **failed jobs** in the Actions tab (not *all jobs*, and not a new run for the tag): they use the commit and the artifacts of
that run (kept for 90 days), so what is finished is what was built. Never delete and re-tag once something was published; the one case in which a tag
may be moved is in the table (the first job refuses the version, so nothing was published).

| Symptom | Cause |
| --- | --- |
| `NuGet/login` fails with 403 | The job lacks `id-token: write` (it has it), or `NUGET_USER` is wrong: it must be the profile name `tinod` |
| No matching policy | The policy on nuget.org differs from the workflow: owner, repository, file name (`publish.yml`) or environment (`release`) |
| Push is unauthorized | The policy is not owned by the account that owns the package, or its scope does not allow new versions of `OBSClient` |
| The job never starts | It is waiting for your approval |
| The first job (*Check the version*) refuses the release | The tag is not `vX.Y.Z` (or `vX.Y.Z-suffix`), differs from `<Version>`, is not on `main`, or `CHANGELOG.md` has no section for the version, or the section has no date (`Unreleased`). Nothing was published. Fix it on `main`, move the tag to the new commit (`git push origin :refs/tags/vX.Y.Z`, `git tag -d vX.Y.Z`, tag again), and push it |
| The first job says that nuget.org lists the version already | A version is never built again: the package that was published can never be replaced, so a new build could not be matched with it (the symbols, the test app and the files of the release would come from another build). To finish a release that failed after the push, re-run the **failed jobs** of its run |
| *Verify the tag still points at the commit that was packed* fails | The tag was moved while the release waited for your approval. Nothing was published (GitHub Packages and the release wait for nuget.org, so this is the first thing that is checked); release a new version |
| *Verify the tag still points at the commit that was released* fails (in *Create the GitHub release*) | The tag was moved after the package was published. The release was not made. Move the tag back to the commit that the message names (`git push --force origin <commit>:refs/tags/vX.Y.Z`) and re-run the job, or make the release by hand |
| *Wait until NuGet.org lists the version* fails | nuget.org did not list the version within 30 minutes: it may have failed its validation (look at the page of the version; you also get an e-mail). Re-run the job when it is fine |
| *Verify that NuGet.org serves the package that was packed* fails | nuget.org lists this version, but not the package that this run packed: another run, or an earlier attempt for the same version, published first (the push skips a duplicate). The step names the files that differ. Nothing after it ran: no symbols, no GitHub Packages, no release. Find the run that published, and if that package is the wrong one, release a new version (a published version can never be replaced) |
| A warning that the symbols were not pushed | The package is published. Re-run the *Publish to NuGet.org* job: it skips the package as a duplicate and pushes the symbols |
| *Create the GitHub release* fails | Re-run it. It updates a release that exists (a draft too: it is published after its files are uploaded), so it is safe after a release that was made by hand. A release is only made the latest one when its version is the newest stable one |

A published version is **permanent**: nuget.org lets you unlist or deprecate it, never delete or replace it. If a release is wrong,
publish a fixed one.
