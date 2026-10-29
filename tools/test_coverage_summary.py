"""Tests of coverage_summary.py: how the reports of a test run are merged, and what the floor does.

Run with: python -m unittest discover -s tools -p "test_*.py"
"""
import contextlib
import io
import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import coverage_summary  # noqa: E402


def line(number, hits, condition=None):
    branch = f' branch="True" condition-coverage="{condition}"' if condition else ""
    return f'<line number="{number}" hits="{hits}"{branch}/>'


def report(classes, package="OBSClient"):
    """A Cobertura report. `classes` are (file, [lines]) pairs; a file can be in several classes, as it is in a real report."""
    body = "".join(f'<class name="C{index}" filename="{name}"><lines>{"".join(lines)}</lines></class>' for index, (name, lines) in enumerate(classes))
    return (
        '<?xml version="1.0"?><coverage><packages>'
        f'<package name="{package}"><classes>{body}</classes></package>'
        '<package name="OBSClient.Tests"><classes><class name="T" filename="Tests/Some.cs"><lines>'
        f'{line(1, 0)}{line(2, 0)}</lines></class></classes></package>'
        "</packages></coverage>"
    )


class CoverageTestCase(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)

    def write(self, name, text):
        path = os.path.join(self.folder.name, name)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as stream:
            stream.write(text)
        return path

    def run_main(self, *arguments):
        output = io.StringIO()
        with contextlib.redirect_stdout(output), contextlib.redirect_stderr(io.StringIO()):
            code = coverage_summary.main(list(arguments))
        return code, output.getvalue()


class ReadTests(CoverageTestCase):
    def read(self, *texts, package="OBSClient"):
        paths = [self.write(f"report{index}.xml", text) for index, text in enumerate(texts)]
        return coverage_summary.read(paths, package)

    def test_a_line_is_hit_when_any_of_the_reports_hit_it(self):
        net8 = report([("Client.cs", [line(1, 0), line(2, 3)])])
        net10 = report([("Client.cs", [line(1, 5), line(2, 0)])])

        lines, _ = self.read(net8, net10)

        self.assertEqual({1: 5, 2: 3}, lines["Client.cs"])

    def test_a_file_in_several_classes_counts_a_line_once(self):
        # A partial class, or the state machine of an async method, makes a second class of the same file with the same lines.
        text = report([("Client.cs", [line(1, 0), line(2, 1)]), ("Client.cs", [line(2, 0), line(3, 0)])])

        lines, _ = self.read(text)

        self.assertEqual({1: 0, 2: 1, 3: 0}, lines["Client.cs"])

    def test_the_branches_of_a_line_are_the_most_that_a_report_reached_and_the_most_that_it_has(self):
        net8 = report([("Client.cs", [line(1, 1, "50% (1/2)")])])
        net10 = report([("Client.cs", [line(1, 1, "100% (2/2)")])])

        _, branches = self.read(net8, net10)

        self.assertEqual({1: {"C0": (2, 2)}}, {number: dict(owners) for number, owners in branches["Client.cs"].items()})

    def test_two_classes_with_a_decision_on_the_same_line_keep_a_decision_each(self):
        # A conditional, and a lambda that is written on the same line: the closure of the lambda is a class of its own.
        text = report([("Client.cs", [line(1, 1, "100% (2/2)")]), ("Client.cs", [line(1, 1, "0% (0/2)")])])

        lines, branches = self.read(text)

        self.assertEqual({1: 1}, lines["Client.cs"])
        found = coverage_summary.totals(lines, branches, lambda name: True)
        self.assertEqual((4, 2), (found.branches, found.branches_hit))
        self.assertEqual(50.0, found.branch_rate)

    def test_the_same_two_classes_in_the_other_report_are_not_counted_again(self):
        text = report([("Client.cs", [line(1, 1, "100% (2/2)")]), ("Client.cs", [line(1, 1, "0% (0/2)")])])

        lines, branches = self.read(text, text)

        found = coverage_summary.totals(lines, branches, lambda name: True)
        self.assertEqual((4, 2), (found.branches, found.branches_hit))

    def test_only_the_assembly_of_the_library_is_counted(self):
        lines, _ = self.read(report([("Client.cs", [line(1, 1)])]))

        self.assertEqual(["Client.cs"], list(lines))

    def test_the_names_of_files_made_on_windows_have_forward_slashes(self):
        lines, _ = self.read(report([("Messages\\ObsMessage.cs", [line(1, 1)])]))

        self.assertEqual(["Messages/ObsMessage.cs"], list(lines))

    def test_a_report_without_the_package_is_refused(self):
        with self.assertRaisesRegex(coverage_summary.CoverageError, "no package 'Other'"):
            self.read(report([("Client.cs", [line(1, 1)])]), package="Other")

    def test_a_report_that_is_not_xml_is_refused(self):
        with self.assertRaisesRegex(coverage_summary.CoverageError, "Cannot read"):
            self.read("not xml")


class TotalsTests(unittest.TestCase):
    def test_generated_code_is_kept_apart_from_what_is_hand_written(self):
        lines = {
            "ObsClient.cs": {1: 1, 2: 0},
            "Generated/Requests.g.cs": {1: 1, 2: 1, 3: 1},
            "Other.g.cs": {1: 0},
            "Generated/Hand.cs": {1: 1},
        }
        branches = {"ObsClient.cs": {1: {"A": (1, 2)}}, "Generated/Requests.g.cs": {1: {"A": (2, 2)}}}

        hand_written = coverage_summary.totals(lines, branches, lambda name: not coverage_summary.is_generated(name))
        generated = coverage_summary.totals(lines, branches, coverage_summary.is_generated)

        self.assertEqual(coverage_summary.Totals(2, 1, 2, 1), hand_written)
        # Everything in a folder named Generated, and every .g.cs file, is generated: 3 + 1 + 1 lines, of which 3 + 0 + 1 are reached.
        self.assertEqual(coverage_summary.Totals(5, 4, 2, 2), generated)
        self.assertEqual(50.0, hand_written.line_rate)

    def test_nothing_to_reach_is_all_reached(self):
        self.assertEqual(100.0, coverage_summary.Totals(0, 0, 0, 0).branch_rate)

    def test_the_files_with_the_most_lines_that_are_not_reached_come_first_and_generated_code_is_left_out(self):
        lines = {
            "A.cs": {1: 0, 2: 1},
            "B.cs": {1: 0, 2: 0, 3: 0},
            "C.cs": {1: 1},
            "Generated/D.g.cs": {1: 0, 2: 0, 3: 0, 4: 0},
        }

        rows = coverage_summary.least_reached(lines, 5)

        self.assertEqual([(3, 3, "B.cs"), (1, 2, "A.cs")], rows)
        self.assertEqual([(3, 3, "B.cs")], coverage_summary.least_reached(lines, 1))


class CommandLineTests(CoverageTestCase):
    def make_report(self, name="TestResults/a/coverage.cobertura.xml"):
        return self.write(
            name,
            report([
                ("Client.cs", [line(1, 1), line(2, 1), line(3, 0), line(4, 1, "50% (1/2)")]),
                ("Generated/Requests.g.cs", [line(1, 1)]),
            ]),
        )

    def floor(self, lines, branches):
        return self.write("floor.json", json.dumps({"lines": lines, "branches": branches}))

    def test_it_prints_the_coverage_of_the_hand_written_code_and_of_the_generated_code(self):
        path = self.make_report()

        code, printed = self.run_main(path)

        self.assertEqual(0, code)
        self.assertIn("hand-written: 3 of 4 lines (75.0%), 1 of 2 branches (50.0%)", printed)
        self.assertIn("generated:    1 of 1 lines (100.0%), 0 of 0 branches (100.0%)", printed)
        self.assertIn("1 of    4 lines not reached  Client.cs", printed)

    def test_a_pattern_finds_the_reports_in_the_folders_below_it(self):
        self.make_report("TestResults/a/coverage.cobertura.xml")
        self.make_report("TestResults/_machine_date/In/machine/coverage.cobertura.xml")
        pattern = os.path.join(self.folder.name, "TestResults", "**", "coverage.cobertura.xml")

        code, printed = self.run_main(pattern)

        self.assertEqual(0, code)
        self.assertIn("2 report(s) merged", printed)

    def test_a_pattern_that_matches_nothing_is_refused(self):
        code, printed = self.run_main(os.path.join(self.folder.name, "nothing", "*.xml"))

        self.assertEqual(1, code)
        self.assertTrue(printed.startswith("::error::No coverage report matches"), printed)

    def test_a_coverage_at_the_floor_passes_and_below_it_fails(self):
        path = self.make_report()

        at_the_floor, _ = self.run_main(path, "--floor", self.floor(75.0, 50.0))
        lines_below, printed = self.run_main(path, "--floor", self.floor(75.1, 50.0))
        branches_below, _ = self.run_main(path, "--floor", self.floor(75.0, 50.1))

        self.assertEqual(0, at_the_floor)
        self.assertEqual(1, lines_below)
        self.assertIn("::error::The lines of the hand-written code are reached for 75.0%, below the floor of 75.1%.", printed)
        self.assertEqual(1, branches_below)

    def test_a_floor_that_cannot_be_used_is_refused(self):
        path = self.make_report()
        broken = self.write("broken.json", '{"lines": 80}')

        code, printed = self.run_main(path, "--floor", broken)

        self.assertEqual(1, code)
        self.assertIn("Cannot use the floor", printed)

    def test_a_floor_that_is_not_a_percentage_is_refused(self):
        # A report with no coverage at all must not pass because of a floor that nothing is below: NaN, a negative number, one over 100.
        empty = self.write("empty.xml", report([("Client.cs", [line(1, 0, "0% (0/2)")])]))

        for text in ('{"lines": "NaN", "branches": "NaN"}', '{"lines": NaN, "branches": 50}', '{"lines": -5, "branches": 50}',
                     '{"lines": 50, "branches": 100.5}', '{"lines": "Infinity", "branches": 50}'):
            with self.subTest(text):
                code, printed = self.run_main(empty, "--floor", self.write("floor.json", text))

                self.assertEqual(1, code)
                self.assertIn("::error::", printed)
                self.assertIn("floor", printed)

    def test_the_summary_is_appended_as_markdown(self):
        path = self.make_report()
        summary = self.write("summary.md", "Earlier text.\n")

        self.run_main(path, "--floor", self.floor(70.0, 40.0), "--summary", summary)
        self.run_main(path, "--summary", summary)

        with open(summary, encoding="utf-8") as stream:
            text = stream.read()
        self.assertTrue(text.startswith("Earlier text.\n### Coverage of the library"))
        self.assertIn("| Hand-written | 75.0% (3 of 4) | 50.0% (1 of 2) |", text)
        self.assertIn("| Generated | 100.0% (1 of 1) | 100.0% (0 of 0) |", text)
        self.assertIn("is 70.0% of the lines and 40.0% of the branches. It holds.", text)
        self.assertEqual(2, text.count("### Coverage of the library"))
        self.assertIn("| `Client.cs` | 1 | 4 |", text)

    def test_the_summary_says_when_the_floor_is_not_held(self):
        path = self.make_report()
        summary = self.write("summary.md", "")

        self.run_main(path, "--floor", self.floor(99.0, 40.0), "--summary", summary)

        with open(summary, encoding="utf-8") as stream:
            self.assertIn("It is below the floor.", stream.read())


if __name__ == "__main__":
    unittest.main()
