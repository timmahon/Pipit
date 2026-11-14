using Pipit.Parsers.Html;
using Pipit.Rendering;
using Markdig;
using System.Text;

namespace Pipit.Tests;

public sealed class MarkdownSinkTests
{
    [Fact]
    public async Task SeparatesParagraphsWithBlankLine()
    {
        var sink = new MarkdownSink();

        await sink.WriteAsync(
            new StartElement(ContentKind.Paragraph));
        await sink.WriteAsync(new TextContent("First paragraph."));
        await sink.WriteAsync(
            new EndElement(ContentKind.Paragraph));

        await sink.WriteAsync(
            new StartElement(ContentKind.Paragraph));
        await sink.WriteAsync(new TextContent("Second paragraph."));
        await sink.WriteAsync(
            new EndElement(ContentKind.Paragraph));

        Assert.Equal(
            "First paragraph.\n\nSecond paragraph.",
            sink.GetContent());
    }

    [Fact]
    public async Task KeepsAdjacentTextFragmentsTogether()
    {
        var sink = new MarkdownSink();

        await sink.WriteAsync(
            new StartElement(ContentKind.Paragraph));
        await sink.WriteAsync(new TextContent("Hello "));
        await sink.WriteAsync(new TextContent("world"));
        await sink.WriteAsync(new TextContent("."));
        await sink.WriteAsync(
            new EndElement(ContentKind.Paragraph));

        Assert.Equal("Hello world.", sink.GetContent());
    }

    [Fact]
    public async Task NestedBlocksDoNotAddExtraSeparators()
    {
        var sink = new MarkdownSink();

        ParseEvent[] events =
        [
            new StartElement(ContentKind.Block),

                new StartElement(ContentKind.Paragraph),
                    new TextContent("First."),
                new EndElement(ContentKind.Paragraph),
    
                new StartElement(ContentKind.Block),
                    new StartElement(ContentKind.Paragraph),
                        new TextContent("Second."),
                    new EndElement(ContentKind.Paragraph),
                new EndElement(ContentKind.Block),

            new EndElement(ContentKind.Block)
        ];

        foreach (var parseEvent in events)
        {
            await sink.WriteAsync(parseEvent);
        }

        Assert.Equal("First.\n\nSecond.", sink.GetContent());
    }

    [Fact]
    public async Task SeparatesTextAroundNestedParagraph()
    {
        var sink = new MarkdownSink();

        ParseEvent[] events =
        [
            new StartElement(ContentKind.Block),
                new TextContent("Before."),
                new StartElement(ContentKind.Paragraph),
                    new TextContent("Inside."),
                new EndElement(ContentKind.Paragraph),
                new TextContent("After."),
            new EndElement(ContentKind.Block)
        ];

        foreach (var parseEvent in events)
        {
            await sink.WriteAsync(parseEvent);
        }

        Assert.Equal(
            "Before.\n\nInside.\n\nAfter.",
            sink.GetContent());
    }

    [Theory]
    [InlineData(1, "#")]
    [InlineData(2, "##")]
    [InlineData(6, "######")]
    public async Task RendersHeadingBetweenParagraphs(
    int level,
    string marker)
    {
        var sink = new MarkdownSink();

        ParseEvent[] events =
        [
            new StartElement(ContentKind.Paragraph),
                new TextContent("Before."),
            new EndElement(ContentKind.Paragraph),

            new StartElement(ContentKind.Heading, level),
                new TextContent("Introduction"),
            new EndElement(ContentKind.Heading),

            new StartElement(ContentKind.Paragraph),
                new TextContent("After."),
            new EndElement(ContentKind.Paragraph)
        ];

        foreach (var parseEvent in events)
        {
            await sink.WriteAsync(parseEvent);
        }

        Assert.Equal(
            $"Before.\n\n{marker} Introduction\n\nAfter.",
            sink.GetContent());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(7)]
    public async Task RejectsUnsupportedHeadingLevel(int? level)
    {
        var sink = new MarkdownSink();

        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await sink.WriteAsync(
                new StartElement(ContentKind.Heading, level)));
    }

    [Theory]
    [InlineData(ContentKind.Emphasis, "*", "*")]
    [InlineData(ContentKind.StrongEmphasis, "**", "**")]
    public async Task RendersInlineEmphasis(
        ContentKind kind,
        string openingTag,
        string closingTag)
    {
        var sink = new MarkdownSink();

        ParseEvent[] events =
        [
            new StartElement(ContentKind.Paragraph),
                new TextContent("Before."),
            new EndElement(ContentKind.Paragraph),

            new StartElement(ContentKind.Paragraph),
                new StartElement(kind),
                    new TextContent("Important"),
                new EndElement(kind),
                new TextContent(" information."),
            new EndElement(ContentKind.Paragraph)
        ];

        foreach (var parseEvent in events)
        {
            await sink.WriteAsync(parseEvent);
        }

        Assert.Equal(
            $"Before.\n\n{openingTag}Important{closingTag} information.",
            sink.GetContent());
    }

    [Theory]
    [InlineData(ContentKind.Emphasis, "", "")]
    [InlineData(ContentKind.Emphasis, "   ", "")]
    [InlineData(
        ContentKind.Emphasis,
        " important ",
        "*important*")]
    [InlineData(ContentKind.StrongEmphasis, "", "")]
    [InlineData(
        ContentKind.StrongEmphasis,
        " important ",
        "**important**")]
    public async Task TrimsBlockEdgeWhitespaceAroundEmphasis(
    ContentKind kind,
    string text,
    string expected)
    {
        var sink = new MarkdownSink();

        ParseEvent[] events =
        [
            new StartElement(ContentKind.Paragraph),
                new StartElement(kind),
                    new TextContent(text),
                new EndElement(kind),
            new EndElement(ContentKind.Paragraph)
        ];

        foreach (var parseEvent in events)
        {
            await sink.WriteAsync(parseEvent);
        }

        Assert.Equal(expected, sink.GetContent());
    }

    [Fact]
    public async Task RejectsMismatchedInlineClosingElement()
    {
        var sink = new MarkdownSink();

        await sink.WriteAsync(
            new StartElement(ContentKind.Emphasis));

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await sink.WriteAsync(
                new EndElement(ContentKind.StrongEmphasis)));
    }

    [Fact]
    public async Task RendersExplicitLineBreak()
    {
        var sink = new MarkdownSink();

        ParseEvent[] events =
        [
            new StartElement(ContentKind.Paragraph),
                new TextContent("First line."),
                new LineBreak(),
                new TextContent("Second line."),
            new EndElement(ContentKind.Paragraph)
        ];

        foreach (var parseEvent in events)
        {
            await sink.WriteAsync(parseEvent);
        }

        Assert.Equal(
            "First line.\\\nSecond line.",
            sink.GetContent());
    }

    [Theory]
    [InlineData("First", 1, "Second", "First\\\nSecond")]
    [InlineData("", 1, "Second", "Second")]
    [InlineData("First", 1, "", "First")]
    [InlineData("First", 2, "Second", "First\\\nSecond")]
    [InlineData("", 1, "", "")]
    public async Task NormalizesExplicitBreaks(
    string before,
    int breakCount,
    string after,
    string expected)
    {
        var sink = new MarkdownSink();

        await sink.WriteAsync(
            new StartElement(ContentKind.Paragraph));
        await sink.WriteAsync(new TextContent(before));

        for (var index = 0; index < breakCount; index++)
        {
            await sink.WriteAsync(new LineBreak());
        }

        await sink.WriteAsync(new TextContent(after));
        await sink.WriteAsync(
            new EndElement(ContentKind.Paragraph));

        Assert.Equal(expected, sink.GetContent());
    }

    [Theory]
    [InlineData("*literal*", "\\*literal\\*")]
    [InlineData("_literal_", "\\_literal\\_")]
    [InlineData("`code`", "\\`code\\`")]
    [InlineData("[link](target)", "\\[link\\](target)")]
    [InlineData("<script>", "\\<script\\>")]
    [InlineData("&copy;", "\\&copy;")]
    [InlineData(@"C:\files", @"C:\\files")]
    public async Task EscapesLiteralInlineSyntax(
    string text,
    string expected)
    {
        var sink = new MarkdownSink();

        await sink.WriteAsync(
            new StartElement(ContentKind.Paragraph));
        await sink.WriteAsync(new TextContent(text));
        await sink.WriteAsync(
            new EndElement(ContentKind.Paragraph));

        Assert.Equal(expected, sink.GetContent());
    }

    [Fact]
    public async Task EscapesTextInsideGeneratedEmphasis()
    {
        var sink = new MarkdownSink();

        ParseEvent[] events =
        [
            new StartElement(ContentKind.Paragraph),
                new StartElement(ContentKind.StrongEmphasis),
                    new TextContent("*literal*"),
                new EndElement(ContentKind.StrongEmphasis),
            new EndElement(ContentKind.Paragraph)
        ];

        foreach (var parseEvent in events)
        {
            await sink.WriteAsync(parseEvent);
        }

        Assert.Equal(
            "**\\*literal\\***",
            sink.GetContent());
    }

    [Theory]
    [InlineData("# Not a heading", "\\# Not a heading")]
    [InlineData("- Not a list", "\\- Not a list")]
    [InlineData("+ Not a list", "\\+ Not a list")]
    [InlineData("1. Not a list", "1\\. Not a list")]
    [InlineData("1) Not a list", "1\\) Not a list")]
    [InlineData("---", "\\-\\-\\-")]
    [InlineData("===", "\\=\\=\\=")]
    public async Task EscapesLiteralBlockSyntax(
    string text,
    string expected)
    {
        var sink = new MarkdownSink();

        await sink.WriteAsync(
            new StartElement(ContentKind.Paragraph));
        await sink.WriteAsync(new TextContent(text));
        await sink.WriteAsync(
            new EndElement(ContentKind.Paragraph));

        Assert.Equal(expected, sink.GetContent());
    }

    [Fact]
    public async Task EscapesOrderedListMarkerAcrossTextFragments()
    {
        var sink = new MarkdownSink();

        await sink.WriteAsync(
            new StartElement(ContentKind.Paragraph));
        await sink.WriteAsync(new TextContent("1"));
        await sink.WriteAsync(new TextContent(". Literal text"));
        await sink.WriteAsync(
            new EndElement(ContentKind.Paragraph));

        Assert.Equal("1\\. Literal text", sink.GetContent());
    }

    [Theory]
    [InlineData(
        "<p><em>one</em><em>two</em></p>",
        "<p><em>onetwo</em></p>")]
    [InlineData(
        "<p><strong>one</strong><strong>two</strong></p>",
        "<p><strong>onetwo</strong></p>")]
    [InlineData(
    "<p>a<em>!</em>b</p>",
    "<p>a!b</p>")]
    [InlineData("<p>a<strong>!</strong>b</p>", "<p>a!b</p>")]
    [InlineData("<p>A<em>word</em>B</p>", "<p>A<em>word</em>B</p>")]
    [InlineData("<p><em>one</em><em>two</em><em>three</em></p>", "<p><em>onetwothree</em></p>")]
    [InlineData("<p><em>word</em><em>!</em></p>", "<p><em>word!</em></p>")]
    [InlineData("<p><em>a</em><strong>b</strong></p>", "<p>ab</p>")]
    [InlineData(
        "<p><strong>a<em>bc</em></strong><em><strong>de</strong>f</em></p>",
        "<p>abcdef</p>")]
    [InlineData("<p><strong><em>both</em></strong></p>", "<p><em><strong>both</strong></em></p>")]
    [InlineData("<p><em>outer <strong>inner</strong> outer</em></p>", "<p><em>outer <strong>inner</strong> outer</em></p>")]
    [InlineData("<p><strong>outer <em>inner</em> outer</strong></p>", "<p><strong>outer <em>inner</em> outer</strong></p>")]
    [InlineData("<p><em>*literal*</em></p>", "<p><em>*literal*</em></p>")]
    [InlineData("<p><em>one</em></p><p><em>two</em></p>", "<p><em>one</em></p>\n<p><em>two</em></p>")]
    [InlineData("<p><em>outer <em>inner</em> outer</em></p>", "<p><em>outer inner outer</em></p>")]
    [InlineData("<p><em>one</em><strong></strong><em>two</em></p>", "<p><em>onetwo</em></p>")]
    [InlineData(
        "<p><em><strong>one</strong></em><em><strong>two</strong></em></p>",
        "<p><em><strong>onetwo</strong></em></p>")]
    [InlineData(
        "<p><em>A<strong>B<em>C</em></strong>D</em></p>",
        "<p><em>A<strong>BC</strong>D</em></p>")]
    [InlineData(
        "<p><strong>A<em>B<strong>C</strong></em>D</strong></p>",
        "<p><strong>A<em>BC</em>D</strong></p>")]
    public async Task NormalizesEmphasisWhenMarkdownIsInterpreted(
    string html,
    string expectedHtml)
    {
        using var input = new MemoryStream(
            Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);

        IParser parser = new HtmlParser();
        var sink = new MarkdownSink();

        await parser.ParseAsync(documentInput, sink);

        var renderedHtml = Markdig.Markdown
            .ToHtml(sink.GetContent(), new MarkdownPipelineBuilder().DisableHtml().Build())
            .TrimEnd();

        Assert.Equal(expectedHtml, renderedHtml);
    }

    [Fact]
    public async Task MergesAdjacentEmphasisInMarkdownOutput()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(
            "<p><em>one</em><em>two</em></p>"));
        await using var documentInput = await DocumentInput.CreateAsync(input);
        var sink = new MarkdownSink();

        await new HtmlParser().ParseAsync(documentInput, sink);

        Assert.Equal("*onetwo*", sink.GetContent());
    }

    [Theory]
    [InlineData("<p>A<br><em>B</em></p>", "<p>A<br />\n<em>B</em></p>")]
    [InlineData("<p><em>A<br>B</em></p>", "<p><em>A<br />\nB</em></p>")]
    [InlineData("<p>A<br><br>B</p>", "<p>A<br />\nB</p>")]
    [InlineData("<p><br>A<br></p>", "<p>A</p>")]
    [InlineData("<p>A<br></p><p>B</p>", "<p>A</p>\n<p>B</p>")]
    [InlineData("<h1>A<br>B</h1>", "<h1>A B</h1>")]
    public async Task NormalizesBreaksWithoutInlineHtml(string html, string expectedHtml)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);
        var sink = new MarkdownSink();

        await new HtmlParser().ParseAsync(documentInput, sink);

        var markdown = sink.GetContent();
        Assert.DoesNotContain("<", markdown);
        Assert.Equal(expectedHtml, Markdig.Markdown.ToHtml(
            markdown, new MarkdownPipelineBuilder().DisableHtml().Build()).TrimEnd());
    }

    [Fact]
    public async Task ReadingContentDoesNotChangeOpenInlineElements()
    {
        var sink = new MarkdownSink();

        await sink.WriteAsync(new StartElement(ContentKind.Paragraph));
        await sink.WriteAsync(new StartElement(ContentKind.Emphasis));
        await sink.WriteAsync(new TextContent(" Hello "));

        Assert.Equal("Hello", sink.GetContent());

        await sink.WriteAsync(new EndElement(ContentKind.Emphasis));
        Assert.Equal("*Hello*", sink.GetContent());

        await sink.WriteAsync(new TextContent("world"));
        await sink.WriteAsync(new EndElement(ContentKind.Paragraph));
        Assert.Equal("*Hello* world", sink.GetContent());
    }

    [Fact]
    public async Task ReopensInlineFormattingAfterBlockBoundary()
    {
        var sink = new MarkdownSink();

        await sink.WriteAsync(new StartElement(ContentKind.Paragraph));
        await sink.WriteAsync(new StartElement(ContentKind.Emphasis));
        await sink.WriteAsync(new TextContent("word"));

        await sink.WriteAsync(new EndElement(ContentKind.Paragraph));
        await sink.WriteAsync(new StartElement(ContentKind.Paragraph));
        await sink.WriteAsync(new TextContent("next"));

        await sink.WriteAsync(new EndElement(ContentKind.Emphasis));
        await sink.WriteAsync(new EndElement(ContentKind.Paragraph));
        Assert.Equal("*word*\n\n*next*", sink.GetContent());
    }

    [Theory]
    [InlineData("<em>before<div>inside</div>after</em>", "*before*\n\n*inside*\n\n*after*")]
    [InlineData("<strong><em>before<div>inside</div>after</em></strong>", "***before***\n\n***inside***\n\n***after***")]
    [InlineData("<em><div></div><div>inside</div></em>", "*inside*")]
    [InlineData("<em>before<h1>heading</h1>after</em>", "*before*\n\n# *heading*\n\n*after*")]
    [InlineData("<h1></h1><p>Body</p>", "Body")]
    [InlineData("<h1> \t\n </h1>", "")]
    [InlineData("<h1><em> </em><br></h1><p>Body</p>", "Body")]
    public async Task HandlesFormattingAcrossBlocksAndEmptyHeadings(string html, string expected)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(html));
        await using var input = await DocumentInput.CreateAsync(source);
        var sink = new MarkdownSink();

        await new HtmlParser().ParseAsync(input, sink);

        Assert.Equal(expected, sink.GetContent());
    }

    [Theory]
    [InlineData("section")]
    [InlineData("article")]
    [InlineData("main")]
    [InlineData("header")]
    [InlineData("footer")]
    [InlineData("nav")]
    [InlineData("aside")]
    [InlineData("blockquote")]
    [InlineData("address")]
    [InlineData("figure")]
    [InlineData("figcaption")]
    [InlineData("ul")]
    [InlineData("ol")]
    [InlineData("li")]
    [InlineData("dl")]
    [InlineData("dt")]
    [InlineData("dd")]
    public async Task PreservesStructuralHtmlBoundaries(string element)
    {
        var html = $"<{element}>First</{element}><{element}>Second</{element}>";
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(html));
        await using var input = await DocumentInput.CreateAsync(source);
        var sink = new MarkdownSink();

        await new HtmlParser().ParseAsync(input, sink);

        Assert.Equal("First\n\nSecond", sink.GetContent());
    }

    [Theory]
    [InlineData(
        "<p><strong><strong><strong>1.?</strong>!</strong><em><strong>?word</strong>?</em></strong>&amp;</p>",
        "1.?!?word?&")]
    [InlineData(
        "<p><em>*literal*<strong><strong>word </strong><strong>word[x]</strong></strong></em>" +
        "<em><strong><strong>![x]</strong><em>!word</em></strong>?</em></p>",
        "*literal*word word[x]![x]!word?")]
    public async Task NestedStylesDoNotIntroduceVisibleDelimiterCharacters(string html, string expectedText)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);
        var sink = new MarkdownSink();
        await new HtmlParser().ParseAsync(documentInput, sink);

        var rendered = Markdig.Markdown.ToHtml(
            sink.GetContent(), new MarkdownPipelineBuilder().DisableHtml().Build());
        using var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(rendered);

        Assert.Equal(expectedText, document.Body!.TextContent.Trim());
    }

    [Theory]
    [InlineData("<p>    ordinary text</p>", "ordinary text")]
    [InlineData("<p> Hello \t\r\n world </p>", "Hello world")]
    [InlineData("<h1>Title</h1>\n    <p>Body</p>", "# Title\n\nBody")]
    [InlineData("<p>A <strong> B </strong> C</p>", "A **B** C")]
    [InlineData("<p>A \n <br> \n B</p>", "A\\\nB")]
    [InlineData("<p>A&nbsp;&nbsp;B</p>", "A\u00a0\u00a0B")]
    [InlineData("<p>&nbsp;<br>B</p>", "\u00a0\\\nB")]
    public async Task NormalizesFlowWhitespaceWithoutCreatingCodeBlocks(string html, string expected)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(html));
        await using var documentInput = await DocumentInput.CreateAsync(input);
        var sink = new MarkdownSink();
        await new HtmlParser().ParseAsync(documentInput, sink);

        var markdown = sink.GetContent();
        var interpreted = Markdig.Markdown.ToHtml(
            markdown, new MarkdownPipelineBuilder().DisableHtml().Build());

        Assert.Equal(expected, markdown);
        Assert.DoesNotContain("<pre>", interpreted);
    }

    [Fact]
    public async Task CollapsesWhitespaceAcrossTextFragments()
    {
        var sink = new MarkdownSink();

        await sink.WriteAsync(new TextContent("A \t"));
        await sink.WriteAsync(new TextContent("\n"));
        await sink.WriteAsync(new TextContent(" \r B"));

        Assert.Equal("A B", sink.GetContent());
    }
}
