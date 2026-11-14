namespace Pipit;

/// <summary>
/// Contains extracted text and it's optional source location.
/// </summary>
/// <param name="Text">
/// The extracted text.
/// </param>
/// <param name="Location">
/// The location in the captured HTML, or null when unavailable.
/// </param>
public sealed record TextContent(
    string Text,
    SourceLocation? Location = null) : ContentEvent;