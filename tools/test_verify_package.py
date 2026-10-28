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


def png_of(width, height, depth, color, rows=b"", interlace=0, compression=0, filtering=0, extra_chunks=(), chunks_after=(), compressed=None):
    """A PNG with the header and the raster that a test gives it (`compressed` replaces the compressed raster), for what `png` does not cover.

    `extra_chunks` come before the image data and `chunks_after` after it."""
    chunks = [png_chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, depth, color, compression, filtering, interlace)), *extra_chunks]
    chunks.append(png_chunk(b"IDAT", zlib.compress(rows) if compressed is None else compressed))
    chunks.extend(chunks_after)
    chunks.append(png_chunk(b"IEND", b""))
    return b"\x89PNG\r\n\x1a\n" + b"".join(chunks)


# Kinds of PNG, as (width, height, bit depth, colour type, interlace, bytes of the raster). The sizes are worked out by hand: a row has a
# byte for its filter type and then its pixels, rounded up to whole bytes; the Adam7 image of 256 by 256 has these seven passes:
# 32 rows of 129 bytes, 32 of 129, 32 of 257, 64 of 257, 64 of 513, 128 of 513 and 128 of 1025.
PNG_KINDS = {
    "8 bits per sample with alpha": (256, 256, 8, 6, 0, 256 * 1025),
    "16 bits per sample, grey": (130, 140, 16, 0, 0, 140 * 261),
    "1 bit grey, rows that are not a whole number of bytes": (130, 130, 1, 0, 0, 130 * 18),
    "a palette": (128, 128, 8, 3, 0, 128 * 129),
    "interlaced (Adam7)": (256, 256, 8, 6, 1, 32 * 129 + 32 * 129 + 32 * 257 + 64 * 257 + 64 * 513 + 128 * 513 + 128 * 1025),
}


def png_of_kind(kind, delta=0):
    width, height, depth, color, interlace, size = PNG_KINDS[kind]
    palette = [png_chunk(b"PLTE", bytes(3))] if color == 3 else []
    return png_of(width, height, depth, color, bytes(size + delta), interlace=interlace, extra_chunks=palette)


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

    def test_an_icon_with_incomplete_pixel_data_in_a_valid_stream_is_refused(self):
        # A stream that is valid (and has a correct checksum), with one byte of the 262,400 that 256 by 256 pixels need.
        code, output = self.verify(packages=package_files(**{"icon.png": png_of(256, 256, 8, 6, b"\x00")}), symbols=symbol_files())

        self.assertEqual(1, code)
        self.assertIn("icon.png is not a complete PNG: its image data is incomplete (1 of the 262400 bytes that 256 by 256 pixels need)", output)

    def test_the_raster_of_every_kind_of_png_has_to_be_as_long_as_its_header_says(self):
        for kind, (width, height, *_) in PNG_KINDS.items():
            with self.subTest(kind):
                self.assertEqual([], verify_package.icon_problems(png_of_kind(kind)))
                short = verify_package.icon_problems(png_of_kind(kind, -1))
                long = verify_package.icon_problems(png_of_kind(kind, 1))

                self.assertEqual(1, len(short), short)
                self.assertIn(f"its image data is incomplete", short[0])
                self.assertEqual(1, len(long), long)
                self.assertIn(f"its image data is longer than", long[0])
                self.assertIn(f"{width} by {height} pixels", long[0])

    def test_a_row_with_a_filter_type_that_does_not_exist_is_refused(self):
        rows = bytearray(256 * 1025)
        rows[7 * 1025] = 5

        problems = verify_package.icon_problems(png_of(256, 256, 8, 6, bytes(rows)))

        self.assertEqual(["icon.png is not a complete PNG: a row of its image data has a filter type that does not exist"], problems)

    def test_a_filter_type_that_does_not_exist_is_found_in_every_pass_of_an_interlaced_icon(self):
        passes = [(32, 129), (32, 129), (32, 257), (64, 257), (64, 513), (128, 513), (128, 1025)]
        for number in range(len(passes)):
            with self.subTest(f"pass {number + 1}"):
                rows = bytearray(sum(count * length for count, length in passes))
                rows[sum(count * length for count, length in passes[:number])] = 5

                problems = verify_package.icon_problems(png_of(256, 256, 8, 6, bytes(rows), interlace=1))

                self.assertEqual(["icon.png is not a complete PNG: a row of its image data has a filter type that does not exist"], problems)

    def test_a_stream_that_does_not_end_and_bytes_after_its_end_are_refused(self):
        stream = zlib.compress(bytes(256 * 1025))

        cut = verify_package.icon_problems(png_of(256, 256, 8, 6, compressed=stream[:-4]))
        trailing = verify_package.icon_problems(png_of(256, 256, 8, 6, compressed=stream + b"more"))

        self.assertEqual(["icon.png is not a complete PNG: its image data is cut off: the compressed stream does not end"], cut)
        self.assertEqual(["icon.png is not a complete PNG: there are bytes after the end of its compressed image data"], trailing)

    def test_a_header_that_is_not_valid_is_refused(self):
        rows = bytes(256 * 1025)
        for name, header in {
            "a bit depth that the colour type does not have": {"depth": 4, "color": 6},
            "a colour type that does not exist": {"depth": 8, "color": 5},
            "a compression method that does not exist": {"depth": 8, "color": 6, "compression": 1},
            "a filter method that does not exist": {"depth": 8, "color": 6, "filtering": 1},
            "an interlace method that does not exist": {"depth": 8, "color": 6, "interlace": 2},
        }.items():
            with self.subTest(name):
                problems = verify_package.icon_problems(png_of(256, 256, rows=rows, **header))

                self.assertEqual(1, len(problems), problems)
                self.assertIn("icon.png is not a complete PNG: its header is not valid", problems[0])

    def test_an_icon_with_a_palette_type_and_no_palette_is_refused(self):
        problems = verify_package.icon_problems(png_of(128, 128, 8, 3, bytes(128 * 129)))

        self.assertEqual(["icon.png is not a complete PNG: it has no palette"], problems)

    def test_the_palette_of_an_indexed_icon_has_to_be_whole_entries_and_has_to_fit_the_bit_depth(self):
        eight, one = bytes(128 * 129), bytes(128 * 17)
        whole = "its palette is not made of whole entries of 3 bytes"
        for name, depth, rows, palette, expected in (
            ("an empty palette", 8, eight, b"", f"{whole} (it has 0 bytes)"),
            ("a palette of two bytes", 8, eight, bytes(2), f"{whole} (it has 2 bytes)"),
            ("a palette of four bytes", 8, eight, bytes(4), f"{whole} (it has 4 bytes)"),
            ("257 entries at 8 bits", 8, eight, bytes(257 * 3), "its palette has 257 entries, where at most 256 are possible"),
            ("3 entries at 1 bit", 1, one, bytes(9), "its palette has 3 entries, where at most 2 are possible"),
        ):
            with self.subTest(name):
                problems = verify_package.icon_problems(png_of(128, 128, depth, 3, rows, extra_chunks=[png_chunk(b"PLTE", palette)]))

                self.assertEqual([f"icon.png is not a complete PNG: {expected}"], problems)

    def test_a_palette_with_as_many_entries_as_the_bit_depth_has_values_is_accepted(self):
        for depth, rows, entries in ((8, bytes(128 * 129), 256), (8, bytes(128 * 129), 1), (1, bytes(128 * 17), 2), (1, bytes(128 * 17), 1)):
            with self.subTest(f"{entries} entries at {depth} bits"):
                data = png_of(128, 128, depth, 3, rows, extra_chunks=[png_chunk(b"PLTE", bytes(entries * 3))])

                self.assertEqual([], verify_package.icon_problems(data))

    def test_more_than_one_palette_and_a_palette_after_the_image_data_are_refused(self):
        rows = bytes(128 * 129)
        palette = png_chunk(b"PLTE", bytes(3))

        twice = verify_package.icon_problems(png_of(128, 128, 8, 3, rows, extra_chunks=[palette, palette]))
        late = verify_package.icon_problems(png_of(128, 128, 8, 3, rows, chunks_after=[palette]))

        self.assertEqual(["icon.png is not a complete PNG: it has more than one palette"], twice)
        self.assertEqual(["icon.png is not a complete PNG: its palette comes after its image data"], late)

    def test_a_greyscale_icon_cannot_have_a_palette_and_a_colour_icon_may_have_a_suggested_one(self):
        palette = png_chunk(b"PLTE", bytes(3 * 16))
        broken = png_chunk(b"PLTE", bytes(2))

        grey = verify_package.icon_problems(png_of(128, 128, 8, 0, bytes(128 * 129), extra_chunks=[palette]))
        grey_with_alpha = verify_package.icon_problems(png_of(128, 128, 8, 4, bytes(128 * 257), extra_chunks=[palette]))
        colour = verify_package.icon_problems(png_of(256, 256, 8, 6, bytes(256 * 1025), extra_chunks=[palette]))
        colour_with_a_broken_one = verify_package.icon_problems(png_of(256, 256, 8, 6, bytes(256 * 1025), extra_chunks=[broken]))

        self.assertEqual(["icon.png is not a complete PNG: it has a palette, which a greyscale image cannot have"], grey)
        self.assertEqual(grey, grey_with_alpha)
        self.assertEqual([], colour)
        self.assertEqual(["icon.png is not a complete PNG: its palette is not made of whole entries of 3 bytes (it has 2 bytes)"], colour_with_a_broken_one)

    def test_chunks_that_do_not_follow_the_rules_of_png_are_refused(self):
        rows = bytes(256 * 1025)
        header = struct.pack(">IIBBBBB", 256, 256, 8, 6, 0, 0, 0)
        stream = zlib.compress(rows)
        half = len(stream) // 2

        def whole(*chunks):
            return b"\x89PNG\r\n\x1a\n" + b"".join(chunks)

        in_a_run = whole(png_chunk(b"IHDR", header), png_chunk(b"IDAT", stream[:half]), png_chunk(b"IDAT", stream[half:]), png_chunk(b"IEND", b""))
        interrupted = whole(
            png_chunk(b"IHDR", header), png_chunk(b"IDAT", stream[:half]), png_chunk(b"tEXt", b"a\x00b"), png_chunk(b"IDAT", stream[half:]), png_chunk(b"IEND", b""))
        self.assertEqual([], verify_package.icon_problems(in_a_run))
        for name, data, expected in (
            ("two headers", png_of(256, 256, 8, 6, rows, extra_chunks=[png_chunk(b"IHDR", header)]), ["it has more than one IHDR chunk"]),
            ("two ends", png_of(256, 256, 8, 6, rows, chunks_after=[png_chunk(b"IEND", b"")]), ["it has more than one IEND chunk"]),
            (
                "image data that another chunk interrupts",
                interrupted,
                ["its image data is not in one run of IDAT chunks", "it has a tEXt chunk, which an icon does not need: save it without metadata (tools/make-icon.py writes none)"],
            ),
            (
                "a chunk that a decoder has to understand, and that PNG does not have",
                png_of(256, 256, 8, 6, rows, extra_chunks=[png_chunk(b"ABCD", b"")]),
                ["it has a ABCD chunk that a decoder has to understand and that is not part of PNG"],
            ),
        ):
            with self.subTest(name):
                self.assertEqual([f"icon.png is not a complete PNG: {problem}" for problem in expected], verify_package.icon_problems(data))

    def test_chunks_that_an_icon_does_not_need_are_refused_with_what_to_do_about_it(self):
        rows = bytes(256 * 1025)
        # A profile that is made as the PNG specification has it, so that it is what the icon has, and not how it is made, that is refused.
        profile = b"icc\x00\x00" + zlib.compress(b"profile")
        for name, kind, payload in (
            ("text", b"tEXt", b"Software\x00a program"),
            ("compressed text", b"zTXt", b"Comment\x00\x00" + zlib.compress(b"text")),
            ("international text", b"iTXt", b"Title\x00\x00\x00\x00\x00text"),
            ("a colour profile", b"iCCP", profile),
            ("the time of the last change", b"tIME", bytes(7)),
            ("the chromaticities", b"cHRM", bytes(32)),
            ("significant bits", b"sBIT", bytes(4)),
            ("a background colour", b"bKGD", bytes(6)),
            ("exif data", b"eXIf", b"II*\x00"),
            ("a chunk of a program that PNG does not know", b"abCD", b"x"),
        ):
            with self.subTest(name):
                problems = verify_package.icon_problems(png_of(256, 256, 8, 6, rows, extra_chunks=[png_chunk(kind, payload)]))

                self.assertEqual(
                    [f"icon.png is not a complete PNG: it has a {kind.decode()} chunk, which an icon does not need: save it without metadata (tools/make-icon.py writes none)"],
                    problems)

    def test_an_empty_or_a_broken_metadata_chunk_is_refused_because_an_icon_has_none(self):
        # What a profile (iCCP) that is empty, or has a name and then no stream that can be read, shows: they are not looked at, they are not allowed.
        rows = bytes(256 * 1025)
        for name, payload in (("empty", b""), ("a name and a stream that is not one", b"name\x00\x00not zlib")):
            with self.subTest(name):
                problems = verify_package.icon_problems(png_of(256, 256, 8, 6, rows, extra_chunks=[png_chunk(b"iCCP", payload)]))

                self.assertEqual(1, len(problems), problems)
                self.assertIn("it has a iCCP chunk, which an icon does not need", problems[0])

    def test_a_chunk_whose_name_is_not_four_letters_is_refused(self):
        rows = bytes(256 * 1025)
        for name, kind in (
            ("a digit", b"t1Xt"),
            ("a space", b"ab d"),
            ("a byte that is not text", b"\x00\x01\x02\x03"),
            ("a letter that is not ASCII", b"t\xe9Xt"),
        ):
            with self.subTest(name):
                problems = verify_package.icon_problems(png_of(256, 256, 8, 6, rows, extra_chunks=[png_chunk(kind, b"")]))

                self.assertEqual([f"icon.png is not a complete PNG: a chunk has a name that is not four letters ({ascii(kind.decode('latin-1'))})"], problems)

    def test_the_transparency_of_a_png_has_to_follow_its_colour_type(self):
        grey, rgb, grey_with_alpha, rgba = bytes(128 * 129), bytes(128 * 385), bytes(128 * 257), bytes(256 * 1025)
        palette = png_chunk(b"PLTE", bytes(6))
        transparency = lambda size: png_chunk(b"tRNS", bytes(size))  # noqa: E731
        for name, data, expected in (
            ("grey, with the grey level that is transparent", png_of(128, 128, 8, 0, grey, extra_chunks=[transparency(2)]), None),
            ("grey, with too many bytes", png_of(128, 128, 8, 0, grey, extra_chunks=[transparency(3)]), "its tRNS chunk has 3 bytes, where 2 are needed"),
            ("RGB, with the colour that is transparent", png_of(128, 128, 8, 2, rgb, extra_chunks=[transparency(6)]), None),
            ("RGB, with too few bytes", png_of(128, 128, 8, 2, rgb, extra_chunks=[transparency(5)]), "its tRNS chunk has 5 bytes, where 6 are needed"),
            ("indexed, with a value for every entry", png_of(128, 128, 8, 3, grey, extra_chunks=[palette, transparency(2)]), None),
            ("indexed, with fewer values than entries", png_of(128, 128, 8, 3, grey, extra_chunks=[palette, transparency(1)]), None),
            ("indexed, with more values than entries", png_of(128, 128, 8, 3, grey, extra_chunks=[palette, transparency(3)]), "its tRNS chunk has 3 values, for a palette of 2 entries"),
            ("grey with alpha", png_of(128, 128, 8, 4, grey_with_alpha, extra_chunks=[transparency(2)]), "it has a tRNS chunk, which an image with an alpha channel cannot have"),
            ("RGBA", png_of(256, 256, 8, 6, rgba, extra_chunks=[transparency(6)]), "it has a tRNS chunk, which an image with an alpha channel cannot have"),
            ("two of them", png_of(128, 128, 8, 0, grey, extra_chunks=[transparency(2), transparency(2)]), "it has more than one tRNS chunk"),
            ("after the image data", png_of(128, 128, 8, 0, grey, chunks_after=[transparency(2)]), "its tRNS chunk comes after its image data"),
        ):
            with self.subTest(name):
                self.assertEqual([] if expected is None else [f"icon.png is not a complete PNG: {expected}"], verify_package.icon_problems(data))

    def test_chunks_that_go_before_or_after_the_palette_are_refused_in_the_wrong_place(self):
        indexed = bytes(128 * 129)
        palette = png_chunk(b"PLTE", bytes(6))
        chunk = lambda name, size: png_chunk(name, bytes([1] * size))  # noqa: E731
        for name, chunks, expected in (
            ("gAMA before the palette", [chunk(b"gAMA", 4), palette], None),
            ("gAMA after the palette", [palette, chunk(b"gAMA", 4)], "its gAMA chunk comes after its palette"),
            ("sRGB before the palette", [chunk(b"sRGB", 1), palette], None),
            ("sRGB after the palette", [palette, chunk(b"sRGB", 1)], "its sRGB chunk comes after its palette"),
            ("tRNS after the palette", [palette, chunk(b"tRNS", 2)], None),
            ("tRNS before the palette", [chunk(b"tRNS", 2), palette], "its tRNS chunk comes before its palette"),
        ):
            with self.subTest(name):
                problems = verify_package.icon_problems(png_of(128, 128, 8, 3, indexed, extra_chunks=chunks))

                self.assertEqual([] if expected is None else [f"icon.png is not a complete PNG: {expected}"], problems)

    def test_chunks_that_go_before_the_image_data_are_refused_after_it(self):
        rows = bytes(256 * 1025)
        chunk = lambda name, payload: png_chunk(name, payload)  # noqa: E731
        physical = struct.pack(">IIB", 2835, 2835, 1)
        for name, data, expected in (
            ("pHYs before the image data", png_of(256, 256, 8, 6, rows, extra_chunks=[chunk(b"pHYs", physical)]), None),
            ("pHYs after the image data", png_of(256, 256, 8, 6, rows, chunks_after=[chunk(b"pHYs", physical)]), "its pHYs chunk comes after its image data"),
            ("gAMA after the image data", png_of(256, 256, 8, 6, rows, chunks_after=[chunk(b"gAMA", struct.pack(">I", 45455))]), "its gAMA chunk comes after its image data"),
            ("sRGB after the image data", png_of(256, 256, 8, 6, rows, chunks_after=[chunk(b"sRGB", b"\x00")]), "its sRGB chunk comes after its image data"),
        ):
            with self.subTest(name):
                self.assertEqual([] if expected is None else [f"icon.png is not a complete PNG: {expected}"], verify_package.icon_problems(data))

    def test_the_chunks_that_an_icon_may_have_are_refused_with_another_size_and_when_there_are_two(self):
        rows = bytes(256 * 1025)
        gamma, physical = struct.pack(">I", 45455), struct.pack(">IIB", 2835, 2835, 1)
        for name, chunks, expected in (
            ("gAMA of 4 bytes", [png_chunk(b"gAMA", gamma)], None),
            ("gAMA of 5 bytes", [png_chunk(b"gAMA", gamma + b"\x00")], "its gAMA chunk has 5 bytes, where 4 are needed"),
            ("sRGB of 1 byte", [png_chunk(b"sRGB", b"\x00")], None),
            ("sRGB of 2 bytes", [png_chunk(b"sRGB", b"\x00\x00")], "its sRGB chunk has 2 bytes, where 1 are needed"),
            ("pHYs of 9 bytes", [png_chunk(b"pHYs", physical)], None),
            ("pHYs of 8 bytes", [png_chunk(b"pHYs", physical[:-1])], "its pHYs chunk has 8 bytes, where 9 are needed"),
            ("an empty sRGB", [png_chunk(b"sRGB", b"")], "its sRGB chunk has 0 bytes, where 1 are needed"),
            ("two gAMA", [png_chunk(b"gAMA", gamma), png_chunk(b"gAMA", gamma)], "it has more than one gAMA chunk"),
            ("two pHYs", [png_chunk(b"pHYs", physical), png_chunk(b"pHYs", physical)], "it has more than one pHYs chunk"),
        ):
            with self.subTest(name):
                problems = verify_package.icon_problems(png_of(256, 256, 8, 6, rows, extra_chunks=chunks))

                self.assertEqual([] if expected is None else [f"icon.png is not a complete PNG: {expected}"], problems)

    def test_the_values_of_the_chunks_that_an_icon_may_have_have_to_be_possible(self):
        rows = bytes(256 * 1025)
        for name, chunk, expected in (
            ("a rendering intent that does not exist", png_chunk(b"sRGB", b"\x04"), "its sRGB chunk has a rendering intent (4) that does not exist"),
            ("a gamma of zero", png_chunk(b"gAMA", bytes(4)), "its gAMA chunk has a gamma of zero"),
            ("a gamma", png_chunk(b"gAMA", struct.pack(">I", 1)), None),
            ("a unit that does not exist", png_chunk(b"pHYs", struct.pack(">IIB", 1, 1, 2)), "its pHYs chunk has a unit (2) that does not exist"),
            ("no unit", png_chunk(b"pHYs", struct.pack(">IIB", 1, 1, 0)), None),
            ("a size that PNG cannot hold, across", png_chunk(b"pHYs", struct.pack(">IIB", 2**31, 1, 1)), "its pHYs chunk has a size that is more than a PNG can hold"),
            ("a size that PNG cannot hold, down", png_chunk(b"pHYs", struct.pack(">IIB", 1, 2**32 - 1, 1)), "its pHYs chunk has a size that is more than a PNG can hold"),
            ("the largest size that PNG holds", png_chunk(b"pHYs", struct.pack(">IIB", 2**31 - 1, 2**31 - 1, 1)), None),
        ):
            with self.subTest(name):
                problems = verify_package.icon_problems(png_of(256, 256, 8, 6, rows, extra_chunks=[chunk]))

                self.assertEqual([] if expected is None else [f"icon.png is not a complete PNG: {expected}"], problems)

    def test_every_rendering_intent_that_exists_is_accepted(self):
        for intent in (0, 1, 2, 3):
            with self.subTest(intent):
                data = png_of(256, 256, 8, 6, bytes(256 * 1025), extra_chunks=[png_chunk(b"sRGB", bytes([intent]))])

                self.assertEqual([], verify_package.icon_problems(data))

    def test_an_end_chunk_with_data_in_it_is_refused(self):
        header = struct.pack(">IIBBBBB", 256, 256, 8, 6, 0, 0, 0)
        data = b"\x89PNG\r\n\x1a\n" + png_chunk(b"IHDR", header) + png_chunk(b"IDAT", zlib.compress(bytes(256 * 1025))) + png_chunk(b"IEND", b"x")

        self.assertEqual(["icon.png is not a complete PNG: its IEND chunk has data in it, where it has none"], verify_package.icon_problems(data))

    def test_an_icon_that_claims_a_huge_image_is_refused_without_being_decoded(self):
        # A few hundred bytes that claim 100,000 by 100,000 pixels: what they hold is never read.
        problems = verify_package.icon_problems(png_of(100_000, 100_000, 8, 6, b"\x00"))

        self.assertEqual(["icon.png is not a complete PNG: its 100000 by 100000 pixels need more than 64 MB once they are decoded"], problems)

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
