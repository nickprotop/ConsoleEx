// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Panel;

/// <summary>
/// A panel element that displays performance metrics (FPS and dirty characters).
/// Uses a timer to invalidate periodically rather than every frame.
/// </summary>
public class PerformanceElement : PanelElement, IDisposable
{
	private const int EstimatedWidth = 15;
	private const int DefaultUpdateIntervalMs = 250;
	private IDisposable? _schedule;
	private bool _disposed;
	private int _updateIntervalMs = DefaultUpdateIntervalMs;

	/// <summary>
	/// Initializes a new PerformanceElement.
	/// </summary>
	/// <param name="name">Optional element name. Defaults to "performance".</param>
	public PerformanceElement(string? name = null)
		: base(name ?? "performance")
	{
	}

	/// <summary>
	/// Gets or sets whether to show FPS metric.
	/// </summary>
	public bool ShowFPS { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to show dirty character count.
	/// </summary>
	public bool ShowDirtyChars { get; set; } = true;

	/// <summary>
	/// Gets or sets the update interval in milliseconds.
	/// </summary>
	public int UpdateIntervalMs
	{
		get => _updateIntervalMs;
		set
		{
			if (_updateIntervalMs != value)
			{
				_updateIntervalMs = value;
				RestartTimer();
			}
		}
	}

	/// <inheritdoc/>
	public override int? FixedWidth => EstimatedWidth;

	/// <inheritdoc/>
	public override void Render(CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg)
	{
		if (WindowSystem?.Performance == null)
			return;

		var perf = WindowSystem.Performance;
		string text;

		if (perf.IsPerformanceMetricsEnabled)
		{
			var parts = new List<string>();
			if (ShowFPS)
				parts.Add($"FPS:{perf.CurrentFPS:F0}");
			if (ShowDirtyChars)
				parts.Add($"D:{perf.CurrentDirtyChars}");
			text = $"[dim]{string.Join(" ", parts)}[/]";
		}
		else
		{
			text = "[dim]Metrics:OFF[/]";
		}

		var cells = MarkupParser.Parse(text, fg, bg);
		var clipRect = new LayoutRect(x, y, width, 1);
		buffer.WriteCellsClipped(x, y, cells, clipRect);
	}

	/// <summary>
	/// Disposes the timer resource.
	/// </summary>
	public void Dispose()
	{
		if (!_disposed)
		{
			_schedule?.Dispose();
			_schedule = null;
			_disposed = true;
		}
	}

	/// <inheritdoc/>
	protected override void OnOwnerChanged()
	{
		RestartTimer();
	}

	private void StartTimer()
	{
		// Serviced by the frame, on the UI thread, rather than by a timer of this
		// element's own on a pool thread. The old one kept ticking while the
		// application was idle and read the machine clock rather than the system's.
		_schedule = WindowSystem?.ScheduleRepaint(
			TimeSpan.FromMilliseconds(_updateIntervalMs), Invalidate);
	}

	private void RestartTimer()
	{
		_schedule?.Dispose();
		_schedule = null;
		if (!_disposed)
		{
			StartTimer();
		}
	}
}
