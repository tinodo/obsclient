#!/usr/bin/env python3
"""Checks the links of a built documentation site, so that a broken link fails the build.

    python tools/docs/check-links.py [SITE_DIR] [--strict]

SITE_DIR defaults to docs/_site. The check follows what a visitor of the site would follow:

* A relative link, and a reference to an image, a script or a style sheet, must lead to a file of the site (an error).
* A root-relative link must start with the base path of the site on GitHub Pages, /obsclient/ (an error otherwise).
* An xref that DocFX could not resolve is an error.
* An #anchor must exist in the page that the link leads to (a warning, since a few anchors are made by script).

Links to other sites are not followed. The exit code is 1 when there is an error, or a warning with --strict.
"""

import argparse
import sys
import urllib.parse
from html.parser import HTMLParser
from pathlib import Path

BASE_PATH = "/obsclient/"
EXTERNAL_SCHEMES = ("http:", "https:", "mailto:", "tel:", "data:", "javascript:", "//")
MAX_REPORTED = 60


class Page(HTMLParser):
    """Collects the ids of a page and the links that it contains."""

    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.ids = set()
        self.links = []

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        for name in ("id", "name"):
            if values.get(name):
                self.ids.add(values[name])

        if tag == "xref":
            self.links.append(("xref", "xref:" + (values.get("href") or "")))
        elif tag in ("a", "link") and values.get("href") is not None:
            self.links.append((tag + "[href]", values["href"]))
        elif tag in ("img", "script", "source", "iframe") and values.get("src") is not None:
            self.links.append((tag + "[src]", values["src"]))


class Checker:
    def __init__(self, site):
        self.site = site.resolve()
        self.pages = {}
        self.errors = []
        self.warnings = []
        self.checked = 0

    def page(self, path):
        if path not in self.pages:
            parser = Page()
            parser.feed(path.read_text(encoding="utf-8", errors="replace"))
            self.pages[path] = parser
        return self.pages[path]

    def resolve(self, source, url):
        """Returns (target file, fragment), or (None, reason) when the link cannot be followed."""
        parts = urllib.parse.urlsplit(url)
        path = urllib.parse.unquote(parts.path)

        if not path:
            return source, parts.fragment

        if path.startswith("/"):
            if not path.startswith(BASE_PATH):
                return None, f"a root-relative link must start with {BASE_PATH}"
            target = self.site / path[len(BASE_PATH):]
        else:
            target = source.parent / path

        target = target.resolve()
        try:
            target.relative_to(self.site)
        except ValueError:
            return None, "the link leaves the site"

        if target.is_dir():
            target = target / "index.html"
        return target, parts.fragment

    def check_link(self, source, kind, url):
        url = url.strip()
        if not url or url.lower().startswith(EXTERNAL_SCHEMES):
            return

        self.checked += 1
        where = source.relative_to(self.site).as_posix()

        if url.lower().startswith("xref:"):
            self.errors.append(f"{where}: unresolved xref {url}")
            return

        target, detail = self.resolve(source, url)
        if target is None:
            self.errors.append(f"{where}: {kind} {url}: {detail}")
            return

        if not target.is_file():
            self.errors.append(f"{where}: {kind} {url}: no such file in the site")
            return

        fragment = urllib.parse.unquote(detail)
        if fragment and target.suffix == ".html" and fragment not in self.page(target).ids:
            self.warnings.append(f"{where}: {kind} {url}: no #{fragment} in {target.relative_to(self.site).as_posix()}")

    def run(self):
        files = sorted(self.site.rglob("*.html"))
        for file in files:
            for kind, url in self.page(file).links:
                self.check_link(file, kind, url)
        return len(files)


def report(title, lines):
    if not lines:
        return
    print(f"\n{title} ({len(lines)}):")
    for line in lines[:MAX_REPORTED]:
        print("  " + line)
    if len(lines) > MAX_REPORTED:
        print(f"  ... and {len(lines) - MAX_REPORTED} more")


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("site", nargs="?", default="docs/_site", help="the built site (default: docs/_site)")
    parser.add_argument("--strict", action="store_true", help="fail on warnings too")
    args = parser.parse_args()

    site = Path(args.site)
    if not site.is_dir():
        print(f"error: {site} is not a directory. Build the site first.", file=sys.stderr)
        return 2

    checker = Checker(site)
    pages = checker.run()
    if pages == 0:
        print(f"error: there are no pages in {site}.", file=sys.stderr)
        return 2

    report("Errors", checker.errors)
    report("Warnings", checker.warnings)
    print(f"\nChecked {checker.checked} links in {pages} pages: {len(checker.errors)} errors, {len(checker.warnings)} warnings.")

    if checker.errors or (args.strict and checker.warnings):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
