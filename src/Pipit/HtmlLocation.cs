namespace Pipit;

/// <summary>
/// Identifies a text range within an element in the captured HTML.
/// </summary>
/// <param name="ElementId">The source element's HTML ID.</param>
/// <param name="Start">
/// The zero-based UTF-16 offset within the element's DOM TextContent</param>
/// <param name="Length">
/// The range legnth in UTF-16 code units.
/// </param>
public sealed record HtmlLocation(
    string ElementId,
    int Start,
    int Length) : SourceLocation;
