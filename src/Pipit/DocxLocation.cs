namespace Pipit;

/// <summary>
/// Identifies a text range within a top-level DOCX body paragraph.
/// </summary>
/// <param name="PartUri">The package part containing the paragraph.</param>
/// <param name="ParagraphIndex">
/// The zero-based index among top-level body paragraphs, including empty ones.
/// </param>
/// <param name="Start">
/// The zero-based UTF-16 offset within the paragraph's concatenated text elements.
/// </param>
/// <param name="Length">The range length in UTF-16 code units.</param>
public sealed record DocxLocation(
    string PartUri,
    int ParagraphIndex,
    int Start,
    int Length) : SourceLocation;