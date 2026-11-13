using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Pipit.Tests;

public sealed class HtmlExtractorTests
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

        var extractor = new HtmlExtractor();

        var result = await extractor.ExtractAsync(input);

        Assert.Equal(expected, result.Title);
    }
}
