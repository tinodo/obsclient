---
applyTo: "OBSClient/Generated/**,OBSClient.TestApp/Generated/**,OBSClient.Tests/Protocol/**"
---
These files are written by `tools/protocol/generate.py` from `tools/protocol/obs-websocket-protocol.json` (obs-websocket's own
protocol definition) and `tools/protocol/overlay.toml`. CI fails when they are not exactly what the generator produces
(`python tools/protocol/generate.py --check`), so do not suggest edits to them. If something in them is wrong, it has to be fixed in
the generator (`tools/protocol/generate.py`, `tools/protocol/protocol_model.py`) or in the overlay; say that in the review comment.
