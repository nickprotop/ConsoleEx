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
/// A panel element that displays the current time, auto-updating via a timer.
/// </summary>
public class ClockElement : PanelElement, IDisposable
{
	private const int DefaultUpdateIntervalMs = 1000;
	private string _format = "HH:mm";
	private IDisposable? _schedule;
	private int _updateIntervalMs = DefaultUpdateIntervalMs;
	private bool _disposed;

	/// <summary>
	/// Initializes a new ClockElement.
	/// </summary>
	/// <param name="name">Optional element name. Defaults to "clock".</param>
	public ClockElement(string? name = null)
		: base(name ?? "clock")
	{
	}

	/// <summary>
	/// Gets or sets the time format string (e.g., "HH:mm", "HH:mm:ss").
	/// </summary>
	public string Format
	{
		get => _format;
		set
		{
			if (_format != value)
			{
				_format = value;
				Invalidate();
			}
		}
	}

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

	/// <summary>
	/// Gets or sets an optional text color override.
	/// </summary>
	public Color? TextColor { get; set; }

	/// <inheritdoc/>
	public override int? FixedWidth => MeasureWidth();

	/// <inheritdoc/>
	public override int MeasureWidth()
	{
		return DateTime.Now.ToString(_format).Length;
	}

	/// <inheritdoc/>
	public override void Render(CharacterBuffer buffer, int x, int y, int width, Color fg, Color bg)
	{
		var effectiveFg = TextColor ?? fg;
		var timeText = DateTime.Now.ToString(_format);
		var cells = MarkupParser.Parse(timeText, effectiveFg, bg);
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
