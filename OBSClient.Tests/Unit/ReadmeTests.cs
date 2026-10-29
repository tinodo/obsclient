using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace OBSStudioClient.Tests.Unit
{
    /// <summary>
    /// The README is the page that nuget.org shows for the package, and nuget.org renders it (with Markdig) with less than GitHub
    /// does: it drops a relative link, so that the page shows the text without a target, and it shows raw HTML as text. A published
    /// version cannot be changed, so a README with either has to fail here, before the package is released. The Markdown is parsed
    /// by Markdig as well, so what is a link, what is HTML and what is code (a span, a fenced or an indented block, an escaped
    /// backtick, a list item, a quote) is decided by the parser that nuget.org uses, and not by a guess of ours.
    /// </summary>
    public class ReadmeTests
    {
        [Fact]
        public void TheReadmeOfThePackage_HasOnlyAbsoluteLinksAndNoRawHtml()
        {
            using var stream = typeof(ReadmeTests).Assembly.GetManifestResourceStream("README.md")
                ?? throw new InvalidOperationException("README.md is not embedded in the tests: see OBSClient.Tests.csproj.");
            using var reader = new StreamReader(stream);

            var problems = Problems(reader.ReadToEnd());

            Assert.True(problems.Count == 0, "nuget.org would not show the README as it is written:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
        }

        [Fact]
        public void ARelativeLinkOrImage_IsReportedWithItsLine()
        {
            var problems = Problems("# Title\n\nSee the [changelog](CHANGELOG.md) and ![logo](docs/images/logo.png).\n");

            Assert.Equal(
                new[]
                {
                    "line 3: the link or image \"CHANGELOG.md\" is relative: nuget.org drops it, so use an absolute URL",
                    "line 3: the link or image \"docs/images/logo.png\" is relative: nuget.org drops it, so use an absolute URL",
                },
                problems);
        }

        [Fact]
        public void ALinkWithoutATarget_IsReported()
        {
            var problems = Problems("# Title\n\nGo [nowhere]().\n");

            Assert.Equal(new[] { "line 3: a link or an image without a target" }, problems);
        }

        [Fact]
        public void RawHtml_IsReported()
        {
            var problems = Problems("# Title\n\nA <ins>complete</ins> client.\n\n<!-- a note -->\n\n<div>block</div>\n");

            Assert.Equal(
                new[]
                {
                    "line 3: the raw HTML \"<ins>\" is not rendered by nuget.org, which shows it as text",
                    "line 3: the raw HTML \"</ins>\" is not rendered by nuget.org, which shows it as text",
                    "line 5: a block of raw HTML is not rendered by nuget.org, which shows it as text",
                    "line 7: a block of raw HTML is not rendered by nuget.org, which shows it as text",
                },
                problems);
        }

        [Fact]
        public void AbsoluteLinksAnchorsAutolinksAndCode_AreAccepted()
        {
            var markdown = string.Join(
                "\n",
                "# Title",
                "",
                "[![Build](https://github.com/tinodo/obsclient/actions/workflows/build.yml/badge.svg)](https://github.com/tinodo/obsclient/actions)",
                "See the [changelog](https://github.com/tinodo/obsclient/blob/main/CHANGELOG.md#top), <https://example.com>, [below](#usage) and https://example.org.",
                "Write to <someone@example.com> or [here](mailto:someone@example.com).",
                "A generic type in a span, `ILogger<ObsClient>`, and a link in a span: `[a](b.md)`.",
                "",
                "```csharp",
                "var names = new List<string>();  // [a link](relative.md) and <b>bold</b>",
                "```",
                "",
                "    var indented = new List<string>();  // [a link](relative.md)",
                "",
                "[reference]: https://example.com/reference",
                "");

            Assert.Empty(Problems(markdown));
        }

        [Fact]
        public void AnEscapedBacktick_DoesNotStartACodeSpan()
        {
            var problems = Problems("# Title\n\n\\`[changes](CHANGELOG.md)\\`\n");

            Assert.Equal(new[] { "line 3: the link or image \"CHANGELOG.md\" is relative: nuget.org drops it, so use an absolute URL" }, problems);
        }

        [Fact]
        public void AnUnclosedBacktick_HidesNothingBeyondItsParagraph()
        {
            var problems = Problems("# Title\n\nAn unclosed ` backtick.\n\nSee the [changelog](CHANGELOG.md).\n\nAnother ` unclosed one.\n");

            Assert.Equal(new[] { "line 5: the link or image \"CHANGELOG.md\" is relative: nuget.org drops it, so use an absolute URL" }, problems);
        }

        [Fact]
        public void AReferenceInAQuote_IsChecked()
        {
            var problems = Problems("# Title\n\n> [changes][ref]\n>\n> [ref]: CHANGELOG.md\n");

            Assert.Equal(new[] { "line 3: the link or image \"CHANGELOG.md\" is relative: nuget.org drops it, so use an absolute URL" }, problems);
        }

        [Fact]
        public void AnIndentedFenceInsideAFence_DoesNotEndIt()
        {
            var problems = Problems("# Title\n\n```text\n    ```\n[in code](ignored.md)\n```\n\n[real](CHANGELOG.md)\n");

            Assert.Equal(new[] { "line 8: the link or image \"CHANGELOG.md\" is relative: nuget.org drops it, so use an absolute URL" }, problems);
        }

        [Fact]
        public void AParagraphThatContinuesAListItem_IsNotCode()
        {
            var problems = Problems("- item\n\n    continued, with [a link](CHANGELOG.md)\n");

            Assert.Equal(new[] { "line 3: the link or image \"CHANGELOG.md\" is relative: nuget.org drops it, so use an absolute URL" }, problems);
        }

        /// <summary>What nuget.org would not show as it is written, for a README: the problems, in the order of the text.</summary>
        internal static List<string> Problems(string markdown)
        {
            var pipeline = new MarkdownPipelineBuilder()
                .UsePipeTables()
                .UseEmphasisExtras()
                .UseTaskLists()
                .UseAutoLinks()
                .UsePreciseSourceLocation()
                .Build();
            var document = Markdown.Parse(markdown, pipeline);
            var found = new List<(int Line, int Column, string Message)>();

            foreach (var link in document.Descendants<LinkInline>())
            {
                var url = link.Url ?? string.Empty;
                if (url.Length == 0)
                {
                    found.Add((link.Line, link.Column, $"line {link.Line + 1}: a link or an image without a target"));
                }
                else if (!url.StartsWith('#') && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto"))
                {
                    found.Add((link.Line, link.Column, $"line {link.Line + 1}: the link or image \"{url}\" is relative: nuget.org drops it, so use an absolute URL"));
                }
            }

            foreach (var html in document.Descendants<HtmlInline>())
            {
                found.Add((html.Line, html.Column, $"line {html.Line + 1}: the raw HTML \"{html.Tag}\" is not rendered by nuget.org, which shows it as text"));
            }

            foreach (var block in document.Descendants<HtmlBlock>())
            {
                found.Add((block.Line, block.Column, $"line {block.Line + 1}: a block of raw HTML is not rendered by nuget.org, which shows it as text"));
            }

            return found.OrderBy(item => item.Line).ThenBy(item => item.Column).Select(item => item.Message).ToList();
        }
    }
}
