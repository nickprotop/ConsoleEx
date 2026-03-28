using SharpConsoleUI.Rendering;

namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration options for Start menu appearance and behavior.
/// Use object initializer syntax to set only the properties you need.
/// Property changes raise <see cref="OptionsChanged"/> so the Start menu can react at runtime.
/// </summary>
public class StartMenuOptions
{
	private StartMenuLayout _layout = StartMenuLayout.TwoColumn;
	private string? _appName;
	private string? _appVersion;
	private bool _showIcons = true;
	private string _headerIcon = "\u2630";
	private bool _showSystemCategory = true;
	private bool _showWindowList = true;
	private GradientBackground? _backgroundGradient;
	private Color? _backgroundColor;
	private Color? _foregroundColor;
	private Color? _highlightBackgroundColor;
	private Color? _highlightForegroundColor;
	private StartMenuSidebarStyle _sidebarStyle = StartMenuSidebarStyle.IconLabel;

	/// <summary>
	/// Raised when any property value changes.
	/// </summary>
	public event EventHandler? OptionsChanged;

	/// <summary>Layout mode: SingleColumn (compact) or TwoColumn (with window list).</summary>
	public StartMenuLayout Layout
	{
		get => _layout;
		set { if (_layout != value) { _layout = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Application name shown in the Start menu header. Defaults to "SharpConsoleUI".</summary>
	public string? AppName
	{
		get => _appName;
		set { if (_appName != value) { _appName = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Application version shown in the Start menu header. Defaults to library version.</summary>
	public string? AppVersion
	{
		get => _appVersion;
		set { if (_appVersion != value) { _appVersion = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Whether to show Unicode icons next to headers and exit.</summary>
	public bool ShowIcons
	{
		get => _showIcons;
		set { if (_showIcons != value) { _showIcons = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Icon displayed next to the app name in the header. Defaults to "☰" (U+2630).</summary>
	public string HeaderIcon
	{
		get => _headerIcon;
		set { if (_headerIcon != value) { _headerIcon = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Show built-in System category (themes, settings, about, performance).</summary>
	public bool ShowSystemCategory
	{
		get => _showSystemCategory;
		set { if (_showSystemCategory != value) { _showSystemCategory = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Show Windows list (right column in TwoColumn, submenu in SingleColumn).</summary>
	public bool ShowWindowList
	{
		get => _showWindowList;
		set { if (_showWindowList != value) { _showWindowList = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Optional gradient background for the Start menu window.</summary>
	public GradientBackground? BackgroundGradient
	{
		get => _backgroundGradient;
		set { if (_backgroundGradient != value) { _backgroundGradient = value; RaiseOptionsChanged(); } }
	}

	// Colors — null means resolve from theme via ColorResolver

	/// <summary>Background color. Null resolves from theme MenuDropdownBackgroundColor.</summary>
	public Color? BackgroundColor
	{
		get => _backgroundColor;
		set { if (_backgroundColor != value) { _backgroundColor = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Foreground color. Null resolves from theme MenuDropdownForegroundColor.</summary>
	public Color? ForegroundColor
	{
		get => _foregroundColor;
		set { if (_foregroundColor != value) { _foregroundColor = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Highlight background color. Null resolves from theme MenuDropdownHighlightBackgroundColor.</summary>
	public Color? HighlightBackgroundColor
	{
		get => _highlightBackgroundColor;
		set { if (_highlightBackgroundColor != value) { _highlightBackgroundColor = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Highlight foreground color. Null resolves from theme MenuDropdownHighlightForegroundColor.</summary>
	public Color? HighlightForegroundColor
	{
		get => _highlightForegroundColor;
		set { if (_highlightForegroundColor != value) { _highlightForegroundColor = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Sidebar display style: IconRail, IconLabel, or TextLabel.</summary>
	public StartMenuSidebarStyle SidebarStyle
	{
		get => _sidebarStyle;
		set { if (_sidebarStyle != value) { _sidebarStyle = value; RaiseOptionsChanged(); } }
	}

	/// <summary>Custom categories for the Start menu sidebar. Mutate the list directly; call <see cref="RaiseOptionsChanged"/> after bulk changes.</summary>
	public List<StartMenuCategory> Categories { get; } = new();

	/// <summary>Info-strip items displayed in the bottom bar (right side). Mutate the list directly; call <see cref="RaiseOptionsChanged"/> after bulk changes.</summary>
	public List<string> InfoStripItems { get; } = new();

	/// <summary>Custom controls added to the bottom bar between exit button and info text. Mutate the list directly; call <see cref="RaiseOptionsChanged"/> after bulk changes.</summary>
	public List<Controls.IWindowControl> BottomBarItems { get; } = new();

	/// <summary>
	/// Raises the <see cref="OptionsChanged"/> event. Call after bulk-mutating
	/// <see cref="Categories"/>, <see cref="InfoStripItems"/>, or <see cref="BottomBarItems"/>.
	/// </summary>
	public void RaiseOptionsChanged()
	{
		OptionsChanged?.Invoke(this, EventArgs.Empty);
	}
}
