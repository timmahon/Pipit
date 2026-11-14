using Pipit.Parsers.OpenXml;

namespace Pipit.Tests;

public sealed class DocxParserTests
{
    [Fact]
    public async Task EmitsMetadataAndParagraphs()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "basic.docx");

        using var input = File.OpenRead(path);
        await using var documentInput = await DocumentInput.CreateAsync(input);

        var sink = new RecordingParseEventSink();
        IParser parser = new DocxParser();

        await parser.ParseAsync(documentInput, sink);

        ParseEvent[] expected =
        [
            new ExtractedMetadata<string>(
                DublinCore.Title, "Example document"),
            new ExtractedMetadata<string>(
                DublinCore.Creator, "Alice"),

            new StartElement(ContentKind.Paragraph),
                new TextContent(
                    "Example document",
                    new DocxLocation("/word/document.xml", 0, 0, 16)),
            new EndElement(ContentKind.Paragraph),

            new StartElement(ContentKind.Paragraph),
                new TextContent(
                    "First paragraph.",
                    new DocxLocation("/word/document.xml", 1, 0, 16)),
            new EndElement(ContentKind.Paragraph),

            new StartElement(ContentKind.Paragraph),
                new TextContent(
                    "Second paragraph.",
                    new DocxLocation("/word/document.xml", 2, 0, 17)),
            new EndElement(ContentKind.Paragraph),

            new StartElement(ContentKind.Paragraph),
            new EndElement(ContentKind.Paragraph)
        ];

        Assert.Equal(expected, sink.Events);
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task EmitsNoEventsWhenAlreadyCancelled()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "basic.docx");
        using var input = File.OpenRead(path);
        await using var documentInput = await DocumentInput.CreateAsync(input);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var sink = new RecordingParseEventSink();
        IParser parser = new DocxParser();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => parser.ParseAsync(documentInput, sink, cancellation.Token));

        Assert.Empty(sink.Events);
        Assert.Equal(input.Length, input.Position);
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task PropagatesSinkFailureAndLeavesInputOpen()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "basic.docx");
        using var input = File.OpenRead(path);
        await using var documentInput = await DocumentInput.CreateAsync(input);
        var failure = new InvalidOperationException("Sink failed.");
        var received = new List<ParseEvent>();
        var sink = new CallbackParseEventSink((parseEvent, _) =>
        {
            received.Add(parseEvent);
            if (parseEvent is TextContent) throw failure;
            return ValueTask.CompletedTask;
        });
        IParser parser = new DocxParser();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => parser.ParseAsync(documentInput, sink));

        Assert.Same(failure, actual);
        Assert.Single(received.OfType<TextContent>());
        Assert.IsType<TextContent>(received[^1]);
        Assert.True(input.CanRead);
    }
}
