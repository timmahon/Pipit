using System.Diagnostics.CodeAnalysis;

namespace Pipit;

/// <summary>
/// Parses source data into metadata, content and source-information events.
/// </summary>
[Experimental("PIPIT001")]
public interface IParser
{
    /// <summary>
    /// Parse input and delivers events sequentially to the supplied sink.
    /// </summary>
    /// <param name="input">
    /// The caller-owned readable source stream, positioned where parsing
    /// should begin. The parser leaves the stream open.
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
    /// Sink failures propogate to the caller and stop parsing.
    /// Events already delivered are not rolled back if parsing fails.
    /// </remarks>
    Task ParseAsync(
        Stream input,
        IParseEventSink sink,
        CancellationToken cancellationToken = default);
}