using AngleSharp.Dom;
using AngleSharpHtmlParser = AngleSharp.Html.Parser.HtmlParser;

namespace Pipit;

/// <summary>
/// Parses HTML documents to extract their content and metadata.
/// </summary>
public sealed class HtmlParser : IParser
{
    /// <summary>
    /// Parses HTML and sends content events to a sink.
    /// </summary>
    /// <param name="input">The readable HTML stream.</param>
    /// <param name="sink">The receiver of parsed content events.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel parsing.
    /// </param>
    /// <returns>A task representing parsing.</returns>
    public async Task ParseAsync(
        Stream input,
        IParseEventSink sink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(sink);
        cancellationToken.ThrowIfCancellationRequested();

        var parser = new AngleSharpHtmlParser();

        using var document = await parser.ParseDocumentAsync(
            input,
            cancellationToken);

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var duplicateIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in document.QuerySelectorAll("[id]"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var id = element.Id;

            if (!string.IsNullOrEmpty(id) && !seenIds.Add(id))
            {
                duplicateIds.Add(id);
            }
        }

        if (document.Head is { } head)
        {
            await EmitHeadMetadataAsync(head, sink, cancellationToken);
        }

        if (document.Body is { } body)
        {
            await VisitAsync(body, null, 0);
        }

        async Task<int> VisitAsync(
            INode node,
            string? elementId,
            int offset)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node is IText text)
            {
                HtmlLocation? location = elementId is null
                    ? null
                    : new HtmlLocation(elementId, offset, text.Data.Length);

                await sink.WriteAsync(
                    new TextContent(text.Data, location),
                    cancellationToken);

                return text.Data.Length;
            }

            if (node is not IElement element)
            {
                return 0;
            }

            var name = element.LocalName;

            if (name == "br")
            {
                await sink.WriteAsync(
                    new LineBreak(),
                    cancellationToken);

                return 0;
            }

            if (name is "script" or "style" or "template")
            {
                return element.TextContent.Length;
            }

            // shortcut: locations require element IDs; add DOM locators for general HTML.
            if (!string.IsNullOrEmpty(element.Id) &&
                !duplicateIds.Contains(element.Id))
            {
                elementId = element.Id;
                offset = 0;
            }

            ContentKind? kind = name switch
            {
                "div" => ContentKind.Block,
                "p" => ContentKind.Paragraph,
                "strong" => ContentKind.StrongEmphasis,
                "em" => ContentKind.Emphasis,
                "h1" or "h2" or "h3" or
                "h4" or "h5" or "h6" => ContentKind.Heading,
                _ => null
            };

            if (kind is { } contentKind)
            {
                int? headingLevel = contentKind == ContentKind.Heading
                    ? name[1] - '0'
                    : null;

                await sink.WriteAsync(
                    new StartElement(contentKind, headingLevel),
                    cancellationToken);
            }

            var consumed = 0;

            foreach (var child in element.ChildNodes)
            {
                consumed += await VisitAsync(
                    child,
                    elementId,
                    offset + consumed);
            }

            if (kind is { } endKind)
            {
                await sink.WriteAsync(
                    new EndElement(endKind),
                    cancellationToken);
            }


            return consumed;
        }
    }

    private static async Task EmitHeadMetadataAsync(
    IElement head,
    IParseEventSink sink,
    CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var title = head.QuerySelector("title")?.TextContent.Trim();

        if (!string.IsNullOrEmpty(title))
        {
            await sink.WriteAsync(
                new ExtractedMetadata<string>(MetadataKeys.Title, title),
                cancellationToken);
        }

        foreach (var meta in head.QuerySelectorAll("meta"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            MetadataKey<string>? key =
                meta.GetAttribute("name")?.ToLowerInvariant() switch
                {
                    "description" => MetadataKeys.Description,
                    "author" => MetadataKeys.Author,
                    _ => null
                };

            if (key is null)
            {
                continue;
            }

            var value = meta.GetAttribute("content")?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            await sink.WriteAsync(
                new ExtractedMetadata<string>(key, value),
                cancellationToken);
        }
    }
}
