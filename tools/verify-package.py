#!/usr/bin/env python3
"""Verifies that the OBSClient NuGet package is exactly one thing: the library.

The package may hold the assembly (and its XML documentation) for each target framework, the README and the
license, and nothing else. In particular there must be no JSON (the protocol definition and the code generator are
design-time inputs under tools/ and never ship), no analyzer or source generator, no build targets and no content
files. The package also must not take on a new runtime dependency unless somebody decides that on purpose.

Usage: verify-package.py PACKAGE.nupkg [PACKAGE.nupkg ...]
"""
import re
import sys
import xml.etree.ElementTree as ElementTree
import zipfile

ASSEMBLY_NAME = "OBSClient"
REQUIRED_FRAMEWORKS = {"net8.0", "net10.0"}
ALLOWED_DEPENDENCIES = {"Microsoft.Extensions.Logging", "Microsoft.Extensions.Logging.Abstractions"}

# Plumbing that every NuGet package has, plus the files this package is meant to carry.
ALLOWED_FILES = [
    r"\[Content_Types\]\.xml",
    r"_rels/\.rels",
    r"package/services/metadata/core-properties/[0-9a-zA-Z]+\.psmdcp",
    rf"{ASSEMBLY_NAME}\.nuspec",
    r"README\.md",
    r"LICENSE",
    rf"lib/(?P<framework>net\d+\.\d+)/{ASSEMBLY_NAME}\.(?P<kind>dll|xml)",
]


def verify(path):
    problems = []
    with zipfile.ZipFile(path) as package:
        names = [name for name in package.namelist() if not name.endswith("/")]
        found = {}
        for name in names:
            for pattern in ALLOWED_FILES:
                match = re.fullmatch(pattern, name)
                if match:
                    if "framework" in match.groupdict():
                        found.setdefault(match.group("framework"), set()).add(match.group("kind"))
                    break
            else:
                problems.append(f"unexpected file in the package: {name}")

        for framework in sorted(REQUIRED_FRAMEWORKS):
            kinds = found.get(framework, set())
            for kind in ("dll", "xml"):
                if kind not in kinds:
                    problems.append(f"lib/{framework}/{ASSEMBLY_NAME}.{kind} is missing")
        for framework in sorted(set(found) - REQUIRED_FRAMEWORKS):
            problems.append(f"unexpected target framework in the package: {framework}")

        nuspec_name = f"{ASSEMBLY_NAME}.nuspec"
        if nuspec_name in names:
            root = ElementTree.fromstring(package.read(nuspec_name))
            dependencies = {
                element.get("id")
                for element in root.iter()
                if element.tag.rsplit("}", 1)[-1] == "dependency"
            }
            for dependency in sorted(dependencies - ALLOWED_DEPENDENCIES):
                problems.append(f"unexpected package dependency: {dependency}")
        else:
            problems.append(f"{nuspec_name} is missing")

    return names, problems


def main(arguments):
    if not arguments:
        print(__doc__)
        return 2

    failed = False
    for path in arguments:
        names, problems = verify(path)
        print(f"{path}: {len(names)} files")
        for name in sorted(names):
            print(f"  {name}")
        if problems:
            failed = True
            for problem in problems:
                print(f"::error::{path}: {problem}")
        else:
            print("  OK: the package is a single assembly (plus its documentation, README and license).")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
