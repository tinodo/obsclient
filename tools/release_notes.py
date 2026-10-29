#!/usr/bin/env python3
"""The release notes of a version of OBSClient, from CHANGELOG.md.

check VERSION [--github-output FILE]
    Fails unless CHANGELOG.md has a section for VERSION that has a date, so that a release is never made from a version that
    is still "Unreleased" (or that has no notes at all). With --github-output it appends `prerelease=true` or
    `prerelease=false` to that file (the $GITHUB_OUTPUT of a workflow step).
extract VERSION
    Prints the notes of VERSION: what is under its heading, up to the next one.
compose VERSION TAG --asset FILE ... --output FILE
    Writes the text of the GitHub release: the notes, how to install, and the assets with their SHA-256.

The version is the one of the package (4.0.0 or 4.0.0-beta2), the tag is the one of the release (v4.0.0).
"""
import argparse
import hashlib
import os
import re
import sys

PACKAGE_ID = "OBSClient"
REPOSITORY = "tinodo/obsclient"
DOCUMENTATION = "https://tinodo.github.io/obsclient/"

# "## [4.0.0-beta1] - 2026-10-27", and "## [4.0.0] - Unreleased" for what is not released yet.
HEADING = re.compile(r"^## \[(?P<version>[^\]]+)\](?:\s+-\s+(?P<date>\S.*?))?\s*$")
ISO_DATE = re.compile(r"\d{4}-\d{2}-\d{2}")
VERSION = re.compile(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?")

# The test app is built for the oldest Windows that the Windows App SDK supports (TargetPlatformMinVersion of the app).
TEST_APP_DESCRIPTION = (
    "The test app. Unzip it and start `OBSClient.TestApp.exe`. It is self-contained (nothing else has to be installed), "
    "built for Windows 10 version 1809 and later, x64. It is not code signed, so Windows may warn that the publisher is "
    "unknown; compare the checksum below."
)
PACKAGE_DESCRIPTION = "The package: the exact file that was pushed to nuget.org and to GitHub Packages."
ATTESTATIONS = "https://docs.github.com/en/actions/concepts/security/artifact-attestations"
PUBLISH_WORKFLOW = f"{REPOSITORY}/.github/workflows/publish.yml"


class ReleaseNotesError(Exception):
    """Something that has to be put right in CHANGELOG.md (or in the arguments) before the release can be made."""


def is_prerelease(version):
    return "-" in version


def find_section(changelog, version):
    """Returns the date of the heading of the version (None when it has none) and the lines under it."""
    if not VERSION.fullmatch(version):
        raise ReleaseNotesError(f"'{version}' is not a version (expected 4.0.0 or 4.0.0-beta2).")

    lines = changelog.splitlines()
    start = None
    date = None
    for index, line in enumerate(lines):
        match = HEADING.match(line)
        if start is None:
            if match and match.group("version") == version:
                start = index
                date = match.group("date")
        elif match or line.startswith("## "):
            return date, _trim(lines[start + 1:index])

    if start is None:
        raise ReleaseNotesError(f"CHANGELOG.md has no section '## [{version}] - yyyy-mm-dd'.")
    return date, _trim(lines[start + 1:])


def _trim(lines):
    first = 0
    last = len(lines)
    while first < last and not lines[first].strip():
        first += 1
    while last > first and not lines[last - 1].strip():
        last -= 1
    return lines[first:last]


def notes_of(changelog, version):
    """The notes of a release, or an error when the version cannot be released from this changelog."""
    date, body = find_section(changelog, version)
    if date is None or not ISO_DATE.fullmatch(date):
        raise ReleaseNotesError(
            f"The section of {version} in CHANGELOG.md has the date '{date or ''}': give it the date of the release (yyyy-mm-dd)."
        )
    if not body:
        raise ReleaseNotesError(f"The section of {version} in CHANGELOG.md is empty: say what is in this release.")
    return "\n".join(body)


def sha256_of(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def describe(name):
    if name.endswith(".nupkg"):
        return PACKAGE_DESCRIPTION
    if "TestApp" in name and name.endswith(".zip"):
        return TEST_APP_DESCRIPTION
    return ""


def compose(version, tag, notes, assets):
    """The text of the GitHub release. `assets` are (file name, SHA-256) pairs."""
    parts = []
    if is_prerelease(version):
        parts.append(
            "> [!IMPORTANT]\n"
            "> This is a **prerelease**. It is meant to be tried, and its public API can still change before the stable "
            f"release. A plain `dotnet add package {PACKAGE_ID}` keeps installing the latest stable version."
        )

    parts.append(notes)
    parts.append(
        "## Install\n\n"
        "```shell\n"
        f"dotnet add package {PACKAGE_ID} --version {version}\n"
        "```\n\n"
        f"[{PACKAGE_ID} {version} on NuGet](https://www.nuget.org/packages/{PACKAGE_ID}/{version}) | "
        f"[Documentation]({DOCUMENTATION}) | "
        f"[CHANGELOG](https://github.com/{REPOSITORY}/blob/{tag}/CHANGELOG.md)"
    )

    rows = "\n".join(f"| `{name}` | {describe(name)} |" for name, _ in assets)
    checksums = "\n".join(f"{digest}  {name}" for name, digest in assets)
    parts.append(
        "## Assets\n\n"
        "| File | What it is |\n| --- | --- |\n"
        f"{rows}\n\n"
        "SHA-256:\n\n"
        f"```text\n{checksums}\n```"
    )
    parts.append(verification(version, tag))
    parts.append(f"Found a problem? Please report it in an [issue](https://github.com/{REPOSITORY}/issues).")
    return "\n\n".join(parts) + "\n"


def verification(version, tag):
    """How to check where the files of a release come from: the build provenance attestation that the publish workflow made."""
    package = f"{PACKAGE_ID}.{version}.nupkg"
    cached = f"~/.nuget/packages/{PACKAGE_ID.lower()}/{version.lower()}/lib/net8.0/{PACKAGE_ID}.dll"
    return (
        "## Verify\n\n"
        f"The files above, and the libraries inside the package, have a signed [build provenance attestation]({ATTESTATIONS}): "
        "GitHub states that the `publish.yml` workflow of this repository made them, from the commit of this release, on a "
        "GitHub-hosted runner. Check one with the [GitHub CLI](https://cli.github.com/):\n\n"
        "```shell\n"
        f"gh attestation verify {package} --repo {REPOSITORY} --signer-workflow {PUBLISH_WORKFLOW} --source-ref refs/tags/{tag}\n"
        "```\n\n"
        "nuget.org signs the package that it serves, so the `.nupkg` from nuget.org is not byte for byte the file above. To check the "
        f"library that you use, run the same command on the DLL in your NuGet cache: `{cached}`."
    )


def read_changelog(path):
    try:
        with open(path, encoding="utf-8") as stream:
            return stream.read()
    except OSError as error:
        raise ReleaseNotesError(f"Cannot read {path}: {error}") from error


def main(arguments):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--changelog", default="CHANGELOG.md", help="the changelog (default: CHANGELOG.md)")
    commands = parser.add_subparsers(dest="command", required=True)

    check = commands.add_parser("check", help="fail unless the version can be released from the changelog")
    check.add_argument("version")
    check.add_argument("--github-output", help="a file to append prerelease=true|false to (the $GITHUB_OUTPUT of a step)")

    extract = commands.add_parser("extract", help="print the notes of the version")
    extract.add_argument("version")

    write = commands.add_parser("compose", help="write the text of the GitHub release")
    write.add_argument("version")
    write.add_argument("tag")
    write.add_argument("--asset", action="append", default=[], help="a file of the release (repeat for each)")
    write.add_argument("--output", required=True)

    options = parser.parse_args(arguments)

    try:
        notes = notes_of(read_changelog(options.changelog), options.version)
        if options.command == "check":
            prerelease = "true" if is_prerelease(options.version) else "false"
            print(f"CHANGELOG.md has the notes of {options.version} ({'a prerelease' if prerelease == 'true' else 'a release'}).")
            if options.github_output:
                with open(options.github_output, "a", encoding="utf-8", newline="\n") as stream:
                    stream.write(f"prerelease={prerelease}\n")
        elif options.command == "extract":
            print(notes)
        else:
            assets = []
            for path in options.asset:
                if not os.path.isfile(path):
                    raise ReleaseNotesError(f"The asset {path} does not exist.")
                assets.append((os.path.basename(path), sha256_of(path)))
            with open(options.output, "w", encoding="utf-8", newline="\n") as stream:
                stream.write(compose(options.version, options.tag, notes, assets))
            print(f"Wrote {options.output}: the notes of {options.version} and {len(assets)} assets.", file=sys.stderr)
    except ReleaseNotesError as error:
        print(f"::error::{error}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
