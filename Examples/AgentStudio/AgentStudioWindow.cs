// -----------------------------------------------------------------------
// AgentStudioWindow - Main showcase window with full declarative UI
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using AgentStudio.Models;
using AgentStudio.Data;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace AgentStudio;

/// <summary>
/// Main AgentStudio window showcasing SharpConsoleUI capabilities with fully declarative UI
/// </summary>
public class AgentStudioWindow : IDisposable
{
    private readonly ConsoleWindowSystem _windowSystem;
    private Window? _window;
    private volatile bool _disposed = false;

    // Named controls for runtime access
    private MarkupControl? _topStatusLeft;
    private MarkupControl? _topStatusRight;
    private MarkupControl? _bottomModeInfo;
    private ScrollablePanelControl? _conversationPanel;
    private MultilineEditControl? _inputArea;

    // State
    private string _currentMode = "Build";
    private string _currentSession = "demo-1";
    private readonly List<Message> _messages = new();
    private Services.MockAiService? _mockAiService;

    public AgentStudioWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        BuildUI();
        SetupEventHandlers();
        AddWelcomeMessages();
    }

    public void Show()
    {
        if (_window != null)
        {
            _windowSystem.AddWindow(_window);
        }
    }

    /// <summary>
    /// Build the entire UI declaratively using fluent builders
    /// </summary>
    private void BuildUI()
    {
        _window = new WindowBuilder(_windowSystem)
            .WithTitle("AgentStudio")
            .WithColors(Color.Grey11, Color.Grey93)
            .AtPosition(0, 0)
            .WithSize(80, 24)
            .WithAsyncWindowThread(WindowThreadMethodAsync)
            .Borderless()
            .Resizable(false)
            .Movable(false)
            .Closable(false)
            .Minimizable(false)
            .Maximizable(false)
            .Maximized()
            .Build();

        // Top status bar
        _topStatusLeft = Controls.Markup($"[grey50]Session: [/][cyan1]{_currentSession}[/]")
            .WithAlignment(HorizontalAlignment.Left)
            .WithMargin(1, 0, 0, 0)
            .Build();

        _topStatusRight = Controls.Markup("[grey70]--:--:--[/]")
            .WithAlignment(HorizontalAlignment.Right)
            .WithMargin(0, 0, 1, 0)
            .Build();

        var topStatusBar = Controls.HorizontalGrid()
            .StickyTop()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Add(_topStatusLeft))
            .Column(col => col.Add(_topStatusRight))
            .Build();
        topStatusBar.BackgroundColor = Color.Grey15;
        topStatusBar.ForegroundColor = Color.Grey93;

        _window.AddControl(topStatusBar);

        _window.AddControl(Controls.RuleBuilder()
            .StickyTop()
            .WithColor(Color.Grey23)
            .Build());

        // Main content area - two columns
        _conversationPanel = Controls.ScrollablePanel()
            .WithVerticalScroll(ScrollMode.Scroll)
            .WithScrollbar(true)
            .WithMouseWheel(true)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithColors(Color.Grey93, Color.Grey11)
            .Build();

        _mockAiService = new Services.MockAiService(_conversationPanel, _messages);

        var mainGrid = Controls.HorizontalGrid()
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col
                .Add(Controls.Markup("[cyan1 bold]Conversation & Tool Outputs[/]")
                    .WithMargin(1, 0, 0, 0)
                    .WithBackgroundColor(Color.Grey11)
                    .Build())
                .Add(_conversationPanel))
            .Column(col => col.Width(1)) // Spacing
            .Column(col => col
                .Width(30)
                .Add(Controls.Markup()
                    .AddEmptyLine()
                    .AddLine("[cyan1 bold]Session Info[/]")
                    .WithMargin(2, 0, 0, 0)
                    .Build())
                .Add(Controls.Markup()
                    .AddEmptyLine()
                    .AddLine("[grey70 bold]Model[/]")
                    .AddLine("[cyan1]claude-sonnet-4-5[/]")
                    .AddEmptyLine()
                    .AddLine("[grey70 bold]Messages[/]")
                    .AddLine("[grey50]0 total[/]")
                    .AddLine("[grey35]━━━━━━━━━━━━━━━━━━━━━━[/]")
                    .AddEmptyLine()
                    .AddEmptyLine()
                    .AddLine("[grey70 bold]Token Usage[/]")
                    .AddLine("[cyan1]█████[/][grey35]░░░░░░░░░░░░░░░[/] 25%")
                    .AddLine("[grey50]2.5K / 10K tokens[/]")
                    .AddEmptyLine()
                    .AddEmptyLine()
                    .AddLine("[grey70 bold]Response Time[/]")
                    .AddLine("[green]█████████[/][grey35]░░░░░░░░░░░[/] 45%")
                    .AddLine("[grey50]avg 0.8s[/]")
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .WithForegroundColor(Color.Grey93)
                    .WithMargin(2, 1, 1, 1)
                    .Build()))
            .Build();

        // Set the right column background
        if (mainGrid.Columns.Count > 2)
        {
            mainGrid.Columns[2].BackgroundColor = Color.Grey19;
        }

        _window.AddControl(mainGrid);

        // Input separator
        _window.AddControl(Controls.RuleBuilder()
            .StickyBottom()
            .WithColor(Color.Grey23)
            .Build());

        // Input area
        _inputArea = Controls.MultilineEdit()
            .WithViewportHeight(3)
            .WithWrapMode(WrapMode.Wrap)
            .WithFocusedColors(Color.White, Color.Grey27)
            .WithColors(Color.Grey70, Color.Grey19)
            .WithStickyPosition(StickyPosition.Bottom)
            .WithMargin(1, 0, 1, 0)
            .Build();

        _window.AddControl(_inputArea);

        // Separator between input and hint
        _window.AddControl(Controls.RuleBuilder()
            .StickyBottom()
            .WithColor(Color.Grey23)
            .Build());

        // Bottom hint bar
        _bottomModeInfo = Controls.Markup($"[cyan1]{_currentMode}[/] [grey50]| Model: [/][cyan1]claude-sonnet-4-5[/] [grey50]| [/][grey70]Ctrl+Space:Mode  Ctrl+J:Sessions  Ctrl+P:Commands  Ctrl+S:Send[/]")
            .WithAlignment(HorizontalAlignment.Left)
            .WithMargin(1, 0, 0, 0)
            .Build();

        var hintGrid = Controls.HorizontalGrid()
            .StickyBottom()
            .WithAlignment(HorizontalAlignment.Stretch)
            .Column(col => col.Add(_bottomModeInfo))
            .Column(col => col
                .Width(12)
                .Add(Controls.Button("Send")
                    .WithAlignment(HorizontalAlignment.Right)
                    .WithMargin(0, 0, 1, 0)
                    .OnClick((s, e) => HandleSendMessage())
                    .Build()))
            .Build();
        hintGrid.BackgroundColor = Color.Grey15;
        hintGrid.ForegroundColor = Color.Grey70;

        _window.AddControl(hintGrid);
    }

    /// <summary>
    /// Window thread for live clock updates
    /// </summary>
    private async Task WindowThreadMethodAsync(Window window, CancellationToken ct)
    {
        // Wait for window to become active (it's not active until AddWindow is called)
        while (!window.GetIsActive() && !ct.IsCancellationRequested && !_disposed)
        {
            await Task.Delay(100, ct);
        }

        // Main clock update loop
        while (!ct.IsCancellationRequested && !_disposed)
        {
            try
            {
                // Update clock
                if (_topStatusRight != null)
                {
                    var timeStr = DateTime.Now.ToString("HH:mm:ss");
                    _topStatusRight.SetContent(new List<string>
                    {
                        $"[grey70]{timeStr}[/]"
                    });
                }

                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                break;
            }
        }
    }

    private void SetupEventHandlers()
    {
        if (_window == null) return;

        _window.KeyPressed += (sender, e) =>
        {
            // DEBUG: Log all keys to /tmp/agentstudio_keys.log
            try
            {
                var logLine = $"{DateTime.Now:HH:mm:ss.fff} | Key: {e.KeyInfo.Key,-15} | Mods: {e.KeyInfo.Modifiers,-20} | Char: '{e.KeyInfo.KeyChar}' (0x{(int)e.KeyInfo.KeyChar:X2}) | AlreadyHandled: {e.AllreadyHandled}\n";
                System.IO.File.AppendAllText("/tmp/agentstudio_keys.log", logLine);
            }
            catch { }

            // Don't process keys already handled by controls or window
            if (e.AllreadyHandled)
            {
                e.Handled = true; // Acknowledge
                return;
            }

            if (e.KeyInfo.Key == ConsoleKey.Spacebar && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                // Ctrl+Space to switch mode
                System.IO.File.AppendAllText("/tmp/agentstudio_keys.log", $"{DateTime.Now:HH:mm:ss.fff} | >>> CTRL+SPACE DETECTED - Switching mode\n");
                _currentMode = _currentMode == "Build" ? "Plan" : "Build";
                _bottomModeInfo?.SetContent(new List<string>
                {
                    $"[cyan1]{_currentMode}[/] [grey50]| Model: [/][cyan1]claude-sonnet-4-5[/] [grey50]| [/][grey70]Ctrl+Space:Mode  Ctrl+J:Sessions  Ctrl+P:Commands  Ctrl+S:Send[/]"
                });
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.S && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                // Ctrl+S to send message
                System.IO.File.AppendAllText("/tmp/agentstudio_keys.log", $"{DateTime.Now:HH:mm:ss.fff} | >>> CTRL+S DETECTED - Sending message\n");
                HandleSendMessage();
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.J && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                // Ctrl+J to open session manager
                System.IO.File.AppendAllText("/tmp/agentstudio_keys.log", $"{DateTime.Now:HH:mm:ss.fff} | >>> CTRL+J DETECTED - Opening Session Manager\n");
                Modals.SessionManagerModal.Show(_windowSystem, _currentSession, selected =>
                {
                    if (selected != null)
                    {
                        _currentSession = selected;
                        _topStatusLeft?.SetContent(new List<string> { $"[grey50]Session: [/][cyan1]{_currentSession}[/]" });
                        System.IO.File.AppendAllText("/tmp/agentstudio_keys.log", $"{DateTime.Now:HH:mm:ss.fff} | >>> Session changed to: {_currentSession}\n");
                    }
                });
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.P && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                // Ctrl+P to open command palette
                System.IO.File.AppendAllText("/tmp/agentstudio_keys.log", $"{DateTime.Now:HH:mm:ss.fff} | >>> CTRL+P DETECTED - Opening Command Palette\n");
                Modals.CommandPaletteModal.Show(_windowSystem, command =>
                {
                    if (command != null)
                    {
                        // Simulate user typing the command
                        _inputArea?.SetContent(command);
                        System.IO.File.AppendAllText("/tmp/agentstudio_keys.log", $"{DateTime.Now:HH:mm:ss.fff} | >>> Command selected: {command}\n");
                    }
                });
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.Enter && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt))
            {
                // Alt+Enter test
                System.IO.File.AppendAllText("/tmp/agentstudio_keys.log", $"{DateTime.Now:HH:mm:ss.fff} | >>> ALT+ENTER DETECTED - Terminal supports Alt+Enter!\n");
                // Could be used as alternative send key if it works
                e.Handled = true;
            }
        };
    }

    private void AddWelcomeMessages()
    {
        AddMessage(new Message(
            MessageRole.System,
            "[grey50 italic]Welcome to AgentStudio! This is a showcase of SharpConsoleUI's TUI capabilities.[/]",
            DateTime.Now
        ));

        AddMessage(new Message(
            MessageRole.System,
            "[grey50 italic]Welcome! Try these demo commands: [/][cyan1]/analyze[/][grey50], [/][cyan1]/diff[/][grey50], or [/][cyan1]/test[/]",
            DateTime.Now
        ));
    }

    private void HandleSendMessage()
    {
        if (_inputArea == null || _mockAiService == null) return;

        var content = _inputArea.Content.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return;

        // Clear input
        _inputArea.SetContent("");

        // Add user message immediately
        AddMessage(new Message(MessageRole.User, content, DateTime.Now));

        // Check for demo commands and run scenarios with animation
        if (content.Contains("/analyze"))
        {
            var scenario = Data.SampleConversations.SecurityAnalysisScenario();
            _ = _mockAiService.AddScenarioAsync(scenario);
            return;
        }

        if (content.Contains("/diff"))
        {
            var scenario = Data.SampleConversations.CodeDiffScenario();
            _ = _mockAiService.AddScenarioAsync(scenario);
            return;
        }

        if (content.Contains("/test"))
        {
            var scenario = Data.SampleConversations.TestExecutionScenario();
            _ = _mockAiService.AddScenarioAsync(scenario);
            return;
        }

        // For other messages, show help response
        var helpScenario = new List<Message>
        {
            new Message(
                MessageRole.Assistant,
                "[grey70]This is a demo showcase. Try: [/][cyan1]/analyze[/][grey70], [/][cyan1]/diff[/][grey70], or [/][cyan1]/test[/]",
                DateTime.Now,
                0.3
            )
        };
        _ = _mockAiService.AddScenarioAsync(helpScenario);
    }

    private void AddMessage(Message message)
    {
        _messages.Add(message);
        RenderMessages();
    }


    private void RenderMessages()
    {
        if (_conversationPanel == null) return;

        // Clear existing
        var existing = _conversationPanel.Children.ToList();
        foreach (var control in existing)
        {
            _conversationPanel.RemoveControl(control);
        }

        // Render all messages
        for (int i = 0; i < _messages.Count; i++)
        {
            var msg = _messages[i];
            var (lines, bgColor, fgColor) = FormatMessage(msg);

            var markupBuilder = Controls.Markup()
                .WithMargin(1, 0, 1, 1)
                .WithAlignment(HorizontalAlignment.Stretch);

            foreach (var line in lines)
            {
                markupBuilder.AddLine(line);
            }

            if (bgColor.HasValue)
                markupBuilder.WithBackgroundColor(bgColor.Value);
            if (fgColor.HasValue)
                markupBuilder.WithForegroundColor(fgColor.Value);

            var markup = markupBuilder.Build();
            markup.Wrap = true;

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

            // Add separator line after AI responses
            if (msg.Role == MessageRole.Assistant)
            {
                _conversationPanel.AddControl(Controls.RuleBuilder()
                    .WithColor(Color.Grey23)
                    .WithAlignment(HorizontalAlignment.Stretch)
                    .WithMargin(1, 1, 1, 0)
                    .Build());
            }
        }

        _conversationPanel.ScrollToBottom();
        _conversationPanel.Invalidate(true);
    }

    private (List<string> lines, Color? bgColor, Color? fgColor) FormatMessage(Message msg)
    {
        var timestamp = msg.Timestamp.ToString("HH:mm:ss");

        return msg.Role switch
        {
            MessageRole.User => (
                new List<string> { $"[silver]User[/] [grey50]{timestamp}[/]" }
                    .Concat(msg.Content.Split('\n'))
                    .ToList(),
                Color.Grey19,
                Color.Grey93
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
                Color.Grey15,
                Color.Grey89
            ),

            MessageRole.System => (
                new List<string> { msg.Content },
                null,
                null
            ),

            _ => (msg.Content.Split('\n').ToList(), null, null)
        };
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
