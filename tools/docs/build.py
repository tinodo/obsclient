#!/usr/bin/env python3
"""Builds the documentation site into docs/_site, the way the Docs workflow does.

    python tools/docs/build.py              # restore, build the site, check its links
    python tools/docs/build.py --serve      # ... and serve it at http://localhost:8080

What it does:

1. restores DocFX, which is pinned in .config/dotnet-tools.json, and the library project (DocFX reads the
   XML documentation from the source through MSBuild, which needs a restored project);
2. copies CHANGELOG.md to docs/changelog.md (the copy is not committed), so that the changelog is a page of the site;
3. runs DocFX: the API reference from the source of OBSClient, and the articles from docs/;
4. checks the links of the result (tools/docs/check-links.py).

Needs the .NET SDK 8.0 or later (the library targets net8.0 and net10.0, so a restore needs the 10.0 SDK).
"""

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
SITE = DOCS / "_site"


def run(*command):
    print("\n==> " + " ".join(command), flush=True)
    result = subprocess.run(command, cwd=ROOT)
    if result.returncode != 0:
        sys.exit(result.returncode)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--serve", action="store_true", help="serve the site after it is built")
    parser.add_argument("--no-restore", action="store_true", help="skip the restore of DocFX and of the library")
    parser.add_argument("--warnings-as-errors", action="store_true", help="let DocFX fail on a warning")
    args = parser.parse_args()

    if shutil.which("dotnet") is None:
        sys.exit("error: the .NET SDK is needed to build the documentation (https://dotnet.microsoft.com/download).")

    if not args.no_restore:
        run("dotnet", "tool", "restore")
        run("dotnet", "restore", "OBSClient/OBSClient.csproj")

    shutil.copyfile(ROOT / "CHANGELOG.md", DOCS / "changelog.md")

    docfx = ["dotnet", "docfx", "docs/docfx.json"]
    if args.warnings_as_errors:
        docfx.append("--warningsAsErrors")
    run(*docfx)

    run(sys.executable, "tools/docs/check-links.py", str(SITE))

    if args.serve:
        run("dotnet", "docfx", "serve", str(SITE))


if __name__ == "__main__":
    main()
