using System.Text;
using Pipit.Rendering.Tree;

namespace Pipit.Rendering;

/// <summary>
/// Renders parsing events as Markdown.
/// </summary>
/// <remarks>
/// Equivalent adjacent emphasis is merged. Formatting that cannot be represented
/// safely is omitted without removing its text. Leading and trailing explicit
/// line breaks are omitted, and consecutive breaks are collapsed.
/// Ordinary spaces, tabs and newlines collapse within blocks and are trimmed at
/// block edges. Non-breaking spaces and explicit line-break events are retained.
/// Inline formatting that spans a block boundary is closed and reopened in each block.
/// </remarks>
public sealed class MarkdownSink : IParseEventSink
{
    private readonly StringBuilder _completed = new();
    private readonly List<SourceMapping> _completedMappings = [];
    private readonly List<MarkdownInline> _current = [];
    private readonly Stack<MarkdownEmphasis> _inlineElements = new();
    private int? _headingLevel;
    private bool _hasLineText;

    /// <inheritdoc />
    public ValueTask WriteAsync(
        ParseEvent parseEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseEvent);
        cancellationToken.ThrowIfCancellationRequested();

        switch (parseEvent)
        {
            case StartElement { Kind: ContentKind.Paragraph or ContentKind.Block }:
            case EndElement { Kind: ContentKind.Paragraph or ContentKind.Block }:
                CompleteBlock();
                break;

            case StartElement { Kind: ContentKind.Heading } heading:
                if (heading.HeadingLevel is not int level || level is < 1 or > 6)
                {
                    throw new NotSupportedException(
                        "Markdown headings require a level from one through six.");
                }

                CompleteBlock();
                _headingLevel = level;
                break;

            case EndElement { Kind: ContentKind.Heading }:
                CompleteBlock();
                _headingLevel = null;
                break;

            case TextContent text when text.Text.Length > 0:
                CurrentChildren.Add(new MarkdownText(text, 0, text.Text.Length));
                _hasLineText |= text.Text.Any(character => !MarkdownInlineWriter.IsFlowWhitespace(character));
                break;

            case TextContent:
                break;

            case StartElement
            {
                Kind: ContentKind.Emphasis or ContentKind.StrongEmphasis
            } opening:
                var emphasis = new MarkdownEmphasis(opening.Kind, []);
                CurrentChildren.Add(emphasis);
                _inlineElements.Push(emphasis);
                break;

            case EndElement
            {
                Kind: ContentKind.Emphasis or ContentKind.StrongEmphasis
            } closing:
                if (!_inlineElements.TryPeek(out var current) || current.Kind != closing.Kind)
                {
                    throw new InvalidDataException(
                        "Inline content elements must close in nesting order.");
                }

                _inlineElements.Pop().IsClosed = true;
                break;

            case LineBreak:
                if (_hasLineText) CurrentChildren.Add(new MarkdownBreak());
                break;

            case ContentEvent:
                throw new NotSupportedException(
                    $"Unsupported content event: {parseEvent.GetType().Name}.");
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the Markdown produced by the events received so far.
    /// </summary>
    /// <returns>The rendered Markdown.</returns>
    public string GetContent() => GetResult().Content;

    /// <summary>
    /// Gets a snapshot of the rendered Markdown and its source mappings.
    /// </summary>
    /// <returns>The rendered content and source evidence received so far.</returns>
    public RenderedContent GetResult()
    {
        var content = new StringBuilder().Append(_completed);
        var mappings = new List<SourceMapping>(_completedMappings);
        AppendBlock(content, mappings, MarkdownInlineWriter.Write(_current, _headingLevel));
        return new RenderedContent(content.ToString(), mappings.AsReadOnly());
    }

    private List<MarkdownInline> CurrentChildren =>
        _inlineElements.TryPeek(out var current) ? current.Children : _current;

    private void CompleteBlock()
    {
        var activeKinds = _inlineElements.Reverse().Select(element => element.Kind).ToArray();
        foreach (var element in _inlineElements) element.IsClosed = true;

        AppendBlock(_completed, _completedMappings, MarkdownInlineWriter.Write(_current, _headingLevel));
        _current.Clear();
        _inlineElements.Clear();
        _hasLineText = false;

        foreach (var kind in activeKinds)
        {
            var emphasis = new MarkdownEmphasis(kind, []);
            CurrentChildren.Add(emphasis);
            _inlineElements.Push(emphasis);
        }
    }

    private static void AppendBlock(
        StringBuilder content,
        List<SourceMapping> mappings,
        RenderedContent block)
    {
        if (block.Content.Length == 0) return;
        if (content.Length > 0) content.Append("\n\n");
        var offset = content.Length;
        content.Append(block.Content);

        foreach (var mapping in block.SourceMappings)
        {
            mappings.Add(mapping with
            {
                OutputRange = new TextRange(checked(offset + mapping.OutputRange.Start), mapping.OutputRange.Length)
            });
        }
    }
}
