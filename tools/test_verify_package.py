"""Tests of verify-package.py: what it lets through, and what it stops.

Run with: python -m unittest discover -s tools -p "test_*.py"
"""
import contextlib
import importlib.util
import io
import os
import tempfile
import unittest
import zipfile

_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "verify-package.py")
_SPEC = importlib.util.spec_from_file_location("verify_package", _PATH)
verify_package = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(verify_package)

COMMIT = "0123456789abcdef0123456789abcdef01234567"
SOURCE_LINK = ('{"documents":{"/_/*":"https://raw.githubusercontent.com/tinodo/obsclient/' + COMMIT + '/*"}}').encode()
PLUMBING = {
    "[Content_Types].xml": b"<Types/>",
    "_rels/.rels": b"<Relationships/>",
    "package/services/metadata/core-properties/0123abcd.psmdcp": b"<coreProperties/>",
}


def nuspec(*dependencies):
    items = "".join(f'<dependency id="{name}" version="8.0.0" />' for name in dependencies)
    return (
        '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>OBSClient</id>'
        f"<dependencies><group targetFramework=\"net8.0\">{items}</group></dependencies></metadata></package>"
    ).encode()


def package_files(**changes):
    files = {
        **PLUMBING,
        "OBSClient.nuspec": nuspec("Microsoft.Extensions.Logging.Abstractions"),
        "README.md": b"# ObsClient",
        "LICENSE": b"MIT",
    }
    for framework in ("net8.0", "net10.0"):
        files[f"lib/{framework}/OBSClient.dll"] = b"MZ"
        files[f"lib/{framework}/OBSClient.xml"] = b"<doc/>"
    files.update(changes)
    return {name: content for name, content in files.items() if content is not None}


def symbol_files(**changes):
    files = {**PLUMBING, "OBSClient.nuspec": nuspec()}
    for framework in ("net8.0", "net10.0"):
        files[f"lib/{framework}/OBSClient.pdb"] = b"BSJB" + SOURCE_LINK
    files.update(changes)
    return {name: content for name, content in files.items() if content is not None}


class VerifyPackageTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)

    def write(self, name, files):
        path = os.path.join(self.folder.name, name)
        with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as package:
            for entry, content in files.items():
                package.writestr(entry, content)
        return path

    def verify(self, packages=None, symbols=None):
        """Verifies a package (and, unless None, the symbol package that is next to it), the way the pipelines do."""
        path = self.write("OBSClient.4.0.0.nupkg", package_files() if packages is None else packages)
        if symbols is not None:
            self.write("OBSClient.4.0.0.snupkg", symbols)
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            code = verify_package.main([path])
        return code, output.getvalue()

    def test_a_package_with_its_symbols_passes(self):
        code, output = self.verify(symbols=symbol_files())

        self.assertEqual(0, code, output)
        self.assertIn("OK: the package is a single assembly", output)
        self.assertIn("OK: the symbol package has the PDB of each target framework, with Source Link.", output)

    def test_a_package_without_its_symbols_is_refused(self):
        code, output = self.verify()

        self.assertEqual(1, code)
        self.assertIn("OBSClient.4.0.0.snupkg: the file is missing", output)

    def test_a_pdb_in_the_package_is_refused(self):
        code, output = self.verify(packages=package_files(**{"lib/net8.0/OBSClient.pdb": b"BSJB"}), symbols=symbol_files())

        self.assertEqual(1, code)
        self.assertIn("unexpected file in the package: lib/net8.0/OBSClient.pdb", output)

    def test_a_file_that_is_not_the_library_is_refused(self):
        code, output = self.verify(packages=package_files(**{"contentFiles/protocol.json": b"{}"}), symbols=symbol_files())

        self.assertEqual(1, code)
        self.assertIn("unexpected file in the package: contentFiles/protocol.json", output)

    def test_a_target_framework_that_is_missing_or_unexpected_is_refused(self):
        code, output = self.verify(
            packages=package_files(**{"lib/net10.0/OBSClient.dll": None, "lib/net9.0/OBSClient.dll": b"MZ"}),
            symbols=symbol_files(),
        )

        self.assertEqual(1, code)
        self.assertIn("lib/net10.0/OBSClient.dll is missing", output)
        self.assertIn("unexpected target framework in the package: net9.0", output)

    def test_a_new_dependency_is_refused(self):
        code, output = self.verify(
            packages=package_files(**{"OBSClient.nuspec": nuspec("Microsoft.Extensions.Logging.Abstractions", "Newtonsoft.Json")}),
            symbols=symbol_files(),
        )

        self.assertEqual(1, code)
        self.assertIn("unexpected package dependency: Newtonsoft.Json", output)

    def test_symbols_without_source_link_are_refused(self):
        code, output = self.verify(symbols=symbol_files(**{"lib/net10.0/OBSClient.pdb": b"BSJB no link"}))

        self.assertEqual(1, code)
        self.assertIn("lib/net10.0/OBSClient.pdb has no Source Link", output)

    def test_source_link_to_another_repository_is_refused(self):
        other = SOURCE_LINK.replace(b"tinodo/obsclient", b"someone/else")

        code, output = self.verify(symbols=symbol_files(**{"lib/net8.0/OBSClient.pdb": b"BSJB" + other}))

        self.assertEqual(1, code)
        self.assertIn("lib/net8.0/OBSClient.pdb has no Source Link", output)

    def test_symbols_that_miss_a_target_framework_are_refused(self):
        code, output = self.verify(symbols=symbol_files(**{"lib/net8.0/OBSClient.pdb": None}))

        self.assertEqual(1, code)
        self.assertIn("lib/net8.0/OBSClient.pdb is missing", output)

    def test_an_assembly_in_the_symbols_is_refused(self):
        code, output = self.verify(symbols=symbol_files(**{"lib/net8.0/OBSClient.dll": b"MZ"}))

        self.assertEqual(1, code)
        self.assertIn("unexpected file in the symbol package: lib/net8.0/OBSClient.dll", output)

    def test_a_symbol_package_that_is_named_is_verified_too_and_only_once(self):
        package = self.write("OBSClient.4.0.0.nupkg", package_files())
        symbols = self.write("OBSClient.4.0.0.snupkg", symbol_files())
        output = io.StringIO()

        with contextlib.redirect_stdout(output):
            code = verify_package.main([package, symbols])

        self.assertEqual(0, code, output.getvalue())
        self.assertEqual(1, output.getvalue().count("OBSClient.4.0.0.snupkg: "))

    def test_without_arguments_it_explains_itself(self):
        output = io.StringIO()

        with contextlib.redirect_stdout(output):
            code = verify_package.main([])

        self.assertEqual(2, code)
        self.assertIn("Usage: verify-package.py", output.getvalue())


if __name__ == "__main__":
    unittest.main()
