// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using Spectre.Console;
using System.Text;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Size = System.Drawing.Size;

namespace SharpConsoleUI.Plugins.DeveloperTools;

/// <summary>
/// A control that provides log export functionality.
/// Displays an export button and status information about the last export.
/// </summary>
public class LogExporterControl : IWindowControl, IInteractiveControl, IFocusableControl, IMouseAwareControl, IDOMPaintable
{
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private bool _enabled = true;
	private bool _focused;
	private Margin _margin = new(0, 0, 0, 0);
	private StickyPosition _stickyPosition = StickyPosition.None;
	private bool _visible = true;
	private int? _width;

	private string _lastExportPath = "";
	private int _lastExportCount;
	private DateTime? _lastExportTime;

	private ILogService? _logService;

	private int _actualX;
	private int _actualY;
	private int _actualWidth;
	private int _actualHeight;

	/// <summary>
	/// Initializes a new instance of the <see cref="LogExporterControl"/> class.
	/// </summary>
	public LogExporterControl()
	{
	}

	/// <summary>
	/// Sets the log service to export logs from.
	/// </summary>
	/// <param name="logService">The log service instance.</param>
	public void SetLogService(ILogService logService)
	{
		_logService = logService;
	}

	/// <summary>
	/// Gets the actual rendered width of the control.
	/// </summary>
	public int? ContentWidth => GetControlWidth() + _margin.Left + _margin.Right;

	private int GetControlWidth()
	{
		return _width ?? 40;
	}

	/// <inheritdoc/>
	public int ActualX => _actualX;

	/// <inheritdoc/>
	public int ActualY => _actualY;

	/// <inheritdoc/>
	public int ActualWidth => _actualWidth;

	/// <inheritdoc/>
	public int ActualHeight => _actualHeight;

	/// <inheritdoc/>
	public HorizontalAlignment HorizontalAlignment
	{
		get => _horizontalAlignment;
		set { _horizontalAlignment = value; Container?.Invalidate(true); }
	}

	/// <inheritdoc/>
	public VerticalAlignment VerticalAlignment
	{
		get => _verticalAlignment;
		set { _verticalAlignment = value; Container?.Invalidate(true); }
	}

	/// <inheritdoc/>
	public IContainer? Container { get; set; }

	/// <inheritdoc/>
	public bool HasFocus
	{
		get => _focused;
		set
		{
			_focused = value;
			Container?.Invalidate(true);
		}
	}

	/// <summary>
	/// Gets or sets whether the control is enabled.
	/// </summary>
	public bool IsEnabled
	{
		get => _enabled;
		set { _enabled = value; Container?.Invalidate(true); }
	}

	/// <inheritdoc/>
	public Margin Margin
	{
		get => _margin;
		set { _margin = value; Container?.Invalidate(true); }
	}

	/// <inheritdoc/>
	public string? Name { get; set; }

	/// <inheritdoc/>
	public StickyPosition StickyPosition
	{
		get => _stickyPosition;
		set { _stickyPosition = value; Container?.Invalidate(true); }
	}

	/// <inheritdoc/>
	public object? Tag { get; set; }

	/// <inheritdoc/>
	public bool Visible
	{
		get => _visible;
		set { _visible = value; Container?.Invalidate(true); }
	}

	/// <summary>
	/// Gets or sets the fixed width of the control.
	/// </summary>
	public int? Width
	{
		get => _width;
		set { _width = value; Container?.Invalidate(true); }
	}

	/// <inheritdoc/>
	public bool CanReceiveFocus => _enabled && _visible;

	/// <inheritdoc/>
	public event EventHandler? GotFocus;

	/// <inheritdoc/>
	public event EventHandler? LostFocus;

	/// <inheritdoc/>
	public void SetFocus(bool focused, FocusReason reason)
	{
		if (_focused == focused) return;
		_focused = focused;

		if (focused)
			GotFocus?.Invoke(this, EventArgs.Empty);
		else
			LostFocus?.Invoke(this, EventArgs.Empty);

		Container?.Invalidate(true);
	}

	/// <inheritdoc/>
	public bool WantsMouseEvents => true;

	/// <inheritdoc/>
	public bool CanFocusWithMouse => true;

	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseClick;

	#pragma warning disable CS0067  // Event never raised (interface requirement)
	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseDoubleClick;

	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseEnter;

	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseLeave;

	/// <inheritdoc/>
	public event EventHandler<MouseEventArgs>? MouseMove;
	#pragma warning restore CS0067

	/// <summary>
	/// Raised when logs are successfully exported.
	/// </summary>
	public event EventHandler<LogExportEventArgs>? LogsExported;

	/// <inheritdoc/>
	public bool ProcessMouseEvent(MouseEventArgs e)
	{
		if (!_enabled) return false;

		if (e.HasFlag(MouseFlags.Button1Clicked))
		{
			ExportLogsFireAndForget();
			MouseClick?.Invoke(this, e);
			e.Handled = true;
			return true;
		}

		return false;
	}

	/// <inheritdoc/>
	public bool ProcessKey(ConsoleKeyInfo keyInfo)
	{
		if (!_enabled || !_focused) return false;

		if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.Spacebar)
		{
			ExportLogsFireAndForget();
			return true;
		}

		return false;
	}

	/// <summary>
	/// Exports logs to a file selected via save file dialog.
	/// </summary>
	public async Task ExportLogsAsync()
	{
		// Use save file dialog if window system is available
		var windowSystem = Container?.GetConsoleWindowSystem;
		if (windowSystem != null)
		{
			// Get parent window for window-scoped modal dialog (traverses up nested containers)
			var parentWindow = this.GetParentWindow();

			var defaultFileName = $"logs-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
			var filePath = await FileDialogs.ShowSaveFileAsync(windowSystem, Environment.CurrentDirectory, "*.txt", defaultFileName, parentWindow);
			if (filePath != null)
			{
				ExportLogs(filePath);
			}
			// User cancelled - do nothing
		}
		else
		{
			// Fallback to default path if no window system
			ExportLogs(GetDefaultLogPath());
		}
	}

	/// <summary>
	/// Fire-and-forget wrapper for async export with proper exception handling.
	/// </summary>
	private void ExportLogsFireAndForget()
	{
		Task.Run(async () =>
		{
			try
			{
				await ExportLogsAsync();
			}
			catch (Exception ex)
			{
				_logService?.Log(Logging.LogLevel.Error, $"Export failed: {ex.Message}", "LogExporter");
			}
		});
	}

	/// <summary>
	/// Exports logs to the specified file path.
	/// </summary>
	/// <param name="filePath">The path to export logs to.</param>
	public void ExportLogs(string filePath)
	{
		if (_logService == null)
		{
			_lastExportPath = "Error: No log service";
			_lastExportCount = 0;
			_lastExportTime = DateTime.Now;
			Container?.Invalidate(true);
			return;
		}

		try
		{
			var logs = _logService.GetAllLogs();
			var sb = new StringBuilder();

			sb.AppendLine($"=== Log Export ===");
			sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			sb.AppendLine($"Entries: {logs.Count}");
			sb.AppendLine();

			foreach (var entry in logs)
			{
				var category = string.IsNullOrEmpty(entry.Category) ? "" : $"[{entry.Category}] ";
				sb.AppendLine($"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] {category}{entry.Message}");

				if (entry.Exception != null)
				{
					sb.AppendLine($"  Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}");
					if (entry.Exception.StackTrace != null)
					{
						foreach (var line in entry.Exception.StackTrace.Split('\n'))
						{
							sb.AppendLine($"    {line.Trim()}");
						}
					}
				}
			}

			File.WriteAllText(filePath, sb.ToString());

			_lastExportPath = filePath;
			_lastExportCount = logs.Count;
			_lastExportTime = DateTime.Now;

			LogsExported?.Invoke(this, new LogExportEventArgs(filePath, logs.Count));
		}
		catch (Exception ex)
		{
			_lastExportPath = $"Error: {ex.Message}";
			_lastExportCount = 0;
			_lastExportTime = DateTime.Now;
		}

		Container?.Invalidate(true);
	}

	private static string GetDefaultLogPath()
	{
		return Path.Combine(Environment.CurrentDirectory, $"logs-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
	}

	/// <inheritdoc/>
	public LayoutSize MeasureDOM(LayoutConstraints constraints)
	{
		int width = Math.Min(GetControlWidth(), constraints.MaxWidth);
		int height = Math.Min(3, constraints.MaxHeight);
		return new LayoutSize(width, height);
	}

	/// <inheritdoc/>
	public void PaintDOM(CharacterBuffer buffer, LayoutRect contentRect, LayoutRect clipRect, Color foregroundColor, Color backgroundColor)
	{
		_actualX = contentRect.X;
		_actualY = contentRect.Y;
		_actualWidth = contentRect.Width;
		_actualHeight = contentRect.Height;

		if (!_visible) return;

		var theme = Container?.GetConsoleWindowSystem?.Theme;
		Color fg, bg;

		if (!_enabled)
		{
			fg = theme?.ButtonDisabledForegroundColor ?? Color.Grey50;
			bg = theme?.ButtonDisabledBackgroundColor ?? Color.Grey15;
		}
		else if (_focused)
		{
			fg = theme?.ButtonFocusedForegroundColor ?? Color.White;
			bg = theme?.ButtonFocusedBackgroundColor ?? Color.Grey30;
		}
		else
		{
			fg = theme?.ButtonForegroundColor ?? Color.Grey78;
			bg = theme?.ButtonBackgroundColor ?? Color.Grey19;
		}

		// Draw button background
		buffer.FillRect(new LayoutRect(contentRect.X, contentRect.Y, contentRect.Width, 1), ' ', fg, bg);

		// Draw button text
		string buttonText = _focused ? "> Export Logs <" : "[ Export Logs ]";
		int textX = contentRect.X + (contentRect.Width - buttonText.Length) / 2;
		buffer.WriteStringClipped(textX, contentRect.Y, buttonText, fg, bg, clipRect);

		// Draw status info (2nd and 3rd line)
		if (contentRect.Height > 1)
		{
			string logCountText = _logService != null ? $"Buffer: {_logService.Count} entries" : "No log service";
			buffer.WriteStringClipped(contentRect.X, contentRect.Y + 1, logCountText, foregroundColor, backgroundColor, clipRect);
		}

		if (contentRect.Height > 2 && _lastExportTime != null)
		{
			string statusText = _lastExportCount > 0
				? $"Last: {_lastExportCount} entries at {_lastExportTime:HH:mm:ss}"
				: _lastExportPath;

			if (statusText.Length > contentRect.Width)
				statusText = statusText[..(contentRect.Width - 3)] + "...";

			buffer.WriteStringClipped(contentRect.X, contentRect.Y + 2, statusText, foregroundColor, backgroundColor, clipRect);
		}
	}

	/// <inheritdoc/>
	public void Invalidate() => Container?.Invalidate(true);

	/// <inheritdoc/>
	public void Invalidate(bool contentChanged) => Container?.Invalidate(contentChanged);

	/// <inheritdoc/>
	public Size GetLogicalContentSize()
	{
		return new Size(GetControlWidth(), 3);
	}

	/// <inheritdoc/>
	public void Dispose() { }
}

/// <summary>
/// Event arguments for log export events.
/// </summary>
/// <param name="FilePath">The path where logs were exported.</param>
/// <param name="EntryCount">The number of log entries exported.</param>
public record LogExportEventArgs(string FilePath, int EntryCount);
