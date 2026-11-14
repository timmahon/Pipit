namespace Pipit;

/// <summary>
/// Defines shared metadata keys using the Dublin Core Terms vocabulary.
/// </summary>
public static class DublinCore
{
    /// <summary>
    /// Gets the title property.
    /// </summary>
    public static MetadataKey<string> Title { get; } =
        new("http://purl.org/dc/terms/title");

    /// <summary>
    /// Gets the description property.
    /// </summary>
    public static MetadataKey<string> Description { get; } =
        new("http://purl.org/dc/terms/description");

    /// <summary>
    /// Gets the property identifying an entity responsible for creating
    /// the resource.
    /// </summary>
    public static MetadataKey<string> Creator { get; } =
        new("http://purl.org/dc/terms/creator");
}