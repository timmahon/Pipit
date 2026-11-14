namespace Pipit.Rendering;

/// <summary>
/// Links a range of rendered text to the extraction evidence that supplied it.
/// </summary>
/// <param name="OutputRange">The range in the rendered content, excluding generated syntax.</param>
/// <param name="Location">The original text event's source location, without modification.</param>
/// <param name="SourceTextRange">
/// The selected range within the original TextContent.Text. Offset zero is the
/// beginning of that event's text; the location's own coordinates remain unchanged.
/// Collapsed whitespace refers to the first source character in its run.
/// </param>
public sealed record SourceMapping(
    TextRange OutputRange,
    SourceLocation Location,
    TextRange SourceTextRange);
