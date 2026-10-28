"""Tests of verify-package.py: what it lets through, and what it stops.

Run with: python -m unittest discover -s tools -p "test_*.py"
"""
import contextlib
import importlib.util
import io
import os
import struct
import tempfile
import unittest
import zipfile
import zlib
from unittest import mock

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


def png_chunk(kind, payload):
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF)


def png(width=256, height=256, extra=0):
    """A valid PNG (an empty RGBA image), with a text chunk of `extra` bytes that makes it as large as a test needs."""
    rows = bytes(height * (1 + 4 * width))
    chunks = [png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))]
    if extra:
        chunks.append(png_chunk(b"tEXt", b"filler\x00" + b"x" * extra))
    chunks.append(png_chunk(b"IDAT", zlib.compress(rows)))
    chunks.append(png_chunk(b"IEND", b""))
    return b"\x89PNG\r\n\x1a\n" + b"".join(chunks)


def nuspec(*dependencies, icon="icon.png"):
    items = "".join(f'<dependency id="{name}" version="8.0.0" />' for name in dependencies)
    icon_element = f"<icon>{icon}</icon>" if icon else ""
    return (
        '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>OBSClient</id>'
        f"{icon_element}<dependencies><group targetFramework=\"net8.0\">{items}</group></dependencies></metadata></package>"
    ).encode()


def package_files(**changes):
    files = {
        **PLUMBING,
        "OBSClient.nuspec": nuspec("Microsoft.Extensions.Logging.Abstractions"),
        "README.md": b"# ObsClient",
        "LICENSE": b"MIT",
        "icon.png": png(),
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

    def verify(self, packages=None, symbols=None, options=None):
        """
        Verifies a package (and, unless None, the symbol package that is next to it), the way the pipelines do. The repository is
        named, unless the options say otherwise, so that the tests do not depend on where they run (GITHUB_REPOSITORY of a fork).
        """
        path = self.write("OBSClient.4.0.0.nupkg", package_files() if packages is None else packages)
        if symbols is not None:
            self.write("OBSClient.4.0.0.snupkg", symbols)
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            code = verify_package.main([*(["--repository", "tinodo/obsclient"] if options is None else options), path])
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

    def test_a_package_without_an_icon_is_refused(self):
        code, output = self.verify(packages=package_files(**{"icon.png": None}), symbols=symbol_files())

        self.assertEqual(1, code)
        self.assertIn("icon.png is missing", output)

    def test_an_icon_that_is_not_a_png_is_refused(self):
        code, output = self.verify(packages=package_files(**{"icon.png": b"<svg/>" + b" " * 40}), symbols=symbol_files())

        self.assertEqual(1, code)
        self.assertIn("icon.png is not a PNG", output)

    def test_an_icon_that_is_too_small_is_refused(self):
        code, output = self.verify(packages=package_files(**{"icon.png": png(width=64, height=256)}), symbols=symbol_files())

        self.assertEqual(1, code)
        self.assertIn("icon.png is 64 by 256 pixels, which is less than the 128 by 128", output)

    def test_an_icon_that_is_too_large_is_refused(self):
        code, output = self.verify(packages=package_files(**{"icon.png": png(extra=1024 * 1024)}), symbols=symbol_files())

        self.assertEqual(1, code)
        self.assertIn("icon.png is larger than 1 MB", output)

    def test_a_real_icon_passes_and_so_does_the_one_of_the_repository(self):
        self.assertEqual([], verify_package.icon_problems(png()))
        with open(os.path.join(os.path.dirname(_PATH), "..", "OBSClient", "icon.png"), "rb") as icon:
            self.assertEqual([], verify_package.icon_problems(icon.read()))

    def test_a_truncated_icon_is_refused(self):
        whole = png()
        for name, data in {
            "only the signature and the start of the header": whole[:24],
            "cut in the middle of the image data": whole[: len(whole) // 2],
            "without the last chunk": whole[:-12],
        }.items():
            with self.subTest(name):
                code, output = self.verify(packages=package_files(**{"icon.png": data}), symbols=symbol_files())

                self.assertEqual(1, code)
                self.assertIn("icon.png is not a complete PNG", output)

    def test_an_icon_with_a_damaged_chunk_is_refused(self):
        whole = bytearray(png())
        whole[len(whole) // 2] ^= 0xFF

        code, output = self.verify(packages=package_files(**{"icon.png": bytes(whole)}), symbols=symbol_files())

        self.assertEqual(1, code)
        self.assertIn("icon.png is not a complete PNG: the IDAT chunk is damaged", output)

    def test_an_icon_without_image_data_is_refused(self):
        header = png_chunk(b"IHDR", struct.pack(">IIBBBBB", 256, 256, 8, 6, 0, 0, 0))
        data = b"\x89PNG\r\n\x1a\n" + header + png_chunk(b"IEND", b"")

        code, output = self.verify(packages=package_files(**{"icon.png": data}), symbols=symbol_files())

        self.assertEqual(1, code)
        self.assertIn("icon.png is not a complete PNG: it has no image data", output)

    def test_an_icon_whose_image_data_cannot_be_read_is_refused(self):
        header = png_chunk(b"IHDR", struct.pack(">IIBBBBB", 256, 256, 8, 6, 0, 0, 0))
        data = b"\x89PNG\r\n\x1a\n" + header + png_chunk(b"IDAT", b"not zlib data") + png_chunk(b"IEND", b"")

        code, output = self.verify(packages=package_files(**{"icon.png": data}), symbols=symbol_files())

        self.assertEqual(1, code)
        self.assertIn("icon.png is not a complete PNG: its image data cannot be read", output)

    def test_an_icon_that_the_package_does_not_name_is_refused(self):
        code, output = self.verify(
            packages=package_files(**{"OBSClient.nuspec": nuspec("Microsoft.Extensions.Logging.Abstractions", icon=None)}),
            symbols=symbol_files(),
        )

        self.assertEqual(1, code)
        self.assertIn("the nuspec does not name icon.png as the icon of the package", output)

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

        with contextlib.redirect_stdout(output), mock.patch.dict(os.environ, {}, clear=True):
            code = verify_package.main([package, symbols])

        self.assertEqual(0, code, output.getvalue())
        self.assertEqual(1, output.getvalue().count("OBSClient.4.0.0.snupkg: "))

    def test_a_fork_verifies_symbols_that_point_at_itself(self):
        fork = SOURCE_LINK.replace(b"tinodo/obsclient", b"someone/else")
        symbols = symbol_files(**{"lib/net8.0/OBSClient.pdb": b"BSJB" + fork, "lib/net10.0/OBSClient.pdb": b"BSJB" + fork})

        accepted_code, accepted = self.verify(symbols=symbols, options=["--repository", "someone/else"])
        refused_code, refused = self.verify(symbols=symbols, options=["--repository", "tinodo/obsclient"])

        self.assertEqual(0, accepted_code, accepted)
        self.assertEqual(1, refused_code)
        self.assertIn("no Source Link to https://raw.githubusercontent.com/tinodo/obsclient/<commit>/", refused)

    def test_the_repository_is_the_one_of_github_actions_unless_one_is_named(self):
        fork = SOURCE_LINK.replace(b"tinodo/obsclient", b"someone/else")
        symbols = symbol_files(**{"lib/net8.0/OBSClient.pdb": b"BSJB" + fork, "lib/net10.0/OBSClient.pdb": b"BSJB" + fork})

        with mock.patch.dict(os.environ, {"GITHUB_REPOSITORY": "someone/else"}):
            from_the_environment, _ = self.verify(symbols=symbols, options=[])
            named_wins, named = self.verify(symbols=symbols, options=["--repository", "tinodo/obsclient"])

        self.assertEqual(0, from_the_environment)
        self.assertEqual(1, named_wins)
        self.assertIn("tinodo/obsclient", named)

    def test_without_a_repository_it_is_the_upstream_one(self):
        with mock.patch.dict(os.environ, {}, clear=True):
            code, output = self.verify(symbols=symbol_files(), options=[])

        self.assertEqual(0, code, output)

    def test_the_name_of_the_repository_is_not_case_sensitive(self):
        code, output = self.verify(symbols=symbol_files(), options=["--repository", "Tinodo/ObsClient"])

        self.assertEqual(0, code, output)

    def test_without_arguments_it_explains_itself(self):
        for arguments in ([], ["--repository", "someone/else"]):
            with self.subTest(arguments):
                output = io.StringIO()

                with contextlib.redirect_stdout(output):
                    code = verify_package.main(arguments)

                self.assertEqual(2, code)
                self.assertIn("Usage: verify-package.py", output.getvalue())


if __name__ == "__main__":
    unittest.main()
