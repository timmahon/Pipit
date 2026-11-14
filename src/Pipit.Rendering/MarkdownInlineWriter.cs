using System.Text;
using Pipit.Rendering.Tree;

namespace Pipit.Rendering;

internal static class MarkdownInlineWriter
{
    internal static string Write(List<MarkdownInline> nodes, int? headingLevel)
    {
        var normalized = MarkdownInlineNormalizer.Normalize(nodes);
        while (normalized.FirstOrDefault() is MarkdownBreak) normalized.RemoveAt(0);
        while (normalized.LastOrDefault() is MarkdownBreak) normalized.RemoveAt(normalized.Count - 1);

        var content = new StringBuilder();
        if (headingLevel is { } level) content.Append('#', level).Append(' ');
        WriteChildren(normalized, content, headingLevel is not null, '\n', '\n');
        return content.ToString();
    }

    private static void WriteChildren(
        List<MarkdownInline> nodes,
        StringBuilder content,
        bool inHeading,
        char before,
        char after)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            switch (nodes[index])
            {
                case MarkdownText text:
                    AppendEscapedText(content, text.Source.Text.AsSpan(text.Start, text.Length));
                    break;

                case MarkdownBreak:
                    content.Append(inHeading ? " " : "\\\n");
                    break;

                case MarkdownEmphasis emphasis:
                    var previous = index == 0 ? before : LastCharacter(nodes[index - 1]);
                    var next = index + 1 == nodes.Count ? after : FirstCharacter(nodes[index + 1]);
                    var marker = CanEmphasize(emphasis, previous, next)
                        ? emphasis.Kind == ContentKind.StrongEmphasis ? "**" : "*"
                        : string.Empty;

                    content.Append(marker);
                    WriteChildren(emphasis.Children, content, inHeading, previous, next);
                    content.Append(marker);
                    break;
            }
        }
    }

    private static bool CanEmphasize(MarkdownEmphasis emphasis, char before, char after)
    {
        var first = FirstCharacter(emphasis);
        var last = LastCharacter(emphasis);

        var canOpen = !char.IsWhiteSpace(first) &&
            (!IsPunctuation(first) || char.IsWhiteSpace(before) || IsPunctuation(before));
        var canClose = !char.IsWhiteSpace(last) &&
            (!IsPunctuation(last) || char.IsWhiteSpace(after) || IsPunctuation(after));

        return canOpen && canClose;
    }

    private static char FirstCharacter(MarkdownInline node) => node switch
    {
        MarkdownText text => text.Source.Text[text.Start],
        MarkdownEmphasis emphasis => FirstCharacter(emphasis.Children[0]),
        _ => '\n'
    };

    private static char LastCharacter(MarkdownInline node) => node switch
    {
        MarkdownText text => text.Source.Text[text.Start + text.Length - 1],
        MarkdownEmphasis emphasis => LastCharacter(emphasis.Children[^1]),
        _ => '\n'
    };

    private static bool IsPunctuation(char character) =>
        char.IsPunctuation(character) || char.IsSymbol(character);

    private static void AppendEscapedText(StringBuilder content, ReadOnlySpan<char> text)
    {
        foreach (var character in text)
        {
            var followsDigit = content.Length > 0 && char.IsAsciiDigit(content[^1]);
            var needsEscape = character is
                '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '&' or '#' or '+' or '-' or '=';

            if (needsEscape || (followsDigit && character is '.' or ')')) content.Append('\\');
            content.Append(character);
        }
    }
}
