#!/usr/bin/env python3
"""Verifies that the OBSClient NuGet package is exactly one thing: the library.

The package may hold the assembly (and its XML documentation) for each target framework, the README, the license and the icon, and
nothing else. In particular there must be no JSON (the protocol definition and the code generator are
design-time inputs under tools/ and never ship), no analyzer or source generator, no build targets and no content
files. The package also must not take on a new runtime dependency unless somebody decides that on purpose. The icon has to be a PNG
that nuget.org accepts (at most 1 MB, and at least the 128 by 128 pixels that NuGet recommends) and a whole one: the checksums of its
chunks are right, they are in the order, the number and the size that PNG asks for, its header, palette and transparency are valid, and
its image data is as long as its header says, with rows that have a filter type that exists. An icon is a plain image: next to the chunks
that every PNG has it may only have the few that say how the picture is shown (tRNS, sRGB, gAMA, pHYs), and none of the metadata that
programs add (text, profiles, times, ...), which it does not need and which would have to be checked as well. The pixels themselves are
not decoded (that is what an image library does, and a look at the icon). The package has to name the icon.

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
import zlib

ASSEMBLY_NAME = "OBSClient"
DEFAULT_REPOSITORY = "tinodo/obsclient"
REQUIRED_FRAMEWORKS = {"net8.0", "net10.0"}
ALLOWED_DEPENDENCIES = {"Microsoft.Extensions.Logging.Abstractions"}

ICON_NAME = "icon.png"
ICON_MAX_BYTES = 1024 * 1024  # nuget.org refuses a bigger icon
ICON_MIN_PIXELS = 128  # what NuGet recommends
ICON_MAX_RASTER_BYTES = 64 * 1024 * 1024  # what an icon may need once it is decoded: a PNG of 1 MB can claim much more, and is not read then
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"

# What a PNG may hold: the bit depths that a colour type allows, and its samples per pixel.
PNG_COLOR_TYPES = {0: ({1, 2, 4, 8, 16}, 1), 2: ({8, 16}, 3), 3: ({1, 2, 4, 8}, 1), 4: ({8, 16}, 2), 6: ({8, 16}, 4)}

CRITICAL_CHUNKS = {b"IHDR", b"PLTE", b"IDAT", b"IEND"}  # the chunks that every decoder has to understand
PNG_MAX_INTEGER = 2**31 - 1  # the largest number that PNG holds in four bytes

# An icon is a plain image. Next to the chunks that every PNG has it may only have the few that say how the picture is shown, and none of
# the metadata that programs add (text, profiles, times, ...): an icon does not need them, and each kind would have to be checked as well.
# As (where, bytes): where is before the palette (before PLTE, when there is one), after the palette, or before the image data; the bytes
# are a number, or a number for each colour type. There is at most one of each of them.
PNG_PLACED_CHUNKS = {
    b"gAMA": ("before the palette", 4),
    b"sRGB": ("before the palette", 1),
    b"tRNS": ("after the palette", {0: 2, 2: 6}),
    b"pHYs": ("before the image data", 9),
}
ICON_CHUNKS = CRITICAL_CHUNKS | set(PNG_PLACED_CHUNKS)

# The passes of an interlaced (Adam7) image, as (x start, y start, x step, y step); a plain image is one pass over everything.
PNG_PASSES = {0: [(0, 0, 1, 1)], 1: [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)]}

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


def png_chunks(data):
    """The chunks of a PNG as (type, payload), in order. A ValueError says what is damaged: a chunk that is cut off, a name that is not four letters, or a wrong CRC."""
    position = len(PNG_SIGNATURE)
    chunks = []
    while position < len(data):
        if position + 8 > len(data):
            raise ValueError("the header of a chunk is cut off")
        length, kind = struct.unpack(">I4s", data[position : position + 8])
        if not re.fullmatch(rb"[A-Za-z]{4}", kind):
            raise ValueError(f"a chunk has a name that is not four letters ({ascii(kind.decode('latin-1'))})")
        end = position + 8 + length + 4
        if end > len(data):
            raise ValueError(f"the {kind.decode('latin-1')} chunk is cut off")
        payload = data[position + 8 : end - 4]
        (crc,) = struct.unpack(">I", data[end - 4 : end])
        if zlib.crc32(kind + payload) & 0xFFFFFFFF != crc:
            raise ValueError(f"the {kind.decode('latin-1')} chunk is damaged (its CRC is wrong)")
        chunks.append((kind, payload))
        position = end
    return chunks


def png_rows(width, height, bits_per_pixel, interlace):
    """What the decoded image of a PNG is made of, as (rows, bytes of a row) for each pass; a row has a byte for its filter type in front."""
    passes = []
    for x_start, y_start, x_step, y_step in PNG_PASSES[interlace]:
        pass_width = (width - x_start + x_step - 1) // x_step if width > x_start else 0
        pass_height = (height - y_start + y_step - 1) // y_step if height > y_start else 0
        if pass_width and pass_height:
            passes.append((pass_height, 1 + (pass_width * bits_per_pixel + 7) // 8))
    return passes


def raster_problems(pixels, width, height, bits_per_pixel, interlace):
    """What is wrong with the compressed image data of a PNG, given what its header says: the problems, as texts."""
    passes = png_rows(width, height, bits_per_pixel, interlace)
    expected = sum(rows * length for rows, length in passes)
    if expected > ICON_MAX_RASTER_BYTES:
        return [f"its {width} by {height} pixels need more than {ICON_MAX_RASTER_BYTES // (1024 * 1024)} MB once they are decoded"]

    # Never more than what the header calls for (and one byte more, to see that there is more): a small file can claim a huge image.
    decoder = zlib.decompressobj()
    try:
        raster = decoder.decompress(pixels, expected + 1)
    except zlib.error as error:
        return [f"its image data cannot be read ({error})"]
    if len(raster) < expected:
        return [f"its image data is incomplete ({len(raster)} of the {expected} bytes that {width} by {height} pixels need)"]
    if len(raster) > expected:
        return [f"its image data is longer than the {expected} bytes that {width} by {height} pixels need"]

    problems = []
    if not decoder.eof:
        problems.append("its image data is cut off: the compressed stream does not end")
    elif decoder.unused_data:
        problems.append("there are bytes after the end of its compressed image data")
    offset = 0
    for rows, length in passes:
        filters = raster[offset : offset + rows * length : length]
        if max(filters) > 4:
            problems.append("a row of its image data has a filter type that does not exist")
            break
        offset += rows * length
    return problems


def palette_problems(chunks, color, depth):
    """What is wrong with the palette (the PLTE chunk) of a PNG, given its colour type and bit depth: the problems, as texts."""
    kinds = [kind for kind, _ in chunks]
    palettes = [payload for kind, payload in chunks if kind == b"PLTE"]
    if color in (0, 4):
        return ["it has a palette, which a greyscale image cannot have"] if palettes else []
    if not palettes:
        return ["it has no palette"] if color == 3 else []
    if len(palettes) > 1:
        return ["it has more than one palette"]

    # An indexed image has one entry for every value that a pixel can have, at most; the others hold a palette only as a suggestion.
    palette = palettes[0]
    most = 2**depth if color == 3 else 256
    problems = []
    if not palette or len(palette) % 3:
        problems.append(f"its palette is not made of whole entries of 3 bytes (it has {len(palette)} bytes)")
    elif len(palette) // 3 > most:
        problems.append(f"its palette has {len(palette) // 3} entries, where at most {most} are possible")
    if b"IDAT" in kinds and kinds.index(b"PLTE") > kinds.index(b"IDAT"):
        problems.append("its palette comes after its image data")
    return problems


def chunk_problems(chunks, color, depth):
    """What is wrong with the chunks of a PNG that is to be an icon: their number, order, size and values, and those that an icon does not need."""
    kinds = [kind for kind, _ in chunks]
    problems = []
    for name in (b"IHDR", b"IEND"):
        if kinds.count(name) > 1:
            problems.append(f"it has more than one {name.decode()} chunk")
    if any(kind == b"IEND" and payload for kind, payload in chunks):
        problems.append("its IEND chunk has data in it, where it has none")
    positions = [position for position, kind in enumerate(kinds) if kind == b"IDAT"]
    if positions and positions[-1] - positions[0] + 1 != len(positions):
        problems.append("its image data is not in one run of IDAT chunks")
    # The first letter of the name of a chunk is a capital when a decoder cannot go without it: one that is not in PNG cannot be read.
    for kind in sorted(set(kinds) - ICON_CHUNKS):
        label = kind.decode()
        if not kind[0] & 0x20:
            problems.append(f"it has a {label} chunk that a decoder has to understand and that is not part of PNG")
        else:
            problems.append(f"it has a {label} chunk, which an icon does not need: save it without metadata (tools/make-icon.py writes none)")

    data_at = kinds.index(b"IDAT") if b"IDAT" in kinds else None
    palette_at = kinds.index(b"PLTE") if b"PLTE" in kinds else None
    for name, (where, size) in PNG_PLACED_CHUNKS.items():
        found = [(position, payload) for position, (kind, payload) in enumerate(chunks) if kind == name]
        label = name.decode()
        if len(found) > 1:
            problems.append(f"it has more than one {label} chunk")
        if len(found) != 1:
            continue
        position, payload = found[0]
        if data_at is not None and position > data_at:
            problems.append(f"its {label} chunk comes after its image data")
        if where == "before the palette" and palette_at is not None and position > palette_at:
            problems.append(f"its {label} chunk comes after its palette")
        if where == "after the palette" and palette_at is not None and position < palette_at:
            problems.append(f"its {label} chunk comes before its palette")
        wanted = size.get(color) if isinstance(size, dict) else size
        if wanted is not None and len(payload) != wanted:
            problems.append(f"its {label} chunk has {len(payload)} bytes, where {wanted} are needed")
        elif name == b"sRGB" and payload[0] > 3:
            problems.append(f"its sRGB chunk has a rendering intent ({payload[0]}) that does not exist")
        elif name == b"gAMA" and not struct.unpack(">I", payload)[0]:
            problems.append("its gAMA chunk has a gamma of zero")
        elif name == b"pHYs":
            across, down, unit = struct.unpack(">IIB", payload)
            if unit > 1:
                problems.append(f"its pHYs chunk has a unit ({unit}) that does not exist")
            if max(across, down) > PNG_MAX_INTEGER:
                problems.append("its pHYs chunk has a size that is more than a PNG can hold")

    # What depends on the colour type and on the palette more than on the place or the size of the chunk.
    palettes = [payload for kind, payload in chunks if kind == b"PLTE"]
    entries = len(palettes[0]) // 3 if len(palettes) == 1 else None
    transparency = [payload for kind, payload in chunks if kind == b"tRNS"]
    if transparency and color in (4, 6):
        problems.append("it has a tRNS chunk, which an image with an alpha channel cannot have")
    elif len(transparency) == 1 and color == 3 and entries is not None and len(transparency[0]) > entries:
        problems.append(f"its tRNS chunk has {len(transparency[0])} values, for a palette of {entries} entries")
    elif len(transparency) == 1 and color in (0, 2) and len(transparency[0]) == (2 if color == 0 else 6):
        # The sample values of the colour that is transparent are 16 bits wide in the chunk, whatever the bit depth: they have to fit the depth.
        for sample in struct.unpack(f">{len(transparency[0]) // 2}H", transparency[0]):
            if sample >= 2**depth:
                problems.append(f"its tRNS chunk has a sample ({sample}) that is too big for a bit depth of {depth}")
                break
    return problems


def icon_problems(data):
    problems = []
    if len(data) > ICON_MAX_BYTES:
        problems.append(f"{ICON_NAME} is larger than 1 MB, which nuget.org refuses")
    if data[:8] != PNG_SIGNATURE:
        problems.append(f"{ICON_NAME} is not a PNG")
        return problems

    try:
        chunks = png_chunks(data)
    except ValueError as error:
        problems.append(f"{ICON_NAME} is not a complete PNG: {error}")
        return problems

    kinds = [kind for kind, _ in chunks]
    if not chunks or kinds[0] != b"IHDR" or len(chunks[0][1]) != 13:
        problems.append(f"{ICON_NAME} is not a complete PNG: it does not start with its IHDR chunk")
        return problems

    width, height, depth, color, compression, filtering, interlace = struct.unpack(">IIBBBBB", chunks[0][1])
    if min(width, height) < ICON_MIN_PIXELS:
        problems.append(f"{ICON_NAME} is {width} by {height} pixels, which is less than the {ICON_MIN_PIXELS} by {ICON_MIN_PIXELS} that NuGet recommends")
    if color not in PNG_COLOR_TYPES or depth not in PNG_COLOR_TYPES[color][0] or compression != 0 or filtering != 0 or interlace not in PNG_PASSES:
        problems.append(f"{ICON_NAME} is not a complete PNG: its header is not valid (colour type {color}, bit depth {depth}, compression {compression}, filter {filtering}, interlace {interlace})")
        return problems
    problems.extend(f"{ICON_NAME} is not a complete PNG: {problem}" for problem in palette_problems(chunks, color, depth))
    problems.extend(f"{ICON_NAME} is not a complete PNG: {problem}" for problem in chunk_problems(chunks, color, depth))

    pixels = b"".join(payload for kind, payload in chunks if kind == b"IDAT")
    if not pixels:
        problems.append(f"{ICON_NAME} is not a complete PNG: it has no image data")
    else:
        bits_per_pixel = depth * PNG_COLOR_TYPES[color][1]
        problems.extend(f"{ICON_NAME} is not a complete PNG: {problem}" for problem in raster_problems(pixels, width, height, bits_per_pixel, interlace))
    if kinds[-1] != b"IEND":
        problems.append(f"{ICON_NAME} is not a complete PNG: it does not end with its IEND chunk")
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
