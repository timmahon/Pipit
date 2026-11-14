using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Pipit.Parsers.OpenXml;

/// <summary>
/// Parses Word Open XML documents into metadata and content events.
/// </summary>
public sealed class DocxParser : IParser
{
    /// <inheritdoc />
    public async Task ParseAsync(
        Stream input,
        IParseEventSink sink,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(sink);
        cancellationToken.ThrowIfCancellationRequested();

        using var document = WordprocessingDocument.Open(
            input,
            isEditable: false);

        var mainPart = document.MainDocumentPart
            ?? throw new InvalidDataException(
            "The Word document has no main document part.");

        var body = mainPart.Document?.Body
            ?? throw new InvalidDataException(
                "The Word document has no main document body.");

        var partUri = mainPart.Uri.ToString();

        await EmitMetadataAsync(
            DublinCore.Title,
            document.PackageProperties.Title);

        await EmitMetadataAsync(
            DublinCore.Creator,
            document.PackageProperties.Creator);

        var paragraphIndex = 0;

        // shortcut: plain top-level paragraphs only; extend traversal for richer DOCX content.
        foreach (var paragraph in body.Elements<Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            await sink.WriteAsync(
                new StartElement(ContentKind.Paragraph),
                cancellationToken);

            var text = string.Concat(
                paragraph.Descendants<Text>()
                    .Select(part => part.Text));

            if (text.Length > 0)
            {
                await sink.WriteAsync(
                    new TextContent(
                        text,
                        new DocxLocation(
                            partUri,
                            paragraphIndex,
                            0,
                            text.Length)),
                    cancellationToken);
            }

            await sink.WriteAsync(
                new EndElement(ContentKind.Paragraph),
                cancellationToken);

            paragraphIndex++;
        }

        async Task EmitMetadataAsync(
            MetadataKey<string> key,
            string? value)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var trimmed = value?.Trim();

            if (!string.IsNullOrEmpty(trimmed))
            {
                await sink.WriteAsync(
                    new ExtractedMetadata<string>(key, trimmed),
                    cancellationToken);
            }
        }
    }
}