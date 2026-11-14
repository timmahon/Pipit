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
/// </remarks>
public sealed class MarkdownSink : IParseEventSink
{
    private readonly StringBuilder _completed = new();
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
                _hasLineText |= text.Text.Any(character => !char.IsWhiteSpace(character));
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
    public string GetContent()
    {
        var content = new StringBuilder().Append(_completed);
        AppendBlock(content, MarkdownInlineWriter.Write(_current, _headingLevel));
        return content.ToString();
    }

    private List<MarkdownInline> CurrentChildren =>
        _inlineElements.TryPeek(out var current) ? current.Children : _current;

    private void CompleteBlock()
    {
        if (_inlineElements.Count > 0)
        {
            throw new InvalidDataException(
                "Inline content elements must close before a block boundary.");
        }

        AppendBlock(_completed, MarkdownInlineWriter.Write(_current, _headingLevel));
        _current.Clear();
        _hasLineText = false;
    }

    private static void AppendBlock(StringBuilder content, string block)
    {
        if (block.Length == 0) return;
        if (content.Length > 0) content.Append("\n\n");
        content.Append(block);
    }
}
