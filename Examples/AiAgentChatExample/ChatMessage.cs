// -----------------------------------------------------------------------
// AiAgentChatExample - AI Agent Chat Interface Demo
//
// Author: Nikolaos Protopapas
// License: MIT
// -----------------------------------------------------------------------

namespace AiAgentChatExample;

/// <summary>
/// Represents the role of a message in the chat conversation.
/// </summary>
public enum MessageRole
{
    /// <summary>User message</summary>
    User,

    /// <summary>AI assistant message</summary>
    Assistant,

    /// <summary>System message</summary>
    System
}

/// <summary>
/// Immutable record representing a chat message with role, content, and timestamp.
/// </summary>
/// <param name="Role">The role of the message sender (User, Assistant, or System)</param>
/// <param name="Content">The text content of the message</param>
/// <param name="Timestamp">The time when the message was created</param>
/// <param name="ResponseTime">Optional response time in seconds for AI messages</param>
public record ChatMessage(
    MessageRole Role,
    string Content,
    DateTime Timestamp,
    double? ResponseTime = null
);
