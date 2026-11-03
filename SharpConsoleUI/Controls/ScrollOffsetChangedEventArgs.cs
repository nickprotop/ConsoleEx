namespace SharpConsoleUI.Controls;

/// <summary>Describes a change to a scrollable panel's vertical offset.</summary>
public sealed class ScrollOffsetChangedEventArgs(int previousOffset, int offset) : EventArgs
{
    /// <summary>Gets the offset before the change.</summary>
    public int PreviousOffset { get; } = previousOffset;

    /// <summary>Gets the offset after the change.</summary>
    public int Offset { get; } = offset;
}
