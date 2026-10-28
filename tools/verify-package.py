#!/usr/bin/env python3
"""Verifies that the OBSClient NuGet package is exactly one thing: the library.

The package may hold the assembly (and its XML documentation) for each target framework, the README, the license and the icon, and
nothing else. In particular there must be no JSON (the protocol definition and the code generator are
design-time inputs under tools/ and never ship), no analyzer or source generator, no build targets and no content
files. The package also must not take on a new runtime dependency unless somebody decides that on purpose. The icon has to be a PNG
that nuget.org accepts (at most 1 MB, and at least the 128 by 128 pixels that NuGet recommends), and the package has to name it.

Next to every package there has to be its symbol package (.snupkg): the debug symbols (PDB) of each target framework and
nothing else, and the PDBs have to carry Source Link, so that a debugger can show the source of the commit that was built.
Source Link names the repository that was built, which is the one of GITHUB_REPOSITORY on GitHub Actions (so a fork's own CI
verifies its own symbols), and tinodo/obsclient anywhere else; --repository names another.

Usage: verify-package.py [--repository OWNER/NAME] PACKAGE.nupkg [PACKAGE.nupkg ...]
       (a .snupkg that is named is verified as a symbol package; the one that belongs to a .nupkg is verified with it)
"""
import argparse
import os
import re
import struct
import sys
import xml.etree.ElementTree as ElementTree
import zipfile

ASSEMBLY_NAME = "OBSClient"
DEFAULT_REPOSITORY = "tinodo/obsclient"
REQUIRED_FRAMEWORKS = {"net8.0", "net10.0"}
ALLOWED_DEPENDENCIES = {"Microsoft.Extensions.Logging.Abstractions"}

ICON_NAME = "icon.png"
ICON_MAX_BYTES = 1024 * 1024  # nuget.org refuses a bigger icon
ICON_MIN_PIXELS = 128  # what NuGet recommends
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"

# Plumbing that every NuGet package has, plus the files this package is meant to carry.
ALLOWED_FILES = [
    r"\[Content_Types\]\.xml",
    r"_rels/\.rels",
    r"package/services/metadata/core-properties/[0-9a-zA-Z]+\.psmdcp",
    rf"{ASSEMBLY_NAME}\.nuspec",
    r"README\.md",
    r"LICENSE",
    r"icon\.png",
    rf"lib/(?P<framework>net\d+\.\d+)/{ASSEMBLY_NAME}\.(?P<kind>dll|xml)",
]

# The symbol package has the same plumbing, and the PDB of each target framework instead of the assembly.
ALLOWED_SYMBOL_FILES = [
    r"\[Content_Types\]\.xml",
    r"_rels/\.rels",
    r"package/services/metadata/core-properties/[0-9a-zA-Z]+\.psmdcp",
    rf"{ASSEMBLY_NAME}\.nuspec",
    rf"lib/(?P<framework>net\d+\.\d+)/{ASSEMBLY_NAME}\.pdb",
]

# What Source Link writes into a PDB (as UTF-8 JSON): the URL at which the file of a commit can be read.
def source_link(repository):
    return re.compile(rb"https://raw\.githubusercontent\.com/" + re.escape(repository.encode()) + rb"/[0-9a-f]{40}/\*", re.IGNORECASE)


def icon_problems(data):
    problems = []
    if len(data) > ICON_MAX_BYTES:
        problems.append(f"{ICON_NAME} is larger than 1 MB, which nuget.org refuses")
    if data[:8] != PNG_SIGNATURE or len(data) < 24:
        problems.append(f"{ICON_NAME} is not a PNG")
    else:
        width, height = struct.unpack(">II", data[16:24])
        if min(width, height) < ICON_MIN_PIXELS:
            problems.append(f"{ICON_NAME} is {width} by {height} pixels, which is less than the {ICON_MIN_PIXELS} by {ICON_MIN_PIXELS} that NuGet recommends")
    return problems


def verify_package(path):
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

        if ICON_NAME in names:
            problems.extend(icon_problems(package.read(ICON_NAME)))
        else:
            problems.append(f"{ICON_NAME} is missing")

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
            icons = [element.text for element in root.iter() if element.tag.rsplit("}", 1)[-1] == "icon"]
            if icons != [ICON_NAME]:
                problems.append(f"the nuspec does not name {ICON_NAME} as the icon of the package")
        else:
            problems.append(f"{nuspec_name} is missing")

    return names, problems


def verify_symbols(path, repository=DEFAULT_REPOSITORY):
    problems = []
    expected = source_link(repository)
    with zipfile.ZipFile(path) as package:
        names = [name for name in package.namelist() if not name.endswith("/")]
        found = set()
        for name in names:
            for pattern in ALLOWED_SYMBOL_FILES:
                match = re.fullmatch(pattern, name)
                if match:
                    if "framework" in match.groupdict():
                        found.add(match.group("framework"))
                        if not expected.search(package.read(name)):
                            problems.append(f"{name} has no Source Link to https://raw.githubusercontent.com/{repository}/<commit>/")
                    break
            else:
                problems.append(f"unexpected file in the symbol package: {name}")

        for framework in sorted(REQUIRED_FRAMEWORKS - found):
            problems.append(f"lib/{framework}/{ASSEMBLY_NAME}.pdb is missing")
        for framework in sorted(found - REQUIRED_FRAMEWORKS):
            problems.append(f"unexpected target framework in the symbol package: {framework}")

        if f"{ASSEMBLY_NAME}.nuspec" not in names:
            problems.append(f"{ASSEMBLY_NAME}.nuspec is missing")

    return names, problems


def verify(path, repository=DEFAULT_REPOSITORY):
    return verify_symbols(path, repository) if path.endswith(".snupkg") else verify_package(path)


def symbols_of(path):
    """The symbol package that belongs to a package (it is built next to it), or None when the path is not a package."""
    return path[: -len(".nupkg")] + ".snupkg" if path.endswith(".nupkg") else None


def main(arguments):
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--repository")
    parser.add_argument("packages", nargs="*")
    options = parser.parse_args(arguments)
    if not options.packages:
        print(__doc__)
        return 2

    repository = options.repository or os.environ.get("GITHUB_REPOSITORY") or DEFAULT_REPOSITORY

    # A package is never verified without its symbols: a pack that forgot them must not go unnoticed.
    paths = []
    for path in options.packages:
        if path not in paths:
            paths.append(path)
        symbols = symbols_of(path)
        if symbols is not None and symbols not in paths:
            paths.append(symbols)

    failed = False
    for path in paths:
        if not os.path.isfile(path):
            failed = True
            print(f"::error::{path}: the file is missing (the symbol package is made by the pack: IncludeSymbols and SymbolPackageFormat=snupkg).")
            continue

        names, problems = verify(path, repository)
        print(f"{path}: {len(names)} files")
        for name in sorted(names):
            print(f"  {name}")
        if problems:
            failed = True
            for problem in problems:
                print(f"::error::{path}: {problem}")
        elif path.endswith(".snupkg"):
            print("  OK: the symbol package has the PDB of each target framework, with Source Link.")
        else:
            print("  OK: the package is a single assembly (plus its documentation, README, license and icon).")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
