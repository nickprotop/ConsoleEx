// -----------------------------------------------------------------------
// AgentStudioWindow - Main showcase window with full declarative UI
// -----------------------------------------------------------------------

using AgentStudio.Models;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

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
	private StatusBarItem? _sessionItem;
	private StatusBarItem? _clockItem;
	private StatusBarItem? _modeItem;
	private ChatTranscriptControl? _transcript;
	private MultilineEditControl? _inputArea;
	private SessionPanel? _sessionPanel;
	private GridControl? _rootGrid;
	private bool _sessionPanelShown = true;

	/// <summary>Width of the session panel's column when shown.</summary>
	private const int SessionPanelWidth = 30;

	// State
	private string _currentMode = "Build";
	private string _currentSession = "demo-1";
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
			.WithColors(Color.Grey93, Color.Grey11)
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

		// Top status bar (native StatusBarControl): Session on the left, live clock on the right.
		var topBar = Controls.StatusBar()
			.WithBackgroundColor(Color.Grey15)
			.WithForegroundColor(Color.Grey93)
			.Build();
		_sessionItem = topBar.AddLeftText($"[grey50]Session: [/][cyan1]{_currentSession}[/]");
		_clockItem = topBar.AddRightText("[grey70]--:--:--[/]");

		// Main content area - two columns
		_transcript = new ChatTranscriptControl
		{
			VerticalAlignment = VerticalAlignment.Fill,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Name = "Conversation"
		};

		// Keep the app's look: cyan assistant header, dim italic-style system messages.
		// Content uses the library's [color] markup tags, so keep Markdown off (markdown mode would
		// escape the tags and render them literally).
		_transcript.SetRoleStyle(ChatRole.Assistant, new ChatRoleStyle
		{
			Markdown = false,
			Header = (_, author) => $"[cyan1]{author ?? "AI"}[/]",
			Background = Color.Grey15,
			Margin = new Margin(1, 0, 1, 1)
		});
		_transcript.SetRoleStyle(ChatRole.User, new ChatRoleStyle
		{
			Markdown = false,
			Header = (_, author) => $"[silver]{author ?? "User"}[/]",
			Background = Color.Grey19,
			Margin = new Margin(1, 0, 1, 1)
		});
		_transcript.SetRoleStyle(ChatRole.System, new ChatRoleStyle
		{
			Markdown = false,
			ShowHeader = false,
			Margin = new Margin(1, 0, 1, 1)
		});
		_transcript.SetRoleStyle(ChatRole.Tool, new ChatRoleStyle
		{
			Markdown = false,
			Header = (_, author) => $"[cyan1]{author ?? "Tool"}[/]",
			Margin = new Margin(1, 0, 1, 1)
		});

		_mockAiService = new Services.MockAiService(_windowSystem, _transcript);

		// Right-hand session panel: the "where you stand" column beside the transcript.
		// Its sections, context gauge and Braille sparklines are the same primitives
		// cxagent's panel is built from. F5 toggles it.
		_sessionPanel = new SessionPanel();
		var sidebar = _sessionPanel.Control;

		// Input area
		_inputArea = Controls.MultilineEdit()
			.WithViewportHeight(3)
			.WithWrapMode(WrapMode.Wrap)
			.WithFocusedColors(Color.White, Color.Grey27)
			.WithColors(Color.Grey70, Color.Grey19)
			.WithMargin(1, 0, 1, 0)
			.Build();

		// Bottom hint bar (native StatusBarControl): clickable shortcuts wired to the same actions
		// as the key bindings, plus static Mode/Model context on the left.
		var hintBar = Controls.StatusBar()
			.WithBackgroundColor(Color.Grey15)
			.WithForegroundColor(Color.Grey70)
			.Build();
		_modeItem = hintBar.AddLeftText($"[cyan1]{_currentMode}[/]");
		hintBar.AddLeftSeparator();
		hintBar.AddLeftText("[grey50]Model: [/][cyan1]claude-sonnet-4-5[/]");
		hintBar.AddCenter("Ctrl+Space", "Mode", ToggleMode);
		hintBar.AddCenter("Ctrl+J", "Sessions", OpenSessions);
		hintBar.AddCenter("Ctrl+P", "Commands", OpenCommands);
		hintBar.AddCenter("Ctrl+S", "Send", HandleSendMessage);
		hintBar.AddCenter("F5", "Panel", ToggleSessionPanel);

		// The whole window is ONE flat 2D GridControl (no nesting):
		//   columns: [0] conversation (Star) | [1] sidebar (30 cells)
		//   rows:    [0] top status bar (Auto) — spans both columns
		//            [1] main content (Star)   — conversation (col 0) + sidebar (col 1)
		//            [2] input area (Auto)      — spans both columns
		//            [3] bottom hint bar (Auto) — spans both columns
		// ColumnSplitterAfter(0) makes the conversation|sidebar split user-resizable.
		// The grid's row layout + the two StatusBarControls carry the section framing
		// (top bar / content / input / bottom bar) — no separator controls needed.
		var root = Controls.Grid()
			.Columns(GridLength.Star(1), GridLength.Cells(SessionPanelWidth))
			.Rows(GridLength.Auto(), GridLength.Star(1), GridLength.Auto(), GridLength.Auto())
			.RowGap(1)
			.ColumnGap(1)
			.ColumnSplitterAfter(0)
			.Place(topBar, 0, 0, colSpan: 2)
			.Place(_transcript!, 1, 0)
			.Place(sidebar, 1, 1)
			.Place(_inputArea!, 2, 0, colSpan: 2)
			.Place(hintBar, 3, 0, colSpan: 2)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithAlignment(HorizontalAlignment.Stretch)
			.Build();

		// Fill the whole Session Info cell with the sidebar background so the panel's margin and its
		// children's margins sit on a matching colour instead of exposing the darker window background.
		_rootGrid = root;
		var sidebarCell = root[1, 1];
		sidebarCell.Background = new Color(24, 26, 31);

		_window.AddControl(root);
	}

	/// <summary>
	/// Shows or hides the right-hand session panel (F5).
	///
	/// <para>Hiding the control is not enough on its own: the grid's column track keeps
	/// its 30 cells and the transcript never gets them back. The COLUMN is what
	/// collapses, and the control's Visible follows so a hidden panel takes no focus.</para>
	/// </summary>
	private void ToggleSessionPanel()
	{
		if (_sessionPanel == null || _rootGrid == null) return;

		_sessionPanelShown = !_sessionPanelShown;
		_sessionPanel.Visible = _sessionPanelShown;
		_rootGrid.ColumnDefinitions[1] = _sessionPanelShown
			? GridLength.Cells(SessionPanelWidth)
			: GridLength.Cells(0);
	}

	/// <summary>Toggles between Build and Plan modes and refreshes the hint bar's mode indicator.</summary>
	private void ToggleMode()
	{
		_currentMode = _currentMode == "Build" ? "Plan" : "Build";
		if (_modeItem != null)
			_modeItem.Label = $"[cyan1]{_currentMode}[/]";
	}

	/// <summary>Opens the session manager modal and syncs the top status bar's session label.</summary>
	private void OpenSessions()
	{
		Modals.SessionManagerModal.Show(_windowSystem, _currentSession, selected =>
		{
			if (selected != null)
			{
				_currentSession = selected;
				if (_sessionItem != null)
					_sessionItem.Label = $"[grey50]Session: [/][cyan1]{_currentSession}[/]";
			}
		});
	}

	/// <summary>Opens the command palette modal and drops the chosen command into the input area.</summary>
	private void OpenCommands()
	{
		Modals.CommandPaletteModal.Show(_windowSystem, command =>
		{
			if (command != null)
			{
				_inputArea?.SetContent(command);
			}
		});
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
				// Update clock (setting the item's Label invalidates the status bar)
				if (_clockItem != null)
				{
					var timeStr = DateTime.Now.ToString("HH:mm:ss");
					_clockItem.Label = $"[grey70]{timeStr}[/]";
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
			// Don't process keys already handled by controls or window
			if (e.AlreadyHandled)
			{
				e.Handled = true; // Acknowledge
				return;
			}

			if (e.KeyInfo.Key == ConsoleKey.F5)
			{
				// F5 shows and hides the session panel, as cxagent does.
				ToggleSessionPanel();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Spacebar && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
			{
				// Ctrl+Space to switch mode
				ToggleMode();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.S && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
			{
				// Ctrl+S to send message
				HandleSendMessage();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.J && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
			{
				// Ctrl+J to open session manager
				OpenSessions();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.P && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
			{
				// Ctrl+P to open command palette
				OpenCommands();
				e.Handled = true;
			}
			else if (e.KeyInfo.Key == ConsoleKey.Enter && e.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt))
			{
				// Alt+Enter test - could be used as alternative send key if it works
				e.Handled = true;
			}
		};
	}

	private void AddWelcomeMessages()
	{
		PostMessage(
			MessageRole.System,
			"[grey50 italic]Welcome to AgentStudio! This is a showcase of SharpConsoleUI's TUI capabilities.[/]"
		);

		PostMessage(
			MessageRole.System,
			"[grey50 italic]Welcome! Try these demo commands: [/][cyan1]/analyze[/][grey50], [/][cyan1]/diff[/][grey50], or [/][cyan1]/test[/]"
		);
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
		PostMessage(MessageRole.User, content);

		// The session panel's figures move with the conversation. Simulated, like the rest.
		_sessionPanel?.TurnCompleted(Random.Shared.Next(0, 4));

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

	/// <summary>
	/// Posts a message to the native chat transcript, mapping the app's message role to the
	/// transcript's <see cref="ChatRole"/>.
	/// </summary>
	private void PostMessage(MessageRole role, string content)
	{
		if (_transcript == null) return;

		var chatRole = role switch
		{
			MessageRole.User => ChatRole.User,
			MessageRole.Assistant => ChatRole.Assistant,
			_ => ChatRole.System,
		};

		_transcript.AddMessage(chatRole, content);
	}

	public void Dispose()
	{
		_disposed = true;
	}
}
