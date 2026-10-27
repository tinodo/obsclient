#!/usr/bin/env python3
"""Brings OBSClient up to a version of obs-websocket's protocol.

obs-websocket publishes a machine-readable definition of its protocol (docs/generated/protocol.json). This script
downloads it, shows what changed compared with the version the client is generated from, stores it as
tools/protocol/obs-websocket-protocol.json and runs the generator (tools/protocol/generate.py), which writes the
protocol-derived code under OBSClient/Generated and the snapshot that the conformance tests compare the client with.

    python tools/protocol/update.py --latest
    python tools/protocol/update.py --tag 5.7.6
    python tools/protocol/update.py --input protocol.json --version 5.7.6
    python tools/protocol/update.py --latest --dry-run --summary-file diff.md

Without --dry-run the inputs and the generated code are rewritten, so review the result with git, build, and run the
tests: the conformance tests fail for whatever the hand-written part of the client still has to do. The protocol
definition and the generator are inputs of the build; none of them is part of the NuGet package.

Only the Python standard library is used.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import generate  # noqa: E402
import protocol_model as pm  # noqa: E402

REPOSITORY = "obsproject/obs-websocket"


def http_get(url: str) -> bytes:
    headers = {"User-Agent": "obsclient-protocol-update"}
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if token and "api.github.com" in url:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def latest_tag() -> str:
    tags = json.loads(http_get(f"https://api.github.com/repos/{REPOSITORY}/tags?per_page=100"))
    versions = []
    for tag in tags:
        match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)", tag["name"])
        if match:
            versions.append((tuple(int(part) for part in match.groups()), tag["name"]))
    if not versions:
        raise SystemExit("No release tags found for " + REPOSITORY)
    return max(versions)[1]


def field_map(fields):
    return {f["name"]: f for f in fields}


def describe_field_changes(label, old_fields, new_fields):
    lines = []
    old, new = field_map(old_fields), field_map(new_fields)
    for name in sorted(set(new) - set(old)):
        lines.append(f"- {label}: field `{name}` ({new[name]['type']}) added")
    for name in sorted(set(old) - set(new)):
        lines.append(f"- {label}: field `{name}` removed")
    for name in sorted(set(old) & set(new)):
        if old[name]["type"] != new[name]["type"] or old[name].get("optional") != new[name].get("optional"):
            lines.append(f"- {label}: field `{name}` changed ({old[name]['type']} -> {new[name]['type']})")
    return lines


def describe_diff(old: dict, new: dict) -> list[str]:
    """What changed between two snapshots (see generate.build_snapshot), as markdown list items."""
    lines = []
    for kind, key in (("Request", "requests"), ("Event", "events")):
        for name in sorted(set(new[key]) - set(old[key])):
            lines.append(f"- **New {kind.lower()}** `{name}`")
        for name in sorted(set(old[key]) - set(new[key])):
            lines.append(f"- **Removed {kind.lower()}** `{name}`")

    for name in sorted(set(old["requests"]) & set(new["requests"])):
        before, after = old["requests"][name], new["requests"][name]
        lines += describe_field_changes(f"Request `{name}` (request)", before["request"], after["request"])
        lines += describe_field_changes(f"Request `{name}` (response)", before["response"], after["response"])

    for name in sorted(set(old["events"]) & set(new["events"])):
        lines += describe_field_changes(f"Event `{name}`", old["events"][name], new["events"][name])

    for name in sorted(set(old["enums"]) | set(new["enums"])):
        before, after = old["enums"].get(name, {}), new["enums"].get(name, {})
        for identifier in sorted(set(after) - set(before)):
            lines.append(f"- Enum `{name}`: `{identifier}` added")
        for identifier in sorted(set(before) - set(after)):
            lines.append(f"- Enum `{name}`: `{identifier}` removed")
        for identifier in sorted(set(before) & set(after)):
            if before[identifier] != after[identifier]:
                lines.append(f"- Enum `{name}`: `{identifier}` changed ({before[identifier]} -> {after[identifier]})")

    return lines


def set_recorded_version(version: str) -> None:
    text = pm.OVERLAY_FILE.read_text(encoding="utf-8")
    updated, count = re.subn(r'(?m)^version = "[^"]*"$', f'version = "{version}"', text, count=1)
    if count != 1:
        raise SystemExit("overlay.toml has no [protocol] version to update.")
    pm.OVERLAY_FILE.write_text(updated, encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--tag", help="obs-websocket tag to download protocol.json from, e.g. 5.7.6")
    source.add_argument("--latest", action="store_true", help="use the newest release tag of obs-websocket")
    source.add_argument("--input", help="a local protocol.json (requires --version)")
    parser.add_argument("--version", help="version label to record when using --input")
    parser.add_argument("--dry-run", action="store_true", help="only report the differences, change nothing")
    parser.add_argument("--summary-file", help="also write the differences, as markdown, to this file")
    args = parser.parse_args(argv)

    if args.input:
        if not args.version:
            parser.error("--input requires --version")
        version = args.version
        raw = Path(args.input).read_bytes()
    else:
        version = args.tag or latest_tag()
        raw = http_get(f"https://raw.githubusercontent.com/{REPOSITORY}/{version}/docs/generated/protocol.json")
    upstream = json.loads(raw.decode("utf-8-sig"))

    current = pm.Model.load()
    current_snapshot = generate.build_snapshot(current)
    summary = [f"Differences between obs-websocket {current.version} (what the client is generated from) and {version}:", ""]

    # Try the new version against the overlay as it is, so that the report says what would need a decision.
    try:
        candidate = pm.Model(upstream, current.overlay, version)
        differences = describe_diff(current_snapshot, generate.build_snapshot(candidate))
        summary.append("\n".join(differences) if differences else "- (no schema differences)")
        unassigned = generate.unassigned_ordinals(candidate)
        if unassigned:
            summary += ["", "New members that get the next ordinal in overlay.toml: " + ", ".join(f"`{n}`" for names in unassigned.values() for n in names)]
        generate.generate(candidate)
        summary += ["", "The generator accepts the new version without changes to overlay.toml."]
    except pm.ModelError as error:
        summary += ["", f"**The overlay needs a decision before the code can be generated:** {error}"]
        candidate = None

    text = "\n".join(summary)
    print(text)
    if args.summary_file:
        Path(args.summary_file).write_text(text + "\n", encoding="utf-8")

    if args.dry_run:
        return 0
    if candidate is None:
        print("\nNothing was changed. Fix overlay.toml, then run this script again.")
        return 1

    # Stored as published (apart from a BOM, if any), so that the file can be compared with upstream.
    pm.PROTOCOL_FILE.write_bytes(raw.decode("utf-8-sig").encode("utf-8"))
    set_recorded_version(version)
    print(f"\nWrote {pm.PROTOCOL_FILE.relative_to(pm.ROOT)} and recorded version {version}.")
    return generate.main([])


if __name__ == "__main__":
    sys.exit(main())
