using System.Text;
using Pipit.Parsers.Html;
using Pipit.Parsers.OpenXml;
using Pipit.Rendering;

namespace Pipit.Tests;

public sealed class SourceMappingTests
{
    [Fact]
    public async Task MapsTextWhenEmphasisSpansBlocksWithoutMappingEmptyHeadingSyntax()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(
            "<h1> </h1><em id=\"source\">before<div>inside</div>after</em>"));
        await using var input = await DocumentInput.CreateAsync(source);
        var sink = new MarkdownSink();

        await new HtmlParser().ParseAsync(input, sink);
        var result = sink.GetResult();

        Assert.Equal("*before*\n\n*inside*\n\n*after*", result.Content);
        SourceMapping[] expected =
        [
            new(new TextRange(1, 6), new HtmlLocation("source", 0, 6), new TextRange(0, 6)),
            new(new TextRange(11, 6), new HtmlLocation("source", 6, 6), new TextRange(0, 6)),
            new(new TextRange(21, 5), new HtmlLocation("source", 12, 5), new TextRange(0, 5))
        ];
        Assert.Equal(expected, result.SourceMappings);
    }

    [Fact]
    public async Task MapsHtmlTextAroundEmphasisWithoutMappingMarkers()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(
            "<p id=\"intro\">Hello <strong>world</strong>.</p>"));
        await using var documentInput = await DocumentInput.CreateAsync(input);
        var sink = new MarkdownSink();

        await new HtmlParser().ParseAsync(documentInput, sink);
        var result = sink.GetResult();

        Assert.Equal("Hello **world**.", result.Content);
        SourceMapping[] expected =
        [
            new(new TextRange(0, 6), new HtmlLocation("intro", 0, 6), new TextRange(0, 6)),
            new(new TextRange(8, 5), new HtmlLocation("intro", 6, 5), new TextRange(0, 5)),
            new(new TextRange(15, 1), new HtmlLocation("intro", 11, 1), new TextRange(0, 1))
        ];
        Assert.Equal(expected, result.SourceMappings);
        Assert.Equal("world", result.Content.Substring(
            result.SourceMappings[1].OutputRange.Start, result.SourceMappings[1].OutputRange.Length));
    }

    [Fact]
    public async Task SplitsMappingsAroundGeneratedEscapeCharacters()
    {
        var location = new HtmlLocation("intro", 100, 3);
        var sink = new MarkdownSink();

        await sink.WriteAsync(new TextContent("A*B", location));
        var result = sink.GetResult();

        Assert.Equal("A\\*B", result.Content);
        SourceMapping[] expected =
        [
            new(new TextRange(0, 1), location, new TextRange(0, 1)),
            new(new TextRange(2, 2), location, new TextRange(1, 2))
        ];
        Assert.Equal(expected, result.SourceMappings);
    }

    [Fact]
    public async Task MapsRetainedSourceSliceWhenBlockEdgeWhitespaceIsTrimmed()
    {
        var location = new HtmlLocation("intro", 10, 6);
        var sink = new MarkdownSink();

        await sink.WriteAsync(new StartElement(ContentKind.Emphasis));
        await sink.WriteAsync(new TextContent(" word ", location));
        await sink.WriteAsync(new EndElement(ContentKind.Emphasis));
        var result = sink.GetResult();

        Assert.Equal("*word*", result.Content);
        SourceMapping[] expected =
        [
            new(new TextRange(1, 4), location, new TextRange(1, 4))
        ];
        Assert.Equal(expected, result.SourceMappings);
    }

    [Fact]
    public async Task LeavesHeadingsSeparatorsAndExplicitBreakSyntaxUnmapped()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(
            "<h1 id=\"heading\">Title</h1><p id=\"paragraph\">A<br>B</p>"));
        await using var documentInput = await DocumentInput.CreateAsync(input);
        var sink = new MarkdownSink();

        await new HtmlParser().ParseAsync(documentInput, sink);
        var result = sink.GetResult();

        Assert.Equal("# Title\n\nA\\\nB", result.Content);
        SourceMapping[] expected =
        [
            new(new TextRange(2, 5), new HtmlLocation("heading", 0, 5), new TextRange(0, 5)),
            new(new TextRange(9, 1), new HtmlLocation("paragraph", 0, 1), new TextRange(0, 1)),
            new(new TextRange(12, 1), new HtmlLocation("paragraph", 1, 1), new TextRange(0, 1))
        ];
        Assert.Equal(expected, result.SourceMappings);
    }

    [Fact]
    public async Task MapsDocxParagraphsUsingTheSameContract()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "basic.docx");
        using var input = File.OpenRead(path);
        await using var documentInput = await DocumentInput.CreateAsync(input);
        var sink = new MarkdownSink();

        await new DocxParser().ParseAsync(documentInput, sink);
        var result = sink.GetResult();

        Assert.Equal("Example document\n\nFirst paragraph.\n\nSecond paragraph.", result.Content);
        SourceMapping[] expected =
        [
            new(new TextRange(0, 16), new DocxLocation("/word/document.xml", 0, 0, 16), new TextRange(0, 16)),
            new(new TextRange(18, 16), new DocxLocation("/word/document.xml", 1, 0, 16), new TextRange(0, 16)),
            new(new TextRange(36, 17), new DocxLocation("/word/document.xml", 2, 0, 17), new TextRange(0, 17))
        ];
        Assert.Equal(expected, result.SourceMappings);
    }

    [Fact]
    public async Task UsesUtf16OffsetsForSupplementaryCharacters()
    {
        var location = new HtmlLocation("intro", 0, 3);
        var sink = new MarkdownSink();

        await sink.WriteAsync(new TextContent("😀*", location));
        var result = sink.GetResult();

        Assert.Equal("😀\\*", result.Content);
        SourceMapping[] expected =
        [
            new(new TextRange(0, 2), location, new TextRange(0, 2)),
            new(new TextRange(3, 1), location, new TextRange(2, 1))
        ];
        Assert.Equal(expected, result.SourceMappings);
    }

    [Fact]
    public async Task DoesNotInventMappingsForUnknownLocations()
    {
        var sink = new MarkdownSink();
        await sink.WriteAsync(new TextContent("Hello"));

        var result = sink.GetResult();

        Assert.Equal("Hello", result.Content);
        Assert.Empty(result.SourceMappings);
    }

    [Fact]
    public async Task ResultsRemainSnapshotsAfterFurtherEvents()
    {
        var sink = new MarkdownSink();
        await sink.WriteAsync(new TextContent("A", new HtmlLocation("intro", 0, 1)));
        var first = sink.GetResult();

        await sink.WriteAsync(new TextContent("B", new HtmlLocation("intro", 1, 1)));
        await sink.WriteAsync(new EndElement(ContentKind.Paragraph));
        var second = sink.GetResult();

        Assert.Equal("A", first.Content);
        Assert.Single(first.SourceMappings);
        Assert.Equal("AB", second.Content);
        Assert.Equal(2, second.SourceMappings.Count);
        Assert.Equal(second.Content, sink.GetContent());
    }

    [Fact]
    public async Task MapsCollapsedWhitespaceWithoutChangingTheSourceEvent()
    {
        const string original = "  A \t\n B  ";
        var location = new HtmlLocation("intro", 20, original.Length);
        var text = new TextContent(original, location);
        var sink = new MarkdownSink();

        await sink.WriteAsync(text);
        var result = sink.GetResult();

        Assert.Equal("A B", result.Content);
        Assert.Equal(original, text.Text);
        SourceMapping[] expected =
        [
            new(new TextRange(0, 2), location, new TextRange(2, 2)),
            new(new TextRange(2, 1), location, new TextRange(7, 1))
        ];
        Assert.Equal(expected, result.SourceMappings);
    }

    [Fact]
    public async Task MapsNormalizedSpaceToTheFirstWhitespaceCharacter()
    {
        var location = new HtmlLocation("intro", 10, 5);
        var sink = new MarkdownSink();

        await sink.WriteAsync(new TextContent("A\t\n B", location));
        var result = sink.GetResult();

        Assert.Equal("A B", result.Content);
        SourceMapping[] expected =
        [
            new(new TextRange(0, 2), location, new TextRange(0, 2)),
            new(new TextRange(2, 1), location, new TextRange(4, 1))
        ];
        Assert.Equal(expected, result.SourceMappings);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(int.MaxValue, 1)]
    public void RejectsInvalidTextRanges(int start, int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextRange(start, length));
    }
}
