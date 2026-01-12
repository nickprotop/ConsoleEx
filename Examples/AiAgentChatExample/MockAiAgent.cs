// -----------------------------------------------------------------------
// AiAgentChatExample - AI Agent Chat Interface Demo
//
// Author: Nikolaos Protopapas
// License: MIT
// -----------------------------------------------------------------------

namespace AiAgentChatExample;

/// <summary>
/// Mock AI agent that provides contextual responses based on keyword matching.
/// Simulates thinking delays and provides varied responses from predefined pools.
/// </summary>
public class MockAiAgent
{
    private readonly Dictionary<string[], string[]> _keywordResponses;
    private readonly string[] _defaultResponses;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the MockAiAgent with response pools.
    /// </summary>
    public MockAiAgent()
    {
        // Initialize keyword-based response pools
        _keywordResponses = new Dictionary<string[], string[]>
        {
            // Greetings
            [new[] { "hello", "hi", "hey", "greetings" }] = new[]
            {
                "Hello! How can I assist you today?",
                "Hi there! What can I help you with?",
                "Greetings! I'm here to help.",
                "Hey! Nice to chat with you. What's on your mind?"
            },

            // Help requests
            [new[] { "help", "?", "commands", "what can you do" }] = new[]
            {
                "I can chat about various topics. Try asking me about programming, general questions, or just have a conversation!",
                "Just type naturally! I understand greetings, questions about tech, or general conversation.",
                "Available topics: Programming, debugging, general chat, and more. What interests you?",
                "I'm here to help! Ask me anything about coding, or just chat casually."
            },

            // Technical topics
            [new[] { "code", "programming", "debug", "error", "bug", "software", "develop" }] = new[]
            {
                "Programming is fascinating! What language interests you?",
                "Debugging can be challenging. Have you tried adding logging statements?",
                "That's a great technical question! Let me share what I know...",
                "Code reviews are essential for quality. Are you working on something specific?",
                "Software development is both art and science. What are you building?"
            },

            // Questions
            [new[] { "what", "how", "why", "when", "where", "who" }] = new[]
            {
                "That's a great question! Let me think about that...",
                "Interesting! Here's what I think about that...",
                "Good question! The answer depends on several factors...",
                "I'm glad you asked! Here's my perspective..."
            },

            // Farewells
            [new[] { "bye", "goodbye", "see you", "exit", "quit" }] = new[]
            {
                "Goodbye! It was nice chatting with you!",
                "See you later! Feel free to come back anytime.",
                "Take care! Happy coding!",
                "Farewell! Have a great day!"
            },

            // Gratitude
            [new[] { "thank", "thanks", "appreciate" }] = new[]
            {
                "You're welcome! Happy to help!",
                "My pleasure! Let me know if you need anything else.",
                "Glad I could assist you!",
                "Anytime! That's what I'm here for."
            }
        };

        // Default responses for unmatched input
        _defaultResponses = new[]
        {
            "I understand. Tell me more about that.",
            "That's interesting! What else is on your mind?",
            "I see. How can I help you further?",
            "Fascinating! Would you like to explore that topic more?",
            "Hmm, interesting point. Can you elaborate?",
            "I'm processing that. What would you like to know?",
            "Great topic! I'd love to hear more about your thoughts on this.",
            "That's worth discussing. What aspect interests you most?"
        };
    }

    /// <summary>
    /// Gets an AI response asynchronously based on the user's message.
    /// Simulates thinking time and matches keywords to response pools.
    /// </summary>
    /// <param name="userMessage">The user's input message</param>
    /// <param name="ct">Cancellation token for async operations</param>
    /// <returns>An AI-generated response string</returns>
    public async Task<string> GetResponseAsync(string userMessage, CancellationToken ct = default)
    {
        // Simulate thinking delay (2500-4500ms to make animation visible)
        await Task.Delay(_random.Next(2500, 4500), ct);

        // Normalize the message for keyword matching
        var normalized = userMessage.ToLowerInvariant().Trim();

        // Check each keyword group for matches
        foreach (var (keywords, responses) in _keywordResponses)
        {
            if (keywords.Any(keyword => normalized.Contains(keyword)))
            {
                // Return a random response from the matched pool
                return responses[_random.Next(responses.Length)];
            }
        }

        // Return a random default response if no keywords matched
        return _defaultResponses[_random.Next(_defaultResponses.Length)];
    }
}
