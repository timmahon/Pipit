namespace Pipit;

/// <summary>
/// Contains information extracted from a document.
/// </summary>
/// <param name="Title">
/// The document-supplied title, or null when absent or empty.
/// </param>
public sealed record ExtractionResult(string? Title);