namespace Pipit;

/// <summary>
/// Identifies a metadata property and it's value type.
/// </summary>
/// <typeparam name="T">
/// The property value type.</typeparam>
/// <param name="Name">
/// The property name.
/// </param>
public sealed record MetadataKey<T>(string Name);