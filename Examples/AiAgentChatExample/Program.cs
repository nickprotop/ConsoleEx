// -----------------------------------------------------------------------
// AiAgentChatExample - AI Agent Chat Interface Demo
//
// Demonstrates a modern chat UI with dark grayscale theme using fluent builder pattern
//
// Author: Nikolaos Protopapas
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace AiAgentChatExample;

/// <summary>
/// Represents the severity level of a system message.
/// </summary>
public enum MessageSeverity
{
    /// <summary>Informational message</summary>
    Info,
    /// <summary>Success message</summary>
    Success,
    /// <summary>Warning message</summary>
    Warning,
    /// <summary>Error message</summary>
    Error
}

/// <summary>
/// AI Agent Chat application demonstrating modern console UI with dark theme.
/// Features async message handling, scrollable message history, and mock AI responses.
/// </summary>
internal class Program
{
    private static ConsoleWindowSystem? _windowSystem;
    private static Window? _chatWindow;
    private static ScrollablePanelControl? _messagePanel;
    private static MultilineEditControl? _inputControl;
    private static MockAiAgent? _aiAgent;
    private static List<ChatMessage> _messages = new();
    private static bool _isProcessing = false;
    private static System.Diagnostics.Stopwatch? _responseTimer = null;
    private static ButtonControl? _sendButton;
    private static ButtonControl? _clearButton;
    private static CancellationTokenSource? _thinkingCancellation = null;

    /// <summary>
    /// Application entry point
    /// </summary>
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Initialize window system with dark theme
            _windowSystem = new ConsoleWindowSystem(RenderMode.Buffer)
            {
                TopStatus = "AI Agent Chat - Dark Theme Demo",
                BottomStatus = "Ctrl+Enter: Send | Ctrl+L: Clear Input | ESC: Close"
            };

            // Setup graceful shutdown handler for Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Prevent immediate termination
                _windowSystem?.Shutdown(0);
            };

            // Initialize mock AI agent
            _aiAgent = new MockAiAgent();

            // Create the chat window
            CreateChatWindow();

            // Add welcome message
            AddSystemMessage("Welcome to AI Agent Chat! Type your message and press Ctrl+Enter to send.");

            // Run the application
            await Task.Run(() => _windowSystem.Run());

            return 0;
        }
        catch (Exception ex)
        {
            // If console system is corrupted, use Spectre.Console to output error
            Console.Clear();
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    /// <summary>
    /// Creates the main chat window with dark grayscale theme using fluent builder pattern.
    /// </summary>
    private static void CreateChatWindow()
    {
        if (_windowSystem == null)
            return;

        // Build chat window with dark theme
        _chatWindow = new WindowBuilder(_windowSystem)
            .WithTitle("AI Agent Chat")
            .WithSize(90, 32)
            .Centered()
            .WithColors(Color.Grey11, Color.Grey93)  // Dark background, light text
            .Closable(true)
            .Build();

        // Message panel - scrollable container for chat history
        _messagePanel = new ScrollablePanelControl
        {
            VerticalScrollMode = ScrollMode.Scroll,
            ShowScrollbar = true,
            EnableMouseWheel = true,
            VerticalAlignment = VerticalAlignment.Fill,
            BackgroundColor = Color.Grey11,
            ForegroundColor = Color.Grey93
        };

        // Separator between messages and input
        var separator = new RuleControl
        {
            StickyPosition = StickyPosition.Bottom
        };

        // Input control - multiline text editor with dark theme
        _inputControl = new MultilineEditControl(viewportHeight: 3)
        {
            WrapMode = WrapMode.Wrap,
            FocusedBackgroundColor = Color.Grey27,
            FocusedForegroundColor = Color.White,
            BackgroundColor = Color.Grey19,
            ForegroundColor = Color.Grey70,
            StickyPosition = StickyPosition.Bottom,
            Margin = new Margin(0, 0, 0, 1)
        };

        // Button grid - Send and Clear buttons
        var buttonGrid = new HorizontalGridControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            StickyPosition = StickyPosition.Bottom
        };

        _sendButton = new ButtonControl { Text = "Send" };
        _sendButton.Click += async (s, b) => await HandleSendMessageAsync();

        _clearButton = new ButtonControl { Text = "Clear" };
        _clearButton.Click += (s, b) => _inputControl?.SetContent("");

        var sendColumn = new ColumnContainer(buttonGrid);
        sendColumn.AddContent(_sendButton);
        buttonGrid.AddColumn(sendColumn);

        var clearColumn = new ColumnContainer(buttonGrid);
        clearColumn.AddContent(_clearButton);
        buttonGrid.AddColumn(clearColumn);

        // Add controls to window in order
        _chatWindow.AddControl(_messagePanel);
        _chatWindow.AddControl(separator);
        _chatWindow.AddControl(_inputControl);
        _chatWindow.AddControl(buttonGrid);

        // Setup key handlers
        _chatWindow.KeyPressed += async (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem?.CloseWindow(_chatWindow);
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.Enter && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                await HandleSendMessageAsync();
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.L && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                _inputControl?.SetContent("");
                e.Handled = true;
            }
        };

        _windowSystem.AddWindow(_chatWindow);
    }

    /// <summary>
    /// Handles sending a user message and getting an AI response asynchronously.
    /// </summary>
    private static async Task HandleSendMessageAsync()
    {
        if (_isProcessing || _aiAgent == null || _inputControl == null)
            return;

        var userMessage = _inputControl.Content.Trim();
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            AddSystemMessage("Please enter a message.", MessageSeverity.Warning);
            return;
        }

        _isProcessing = true;

        // Change button text to indicate processing
        if (_sendButton != null)
        {
            _sendButton.Text = "Sending...";
        }

        try
        {
            // Add user message to chat
            var userMsg = new ChatMessage(MessageRole.User, userMessage, DateTime.Now);
            _messages.Add(userMsg);
            _inputControl.SetContent("");
            RenderMessages();

            // Show "thinking" indicator with animation
            AddSystemMessage("AI is thinking.");

            // Start animation
            _thinkingCancellation = new CancellationTokenSource();
            var thinkingTask = AnimateThinkingIndicatorAsync(_thinkingCancellation.Token);

            // Start timer before AI call
            _responseTimer = System.Diagnostics.Stopwatch.StartNew();

            // Get AI response (async with simulated delay)
            var response = await _aiAgent.GetResponseAsync(userMessage);

            // Stop timer
            _responseTimer.Stop();
            double elapsedSeconds = _responseTimer.Elapsed.TotalSeconds;

            // Stop animation
            _thinkingCancellation?.Cancel();
            await thinkingTask; // Wait for animation task to complete

            // Remove thinking message
            _messages.RemoveAt(_messages.Count - 1);

            // Add AI response to chat WITH timing
            var aiMsg = new ChatMessage(MessageRole.Assistant, response, DateTime.Now, elapsedSeconds);
            _messages.Add(aiMsg);
            RenderMessages();
        }
        catch (Exception ex)
        {
            AddSystemMessage($"Error: {ex.Message}", MessageSeverity.Error);
        }
        finally
        {
            // Restore button text
            if (_sendButton != null)
            {
                _sendButton.Text = "Send";
            }
            _isProcessing = false;
        }
    }

    /// <summary>
    /// Adds a system message to the chat with the specified severity level.
    /// </summary>
    /// <param name="message">The system message text</param>
    /// <param name="severity">The severity level (Info, Success, Warning, Error)</param>
    private static void AddSystemMessage(string message, MessageSeverity severity = MessageSeverity.Info)
    {
        string color = severity switch
        {
            MessageSeverity.Error => "red",
            MessageSeverity.Warning => "yellow",
            MessageSeverity.Success => "green",
            MessageSeverity.Info => "grey50",
            _ => "grey50"
        };

        string formattedMessage = $"[{color}]{message}[/]";
        _messages.Add(new ChatMessage(MessageRole.System, formattedMessage, DateTime.Now));
        RenderMessages();
    }

    /// <summary>
    /// Animates the thinking indicator by cycling through dots.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the animation</param>
    private static async Task AnimateThinkingIndicatorAsync(CancellationToken ct)
    {
        string[] frames = { ".", "..", "..." };
        int frameIndex = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Update the last message (thinking indicator)
                if (_messages.Count > 0 && _messages[^1].Role == MessageRole.System)
                {
                    var currentFrame = frames[frameIndex];
                    _messages[^1] = new ChatMessage(
                        MessageRole.System,
                        $"[grey50]AI is thinking{currentFrame}[/]",
                        _messages[^1].Timestamp
                    );
                    RenderMessages();

                    frameIndex = (frameIndex + 1) % frames.Length;
                }

                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    /// <summary>
    /// Renders all messages in the message panel with dark theme styling.
    /// </summary>
    private static void RenderMessages()
    {
        if (_messagePanel == null)
            return;

        // Remove all existing controls
        var existingControls = _messagePanel.Children.ToList();
        foreach (var control in existingControls)
        {
            _messagePanel.RemoveControl(control);
        }

        // Add all messages
        for (int i = 0; i < _messages.Count; i++)
        {
            var msg = _messages[i];
            var (lines, bgColor, fgColor) = FormatMessage(msg);
            var markup = new MarkupControl(lines)
            {
                Wrap = true,
                Margin = new Margin(1, 0, 1, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                BackgroundColor = bgColor,
                ForegroundColor = fgColor
            };

            _messagePanel.AddControl(markup);

            // Add separator after AI responses (but not after last message)
            if (msg.Role == MessageRole.Assistant && i < _messages.Count - 1)
            {
                var separator = new MarkupControl(new List<string> { "[grey23]─────────────────────────────────────────────[/]" })
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Margin(0, 0, 0, 1)  // No top margin (AI message already has bottom margin), 1 line below
                };
                _messagePanel.AddControl(separator);
            }
        }

        _messagePanel.ScrollToBottom();
        _messagePanel.Invalidate(true);
    }

    /// <summary>
    /// Formats a chat message with appropriate colors and styling for dark theme.
    /// Uses subtle background colors to distinguish user vs AI messages.
    /// </summary>
    /// <param name="msg">The message to format</param>
    /// <returns>Tuple of formatted markup lines, background color, and foreground color</returns>
    private static (List<string> lines, Color? bgColor, Color? fgColor) FormatMessage(ChatMessage msg)
    {
        var timestamp = msg.Timestamp.ToString("HH:mm:ss");

        return msg.Role switch
        {
            MessageRole.User => (
                new List<string>
                {
                    $"[silver]User[/] [grey50]{timestamp}[/]"
                }
                .Concat(msg.Content.Split('\n'))
                .ToList(),
                Color.Grey19,  // Background
                Color.Grey93   // Foreground
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
                Color.Grey15,  // Background
                Color.Grey89   // Foreground
            ),

            MessageRole.System => (
                new List<string>
                {
                    $"[italic]{msg.Content}[/]"
                },
                null,  // No background (use container's)
                null   // No foreground (use container's)
            ),

            _ => (msg.Content.Split('\n').ToList(), null, null)
        };
    }
}
