namespace Pipit;

/// <summary>
/// Ends a logical content element.
/// </summary>
/// <param name="Kind">
/// The logical content kind being closed.
/// </param>
public sealed record EndElement(ContentKind Kind) : ContentEvent;