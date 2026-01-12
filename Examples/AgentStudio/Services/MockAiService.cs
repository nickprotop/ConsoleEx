using AgentStudio.Models;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;

namespace AgentStudio.Services;

/// <summary>
/// Mock AI service for simulating AI responses with animations
/// </summary>
public class MockAiService
{
    private readonly ScrollablePanelControl _conversationPanel;
    private readonly List<Message> _messages;

    public MockAiService(ScrollablePanelControl conversationPanel, List<Message> messages)
    {
        _conversationPanel = conversationPanel;
        _messages = messages;
    }

    /// <summary>
    /// Adds messages from a scenario with simulated delays and animations
    /// </summary>
    public async Task AddScenarioAsync(List<Message> scenario, CancellationToken cancellationToken = default)
    {
        foreach (var message in scenario)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Simulate network delay before each message
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

            // For AI messages, show thinking animation first
            if (message.Role == MessageRole.Assistant && message != scenario.First())
            {
                await ShowThinkingAnimation(cancellationToken);
            }

            // Add the actual message
            _messages.Add(message);
            RenderMessages();

            // Longer delay after tool calls to simulate execution time
            if (message.ToolCall != null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Shows an animated "thinking" indicator
    /// </summary>
    private async Task ShowThinkingAnimation(CancellationToken cancellationToken)
    {
        var thinkingMessage = new Message(
            MessageRole.System,
            "[grey50 italic]. [/]",
            DateTime.Now
        );

        _messages.Add(thinkingMessage);
        var messageIndex = _messages.Count - 1;

        // Animate the dots
        string[] frames = { ".", "..", "...", "..", "." };

        for (int cycle = 0; cycle < 2; cycle++) // Two cycles
        {
            foreach (var frame in frames)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Update the thinking message
                _messages[messageIndex] = thinkingMessage with
                {
                    Content = $"[grey50 italic]{frame}[/]"
                };

                RenderMessages();
                await Task.Delay(200, cancellationToken);
            }
        }

        // Remove thinking message
        _messages.RemoveAt(messageIndex);
        RenderMessages();
    }

    /// <summary>
    /// Renders all messages to the conversation panel
    /// </summary>
    private void RenderMessages()
    {
        // Clear existing
        var existing = _conversationPanel.Children.ToList();
        foreach (var control in existing)
        {
            _conversationPanel.RemoveControl(control);
        }

        // Render all messages
        foreach (var msg in _messages)
        {
            var (lines, bgColor, fgColor) = FormatMessage(msg);
            var markup = new MarkupControl(lines)
            {
                Wrap = true,
                Margin = new Margin(1, 0, 1, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                BackgroundColor = bgColor,
                ForegroundColor = fgColor
            };

            _conversationPanel.AddControl(markup);

            // Add tool call panel if present
            if (msg.ToolCall != null)
            {
                var toolPanel = Components.ToolCallPanel.CreatePanel(msg.ToolCall);
                _conversationPanel.AddControl(toolPanel);
            }

            // Add analysis panel if present
            if (msg.Findings != null && msg.Findings.Count > 0)
            {
                var analysisPanel = Components.AnalysisPanel.CreatePanel(msg.Findings);
                _conversationPanel.AddControl(analysisPanel);
            }
        }

        _conversationPanel.ScrollToBottom();
        _conversationPanel.Invalidate(true);
    }

    /// <summary>
    /// Formats a message for display
    /// </summary>
    private (List<string> lines, Spectre.Console.Color? bgColor, Spectre.Console.Color? fgColor) FormatMessage(Message msg)
    {
        var timestamp = msg.Timestamp.ToString("HH:mm:ss");

        return msg.Role switch
        {
            MessageRole.User => (
                new List<string> { $"[silver]User[/] [grey50]{timestamp}[/]" }
                    .Concat(msg.Content.Split('\n'))
                    .ToList(),
                Spectre.Console.Color.Grey19,
                Spectre.Console.Color.Grey93
            ),

            MessageRole.Assistant => (
                new List<string>
                {
                    msg.ResponseTime.HasValue
                        ? $"[grey78]AI[/] [grey50]{timestamp} • {msg.ResponseTime.Value:F1}s[/]"
                        : $"[grey78]AI[/] [grey50]{timestamp}[/]"
                }
                .Concat(msg.Content.Split('\n'))
                .ToList(),
                Spectre.Console.Color.Grey15,
                Spectre.Console.Color.Grey89
            ),

            MessageRole.System => (
                new List<string> { msg.Content },
                null,
                null
            ),

            _ => (msg.Content.Split('\n').ToList(), null, null)
        };
    }
}
