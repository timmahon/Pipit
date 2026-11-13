namespace Pipit.Tests;

internal sealed class CallbackParseEventSink(
    Func<ParseEvent, CancellationToken, ValueTask> callback)
    : IParseEventSink
{
    public ValueTask WriteAsync(
        ParseEvent parseEvent,
        CancellationToken cancellationToken = default)
    {
        return callback(parseEvent, cancellationToken);
    }
}
