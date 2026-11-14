using System.Text;
using Pipit.Parsers.OpenXml;

namespace Pipit.Tests;

public sealed class DocumentInputTests
{
    [Fact]
    public async Task StartsAtSourcePositionAndProvidesIndependentSeekableViews()
    {
        using var directory = new TemporaryDirectory();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("prefixdocument"));
        source.Position = 6;
        await using var input = await DocumentInput.CreateAsync(source, directory.Path);

        var first = input.OpenRead();
        var second = input.OpenRead();

        Assert.True(first.CanSeek);
        Assert.False(first.CanWrite);
        Assert.Equal(0L, first.Position);
        Assert.Equal(8L, first.Length);
        Assert.Equal((int)'d', first.ReadByte());
        Assert.Equal(0L, second.Position);
        Assert.Equal((int)'d', second.ReadByte());
        Assert.Equal(7L, first.Seek(-1, SeekOrigin.End));
        Assert.Equal((int)'t', first.ReadByte());
        Assert.Equal(1L, second.Position);
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task PreparesNonSeekableSources()
    {
        using var directory = new TemporaryDirectory();
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes("document"));
        using var source = new CallbackReadStream((buffer, token) => bytes.ReadAsync(buffer, token));
        await using var input = await DocumentInput.CreateAsync(source, directory.Path);

        using var reader = new StreamReader(input.OpenRead());

        Assert.False(source.CanSeek);
        Assert.Equal("document", await reader.ReadToEndAsync());
        Assert.False(source.IsDisposed);
        Assert.Equal(0L, input.OpenRead().Position);
    }

    [Fact]
    public async Task ParserCloseAndDisposeCallsCannotCloseBorrowedViews()
    {
        using var directory = new TemporaryDirectory();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("document"));
        await using var input = await DocumentInput.CreateAsync(source, directory.Path);
        var view = input.OpenRead();

        view.Close();
        view.Dispose();
        await view.DisposeAsync();

        Assert.Equal((int)'d', view.ReadByte());
        view.Position = 0;
        using (var reader = new StreamReader(view))
        {
            Assert.Equal("document", await reader.ReadToEndAsync());
        }

        view.Position = 0;
        Assert.Equal((int)'d', view.ReadByte());
        Assert.Throws<NotSupportedException>(() => view.WriteByte(1));
        Assert.Throws<NotSupportedException>(() => view.SetLength(0));
        Assert.True(source.CanRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerDisposalClosesEveryViewAndDeletesStorage(bool asynchronously)
    {
        using var directory = new TemporaryDirectory();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("document"));
        using var input = await DocumentInput.CreateAsync(source, directory.Path);
        var first = input.OpenRead();
        var second = input.OpenRead();
        Assert.Single(Directory.GetFiles(directory.Path));

        if (asynchronously) await input.DisposeAsync();
        else input.Dispose();

        Assert.False(first.CanRead);
        Assert.False(second.CanRead);
        Assert.Throws<ObjectDisposedException>(() => first.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => input.OpenRead());
        Assert.Empty(Directory.GetFiles(directory.Path));
        Assert.True(source.CanRead);
        await input.DisposeAsync();
    }

    [Fact]
    public async Task PreparationDoesNotExposeLaterSourceChanges()
    {
        using var directory = new TemporaryDirectory();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("original"));
        await using var input = await DocumentInput.CreateAsync(source, directory.Path);

        source.Position = 0;
        source.WriteByte((byte)'X');
        using var reader = new StreamReader(input.OpenRead());

        Assert.Equal("original", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task CancellationBeforePreparationConsumesNothingAndCreatesNoStorage()
    {
        using var directory = new TemporaryDirectory();
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("document"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DocumentInput.CreateAsync(source, directory.Path, cancellation.Token));

        Assert.Equal(0L, source.Position);
        Assert.True(source.CanRead);
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task ReadFailureDeletesPartialStorageWithoutClosingTheSource()
    {
        using var directory = new TemporaryDirectory();
        var failure = new IOException("Source failed.");
        var reads = 0;
        using var source = new CallbackReadStream((buffer, _) =>
        {
            if (reads++ > 0) throw failure;
            buffer.Span[0] = 42;
            return ValueTask.FromResult(1);
        });

        var actual = await Assert.ThrowsAsync<IOException>(() =>
            DocumentInput.CreateAsync(source, directory.Path));

        Assert.Same(failure, actual);
        Assert.False(source.IsDisposed);
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task CancellationDuringPreparationDeletesPartialStorage()
    {
        using var directory = new TemporaryDirectory();
        using var cancellation = new CancellationTokenSource();
        using var source = new CallbackReadStream((buffer, token) =>
        {
            token.ThrowIfCancellationRequested();
            buffer.Span[0] = 42;
            cancellation.Cancel();
            return ValueTask.FromResult(1);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DocumentInput.CreateAsync(source, directory.Path, cancellation.Token));

        Assert.False(source.IsDisposed);
        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task EmptySourcesAreValidPreparedInputs()
    {
        using var directory = new TemporaryDirectory();
        using var source = new MemoryStream();
        await using var input = await DocumentInput.CreateAsync(source, directory.Path);

        var view = input.OpenRead();
        Assert.Equal(0L, view.Length);
        Assert.Equal(-1, view.ReadByte());
    }

    [Fact]
    public async Task RejectsUnreadableSourcesBeforeCreatingStorage()
    {
        using var directory = new TemporaryDirectory();
        using var source = new MemoryStream();
        source.Dispose();

        await Assert.ThrowsAsync<ArgumentException>(() => DocumentInput.CreateAsync(source, directory.Path));

        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    [Fact]
    public async Task DocxParsingUsesDocumentOriginAfterAnotherReaderConsumesBytes()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "basic.docx");
        var documentBytes = await File.ReadAllBytesAsync(path);
        byte[] prefixed = [1, 2, 3, 4, .. documentBytes];
        using var source = new MemoryStream(prefixed);
        source.Position = 4;
        await using var input = await DocumentInput.CreateAsync(source, directory.Path);
        var inspection = input.OpenRead();
        inspection.ReadExactly(new byte[7]);
        inspection.Close();

        var sink = new RecordingParseEventSink();
        await new DocxParser().ParseAsync(input, sink);

        Assert.Equal(7L, inspection.Position);
        Assert.Contains(new ExtractedMetadata<string>(DublinCore.Title, "Example document"), sink.Events);
        Assert.Equal("Example document", sink.Events.OfType<TextContent>().First().Text);
        Assert.True(source.CanRead);
        Assert.Equal(source.Length, source.Position);
    }
}
