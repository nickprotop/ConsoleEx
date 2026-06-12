namespace TerminalMail.Models;

/// <summary>A mail folder holding messages.</summary>
public sealed class Folder
{
    public required string Name { get; init; }
    public List<Message> Messages { get; init; } = new();

    /// <summary>Count of unread messages in this folder.</summary>
    public int UnreadCount => Messages.Count(m => !m.IsRead);
}
