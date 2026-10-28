#!/usr/bin/env python3
"""Verifies that the package that nuget.org serves is the package that was built.

`dotnet nuget push --skip-duplicate` treats a version that nuget.org already has as done. That is what lets the re-run of a job that
failed later carry on, but it also means that a run that lost a race (another run, or an earlier attempt for the same version, was
first) would carry on too: it would push symbols and make a release for a package that is not its own. So after the push this
downloads the package that nuget.org serves and holds it against the one that was built, file by file.

nuget.org signs what it serves: it adds .signature.p7s to the package and changes nothing else. So every file of the package that
was built has to be in the served package with the same bytes, and the signature is the only file that the served package may have
on top of that.

The id and the version are the ones in the nuspec of the package that was built.

Usage: compare-published.py [--source URL] [--attempts N] [--wait SECONDS] PACKAGE.nupkg
Exit code: 0 the same, 1 not the same, 2 the package could not be downloaded.
"""
import argparse
import hashlib
import io
import sys
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ElementTree
import zipfile

DEFAULT_SOURCE = "https://api.nuget.org/v3-flatcontainer"
SIGNATURE = ".signature.p7s"


def read_entries(data):
    """The files of a package by name, with the SHA-256 of the content of each."""
    with zipfile.ZipFile(io.BytesIO(data)) as package:
        return {
            name: hashlib.sha256(package.read(name)).hexdigest()
            for name in package.namelist()
            if not name.endswith("/")
        }


def identity(data):
    """The id and the (normalized, lowercase) version of a package, from its nuspec."""
    with zipfile.ZipFile(io.BytesIO(data)) as package:
        nuspecs = [name for name in package.namelist() if name.endswith(".nuspec") and "/" not in name]
        if len(nuspecs) != 1:
            raise ValueError(f"the package has {len(nuspecs)} nuspec files in its root, not one")
        root = ElementTree.fromstring(package.read(nuspecs[0]))

    namespace = root.tag[: root.tag.index("}") + 1] if root.tag.startswith("{") else ""
    metadata = root.find(f"{namespace}metadata")
    package_id = metadata.findtext(f"{namespace}id") if metadata is not None else None
    version = metadata.findtext(f"{namespace}version") if metadata is not None else None
    if not package_id or not version:
        raise ValueError("the nuspec of the package has no id or no version")

    # The flat container names a version in lowercase, without the build metadata that comes after a plus.
    return package_id.lower(), version.split("+")[0].lower()


def compare(built, served):
    """What is different between the package that was built and the one that is served; nothing when they are the same."""
    problems = []
    wanted = read_entries(built)
    found = read_entries(served)
    for name, digest in sorted(wanted.items()):
        if name not in found:
            problems.append(f"{name}: in the package that was built, not in the one on nuget.org")
        elif found[name] != digest:
            problems.append(f"{name}: not the same as in the package that was built")
    for name in sorted(set(found) - set(wanted)):
        if name != SIGNATURE:
            problems.append(f"{name}: on nuget.org, not in the package that was built")
    return problems


def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "obsclient-release-check"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def download(url, attempts, wait):
    """The bytes at the url. A package that was just listed may take a moment to be served, so it is tried again."""
    last = None
    for attempt in range(1, attempts + 1):
        try:
            return fetch(url)
        except (urllib.error.URLError, TimeoutError, ConnectionError) as error:
            last = error
            print(f"Could not download {url} (attempt {attempt} of {attempts}): {error}", file=sys.stderr)
            if attempt < attempts:
                time.sleep(wait)
    raise RuntimeError(f"{url} could not be downloaded: {last}")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("package", help="the package (.nupkg) that was built")
    parser.add_argument("--source", default=DEFAULT_SOURCE, help=f"the flat container to download from ({DEFAULT_SOURCE})")
    parser.add_argument("--attempts", type=int, default=6, help="how many times to try the download (6)")
    parser.add_argument("--wait", type=float, default=10, help="seconds between two attempts (10)")
    options = parser.parse_args(argv)

    with open(options.package, "rb") as stream:
        built = stream.read()
    package_id, version = identity(built)
    url = f"{options.source.rstrip('/')}/{package_id}/{version}/{package_id}.{version}.nupkg"

    try:
        served = download(url, options.attempts, options.wait)
    except RuntimeError as error:
        print(f"::error::{error}")
        return 2

    problems = compare(built, served)
    if problems:
        print(f"::error::The package that nuget.org serves for {package_id} {version} is not the package that this run built:")
        for problem in problems:
            print(f"  - {problem}")
        return 1

    print(f"The package that nuget.org serves for {package_id} {version} is the package that was built ({len(read_entries(built))} files).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
