#!/usr/bin/env python3
"""What the tests reach of the library: a summary of Cobertura coverage reports, and a floor under it.

coverage_summary.py REPORT... [--package OBSClient] [--floor FILE] [--summary FILE] [--top N]

Each REPORT is a Cobertura file, or a pattern for several ("TestResults/**/coverage.cobertura.xml"): the reports of the target frameworks of
one test run. They are merged: a line is hit when any of them hit it. Only the assembly of the library is counted, and the code that the
protocol generator writes (OBSClient/Generated, *.g.cs) is kept apart from the hand-written code: the same tests reach it, and it would
make the number say little about what a person wrote.

--floor FILE    A JSON file with the least coverage of the hand-written code, in percent: {"lines": 86.0, "branches": 76.0}. The run fails
                (exit code 1) below either of them.
--summary FILE  Appends the summary, as Markdown, to FILE (the $GITHUB_STEP_SUMMARY of a step).
--top N         How many files with the most lines that no test reaches to list (default 10).
"""
import argparse
import glob
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass

DEFAULT_PACKAGE = "OBSClient"
CONDITION = re.compile(r"\((\d+)/(\d+)\)")


class CoverageError(Exception):
    """Something wrong with the reports, or with the floor: not a coverage that is too low."""


@dataclass(frozen=True)
class Totals:
    lines: int
    lines_hit: int
    branches: int
    branches_hit: int

    @property
    def line_rate(self):
        return percent(self.lines_hit, self.lines)

    @property
    def branch_rate(self):
        return percent(self.branches_hit, self.branches)


def percent(hit, total):
    return 100.0 * hit / total if total else 100.0


def normalize(name):
    """Reports made on Windows have backslashes in the names of the files."""
    return name.replace("\\", "/")


def is_generated(name):
    return name.endswith(".g.cs") or "Generated" in name.split("/")


def expand(patterns):
    """The files that the arguments name: a file as it is, a pattern as what it matches (the shell of a step does not do ** everywhere)."""
    paths = []
    for pattern in patterns:
        found = sorted(set(glob.glob(pattern, recursive=True)))
        if not found:
            raise CoverageError(f"No coverage report matches '{pattern}'.")
        paths.extend(found)
    return paths


def read(paths, package):
    """Merges the reports. Returns {file: {line: hits}} and {file: {line: {class: (branches hit, branches)}}}."""
    lines = defaultdict(dict)
    branches = defaultdict(lambda: defaultdict(dict))
    for path in paths:
        try:
            root = ET.parse(path).getroot()
        except (OSError, ET.ParseError) as error:
            raise CoverageError(f"Cannot read {path}: {error}") from error

        packages = [candidate for candidate in root.iter("package") if candidate.get("name") == package]
        if not packages:
            raise CoverageError(f"{path} has no package '{package}'.")

        for candidate in packages:
            # A file is in several classes (partial classes, the state machines of async methods, lambdas): a line is reached when any of
            # them reached it. The branches are not merged by line: two classes can have a decision of their own on the same line (a
            # conditional, and a lambda that is written on it), so they are kept by class, and summed. The same class in another report
            # (the other target framework) is the same decisions again: the most that either reached.
            for item in candidate.iter("class"):
                name = normalize(item.get("filename", ""))
                owner = item.get("name", "")
                for line in item.iter("line"):
                    number = int(line.get("number"))
                    lines[name][number] = max(lines[name].get(number, 0), int(line.get("hits", "0")))
                    if line.get("branch", "").lower() == "true":
                        match = CONDITION.search(line.get("condition-coverage", ""))
                        if match:
                            hit, total = int(match.group(1)), int(match.group(2))
                            previous = branches[name][number].get(owner, (0, 0))
                            branches[name][number][owner] = (max(previous[0], hit), max(previous[1], total))
    return lines, branches


def totals(lines, branches, select):
    line_count = line_hit = branch_count = branch_hit = 0
    for name, entries in lines.items():
        if not select(name):
            continue
        line_count += len(entries)
        line_hit += sum(1 for hits in entries.values() if hits > 0)
        for owners in branches.get(name, {}).values():
            for hit, total in owners.values():
                branch_count += total
                branch_hit += hit
    return Totals(line_count, line_hit, branch_count, branch_hit)


def least_reached(lines, top):
    """The files of the hand-written code with the most lines that no test reaches: (missed, lines, name)."""
    rows = []
    for name, entries in lines.items():
        if is_generated(name):
            continue
        missed = sum(1 for hits in entries.values() if hits == 0)
        if missed:
            rows.append((missed, len(entries), name))
    return sorted(rows, key=lambda row: (-row[0], row[2]))[:top]


def read_floor(path):
    try:
        with open(path, encoding="utf-8") as stream:
            floor = json.load(stream)
        values = (float(floor["lines"]), float(floor["branches"]))
    except (OSError, ValueError, KeyError, TypeError) as error:
        raise CoverageError(f'Cannot use the floor in {path} (expected {{"lines": 86.0, "branches": 76.0}}): {error}') from error

    # NaN is not below anything, and a negative floor is never held: a floor that is not a percentage would hold nothing, silently.
    if not all(0.0 <= value <= 100.0 for value in values):
        raise CoverageError(f"The floor in {path} has to be two percentages between 0 and 100, not {values[0]} and {values[1]}.")
    return values


def below_floor(hand_written, floor):
    problems = []
    if hand_written.line_rate < floor[0]:
        problems.append(f"The lines of the hand-written code are reached for {hand_written.line_rate:.1f}%, below the floor of {floor[0]:.1f}%.")
    if hand_written.branch_rate < floor[1]:
        problems.append(f"The branches of the hand-written code are reached for {hand_written.branch_rate:.1f}%, below the floor of {floor[1]:.1f}%.")
    return problems


def describe(found):
    return f"{found.lines_hit} of {found.lines} lines ({found.line_rate:.1f}%), {found.branches_hit} of {found.branches} branches ({found.branch_rate:.1f}%)"


def markdown(hand_written, generated, rows, floor, problems):
    text = [
        "### Coverage of the library",
        "",
        "| Code | Lines | Branches |",
        "| --- | --- | --- |",
        f"| Hand-written | {hand_written.line_rate:.1f}% ({hand_written.lines_hit} of {hand_written.lines}) | {hand_written.branch_rate:.1f}% ({hand_written.branches_hit} of {hand_written.branches}) |",
        f"| Generated | {generated.line_rate:.1f}% ({generated.lines_hit} of {generated.lines}) | {generated.branch_rate:.1f}% ({generated.branches_hit} of {generated.branches}) |",
        "",
    ]
    if floor is not None:
        verdict = "It is below the floor." if problems else "It holds."
        text.append(f"The floor of the hand-written code (`tools/coverage-floor.json`) is {floor[0]:.1f}% of the lines and {floor[1]:.1f}% of the branches. {verdict}")
        text.append("")
    if rows:
        text.extend(["Hand-written files with the most lines that no test reaches:", "", "| File | Lines not reached | Lines |", "| --- | --- | --- |"])
        text.extend(f"| `{name}` | {missed} | {count} |" for missed, count, name in rows)
        text.append("")
    return "\n".join(text)


def main(arguments):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("reports", nargs="+", help="Cobertura files, or patterns for them")
    parser.add_argument("--package", default=DEFAULT_PACKAGE, help=f"the assembly to count (default: {DEFAULT_PACKAGE})")
    parser.add_argument("--floor", help="a JSON file with the least coverage of the hand-written code")
    parser.add_argument("--summary", help="a file to append the summary to, as Markdown (the $GITHUB_STEP_SUMMARY of a step)")
    parser.add_argument("--top", type=int, default=10, help="how many files with the most lines that are not reached to list (default 10)")
    options = parser.parse_args(arguments)

    try:
        paths = expand(options.reports)
        lines, branches = read(paths, options.package)
        floor = read_floor(options.floor) if options.floor else None
    except CoverageError as error:
        print(f"::error::{error}")
        return 1

    hand_written = totals(lines, branches, lambda name: not is_generated(name))
    generated = totals(lines, branches, is_generated)
    rows = least_reached(lines, options.top)
    problems = below_floor(hand_written, floor) if floor is not None else []

    print(f"Coverage of {options.package}, {len(paths)} report(s) merged.")
    print(f"  hand-written: {describe(hand_written)}")
    print(f"  generated:    {describe(generated)}")
    for missed, count, name in rows:
        print(f"  {missed:4} of {count:4} lines not reached  {name}")

    if options.summary:
        with open(options.summary, "a", encoding="utf-8", newline="\n") as stream:
            stream.write(markdown(hand_written, generated, rows, floor, problems) + "\n")

    for problem in problems:
        print(f"::error::{problem} Add tests, or (when the code that was reached is gone on purpose) lower the floor in {options.floor} and say why.")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
