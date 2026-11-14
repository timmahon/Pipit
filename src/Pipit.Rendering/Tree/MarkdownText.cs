namespace Pipit.Rendering.Tree;

internal sealed record MarkdownText(TextContent Source, int Start, int Length) : MarkdownInline;
