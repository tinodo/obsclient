"""Tests of compare-published.py: when the package on nuget.org is the package that was built, and when it is not.

Run with: python -m unittest discover -s tools -p "test_*.py"
"""
import contextlib
import importlib.util
import io
import os
import tempfile
import unittest
import urllib.error
import zipfile
from unittest import mock

_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "compare-published.py")
_SPEC = importlib.util.spec_from_file_location("compare_published", _PATH)
compare_published = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(compare_published)

NUSPEC = (
    '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata>'
    "<id>OBSClient</id><version>4.0.0-Beta1+build5</version></metadata></package>"
).encode()


def package_bytes(**changes):
    files = {
        "[Content_Types].xml": b"<Types/>",
        "_rels/.rels": b"<Relationships/>",
        "OBSClient.nuspec": NUSPEC,
        "lib/net8.0/OBSClient.dll": b"MZ8",
        "lib/net10.0/OBSClient.dll": b"MZ10",
    }
    files.update(changes)
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as package:
        for name, content in files.items():
            if content is not None:
                package.writestr(name, content)
    return stream.getvalue()


class CompareTests(unittest.TestCase):
    def test_the_same_package_is_the_same(self):
        self.assertEqual([], compare_published.compare(package_bytes(), package_bytes()))

    def test_the_signature_that_nuget_org_adds_is_allowed(self):
        served = package_bytes(**{".signature.p7s": b"signed by nuget.org"})
        self.assertEqual([], compare_published.compare(package_bytes(), served))

    def test_a_file_that_differs_is_found(self):
        served = package_bytes(**{"lib/net8.0/OBSClient.dll": b"another build"})
        problems = compare_published.compare(package_bytes(), served)
        self.assertEqual(1, len(problems))
        self.assertIn("lib/net8.0/OBSClient.dll", problems[0])

    def test_a_file_that_is_missing_is_found(self):
        served = package_bytes(**{"lib/net10.0/OBSClient.dll": None})
        problems = compare_published.compare(package_bytes(), served)
        self.assertEqual(1, len(problems))
        self.assertIn("not in the one on nuget.org", problems[0])

    def test_a_file_that_is_only_on_nuget_org_is_found_unless_it_is_the_signature(self):
        served = package_bytes(**{"content/extra.txt": b"x", ".signature.p7s": b"s"})
        problems = compare_published.compare(package_bytes(), served)
        self.assertEqual(1, len(problems))
        self.assertIn("content/extra.txt", problems[0])

    def test_the_identity_is_in_lowercase_without_the_build_metadata(self):
        self.assertEqual(("obsclient", "4.0.0-beta1"), compare_published.identity(package_bytes()))

    def test_a_package_without_a_nuspec_has_no_identity(self):
        with self.assertRaises(ValueError):
            compare_published.identity(package_bytes(**{"OBSClient.nuspec": None}))


class MainTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.path = os.path.join(self.folder.name, "OBSClient.4.0.0-beta1.nupkg")
        with open(self.path, "wb") as stream:
            stream.write(package_bytes())

    def run_main(self, served=None, error=None, *arguments):
        calls = []

        def fetch(url):
            calls.append(url)
            if error is not None:
                raise error
            return served

        output = io.StringIO()
        with mock.patch.object(compare_published, "fetch", fetch), mock.patch.object(compare_published.time, "sleep") as sleep, \
                contextlib.redirect_stdout(output), contextlib.redirect_stderr(io.StringIO()):
            code = compare_published.main([self.path, *arguments])
        return code, output.getvalue(), calls, sleep

    def test_the_same_package_succeeds_and_the_url_is_the_one_of_the_flat_container(self):
        code, output, calls, _ = self.run_main(served=package_bytes(**{".signature.p7s": b"s"}))
        self.assertEqual(0, code)
        self.assertEqual(["https://api.nuget.org/v3-flatcontainer/obsclient/4.0.0-beta1/obsclient.4.0.0-beta1.nupkg"], calls)
        self.assertIn("is the package that was built", output)

    def test_another_package_fails_and_says_what_differs(self):
        code, output, _, _ = self.run_main(served=package_bytes(**{"lib/net8.0/OBSClient.dll": b"other"}))
        self.assertEqual(1, code)
        self.assertIn("::error::", output)
        self.assertIn("lib/net8.0/OBSClient.dll", output)

    def test_a_package_that_cannot_be_downloaded_is_tried_again_and_then_fails_with_another_code(self):
        error = urllib.error.HTTPError("url", 404, "Not Found", {}, None)
        code, output, calls, sleep = self.run_main(None, error, "--attempts", "3", "--wait", "1")
        self.assertEqual(2, code)
        self.assertEqual(3, len(calls))
        self.assertEqual(2, sleep.call_count)
        self.assertIn("could not be downloaded", output)

    def test_a_package_that_is_served_late_is_found(self):
        served = package_bytes()
        attempts = []

        def fetch(url):
            attempts.append(url)
            if len(attempts) < 3:
                raise urllib.error.HTTPError(url, 404, "Not Found", {}, None)
            return served

        with mock.patch.object(compare_published, "fetch", fetch), mock.patch.object(compare_published.time, "sleep"), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = compare_published.main([self.path, "--attempts", "5", "--wait", "0"])
        self.assertEqual(0, code)
        self.assertEqual(3, len(attempts))


if __name__ == "__main__":
    unittest.main()
