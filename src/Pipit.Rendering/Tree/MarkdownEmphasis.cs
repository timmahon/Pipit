namespace Pipit.Rendering.Tree;

internal sealed record MarkdownEmphasis(ContentKind Kind, List<MarkdownInline> Children) : MarkdownInline
{
    internal bool IsClosed { get; set; }
}
