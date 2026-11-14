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
/// <remarks>
/// Inline formatting may span block boundaries. Renderers that require formatting
/// within individual blocks should close and reopen it at those boundaries.
/// Elements must still close in nesting order.
/// </remarks>
public sealed record StartElement(
    ContentKind Kind,
    int? HeadingLevel = null) : ContentEvent;
