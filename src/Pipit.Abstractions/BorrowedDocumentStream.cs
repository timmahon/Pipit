namespace Pipit;

internal sealed class BorrowedDocumentStream(FileStream stream) : Stream
{
    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => stream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => stream.Length;
    public override long Position { get => stream.Position; set => stream.Position = value; }

    public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => stream.Read(buffer);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        stream.ReadAsync(buffer, offset, count, cancellationToken);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        stream.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);
    public override void Flush() => stream.Flush();
    public override void SetLength(long value) => throw new NotSupportedException("Document input is read-only.");
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Document input is read-only.");

    // Only DocumentInput owns this handle; parser disposal must not end its lifetime.
    protected override void Dispose(bool disposing) { }
    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

    internal void Release() => stream.Dispose();
    internal ValueTask ReleaseAsync() => stream.DisposeAsync();
}
