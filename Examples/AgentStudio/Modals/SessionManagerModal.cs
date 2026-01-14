// -----------------------------------------------------------------------
// SessionManagerModal - Session selection modal dialog
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace AgentStudio.Modals;

/// <summary>
/// Modal dialog for selecting and managing sessions
/// </summary>
public static class SessionManagerModal
{
    /// <summary>
    /// Shows the session manager modal with a callback for the result
    /// </summary>
    /// <param name="windowSystem">The window system</param>
    /// <param name="currentSession">Currently active session name</param>
    /// <param name="onSessionSelected">Callback invoked with selected session (null if cancelled)</param>
    public static void Show(ConsoleWindowSystem windowSystem, string currentSession, Action<string?> onSessionSelected)
    {
        var modal = new WindowBuilder(windowSystem)
            .WithTitle("Session Manager")
            .Centered()
            .WithSize(60, 18)
            .AsModal()
            .Borderless()
            .Resizable(false)
            .Movable(false)
            .WithColors(Color.Grey15, Color.Grey93)
            .Build();

        // Header
        modal.AddControl(Controls.Markup()
            .AddLine("[cyan1 bold]Sessions[/]")
            .AddLine("[grey50]Select a session to switch, or create a new one[/]")
            .WithAlignment(HorizontalAlignment.Left)
            .WithMargin(1, 0, 1, 0)
            .Build());

        modal.AddControl(Controls.RuleBuilder()
            .WithColor(Color.Grey23)
            .Build());

        // Session list
        var sessionList = Controls.List()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .WithColors(Color.Grey15, Color.Grey93)
            .WithFocusedColors(Color.Grey15, Color.Grey93)
            .WithHighlightColors(Color.Grey35, Color.White)  // Subtle gray highlight instead of bright blue
            .SimpleMode()  // Clean, no markers
            .WithDoubleClickActivation(false)  // Click only selects, doesn't close modal
            .Build();

        // Mock sessions data
        var sessions = new List<(string name, string info)>
        {
            ("demo-1", "3 messages, 5m ago"),
            ("feature-auth", "12 messages, 1h ago"),
            ("bugfix-rendering", "8 messages, 2h ago"),
            ("refactor-layout", "15 messages, 1d ago"),
            ("new-session", "[grey50]Create new session[/]")
        };

        foreach (var (name, info) in sessions)
        {
            var label = name == "new-session"
                ? $"[cyan1]+ New Session[/]"
                : $"[white]{name}[/] [grey50]{info}[/]";

            sessionList.AddItem(new ListItem(label) { Tag = name });
        }

        // Set initial selection to current session
        var currentIdx = sessions.FindIndex(s => s.name == currentSession);
        if (currentIdx >= 0)
        {
            sessionList.SelectedIndex = currentIdx;
        }

        modal.AddControl(sessionList);

        modal.AddControl(Controls.RuleBuilder()
            .WithColor(Color.Grey23)
            .StickyBottom()
            .Build());

        // Footer with instructions
        modal.AddControl(Controls.Markup()
            .AddLine("[grey70]Enter: Select  •  Escape: Cancel[/]")
            .WithAlignment(HorizontalAlignment.Center)
            .WithMargin(0, 0, 0, 0)
            .StickyBottom()
            .Build());

        // Handle Enter and Escape keys
        modal.KeyPressed += (sender, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Enter)
            {
                // Confirm selection with Enter key
                var selectedItem = sessionList.SelectedItem;
                if (selectedItem?.Tag is string sessionName)
                {
                    onSessionSelected(sessionName);
                    modal.Close();
                }
                e.Handled = true;
            }
            else if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                // Cancel with Escape key
                onSessionSelected(null);
                modal.Close();
                e.Handled = true;
            }
        };

        // Add modal to window system - it will handle blocking automatically
        windowSystem.AddWindow(modal);
        windowSystem.SetActiveWindow(modal);

        // Focus the list after modal is active
        sessionList.SetFocus(true, FocusReason.Programmatic);
    }
}
