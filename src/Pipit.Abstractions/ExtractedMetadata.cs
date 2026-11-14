namespace Pipit;

/// <summary>
/// Represents a typed metadata value extracted from a source.
/// </summary>
/// <typeparam name="T">
/// The metadata value type.
/// </typeparam>
/// <param name="Key">
/// The metadata property.
/// </param>
/// <param name="Value">
/// The document-supplied value.
/// </param>
public sealed record ExtractedMetadata<T>(
    MetadataKey<T> Key,
    T Value) : ParseEvent;