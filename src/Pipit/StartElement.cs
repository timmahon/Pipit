namespace Pipit;

/// <summary>
/// Begins a logical content element.
/// </summary>
/// <param name="Kind">
/// The logical content kind.
/// </param>
/// <param name="HeadingLevel">
/// The positive heading level for headings; otherwise null.
/// </param>
public sealed record StartElement(
    ContentKind Kind,
    int? HeadingLevel = null) : ContentEvent;