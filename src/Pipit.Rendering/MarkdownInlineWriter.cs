using System.Text;
using Pipit.Rendering.Tree;

namespace Pipit.Rendering;

internal sealed class MarkdownInlineWriter
{
    private readonly StringBuilder _content = new();
    private readonly List<SourceMapping> _mappings = [];
    private (TextContent Source, int Offset)? _pendingSpace;
    private TextContent? _lastMappedSource;
    private bool _hasLineText;

    internal static RenderedContent Write(List<MarkdownInline> nodes, int? headingLevel)
    {
        var normalized = MarkdownInlineNormalizer.Normalize(nodes);
        while (normalized.FirstOrDefault() is MarkdownBreak) normalized.RemoveAt(0);
        while (normalized.LastOrDefault() is MarkdownBreak) normalized.RemoveAt(normalized.Count - 1);

        var writer = new MarkdownInlineWriter();
        if (headingLevel is { } level) writer._content.Append('#', level).Append(' ');
        writer.WriteChildren(normalized, headingLevel is not null, '\n', '\n');
        if (headingLevel is { } heading && writer._content.Length == heading + 1)
        {
            return new RenderedContent(string.Empty, Array.Empty<SourceMapping>());
        }
        return new RenderedContent(writer._content.ToString(), writer._mappings.AsReadOnly());
    }

    private void WriteChildren(List<MarkdownInline> nodes, bool inHeading, char before, char after)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            switch (nodes[index])
            {
                case MarkdownText text:
                    AppendEscapedText(text);
                    break;

                case MarkdownBreak:
                    _pendingSpace = null;
                    _content.Append(inHeading ? " " : "\\\n");
                    _hasLineText = false;
                    break;

                case MarkdownEmphasis emphasis:
                    var previous = index == 0 ? before : LastCharacter(nodes[index - 1]);
                    var next = index + 1 == nodes.Count ? after : FirstCharacter(nodes[index + 1]);
                    var marker = CanEmphasize(emphasis, previous, next)
                        ? emphasis.Kind == ContentKind.StrongEmphasis ? "**" : "*"
                        : string.Empty;

                    FlushSpace();
                    _content.Append(marker);
                    WriteChildren(emphasis.Children, inHeading, previous, next);
                    _content.Append(marker);
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

    // shortcut: flow text only; add a preformatted content kind before rendering code blocks.
    internal static bool IsFlowWhitespace(char character) => character is ' ' or '\t' or '\r' or '\n' or '\f';

    private void AppendEscapedText(MarkdownText text)
    {
        var outputStart = _content.Length;
        var sourceStart = text.Start;

        for (var sourceOffset = text.Start; sourceOffset < text.Start + text.Length; sourceOffset++)
        {
            var character = text.Source.Text[sourceOffset];

            if (IsFlowWhitespace(character))
            {
                AddRun(sourceOffset);
                if (_hasLineText) _pendingSpace ??= (text.Source, sourceOffset);
                sourceStart = sourceOffset + 1;
                outputStart = _content.Length;
                continue;
            }

            if (_pendingSpace is not null)
            {
                FlushSpace();
                sourceStart = sourceOffset;
                outputStart = _content.Length;
            }

            var followsDigit = _content.Length > 0 && char.IsAsciiDigit(_content[^1]);
            var needsEscape = character is
                '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '&' or '#' or '+' or '-' or '=';

            if (needsEscape || (followsDigit && character is '.' or ')'))
            {
                AddRun(sourceOffset);
                _content.Append('\\');
                sourceStart = sourceOffset;
                outputStart = _content.Length;
            }

            _content.Append(character);
            _hasLineText = true;
        }

        AddRun(text.Start + text.Length);

        void AddRun(int sourceEnd)
        {
            if (sourceEnd > sourceStart)
            {
                AddMapping(text.Source, outputStart, sourceStart, sourceEnd - sourceStart);
            }
        }
    }

    private void FlushSpace()
    {
        if (_pendingSpace is not { } space) return;
        var outputStart = _content.Length;
        _content.Append(' ');
        AddMapping(space.Source, outputStart, space.Offset, 1);
        _pendingSpace = null;
    }

    private void AddMapping(TextContent source, int outputStart, int sourceStart, int length)
    {
        if (source.Location is not { } location) return;

        if (_mappings.Count > 0 && ReferenceEquals(source, _lastMappedSource))
        {
            var previous = _mappings[^1];
            if (previous.OutputRange.Start + previous.OutputRange.Length == outputStart &&
                previous.SourceTextRange.Start + previous.SourceTextRange.Length == sourceStart)
            {
                _mappings[^1] = previous with
                {
                    OutputRange = new TextRange(previous.OutputRange.Start, previous.OutputRange.Length + length),
                    SourceTextRange = new TextRange(previous.SourceTextRange.Start, previous.SourceTextRange.Length + length)
                };
                return;
            }
        }

        _mappings.Add(new SourceMapping(
            new TextRange(outputStart, length), location, new TextRange(sourceStart, length)));
        _lastMappedSource = source;
    }
}
