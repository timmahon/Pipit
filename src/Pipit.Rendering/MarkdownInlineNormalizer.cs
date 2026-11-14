using Pipit.Rendering.Tree;

namespace Pipit.Rendering;

internal static class MarkdownInlineNormalizer
{
    internal static List<MarkdownInline> Normalize(
        List<MarkdownInline> nodes,
        bool inEmphasis = false,
        bool inStrongEmphasis = false)
    {
        var result = new List<MarkdownInline>();

        for (var index = 0; index < nodes.Count; index++)
        {
            if (nodes[index] is not MarkdownEmphasis emphasis)
            {
                Add(result, nodes[index]);
                continue;
            }

            var children = Normalize(
                emphasis.Children,
                inEmphasis || emphasis.IsClosed && emphasis.Kind == ContentKind.Emphasis,
                inStrongEmphasis || emphasis.IsClosed && emphasis.Kind == ContentKind.StrongEmphasis);

            var alreadyActive = emphasis.Kind == ContentKind.Emphasis ? inEmphasis : inStrongEmphasis;

            if (!emphasis.IsClosed || alreadyActive)
            {
                foreach (var child in children) Add(result, child);
                continue;
            }

            var leading = TakeEdgeWhitespace(children, leading: true);
            var trailing = TakeEdgeWhitespace(children, leading: false);

            foreach (var child in leading) Add(result, child);
            if (children.Count > 0)
            {
                Add(result, new MarkdownEmphasis(emphasis.Kind, children) { IsClosed = true });
            }
            foreach (var child in trailing) Add(result, child);
        }

        var normalized = new List<MarkdownInline>();

        for (var index = 0; index < result.Count; index++)
        {
            if (result[index] is not MarkdownEmphasis first)
            {
                Add(normalized, result[index]);
                continue;
            }

            var adjacent = new List<MarkdownEmphasis> { first };
            while (index + 1 < result.Count && result[index + 1] is MarkdownEmphasis next)
            {
                adjacent.Add(next);
                index++;
            }

            if (adjacent.Count == 1)
            {
                Add(normalized, first);
            }
            else if (adjacent.All(emphasis => emphasis.Kind == first.Kind))
            {
                var children = adjacent.SelectMany(emphasis => emphasis.Children).ToList();
                Add(normalized, new MarkdownEmphasis(first.Kind,
                    Normalize(children,
                        inEmphasis || first.Kind == ContentKind.Emphasis,
                        inStrongEmphasis || first.Kind == ContentKind.StrongEmphasis))
                {
                    IsClosed = true
                });
            }
            else
            {
                // Different touching styles can create ambiguous Markdown delimiter runs.
                foreach (var emphasis in adjacent) Flatten(emphasis, normalized);
            }
        }

        return normalized;
    }

    private static List<MarkdownInline> TakeEdgeWhitespace(List<MarkdownInline> children, bool leading)
    {
        var whitespace = new List<MarkdownInline>();

        while (children.Count > 0)
        {
            var index = leading ? 0 : children.Count - 1;
            var child = children[index];

            if (child is MarkdownBreak)
            {
                children.RemoveAt(index);
                whitespace.Add(child);
                continue;
            }

            if (child is not MarkdownText text) break;

            var count = 0;
            while (count < text.Length && char.IsWhiteSpace(text.Source.Text[
                leading ? text.Start + count : text.Start + text.Length - count - 1])) count++;

            if (count == 0) break;

            whitespace.Add(text with
            {
                Start = leading ? text.Start : text.Start + text.Length - count,
                Length = count
            });

            if (count == text.Length)
            {
                children.RemoveAt(index);
            }
            else
            {
                children[index] = text with
                {
                    Start = leading ? text.Start + count : text.Start,
                    Length = text.Length - count
                };
                break;
            }
        }

        if (!leading) whitespace.Reverse();
        return whitespace;
    }

    private static void Flatten(MarkdownInline node, List<MarkdownInline> result)
    {
        if (node is MarkdownEmphasis emphasis)
        {
            foreach (var child in emphasis.Children) Flatten(child, result);
        }
        else
        {
            Add(result, node);
        }
    }

    private static void Add(List<MarkdownInline> result, MarkdownInline node)
    {
        if (node is MarkdownBreak && result.LastOrDefault() is MarkdownBreak) return;
        result.Add(node);
    }
}
