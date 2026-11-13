namespace Pipit;

internal sealed class RecordingParseEventSink : IParseEventSink
{
    public List<ParseEvent> Events { get; } = [];
    
    public ValueTask WriteAsync(ParseEvent parseEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Events.Add(parseEvent);

        return ValueTask.CompletedTask;
    }
}
