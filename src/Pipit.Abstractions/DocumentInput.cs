namespace Pipit;

/// <summary>
/// Owns prepared document bytes and supplies independent, read-only, seekable views.
/// </summary>
/// <remarks>
/// Dispose this object after its detectors and parsers have finished. Disposing a
/// borrowed view has no effect; disposing this owner closes every view and deletes
/// its temporary storage. The caller's original source is never disposed.
/// </remarks>
public sealed class DocumentInput : IDisposable, IAsyncDisposable
{
    private readonly FileStream _storage;
    private readonly List<BorrowedDocumentStream> _views = [];
    private bool _disposed;

    private DocumentInput(FileStream storage) => _storage = storage;

    /// <summary>
    /// Copies the remaining source bytes into owned seekable storage.
    /// </summary>
    /// <param name="source">
    /// The caller-owned readable stream, which need not support seeking.
    /// Its current position becomes offset zero in the prepared document.
    /// </param>
    /// <param name="temporaryDirectory">
    /// An existing directory for temporary storage, or null for the system temporary directory.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel preparation.</param>
    /// <returns>The owner of the prepared document bytes.</returns>
    public static async Task<DocumentInput> CreateAsync(
        Stream source,
        string? temporaryDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) throw new ArgumentException("The source must be readable.", nameof(source));
        cancellationToken.ThrowIfCancellationRequested();

        temporaryDirectory ??= Path.GetTempPath();
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryDirectory);
        var path = Path.Combine(Path.GetFullPath(temporaryDirectory), $"pipit-{Guid.NewGuid():N}.tmp");
        var storage = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.Read | FileShare.Delete, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

        try
        {
            await source.CopyToAsync(storage, cancellationToken).ConfigureAwait(false);
            await storage.FlushAsync(cancellationToken).ConfigureAwait(false);
            return new DocumentInput(storage);
        }
        catch
        {
            await storage.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Opens an independent read-only, seekable view positioned at document offset zero.
    /// </summary>
    /// <returns>A borrowed stream whose close and dispose calls leave it usable.</returns>
    public Stream OpenRead()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var stream = new FileStream(_storage.Name, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var view = new BorrowedDocumentStream(stream);
        _views.Add(view);
        return view;
    }

    /// <summary>
    /// Closes all borrowed views and releases the owned temporary storage.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            foreach (var view in _views) view.Release();
        }
        finally
        {
            _storage.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously closes all borrowed views and releases the owned temporary storage.
    /// </summary>
    /// <returns>A task representing cleanup.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            foreach (var view in _views) await view.ReleaseAsync().ConfigureAwait(false);
        }
        finally
        {
            await _storage.DisposeAsync().ConfigureAwait(false);
        }
        GC.SuppressFinalize(this);
    }
}
