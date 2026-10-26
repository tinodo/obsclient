#!/usr/bin/env python3
"""Creates a compact snapshot of the obs-websocket protocol for the conformance tests.

The obs-websocket repository publishes a machine-readable description of every request, event and enum of the
protocol (docs/generated/protocol.json). The conformance tests in OBSClient.Tests compare the client against it, so
the client can no longer silently fall behind the protocol. This script keeps only what those tests need (names,
value types and whether a field is optional) which keeps the snapshot small and its diffs readable.

Examples:
    python tools/protocol/update-protocol-snapshot.py --latest
    python tools/protocol/update-protocol-snapshot.py --tag 5.7.6
    python tools/protocol/update-protocol-snapshot.py --input protocol.json --version 5.7.6
    python tools/protocol/update-protocol-snapshot.py --latest --dry-run --summary-file diff.md

Only the Python standard library is used.
"""

import argparse
import json
import os
import re
import sys
import urllib.request
from pathlib import Path

REPOSITORY = "obsproject/obs-websocket"
DEFAULT_SNAPSHOT = Path(__file__).resolve().parents[2] / "OBSClient.Tests" / "Protocol" / "protocol.schema.json"


def http_get(url):
    headers = {"User-Agent": "obsclient-protocol-snapshot"}
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if token and "api.github.com" in url:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def latest_tag():
    tags = json.loads(http_get(f"https://api.github.com/repos/{REPOSITORY}/tags?per_page=100"))
    versions = []
    for tag in tags:
        match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)", tag["name"])
        if match:
            versions.append((tuple(int(part) for part in match.groups()), tag["name"]))
    if not versions:
        raise SystemExit("No release tags found for " + REPOSITORY)
    return max(versions)[1]


def field(raw):
    result = {"name": raw["valueName"], "type": raw["valueType"]}
    if raw.get("valueOptional"):
        result["optional"] = True
    return result


def evaluate_enum_value(value, resolved):
    """Turns an upstream enumValue (an int, "-1", "(1 << 4)" or "(A | B)") into an int; anything else stays text."""
    if isinstance(value, int):
        return value
    text = str(value).strip()
    if re.fullmatch(r"-?\d+", text):
        return int(text)
    shift = re.fullmatch(r"\(?\s*(\d+)\s*<<\s*(\d+)\s*\)?", text)
    if shift:
        return int(shift.group(1)) << int(shift.group(2))
    if "|" in text:
        total = 0
        for part in text.strip("() ").split("|"):
            name = part.strip()
            if name not in resolved or not isinstance(resolved[name], int):
                return text
            total |= resolved[name]
        return total
    return text


def reduce_enum(enum):
    resolved = {}
    for identifier in enum["enumIdentifiers"]:
        resolved[identifier["enumIdentifier"]] = evaluate_enum_value(identifier.get("enumValue"), resolved)
    return resolved


def reduce_protocol(protocol, version):
    return {
        "obsWebSocketVersion": version,
        "source": f"https://github.com/{REPOSITORY}/blob/{version}/docs/generated/protocol.json",
        "requests": {
            request["requestType"]: {
                "request": [field(f) for f in request["requestFields"]],
                "response": [field(f) for f in request["responseFields"]],
            }
            for request in sorted(protocol["requests"], key=lambda r: r["requestType"])
        },
        "events": {
            event["eventType"]: [field(f) for f in event["dataFields"]]
            for event in sorted(protocol["events"], key=lambda e: e["eventType"])
        },
        "enums": {enum["enumType"]: reduce_enum(enum) for enum in sorted(protocol["enums"], key=lambda e: e["enumType"])},
    }


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


def describe_diff(old, new):
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


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--tag", help="obs-websocket tag to download protocol.json from, e.g. 5.7.6")
    source.add_argument("--latest", action="store_true", help="use the newest release tag of obs-websocket")
    source.add_argument("--input", help="a local protocol.json (requires --version)")
    parser.add_argument("--version", help="version label to store when using --input")
    parser.add_argument("--output", default=str(DEFAULT_SNAPSHOT), help="snapshot file to write")
    parser.add_argument("--baseline", default=str(DEFAULT_SNAPSHOT), help="snapshot to compare against")
    parser.add_argument("--dry-run", action="store_true", help="only report the differences, do not write the snapshot")
    parser.add_argument("--summary-file", help="also write the differences, as markdown, to this file")
    args = parser.parse_args()

    if args.input:
        if not args.version:
            parser.error("--input requires --version")
        version = args.version
        protocol = json.loads(Path(args.input).read_text(encoding="utf-8-sig"))
    else:
        version = args.tag or latest_tag()
        protocol = json.loads(
            http_get(f"https://raw.githubusercontent.com/{REPOSITORY}/{version}/docs/generated/protocol.json")
        )

    snapshot = reduce_protocol(protocol, version)

    baseline_path = Path(args.baseline)
    if baseline_path.exists():
        baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
        differences = describe_diff(baseline, snapshot)
        summary = (
            f"Differences between obs-websocket {baseline['obsWebSocketVersion']} (current snapshot) "
            f"and {version}:\n\n" + ("\n".join(differences) if differences else "- (no schema differences)")
        )
    else:
        summary = f"No baseline snapshot found; creating the snapshot for obs-websocket {version}."

    print(summary)
    if args.summary_file:
        Path(args.summary_file).write_text(summary + "\n", encoding="utf-8")

    if not args.dry_run:
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        with open(output, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(json.dumps(snapshot, indent=2) + "\n")
        print(f"\nWrote {output}")


if __name__ == "__main__":
    sys.exit(main())
