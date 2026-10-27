"""Turns the raw capture of obs-capture.ps1 into a fixture for the tests (OBSClient.Tests/Fixtures/obs-capture-*.json): only what OBS
Studio answered with success, and with what identifies the machine replaced (the user name in a path, the names of the monitors).
The capture itself holds more than that and stays out of the repository.

    python tools/capture/make-fixture.py <the folder of the capture> <the fixture file>

Look at the fixture before you commit it: scene names, input names and the settings of inputs are yours, and are kept."""
import json
import os
import re
import sys

# What replaces what identifies the machine.
RECORD_DIRECTORY = "C:\\Users\\user\\Videos"
MONITOR_NAME = "Display {}({})"
MONITOR_NAME_PATTERN = r"Display \d+\(\d+\)"

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
        data["recordDirectory"] = RECORD_DIRECTORY
    if d["requestType"] == "GetMonitorList":
        for index, monitor in enumerate(data["monitors"]):
            monitor["monitorName"] = MONITOR_NAME.format(index + 1, index)
    entry = {"requestType": d["requestType"]}
    if label:
        entry["label"] = label
    entry["responseData"] = data
    responses.append(entry)

if version is None:
    sys.exit("The capture has no answer to GetVersion.")

events = []
events_path = os.path.join(source, "events.jsonl")
if os.path.exists(events_path):
    for line in open(events_path, encoding="utf-8-sig"):
        line = line.strip()
        if not line:
            continue
        message = json.loads(line)
        if message.get("op") != 5:
            continue
        entry = {"eventType": message["d"]["eventType"], "eventIntent": message["d"].get("eventIntent")}
        if "eventData" in message["d"]:
            entry["eventData"] = message["d"]["eventData"]
        events.append(entry)

fixture = {
    "note": "What a real OBS Studio sent, captured with tools/capture/obs-capture.ps1: the responses to Get requests, and the events. "
            "What identifies the machine (the user name in a path, the names of the monitors) is replaced; the rest is as OBS Studio sent it.",
    "obsVersion": version["obsVersion"],
    "obsWebSocketVersion": version["obsWebSocketVersion"],
    "platform": version["platform"],
    "responses": responses,
    "events": events,
}
text = json.dumps(fixture, indent=2, ensure_ascii=False)

# The name of the user must not be left in what was captured. What this script wrote itself (the note, the replacements above)
# is not looked at: on an account that is called User, the fixture would otherwise always be refused.
captured = json.dumps({"responses": responses, "events": events}, ensure_ascii=False)
captured = captured.replace(json.dumps(RECORD_DIRECTORY, ensure_ascii=False)[1:-1], "")
captured = re.sub(MONITOR_NAME_PATTERN, "", captured)
user = os.environ.get("USERNAME") or os.environ.get("USER")
if user and re.search(re.escape(user), captured, re.I):
    sys.exit(f"The capture still holds the name of the user ({user}): replace it in this script.")
with open(target, "w", encoding="utf-8", newline="\n") as file:
    file.write(text + "\n")
print(f"{len(responses)} responses and {len(events)} events written to {target}")
