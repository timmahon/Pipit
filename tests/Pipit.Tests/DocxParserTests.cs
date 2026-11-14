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

        var sink = new RecordingParseEventSink();
        IParser parser = new DocxParser();

        await parser.ParseAsync(input, sink);

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
}