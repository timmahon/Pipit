using Pipit.Parsers.Html;
using System.Text;

namespace Pipit.Tests;

public sealed class HtmlParserTests
{
    [Theory]
    [InlineData("<html><head><title>Example document</title></head></html>", "Example document")]
    [InlineData("<title> Research &amp; development   </title>", "Research & development")]
    [InlineData("<title> </title>", null)]
    [InlineData("<p>No title here.</p>", null)]
    public async Task ExtractsTitle(string html, string? expected)
    {
        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        var titles = sink.Events
            .OfType<ExtractedMetadata<string>>()
            .Where(metadata => metadata.Key == MetadataKeys.Title)
            .Select(metadata => metadata.Value)
            .ToArray();

        string[] expectedTitles = expected is null ? [] : [expected];

        Assert.Equal(expectedTitles, titles);
    }


    [Fact]
    public async Task EmitsParagraphWithSourceLocations()
    {
        const string html =
            "<p id=\"summary\">Hello <strong>world</strong>.</p>";

        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        ParseEvent[] expected =
        [
            new StartElement(ContentKind.Paragraph),
                new TextContent(
                    "Hello ",
                    new HtmlLocation("summary", 0, 6)),
                new StartElement(ContentKind.StrongEmphasis),
                    new TextContent(
                        "world",
                        new HtmlLocation("summary", 6, 5)),
                new EndElement(ContentKind.StrongEmphasis),
                new TextContent(
                    ".",
                    new HtmlLocation("summary", 11, 1)),
                new EndElement(ContentKind.Paragraph)
        ];

        Assert.Equal(expected, sink.Events);
    }

    [Fact]
    public async Task EmitsHeadingWithLevel()
    {
        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes("<h2>Introduction</h2>"));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        ParseEvent[] expected =
        [
            new StartElement(ContentKind.Heading, 2),
                new TextContent("Introduction"),
            new EndElement(ContentKind.Heading)
        ];

        Assert.Equal(expected, sink.Events);
    }

    [Fact]
    public async Task EmitsHeadMetadataBeforeBodyContent()
    {
        const string html =
            "<html><head>" +
            "<title>Example</title>" +
            "<meta name=\"DESCRIPTION\" content=\" Research &amp; development \">" +
            "<meta name=\"author\" content=\" Alice \">" +
            "<meta name=\"Author\" content=\"Bob\">" +
            "</head><body><p>Hello.</p></body></html>";

        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        ParseEvent[] expected =
        [
            new ExtractedMetadata<string>(
                MetadataKeys.Title, "Example"),
            new ExtractedMetadata<string>(
                MetadataKeys.Description, "Research & development"),
            new ExtractedMetadata<string>(
                MetadataKeys.Author, "Alice"),
            new ExtractedMetadata<string>(
                MetadataKeys.Author, "Bob"),
            new StartElement(ContentKind.Paragraph),
                new TextContent("Hello."),
            new EndElement(ContentKind.Paragraph)
        ];

        Assert.Equal(expected, sink.Events);
    }

    [Theory]
    [InlineData("<meta name=\"author\">")]
    [InlineData("<meta name=\"author\" content=\"   \">")]
    [InlineData("<meta content=\"No name\">")]
    [InlineData("<meta name=\"unknown\" content=\"Something\">")]
    public async Task IgnoresEmptyOrUnsupportedMetadata(string declaration)
    {
        var html = $"<html><head>{declaration}</head><body></body></html>";

        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        Assert.Empty(sink.Events);
    }

    [Fact]
    public async Task PreservesExplicitLineBreak()
    {
        const string html = "<p id=\"summary\">Hello<br>world</p>";

        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        ParseEvent[] expected =
        [
            new StartElement(ContentKind.Paragraph),
        new TextContent(
            "Hello",
            new HtmlLocation("summary", 0, 5)),
        new LineBreak(),
        new TextContent(
            "world",
            new HtmlLocation("summary", 5, 5)),
        new EndElement(ContentKind.Paragraph)
        ];

        Assert.Equal(expected, sink.Events);
    }

    [Fact]
    public async Task PreservesBlockBoundaries()
    {
        const string html = "<div>First</div><div>Second</div>";

        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        ParseEvent[] expected =
        [
            new StartElement(ContentKind.Block),
        new TextContent("First"),
        new EndElement(ContentKind.Block),
        new StartElement(ContentKind.Block),
        new TextContent("Second"),
        new EndElement(ContentKind.Block)
        ];

        Assert.Equal(expected, sink.Events);
    }

    [Fact]
    public async Task OmitsAmbiguousSourceLocations()
    {
        const string html =
            "<p id=\"same\">First</p>" +
            "<p id=\"same\">Second</p>";

        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        TextContent[] expected =
        [
            new TextContent("First"),
            new TextContent("Second")
        ];

        Assert.Equal(
            expected,
            sink.Events.OfType<TextContent>().ToArray());
    }

    [Fact]
    public async Task UsesUniqueAncestorWhenChildIdsAreDuplicated()
    {
        const string html =
            "<div id=\"root\">" +
            "<p id=\"same\">First</p>" +
            "<p id=\"same\">Second</p>" +
            "</div>";

        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        TextContent[] expected =
        [
            new TextContent(
                "First",
                new HtmlLocation("root", 0, 5)),
            new TextContent(
                "Second",
                new HtmlLocation("root", 5, 6))
        ];

        Assert.Equal(
            expected,
            sink.Events.OfType<TextContent>().ToArray());
    }

    [Fact]
    public async Task LeavesInputStreamOpen()
    {
        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes("<p>Hello</p>"));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task EmitsNoEventsWhenAlreadyCancelled()
    {
        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes("<p>Hello</p>"));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => parser.ParseAsync(documentInput, sink, cancellation.Token));

        Assert.Empty(sink.Events);
        Assert.Equal(input.Length, input.Position);
    }

    [Fact]
    public async Task StopsEmittingEventsWhenCancelled()
    {
        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes("<p>Hello</p><p>World</p>"));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        using var cancellation = new CancellationTokenSource();
        var events = new List<ParseEvent>();

        var sink = new CallbackParseEventSink((parseEvent, _) =>
        {
            events.Add(parseEvent);
            cancellation.Cancel();

            return ValueTask.CompletedTask;
        });

        IParser parser = new HtmlParser();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => parser.ParseAsync(documentInput, sink, cancellation.Token));

        var firstEvent = Assert.Single(events);

        Assert.Equal(
            new StartElement(ContentKind.Paragraph),
            firstEvent);
    }

    [Fact]
    public async Task PropagatesSinkFailureAndStopsEmittingEvents()
    {
        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes("<p>Hello</p><p>World</p>"));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var failure = new InvalidOperationException("Sink failed.");
        var attempts = 0;

        var sink = new CallbackParseEventSink((_, _) =>
        {
            attempts++;
            throw failure;
        });

        IParser parser = new HtmlParser();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => parser.ParseAsync(documentInput, sink));

        Assert.Same(failure, actual);
        Assert.Equal(1, attempts);
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task ResumesParentLocationAfterChildWithOwnId()
    {
        const string html =
            "<p id=\"paragraph\">A<strong id=\"emphasis\">BC</strong>D</p>";

        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        ParseEvent[] expected =
        [
            new StartElement(ContentKind.Paragraph),
                new TextContent(
                    "A",
                    new HtmlLocation("paragraph", 0, 1)),
                new StartElement(ContentKind.StrongEmphasis),
                    new TextContent(
                        "BC",
                        new HtmlLocation("emphasis", 0, 2)),
                new EndElement(ContentKind.StrongEmphasis),
                new TextContent(
                    "D",
                    new HtmlLocation("paragraph", 3, 1)),
            new EndElement(ContentKind.Paragraph)
        ];

        Assert.Equal(expected, sink.Events);
    }

    [Fact]
    public async Task ExcludesNonContentElementsAndPreservesSourceOffsets()
    {
        const string html =
            "<p id=\"paragraph\">" +
            "A" +
            "<script>X</script>" +
            "<style>Y</style>" +
            "<template>Z</template>" +
            "B" +
            "</p>";

        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new HtmlParser();

        await parser.ParseAsync(documentInput, sink);

        ParseEvent[] expected =
        [
            new StartElement(ContentKind.Paragraph),
        new TextContent(
            "A",
            new HtmlLocation("paragraph", 0, 1)),
        new TextContent(
            "B",
            new HtmlLocation("paragraph", 3, 1)),
        new EndElement(ContentKind.Paragraph)
        ];

        Assert.Equal(expected, sink.Events);
    }
}
