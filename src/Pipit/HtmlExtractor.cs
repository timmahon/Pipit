using AngleSharp.Html.Parser;

namespace Pipit;

/// <summary>
/// Provides functionality for extracting information from HTML content.
/// </summary>
public sealed class HtmlExtractor
{
    /// <summary>
    /// Extracts the title from a HTML stream.
    /// </summary>
    /// <param name="input">The readable HTML stream.</param>
    /// <param name="cancellationToken">A token used to cancel extraction.</param>
    /// <returns>The extracted document information.</returns>
    public async Task<ExtractionResult> ExtractAsync(
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var parser = new HtmlParser();

        using var document = await parser.ParseDocumentAsync(
            input,
            cancellationToken);

        var title = document.QuerySelector("title")?.TextContent.Trim();

        return new ExtractionResult(
            string.IsNullOrEmpty(title) ? null : title);
    }
}
