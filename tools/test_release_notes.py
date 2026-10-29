"""Tests of release_notes.py: what a release is made of, and what stops one that is not ready.

Run with: python -m unittest discover -s tools -p "test_*.py"
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import release_notes  # noqa: E402

CHANGELOG = """# Changelog

All notable changes to this project are documented in this file.

## [4.1.0] - Unreleased

Not released yet.

## [4.0.0] - 2026-11-20

The stable release.

### Breaking changes
- Something that changed.

### Added
- Something new.

## [4.0.0-beta2] - 2026-11-02

The second prerelease.

## [4.0.0-beta1] - 2026-10-27

The first prerelease.

## Links

Not a version.
"""


class FindSectionTests(unittest.TestCase):
    def test_a_section_is_what_is_under_its_heading_up_to_the_next_one(self):
        date, body = release_notes.find_section(CHANGELOG, "4.0.0")

        self.assertEqual("2026-11-20", date)
        self.assertEqual(
            ["The stable release.", "", "### Breaking changes", "- Something that changed.", "", "### Added", "- Something new."],
            body,
        )

    def test_a_prerelease_is_not_taken_for_its_release(self):
        self.assertEqual(["The second prerelease."], release_notes.find_section(CHANGELOG, "4.0.0-beta2")[1])
        self.assertEqual(["The first prerelease."], release_notes.find_section(CHANGELOG, "4.0.0-beta1")[1])

    def test_the_last_section_ends_at_a_heading_that_is_not_a_version(self):
        self.assertEqual(["The first prerelease."], release_notes.find_section(CHANGELOG, "4.0.0-beta1")[1])

    def test_the_last_section_of_the_file_ends_with_the_file(self):
        date, body = release_notes.find_section("## [1.0.0] - 2020-01-01\n\nText.\n", "1.0.0")

        self.assertEqual("2020-01-01", date)
        self.assertEqual(["Text."], body)

    def test_a_heading_without_a_date_has_none(self):
        self.assertIsNone(release_notes.find_section("## [1.0.0]\n\nText.\n", "1.0.0")[0])

    def test_a_version_without_a_section_is_refused(self):
        with self.assertRaisesRegex(release_notes.ReleaseNotesError, "no section"):
            release_notes.find_section(CHANGELOG, "9.9.9")

    def test_a_version_that_is_not_one_is_refused(self):
        for text in ("4.0", "v4.0.0", "4.0.0 beta", "", "4.0.0\n## [4.0.0]"):
            with self.subTest(text), self.assertRaisesRegex(release_notes.ReleaseNotesError, "not a version"):
                release_notes.find_section(CHANGELOG, text)

    def test_line_endings_of_windows_do_not_matter(self):
        date, body = release_notes.find_section(CHANGELOG.replace("\n", "\r\n"), "4.0.0-beta2")

        self.assertEqual("2026-11-02", date)
        self.assertEqual(["The second prerelease."], body)


class NotesOfTests(unittest.TestCase):
    def test_the_notes_of_a_released_version(self):
        self.assertEqual("The second prerelease.", release_notes.notes_of(CHANGELOG, "4.0.0-beta2"))

    def test_a_version_that_is_not_released_yet_is_refused(self):
        with self.assertRaisesRegex(release_notes.ReleaseNotesError, "Unreleased"):
            release_notes.notes_of(CHANGELOG, "4.1.0")

    def test_a_section_without_a_date_is_refused(self):
        with self.assertRaisesRegex(release_notes.ReleaseNotesError, "give it the date"):
            release_notes.notes_of("## [1.0.0]\n\nText.\n", "1.0.0")

    def test_an_empty_section_is_refused(self):
        with self.assertRaisesRegex(release_notes.ReleaseNotesError, "empty"):
            release_notes.notes_of("## [1.0.0] - 2020-01-01\n\n## [0.9.0] - 2019-01-01\n\nText.\n", "1.0.0")

    def test_a_date_that_is_not_a_date_is_refused(self):
        with self.assertRaisesRegex(release_notes.ReleaseNotesError, "give it the date"):
            release_notes.notes_of("## [1.0.0] - soon\n\nText.\n", "1.0.0")


class ComposeTests(unittest.TestCase):
    ASSETS = [("OBSClient.4.0.0.nupkg", "a" * 64), ("OBSClient.TestApp-4.0.0-win-x64.zip", "b" * 64)]

    def test_a_prerelease_says_so_and_a_release_does_not(self):
        prerelease = release_notes.compose("4.0.0-beta2", "v4.0.0-beta2", "Notes.", self.ASSETS)
        release = release_notes.compose("4.0.0", "v4.0.0", "Notes.", self.ASSETS)

        self.assertTrue(prerelease.startswith("> [!IMPORTANT]\n> This is a **prerelease**."))
        self.assertNotIn("prerelease", release)

    def test_it_says_how_to_install_that_version(self):
        text = release_notes.compose("4.0.0-beta2", "v4.0.0-beta2", "Notes.", self.ASSETS)

        self.assertIn("dotnet add package OBSClient --version 4.0.0-beta2", text)
        self.assertIn("https://www.nuget.org/packages/OBSClient/4.0.0-beta2", text)
        self.assertIn("https://github.com/tinodo/obsclient/blob/v4.0.0-beta2/CHANGELOG.md", text)

    def test_the_notes_come_before_the_install_and_the_assets(self):
        text = release_notes.compose("4.0.0", "v4.0.0", "These are the notes.", self.ASSETS)

        self.assertLess(text.index("These are the notes."), text.index("## Install"))
        self.assertLess(text.index("## Install"), text.index("## Assets"))

    def test_every_asset_is_described_and_has_its_checksum_in_sha256sum_format(self):
        text = release_notes.compose("4.0.0", "v4.0.0", "Notes.", self.ASSETS)

        self.assertIn("| `OBSClient.4.0.0.nupkg` | The package: the exact file that was pushed", text)
        self.assertIn("| `OBSClient.TestApp-4.0.0-win-x64.zip` | The test app.", text)
        self.assertIn("\n" + "a" * 64 + "  OBSClient.4.0.0.nupkg\n", text)
        self.assertIn("\n" + "b" * 64 + "  OBSClient.TestApp-4.0.0-win-x64.zip\n", text)

    def test_the_text_ends_with_one_newline(self):
        text = release_notes.compose("4.0.0", "v4.0.0", "Notes.", self.ASSETS)

        self.assertTrue(text.endswith("issues).\n"))
        self.assertFalse(text.endswith("\n\n"))

    def test_it_says_how_to_check_where_the_files_come_from(self):
        text = release_notes.compose("4.0.0", "v4.0.0", "Notes.", self.ASSETS)

        self.assertIn(
            "gh attestation verify OBSClient.4.0.0.nupkg --repo tinodo/obsclient "
            "--signer-workflow tinodo/obsclient/.github/workflows/publish.yml --source-ref refs/tags/v4.0.0\n",
            text,
        )
        self.assertLess(text.index("## Assets"), text.index("## Verify"))
        self.assertLess(text.index("## Verify"), text.index("Found a problem?"))

    def test_the_check_names_the_tag_and_the_folder_in_the_nuget_cache_of_that_version(self):
        text = release_notes.compose("4.0.0-RC.1", "v4.0.0-RC.1", "Notes.", self.ASSETS)

        self.assertIn("gh attestation verify OBSClient.4.0.0-RC.1.nupkg ", text)
        self.assertIn("--source-ref refs/tags/v4.0.0-RC.1\n", text)
        # NuGet keeps a package in a folder named with the lower case of its ID and of its version.
        self.assertIn("`~/.nuget/packages/obsclient/4.0.0-rc.1/lib/net8.0/OBSClient.dll`", text)


class CommandLineTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.changelog = os.path.join(self.folder.name, "CHANGELOG.md")
        with open(self.changelog, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(CHANGELOG)

    def run_main(self, *arguments):
        output = io.StringIO()
        errors = io.StringIO()
        with contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            code = release_notes.main(["--changelog", self.changelog, *arguments])
        return code, output.getvalue(), errors.getvalue()

    def test_check_says_what_kind_of_release_it_is_to_the_log_and_to_the_output_file_of_the_step(self):
        output = os.path.join(self.folder.name, "github-output")

        self.assertEqual(
            (0, "CHANGELOG.md has the notes of 4.0.0-beta2 (a prerelease).\n", ""),
            self.run_main("check", "4.0.0-beta2", "--github-output", output),
        )
        self.assertEqual(
            (0, "CHANGELOG.md has the notes of 4.0.0 (a release).\n", ""),
            self.run_main("check", "4.0.0", "--github-output", output),
        )

        # Appended, as every step of a job adds to the same file.
        with open(output, encoding="utf-8") as stream:
            self.assertEqual("prerelease=true\nprerelease=false\n", stream.read())

    def test_check_without_an_output_file_only_talks(self):
        self.assertEqual((0, "CHANGELOG.md has the notes of 4.0.0 (a release).\n", ""), self.run_main("check", "4.0.0"))

    def test_check_refuses_a_version_that_is_not_ready_with_an_annotation_for_the_run(self):
        output = os.path.join(self.folder.name, "github-output")

        code, printed, _ = self.run_main("check", "4.1.0", "--github-output", output)

        self.assertEqual(1, code)
        self.assertTrue(printed.startswith("::error::"), printed)
        self.assertIn("Unreleased", printed)
        self.assertFalse(os.path.exists(output), "a version that is not ready must not tell the workflow anything")

    def test_extract_prints_the_notes(self):
        self.assertEqual((0, "The first prerelease.\n", ""), self.run_main("extract", "4.0.0-beta1"))

    def test_compose_writes_the_release_with_the_checksums_of_the_files(self):
        package = os.path.join(self.folder.name, "OBSClient.4.0.0.nupkg")
        with open(package, "wb") as stream:
            stream.write(b"abc")
        output = os.path.join(self.folder.name, "notes.md")

        code, _, errors = self.run_main("compose", "4.0.0", "v4.0.0", "--asset", package, "--output", output)

        self.assertEqual(0, code, errors)
        with open(output, encoding="utf-8", newline="") as stream:
            text = stream.read()
        # SHA-256 of "abc", from the specification of the algorithm.
        self.assertIn("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  OBSClient.4.0.0.nupkg", text)
        self.assertIn("The stable release.", text)
        self.assertIn("gh attestation verify OBSClient.4.0.0.nupkg", text)
        self.assertNotIn("\r", text)

    def test_compose_refuses_a_file_that_is_not_there(self):
        output = os.path.join(self.folder.name, "notes.md")

        code, printed, _ = self.run_main("compose", "4.0.0", "v4.0.0", "--asset", "missing.nupkg", "--output", output)

        self.assertEqual(1, code)
        self.assertIn("missing.nupkg", printed)
        self.assertFalse(os.path.exists(output))

    def test_a_changelog_that_cannot_be_read_is_reported(self):
        os.remove(self.changelog)

        code, printed, _ = self.run_main("check", "4.0.0")

        self.assertEqual(1, code)
        self.assertIn("Cannot read", printed)


if __name__ == "__main__":
    unittest.main()
