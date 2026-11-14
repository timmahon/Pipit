namespace Pipit;

/// <summary>
/// Identifies the logical meaning of document content.
/// </summary>
public enum ContentKind
{
    /// <summary>
    /// A block-level grouping that may contain text or other elements.
    /// </summary>
    Block,

    /// <summary>
    /// A paragraph.
    /// </summary>
    Paragraph,

    /// <summary>
    /// A heading.
    /// </summary>
    Heading,

    /// <summary>
    /// Strong Emphasis
    /// </summary>
    StrongEmphasis,

    /// <summary>
    /// Emphasis.
    /// </summary>
    Emphasis
}
