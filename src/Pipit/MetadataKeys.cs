namespace Pipit;

/// <summary>
/// Defines shared metadata properties.
/// </summary>
public static class MetadataKeys
{
    /// <summary>
    /// Gets the document-supplied title property.
    /// </summary>
    public static MetadataKey<string> Title { get; } = DublinCore.Title;

    /// <summary>
    /// Gets the document-supplied description property.
    /// </summary>
    public static MetadataKey<string> Description { get; } = DublinCore.Description;

    /// <summary>
    /// Gets the document-supplied author property.
    /// </summary>
    public static MetadataKey<string> Author { get; } = DublinCore.Creator;
}
