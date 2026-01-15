// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Plugins.DeveloperTools;

/// <summary>
/// Factory for creating debug console windows that display real-time log output.
/// </summary>
public static class DebugConsoleWindow
{
	/// <summary>
	/// Creates a debug console window with real-time log streaming.
	/// </summary>
	/// <param name="windowSystem">The window system to create the window in.</param>
	/// <returns>A configured debug console window.</returns>
	public static Window Create(ConsoleWindowSystem windowSystem)
	{
		return Create(windowSystem, "Debug Console");
	}

	/// <summary>
	/// Creates a debug console window with a custom title.
	/// </summary>
	/// <param name="windowSystem">The window system to create the window in.</param>
	/// <param name="title">The window title.</param>
	/// <returns>A configured debug console window.</returns>
	public static Window Create(ConsoleWindowSystem windowSystem, string title)
	{
		var logService = windowSystem.LogService;

		// Create the window
		var window = new WindowBuilder(windowSystem)
			.WithTitle(title)
			.WithSize(80, 20)
			.Centered()
			.Build();

		// Create log viewer list
		var logList = Builders.Controls.List()
			.WithName("LogList")
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Build();

		// Create level filter dropdown with items
		var levelFilter = Builders.Controls.Dropdown()
			.WithName("LevelFilter")
			.AddItem(new DropdownItem("All") { Tag = LogLevel.Trace })
			.AddItem(new DropdownItem("Debug+") { Tag = LogLevel.Debug })
			.AddItem(new DropdownItem("Info+") { Tag = LogLevel.Information })
			.AddItem(new DropdownItem("Warning+") { Tag = LogLevel.Warning })
			.AddItem(new DropdownItem("Error+") { Tag = LogLevel.Error })
			.AddItem(new DropdownItem("Critical") { Tag = LogLevel.Critical })
			.SelectedIndex(0)
			.Build();

		// Create clear button
		var clearButton = Builders.Controls.Button("Clear")
			.OnClick((sender, btn) =>
			{
				logList.ClearItems();
			})
			.Build();

		// Create toolbar with filter and button
		var toolbar = Builders.Controls.Toolbar()
			.Add(Builders.Controls.Label("[grey50]Level:[/]"))
			.Add(levelFilter)
			.AddSeparator(1)
			.AddButton(clearButton)
			.StickyTop()
			.Build();

		// Add controls to window
		window.AddControl(toolbar);
		window.AddControl(logList);

		// Get current log level filter
		LogLevel GetFilterLevel()
		{
			var selected = levelFilter.SelectedItem;
			if (selected?.Tag is LogLevel level)
				return level;
			return LogLevel.Trace;
		}

		// Format log entry for display
		string FormatLogEntry(LogEntry entry)
		{
			var levelColor = entry.Level switch
			{
				LogLevel.Trace => "grey50",
				LogLevel.Debug => "grey70",
				LogLevel.Information => "cyan1",
				LogLevel.Warning => "yellow",
				LogLevel.Error => "red",
				LogLevel.Critical => "red bold",
				_ => "white"
			};

			var levelStr = entry.Level.ToString().ToUpper()[..3];
			var category = string.IsNullOrEmpty(entry.Category) ? "" : $"[grey50][[{entry.Category}]][/] ";
			var time = entry.Timestamp.ToString("HH:mm:ss.fff");

			return $"[grey42]{time}[/] [{levelColor}]{levelStr}[/] {category}{EscapeMarkup(entry.Message)}";
		}

		// Escape markup characters
		string EscapeMarkup(string text)
		{
			return text.Replace("[", "[[").Replace("]", "]]");
		}

		// Load initial logs
		void RefreshLogs()
		{
			logList.ClearItems();
			if (logService == null) return;

			var filterLevel = GetFilterLevel();
			var logs = logService.GetAllLogs()
				.Where(e => e.Level >= filterLevel)
				.ToList();

			foreach (var entry in logs)
			{
				logList.AddItem(new ListItem(FormatLogEntry(entry)) { Tag = entry });
			}

			// Scroll to end
			if (logList.Items.Count > 0)
				logList.SelectedIndex = logList.Items.Count - 1;
		}

		// Subscribe to new log entries
		if (logService != null)
		{
			EventHandler<LogEntry>? logHandler = null;
			logHandler = (sender, entry) =>
			{
				var filterLevel = GetFilterLevel();
				if (entry.Level >= filterLevel)
				{
					logList.AddItem(new ListItem(FormatLogEntry(entry)) { Tag = entry });
					// Auto-scroll to new entry if at bottom
					if (logList.SelectedIndex >= logList.Items.Count - 2)
						logList.SelectedIndex = logList.Items.Count - 1;
				}
			};

			logService.LogAdded += logHandler;

			// Unsubscribe when window closes
			window.OnClosed += (s, e) =>
			{
				logService.LogAdded -= logHandler;
			};
		}

		// Refresh when filter changes
		levelFilter.SelectedIndexChanged += (s, e) => RefreshLogs();

		// Load initial logs
		RefreshLogs();

		return window;
	}
}
