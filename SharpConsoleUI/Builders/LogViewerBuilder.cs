// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for <see cref="LogViewerControl"/>.
/// </summary>
/// <remarks>
/// The log service is required, so this builder is reached through
/// <see cref="Controls.LogViewer(ILogService)"/> rather than a no-argument factory:
/// <c>Controls.LogViewer(windowSystem.LogService).WithTitle("Logs").Build()</c>.
/// </remarks>
public sealed class LogViewerBuilder : IControlBuilder<LogViewerControl>
{
	private readonly ILogService _logService;

	private bool _autoScroll = true;
	private bool _showToolbar = true;
	private bool _showRowSeparators;
	private bool _columnResizeEnabled = true;
	private bool _sortingEnabled = true;
	private bool _filteringEnabled = true;
	private bool _globalFilterHotkey;
	private LogLevel? _filterLevel;
	private string? _filterCategory;
	private string? _title;
	private string? _name;
	private Margin _margin = new(0, 0, 0, 0);

	/// <summary>Creates a builder bound to the given log service.</summary>
	/// <param name="logService">The service whose entries the viewer shows.</param>
	/// <exception cref="ArgumentNullException"><paramref name="logService"/> is null.</exception>
	public LogViewerBuilder(ILogService logService)
	{
		_logService = logService ?? throw new ArgumentNullException(nameof(logService));
	}

	/// <summary>
	/// Sets whether the newest entry is kept visible. Defaults to <c>true</c>.
	/// </summary>
	public LogViewerBuilder WithAutoScroll(bool autoScroll = true)
	{
		_autoScroll = autoScroll;
		return this;
	}

	/// <summary>
	/// Sets whether the toolbar (level, filter, pause, clear, export) is shown. Defaults to <c>true</c>.
	/// </summary>
	public LogViewerBuilder WithToolbar(bool show = true)
	{
		_showToolbar = show;
		return this;
	}

	/// <summary>
	/// Sets whether a separator is drawn between rows. Defaults to <c>false</c>, which fits more
	/// entries in the pane.
	/// </summary>
	public LogViewerBuilder WithRowSeparators(bool show = true)
	{
		_showRowSeparators = show;
		return this;
	}

	/// <summary>
	/// Sets whether a column border can be dragged to resize the column. Defaults to <c>true</c>.
	/// </summary>
	public LogViewerBuilder WithColumnResize(bool enabled = true)
	{
		_columnResizeEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Sets whether clicking a column header sorts by it. Defaults to <c>true</c>.
	/// </summary>
	public LogViewerBuilder WithSorting(bool enabled = true)
	{
		_sortingEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Sets whether the table's filter grammar is available. Defaults to <c>true</c>.
	/// </summary>
	public LogViewerBuilder WithFiltering(bool enabled = true)
	{
		_filteringEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Sets whether pressing <c>/</c> anywhere on the window opens the filter, even when the
	/// viewer does not have focus. Defaults to <c>false</c>.
	/// </summary>
	/// <remarks>
	/// Leave it off on a page where another control legitimately takes <c>/</c>, such as a
	/// terminal or a text editor. Even when on, it declines while focus is on another
	/// interactive control, so a typed slash stays a typed slash.
	/// </remarks>
	public LogViewerBuilder WithGlobalFilterHotkey(bool enabled = true)
	{
		_globalFilterHotkey = enabled;
		return this;
	}

	/// <summary>
	/// Sets the minimum level the VIEW shows. This hides entries without discarding them, and is
	/// not the same as <see cref="ILogService.MinimumLevel"/>, which decides what is captured.
	/// </summary>
	public LogViewerBuilder WithFilterLevel(LogLevel level)
	{
		_filterLevel = level;
		return this;
	}

	/// <summary>
	/// Restricts the view to one category. Null shows every category.
	/// </summary>
	public LogViewerBuilder WithFilterCategory(string? category)
	{
		_filterCategory = category;
		return this;
	}

	/// <summary>Sets a title shown above the log table.</summary>
	public LogViewerBuilder WithTitle(string? title)
	{
		_title = title;
		return this;
	}

	/// <summary>Sets the control name for lookup.</summary>
	public LogViewerBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>Sets a uniform margin on all four sides.</summary>
	public LogViewerBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>Sets the margin with individual values for each side.</summary>
	public LogViewerBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Builds and returns the configured <see cref="LogViewerControl"/>.
	/// </summary>
	public LogViewerControl Build()
	{
		var control = new LogViewerControl(_logService)
		{
			AutoScroll = _autoScroll,
			ShowToolbar = _showToolbar,
			ShowRowSeparators = _showRowSeparators,
			ColumnResizeEnabled = _columnResizeEnabled,
			SortingEnabled = _sortingEnabled,
			FilteringEnabled = _filteringEnabled,
			GlobalFilterHotkey = _globalFilterHotkey,
			Title = _title,
			Name = _name,
			Margin = _margin
		};

		// Applied only when asked for, so the control's own defaults stand otherwise.
		if (_filterLevel.HasValue) control.FilterLevel = _filterLevel.Value;
		if (_filterCategory is not null) control.FilterCategory = _filterCategory;

		return control;
	}

	/// <summary>
	/// Implicit conversion to <see cref="LogViewerControl"/>.
	/// </summary>
	public static implicit operator LogViewerControl(LogViewerBuilder builder) => builder.Build();
}
