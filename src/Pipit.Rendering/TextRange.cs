namespace Pipit.Rendering;

/// <summary>
/// Identifies a zero-based range measured in UTF-16 code units.
/// </summary>
public readonly record struct TextRange
{
    /// <summary>
    /// Initializes a range with a non-negative start and length.
    /// </summary>
    /// <param name="start">The zero-based UTF-16 offset.</param>
    /// <param name="length">The number of UTF-16 code units.</param>
    public TextRange(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, int.MaxValue - start);

        Start = start;
        Length = length;
    }

    /// <summary>
    /// Gets the zero-based UTF-16 offset.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the number of UTF-16 code units.
    /// </summary>
    public int Length { get; }
}
