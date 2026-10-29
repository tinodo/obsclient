# Security Policy

## Supported Versions

Only the latest published release of `OBSClient` on [NuGet](https://www.nuget.org/packages/OBSClient) is supported
with security fixes. Please upgrade to the latest version before reporting a vulnerability.

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Instead, use GitHub's private vulnerability reporting for this repository:
[Report a vulnerability](https://github.com/tinodo/obsclient/security/advisories/new).

Please include:

- A description of the vulnerability and its potential impact.
- Steps to reproduce, or a minimal repro project.
- The version of `OBSClient` and the target framework/OS you tested against.

We'll do our best to acknowledge your report promptly and keep you updated as we investigate and address the issue.

## Scope notes

`OBSClient` is a WebSocket client that connects to a local or remote OBS Studio instance over the
[obs-websocket](https://github.com/obsproject/obs-websocket) protocol. It does not itself expose a network service;
most security-relevant concerns involve how connection credentials (the OBS WebSocket password) and received data
are handled by the client and by consuming applications.

## Verifying a release

The package and the test app of every release are built by the `publish.yml` workflow of this repository, on GitHub-hosted runners, and
have a signed [build provenance attestation](https://docs.github.com/en/actions/concepts/security/artifact-attestations): GitHub's
statement of which workflow built the file, from which commit. The libraries inside the package have one as well. Check a file you
downloaded from a [release](https://github.com/tinodo/obsclient/releases) with the [GitHub CLI](https://cli.github.com/):

```shell
gh attestation verify OBSClient.4.0.0.nupkg --repo tinodo/obsclient --signer-workflow tinodo/obsclient/.github/workflows/publish.yml --source-ref refs/tags/v4.0.0
```

nuget.org signs the package that it serves, so the `.nupkg` that a restore downloads from nuget.org is not byte for byte the file of
the release page, and does not verify. The libraries in it are the same, so run the command on
`~/.nuget/packages/obsclient/<version>/lib/net8.0/OBSClient.dll` (or `net10.0`) to check the library that your application loads.
The notes of every release repeat this with its version. The attestation is also an asset of the release, `OBSClient.<version>.intoto.jsonl`
(a Sigstore bundle, one attestation for all the files): add `--bundle OBSClient.4.0.0.intoto.jsonl` to the command and it checks the file
against that one, without asking GitHub for the attestation (it still fetches the keys that check the signature).
The two prereleases that came before it (4.0.0-beta1 and 4.0.0-beta2) have no attestation.
