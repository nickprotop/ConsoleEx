namespace OpenCodeShowcase.Models;

/// <summary>
/// Represents the role of a message sender
/// </summary>
public enum MessageRole
{
    User,
    Assistant,
    System
}

/// <summary>
/// Immutable chat message record
/// </summary>
/// <param name="Role">Message sender role</param>
/// <param name="Content">Message text content</param>
/// <param name="Timestamp">When the message was created</param>
/// <param name="ResponseTime">Optional response time in seconds for AI messages</param>
public record Message(
    MessageRole Role,
    string Content,
    DateTime Timestamp,
    double? ResponseTime = null
);
