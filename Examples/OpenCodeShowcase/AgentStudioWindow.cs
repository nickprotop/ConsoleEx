// -----------------------------------------------------------------------
// AgentStudioWindow - Main showcase window
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using OpenCodeShowcase.Models;
using OpenCodeShowcase.Data;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace OpenCodeShowcase;

/// <summary>
/// Main AgentStudio window showcasing SharpConsoleUI capabilities
/// </summary>
public class AgentStudioWindow : IDisposable
{
    private readonly ConsoleWindowSystem _windowSystem;
    private Window? _window;
    private volatile bool _disposed = false;

    // Layout controls
    private HorizontalGridControl? _mainGrid;
    private ScrollablePanelControl? _conversationPanel;
    private TreeControl? _projectTree;
    private MultilineEditControl? _inputArea;

    // Status bar controls
    private MarkupControl? _topStatusLeft;
    private MarkupControl? _topStatusRight;
    private MarkupControl? _bottomStatus;

    // State
    private string _currentMode = "Build";
    private string _currentSession = "demo-1";
    private readonly List<Message> _messages = new();

    public AgentStudioWindow(ConsoleWindowSystem windowSystem)
    {
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
        CreateWindow();
        SetupControls();
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

    private void CreateWindow()
    {
        // Create fullscreen window - no Width/Height for maximum screen usage
        _window = new Window(_windowSystem, WindowThreadMethod)
        {
            Title = "AgentStudio",
            BackgroundColor = Color.Grey11,
            ForegroundColor = Color.Grey93,
            Left = 0,
            Top = 0,
            Width = Console.WindowWidth,
            Height = Console.WindowHeight
        };
    }

    /// <summary>
    /// Window thread for live clock updates
    /// </summary>
    private void WindowThreadMethod(Window window)
    {
        while (window.GetIsActive() && !_disposed)
        {
            try
            {
                // Update clock
                if (_topStatusRight != null)
                {
                    var timeStr = DateTime.Now.ToString("HH:mm:ss");
                    _topStatusRight.SetContent(new List<string>
                    {
                        $"[grey50]Session: {_currentSession}[/]                   [grey70]{timeStr}[/]"
                    });
                }

                Thread.Sleep(1000);
            }
            catch
            {
                break;
            }
        }
    }

    private void SetupControls()
    {
        if (_window == null) return;

        CreateTopStatusBar();
        CreateMainLayout();
        CreateBottomStatusBar();
    }

    /// <summary>
    /// Create top status bar with mode and clock
    /// </summary>
    private void CreateTopStatusBar()
    {
        if (_window == null) return;

        var statusGrid = new HorizontalGridControl
        {
            StickyPosition = StickyPosition.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BackgroundColor = Color.Grey15,
            ForegroundColor = Color.Grey93
        };

        // Left side: Mode indicator
        _topStatusLeft = new MarkupControl(new List<string>
        {
            $"[cyan1]Mode: {_currentMode}[/]"
        })
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Margin(1, 0, 0, 0)
        };

        // Right side: Session and clock
        _topStatusRight = new MarkupControl(new List<string>
        {
            $"[grey50]Session: {_currentSession}[/]                   [grey70]--:--:--[/]"
        })
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Margin(0, 0, 1, 0)
        };

        var leftColumn = new ColumnContainer(statusGrid);
        leftColumn.AddContent(_topStatusLeft);
        statusGrid.AddColumn(leftColumn);

        var rightColumn = new ColumnContainer(statusGrid);
        rightColumn.AddContent(_topStatusRight);
        statusGrid.AddColumn(rightColumn);

        _window.AddControl(statusGrid);

        // Separator
        _window.AddControl(new RuleControl
        {
            StickyPosition = StickyPosition.Top,
            Color = Color.Grey23
        });
    }

    /// <summary>
    /// Create main 2-panel layout with splitter
    /// </summary>
    private void CreateMainLayout()
    {
        if (_window == null) return;

        _mainGrid = new HorizontalGridControl
        {
            VerticalAlignment = VerticalAlignment.Fill,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Left panel: Project Explorer (30 chars wide)
        var leftColumn = new ColumnContainer(_mainGrid)
        {
            Width = 30
        };

        var explorerHeader = new MarkupControl(new List<string> { "[cyan1 bold]Project Explorer[/]" })
        {
            Margin = new Margin(1, 1, 0, 0),
            BackgroundColor = Color.Grey15
        };
        leftColumn.AddContent(explorerHeader);

        _projectTree = new TreeControl
        {
            VerticalAlignment = VerticalAlignment.Fill,
            BackgroundColor = Color.Grey15,
            ForegroundColor = Color.Grey93,
            Margin = new Margin(1, 1, 1, 1)
        };

        // Populate with sample project
        SampleProject.PopulateProjectTree(_projectTree);

        leftColumn.AddContent(_projectTree);

        _mainGrid.AddColumn(leftColumn);

        // Splitter
        var splitterColumn = new ColumnContainer(_mainGrid)
        {
            Width = 1
        };
        splitterColumn.AddContent(new SplitterControl
        {
            VerticalAlignment = VerticalAlignment.Fill,
            ForegroundColor = Color.Grey23
        });
        _mainGrid.AddColumn(splitterColumn);

        // Right panel: Conversation
        var rightColumn = new ColumnContainer(_mainGrid);

        var conversationHeader = new MarkupControl(new List<string> { "[cyan1 bold]Conversation & Tool Outputs[/]" })
        {
            Margin = new Margin(1, 1, 0, 0),
            BackgroundColor = Color.Grey11
        };
        rightColumn.AddContent(conversationHeader);

        _conversationPanel = new ScrollablePanelControl
        {
            VerticalScrollMode = ScrollMode.Scroll,
            ShowScrollbar = true,
            EnableMouseWheel = true,
            VerticalAlignment = VerticalAlignment.Fill,
            BackgroundColor = Color.Grey11,
            ForegroundColor = Color.Grey93
        };
        rightColumn.AddContent(_conversationPanel);

        _mainGrid.AddColumn(rightColumn);

        _window.AddControl(_mainGrid);

        // Input separator
        _window.AddControl(new RuleControl
        {
            StickyPosition = StickyPosition.Bottom,
            Color = Color.Grey23
        });

        // Input area
        _inputArea = new MultilineEditControl(viewportHeight: 3)
        {
            WrapMode = WrapMode.Wrap,
            FocusedBackgroundColor = Color.Grey27,
            FocusedForegroundColor = Color.White,
            BackgroundColor = Color.Grey19,
            ForegroundColor = Color.Grey70,
            StickyPosition = StickyPosition.Bottom,
            Margin = new Margin(1, 0, 1, 0)
        };
        _window.AddControl(_inputArea);
    }

    /// <summary>
    /// Create bottom status bar
    /// </summary>
    private void CreateBottomStatusBar()
    {
        if (_window == null) return;

        _window.AddControl(new RuleControl
        {
            StickyPosition = StickyPosition.Bottom,
            Color = Color.Grey23
        });

        _bottomStatus = new MarkupControl(new List<string>
        {
            "[grey50]Tokens: 0  |  [/][grey70]Ctrl+N:New  Ctrl+S:Session  Tab:Mode[/]"
        })
        {
            StickyPosition = StickyPosition.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            BackgroundColor = Color.Grey15,
            ForegroundColor = Color.Grey70,
            Margin = new Margin(0, 0, 0, 0)
        };

        _window.AddControl(_bottomStatus);
    }

    private void SetupEventHandlers()
    {
        if (_window == null) return;

        _window.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                _windowSystem.CloseWindow(_window);
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.Tab)
            {
                // Switch mode
                _currentMode = _currentMode == "Build" ? "Plan" : "Build";
                _topStatusLeft?.SetContent(new List<string> { $"[cyan1]Mode: {_currentMode}[/]" });
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.Enter && !e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                // Regular enter in input - do nothing (allow multiline)
            }
            else if (e.KeyInfo.Key == ConsoleKey.Enter && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                // Ctrl+Enter to send
                HandleSendMessage();
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
            "[grey50 italic]Type /analyze, /diff, or /test to see mock tool interactions.[/]",
            DateTime.Now
        ));
    }

    private void HandleSendMessage()
    {
        if (_inputArea == null) return;

        var content = _inputArea.Content.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return;

        // Add user message
        AddMessage(new Message(MessageRole.User, content, DateTime.Now));
        _inputArea.SetContent("");

        // Placeholder for AI response (will be implemented in Phase 5)
        AddMessage(new Message(
            MessageRole.Assistant,
            "[grey70]Mock AI response will be implemented in Phase 5.[/]",
            DateTime.Now,
            0.5
        ));
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
