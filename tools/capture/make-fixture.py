"""Turns the raw capture of obs-capture.ps1 into a fixture for the tests (OBSClient.Tests/Fixtures/obs-capture-*.json): only what OBS
Studio answered with success, and with what identifies the machine replaced (the user name in a path, the names of the monitors).
The capture itself holds more than that and stays out of the repository.

    python tools/capture/make-fixture.py <the folder of the capture> <the fixture file>

Look at the fixture before you commit it: scene names, input names and the settings of inputs are yours, and are kept."""
import json
import os
import re
import sys

source = sys.argv[1]
target = sys.argv[2]

responses = []
version = None
for name in sorted(os.listdir(source)):
    if not name.endswith(".json"):
        continue
    d = json.load(open(os.path.join(source, name), encoding="utf-8-sig"))["d"]
    if not d["requestStatus"]["result"] or d.get("responseData") is None:
        continue
    label = name[len(d["requestType"]) + 2:-5] if name.startswith(d["requestType"] + "__") else None
    data = d["responseData"]
    if d["requestType"] == "GetVersion":
        version = data
    if d["requestType"] == "GetRecordDirectory":
        data["recordDirectory"] = "C:\\Users\\user\\Videos"
    if d["requestType"] == "GetMonitorList":
        for index, monitor in enumerate(data["monitors"]):
            monitor["monitorName"] = f"Display {index + 1}({index})"
    entry = {"requestType": d["requestType"]}
    if label:
        entry["label"] = label
    entry["responseData"] = data
    responses.append(entry)

if version is None:
    sys.exit("The capture has no answer to GetVersion.")

fixture = {
    "note": "Responses of a real OBS Studio, captured read-only (Get requests only) with tools/capture/obs-capture.ps1. "
            "What identifies the machine (the user name in a path, the names of the monitors) is replaced; the rest is as OBS Studio sent it.",
    "obsVersion": version["obsVersion"],
    "obsWebSocketVersion": version["obsWebSocketVersion"],
    "platform": version["platform"],
    "responses": responses,
}
text = json.dumps(fixture, indent=2, ensure_ascii=False)
user = os.environ.get("USERNAME") or os.environ.get("USER")
if user and re.search(re.escape(user), text, re.I):
    sys.exit(f"The fixture still holds the name of the user ({user}): replace it in this script.")
with open(target, "w", encoding="utf-8", newline="\n") as file:
    file.write(text + "\n")
print(f"{len(responses)} responses written to {target}")
