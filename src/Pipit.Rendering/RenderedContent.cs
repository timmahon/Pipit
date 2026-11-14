namespace Pipit.Rendering;

/// <summary>
/// Contains rendered text and mappings back to its extraction evidence.
/// </summary>
/// <param name="Content">The rendered content.</param>
/// <param name="SourceMappings">
/// Mappings for source text with known locations. Generated syntax and separators
/// have no mapping. Locations refer to the input capture supplied to the parser.
/// </param>
public sealed record RenderedContent(
    string Content,
    IReadOnlyList<SourceMapping> SourceMappings);
