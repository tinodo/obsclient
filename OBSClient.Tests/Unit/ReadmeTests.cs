using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text.RegularExpressions;

namespace OBSStudioClient.Tests.Unit
{
    /// <summary>
    /// The README is the page that nuget.org shows for the package, and nuget.org renders it (with Markdig) with less than GitHub
    /// does: it drops a relative link, so that the page shows the text without a target, it shows raw HTML as text, and it only shows
    /// an image from a short list of domains. A published version cannot be changed, so a README with any of these has to fail here,
    /// before the package is released. The Markdown is parsed by Markdig as well, so what is a link, what is HTML and what is code
    /// (a span, a fenced or an indented block, an escaped backtick, a list item, a quote) is decided by the parser that nuget.org
    /// uses, and not by a guess of ours.
    /// </summary>
    public class ReadmeTests
    {
        // The domains that the documentation of nuget.org lists for the images (a badge too) that it renders; an image from another one is not
        // shown, and only the owner of the package is told. From
        // https://learn.microsoft.com/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images-and-badges, where github.com is
        // only listed for the badge of a workflow (see WorkflowBadge). The list is NuGet's to change: when it grows, add the domain.
        private static readonly string[] DocumentedImageDomains =
        [
            "api.codacy.com", "api.codeclimate.com", "api.dependabot.com", "api.reuse.software", "api.travis-ci.com", "app.codacy.com",
            "app.deepsource.com", "avatars.githubusercontent.com", "badgen.net", "badges.gitter.im", "camo.githubusercontent.com",
            "caniuse.bitsofco.de", "cdn.jsdelivr.net", "cdn.syncfusion.com", "ci.appveyor.com", "circleci.com", "cloudback.it", "codecov.io",
            "codefactor.io", "coveralls.io", "dev.azure.com", "devpod.sh", "flat.badgen.net", "gitlab.com", "i.imgur.com", "img.shields.io",
            "infragistics.com", "isitmaintained.com", "media.githubusercontent.com", "opencollective.com", "raw.github.com",
            "raw.githubusercontent.com", "snyk.io", "sonarcloud.io", "travis-ci.com", "travis-ci.org", "user-images.githubusercontent.com",
        ];

        // How nuget.org reads that list (TrustedImageDomains of NuGetGallery): a domain without a subdomain is trusted with www in front of it
        // as well (codefactor.io, www.codefactor.io), and no other subdomain is trusted (img.shields.io is, www.img.shields.io is not).
        private static readonly HashSet<string> ImageHosts = ExpandWithWww(DocumentedImageDomains);

        // How nuget.org recognises the badge of a workflow (ImageDomainValidator of NuGetGallery): an address on github.com in which
        // /workflows/ follows the owner and the repository, with only /actions between them, and that ends in badge.svg. A path that has
        // /workflows/ further on (a file of the repository) is an ordinary image of github.com, which is not rendered.
        private static readonly Regex WorkflowBadge = new(
            @"^(https|http)://github\.com/[^/]+/[^/]+(/actions)?/workflows/.*badge\.svg",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        [Fact]
        public void TheReadmeOfThePackage_IsShownByNugetOrgAsItIsWritten()
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
        public void AnImageFromADomainThatNugetOrgDoesNotRender_IsReported()
        {
            var problems = Problems("# Title\n\n[![Score](https://api.scorecard.dev/projects/github.com/tinodo/obsclient/badge)](https://scorecard.dev/viewer/?uri=github.com/tinodo/obsclient)\n");

            Assert.Equal(
                new[]
                {
                    "line 3: the image \"https://api.scorecard.dev/projects/github.com/tinodo/obsclient/badge\" is from a domain that nuget.org does not render images from, so the page shows no image (a badge can come from img.shields.io)",
                },
                problems);
        }

        [Fact]
        public void AnImageFromADomainThatNugetOrgRenders_IsAccepted()
        {
            var markdown = string.Join(
                "\n",
                "# Title",
                "",
                "[![NuGet](https://img.shields.io/nuget/vpre/OBSClient.svg?logo=nuget)](https://www.nuget.org/packages/OBSClient)",
                "[![Score](https://img.shields.io/ossf-scorecard/github.com/tinodo/obsclient)](https://scorecard.dev/viewer/?uri=github.com/tinodo/obsclient)",
                "[![Build](https://github.com/tinodo/obsclient/actions/workflows/build.yml/badge.svg)](https://github.com/tinodo/obsclient/actions)",
                "[![Build](https://github.com/tinodo/obsclient/workflows/Build/badge.svg)](https://github.com/tinodo/obsclient/actions)",
                "![Logo](https://RAW.githubusercontent.com/tinodo/obsclient/main/docs/images/logo.png)",
                "![Quality](http://img.shields.io/codefactor/grade/github/tinodo/obsclient)",
                "");

            Assert.Empty(Problems(markdown));
        }

        [Fact]
        public void AnImageOnTheWwwVariantOfADomainWithoutASubdomain_IsAccepted()
        {
            // nuget.org trusts codefactor.io, and www.codefactor.io with it.
            Assert.Empty(Problems("# Title\n\n![Quality](https://www.codefactor.io/repository/github/tinodo/obsclient/badge)\n![Quality](https://codefactor.io/repository/github/tinodo/obsclient/badge)\n"));
        }

        [Theory]
        [InlineData("https://www.img.shields.io/nuget/vpre/OBSClient.svg")]
        [InlineData("https://evil.img.shields.io/nuget/vpre/OBSClient.svg")]
        [InlineData("https://img.shields.io.example.com/nuget/vpre/OBSClient.svg")]
        [InlineData("https://www.api.codacy.com/project/badge/Grade/1")]
        public void AnImageOnAnotherSubdomainThanTheOnesOnTheList_IsReported(string image)
        {
            var problems = Problems($"# Title\n\n![Badge]({image})\n");

            Assert.Equal(
                new[] { $"line 3: the image \"{image}\" is from a domain that nuget.org does not render images from, so the page shows no image (a badge can come from img.shields.io)" },
                problems);
        }

        [Theory]
        [InlineData("https://github.com/tinodo/obsclient/raw/main/docs/images/logo.png")]
        [InlineData("https://github.com/tinodo/obsclient/raw/main/docs/workflows/example/badge.svg")]
        [InlineData("https://github.com/tinodo/obsclient/blob/main/.github/workflows/build.yml/badge.svg")]
        [InlineData("https://www.github.com/tinodo/obsclient/actions/workflows/build.yml/badge.svg")]
        [InlineData("https://github.com/tinodo/obsclient/actions/workflows/build.yml/status.png")]
        public void AnImageOnGitHubThatIsNotTheBadgeOfAWorkflow_IsReported(string image)
        {
            var problems = Problems($"# Title\n\n![Image]({image})\n");

            Assert.Equal(
                new[] { $"line 3: the image \"{image}\" is from a domain that nuget.org does not render images from, so the page shows no image (a badge can come from img.shields.io)" },
                problems);
        }

        [Fact]
        public void ALinkToADomainThatIsNotOnTheListOfImages_IsNotAnImage()
        {
            Assert.Empty(Problems("# Title\n\nSee [the score](https://scorecard.dev/viewer/?uri=github.com/tinodo/obsclient).\n"));
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
        public void AFragmentOnlyTarget_IsAcceptedForALinkButNotForAnImage()
        {
            var problems = Problems("# Usage\n\nSee [below](#usage) and ![logo](#usage).\n");

            Assert.Equal(new[] { "line 3: the link or image \"#usage\" is relative: nuget.org drops it, so use an absolute URL" }, problems);
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
                var isAnchor = !link.IsImage && url.StartsWith('#');
                var isAbsolute = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto";
                if (url.Length == 0)
                {
                    found.Add((link.Line, link.Column, $"line {link.Line + 1}: a link or an image without a target"));
                }
                else if (!isAnchor && !isAbsolute)
                {
                    found.Add((link.Line, link.Column, $"line {link.Line + 1}: the link or image \"{url}\" is relative: nuget.org drops it, so use an absolute URL"));
                }
                else if (link.IsImage && !IsRenderedImage(url, uri!))
                {
                    found.Add((link.Line, link.Column, $"line {link.Line + 1}: the image \"{url}\" is from a domain that nuget.org does not render images from, so the page shows no image (a badge can come from img.shields.io)"));
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

        // A domain with only two labels has no subdomain: nuget.org trusts it with www in front of it as well.
        private static HashSet<string> ExpandWithWww(IEnumerable<string> domains)
        {
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var domain in domains)
            {
                expanded.Add(domain);
                if (domain.Split('.').Length <= 2)
                {
                    expanded.Add("www." + domain);
                }
            }

            return expanded;
        }

        private static bool IsRenderedImage(string url, Uri uri) => ImageHosts.Contains(uri.Host) || WorkflowBadge.IsMatch(url);
    }
}
