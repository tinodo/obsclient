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
