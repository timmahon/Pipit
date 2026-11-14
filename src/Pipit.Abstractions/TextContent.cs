namespace Pipit;

/// <summary>
/// Contains extracted text and its optional source location.
/// </summary>
/// <param name="Text">
/// The extracted text.
/// </param>
/// <param name="Location">
/// The location in the source resource, or null when unavailable.
/// </param>
public sealed record TextContent(
    string Text,
    SourceLocation? Location = null) : ContentEvent;
