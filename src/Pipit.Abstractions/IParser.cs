using System.Diagnostics.CodeAnalysis;

namespace Pipit;

/// <summary>
/// Parses source data into metadata, content and source-information events.
/// </summary>
[Experimental("PIPIT001")]
public interface IParser
{
    /// <summary>
    /// Parses input and delivers events sequentially to the supplied sink.
    /// </summary>
    /// <param name="input">
    /// The caller-owned prepared document. The parser borrows its read views
    /// and must not dispose the document owner.
    /// </param>
    /// <param name="sink">
    /// The receiver of parsing events.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel parsing.
    /// </param>
    /// <returns>
    /// A task representing the parsing operation.
    /// </returns>
    /// <remarks>
    /// Each invocation opens its own view at document offset zero.
    /// Sink failures propagate to the caller and stop parsing.
    /// Events already delivered are not rolled back if parsing fails.
    /// </remarks>
    Task ParseAsync(
        DocumentInput input,
        IParseEventSink sink,
        CancellationToken cancellationToken = default);
}
