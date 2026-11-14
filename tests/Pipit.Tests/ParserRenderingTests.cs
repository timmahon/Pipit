using System.Text;
using Pipit.Parsers.Html;
using Pipit.Parsers.OpenXml;
using Pipit.Rendering;

namespace Pipit.Tests;

public sealed class ParserRenderingTests
{
    [Fact]
    public async Task RendersHtmlAsMarkdown()
    {
        const string html =
            "<html><head><title>Metadata title</title></head>" +
            "<body>" +
            "<h1>Introduction</h1>" +
            "<p>Hello <strong>world</strong>.</p>" +
            "<p>Second paragraph.</p>" +
            "</body></html>";

        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        IParser parser = new HtmlParser();
        var sink = new MarkdownSink();

        await parser.ParseAsync(documentInput, sink);

        Assert.Equal(
            "# Introduction\n\nHello **world**.\n\nSecond paragraph.",
            sink.GetContent());
    }

    [Fact]
    public async Task RendersDocxAsMarkdown()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "basic.docx");

        using var input = File.OpenRead(path);
        await using var documentInput = await DocumentInput.CreateAsync(input);

        IParser parser = new DocxParser();
        var sink = new MarkdownSink();

        await parser.ParseAsync(documentInput, sink);

        Assert.Equal(
            "Example document\n\nFirst paragraph.\n\nSecond paragraph.",
            sink.GetContent());
    }
}
