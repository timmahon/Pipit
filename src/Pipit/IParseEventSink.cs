namespace Pipit;


/// <summary>
/// Receives parsing events in emission order.
/// </summary>
public interface IParseEventSink
{
    /// <summary>
    /// Processes one parsing event.
    /// </summary>
    /// <param name="parseEvent">
    /// The event to process.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel processing.
    /// </param>
    /// <returns>
    /// A task representing event processing.
    /// </returns>
    ValueTask WriteAsync(
        ParseEvent parseEvent,
        CancellationToken cancellationToken = default);
}
