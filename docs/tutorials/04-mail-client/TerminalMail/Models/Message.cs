namespace TerminalMail.Models;

/// <summary>A single mail message (plain data, no UI concerns).</summary>
public sealed class Message
{
    public required string From { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public required DateTime Date { get; init; }
    public bool IsRead { get; set; }
    public bool IsFlagged { get; set; }
}
