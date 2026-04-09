// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for HtmlControl
/// </summary>
public sealed class HtmlBuilder : IControlBuilder<HtmlControl>
{
	// Content
	private string? _content;
	private string? _baseUrl;

	// Colors
	private Color? _foregroundColor;
	private Color? _backgroundColor;
	private Color? _linkColor;
	private Color? _visitedLinkColor;

	// Display options
	private bool _showImages = false;
	private bool _showBulletPoints = true;
	private int _tabSize = 4;
	private int _blockSpacing = 1;
	private ScrollbarVisibility _scrollbarVisibility = ScrollbarVisibility.Auto;
	private string _loadingText = "Loading...";

	// Layout
	private Margin _margin = new(0, 0, 0, 0);
	private int? _width;
	private int? _height;
	private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;

	// Control metadata
	private string? _name;
	private object? _tag;
	private bool _visible = true;

	// Event handlers
	private EventHandler<LinkClickedEventArgs>? _linkClickedHandler;
	private EventHandler<LinkHoverEventArgs>? _linkHoverHandler;
	private EventHandler? _contentLoadedHandler;
	private EventHandler? _loadingCompletedHandler;
	private EventHandler<LoadErrorEventArgs>? _loadErrorHandler;

	/// <summary>
	/// Creates a new HtmlBuilder instance.
	/// </summary>
	public static HtmlBuilder Create() => new();

	/// <summary>
	/// Sets the HTML content to display.
	/// </summary>
	public HtmlBuilder WithContent(string content)
	{
		_content = content;
		return this;
	}

	/// <summary>
	/// Sets the HTML content with a base URL for resolving relative links.
	/// </summary>
	public HtmlBuilder WithContent(string content, string baseUrl)
	{
		_content = content;
		_baseUrl = baseUrl;
		return this;
	}

	/// <summary>
	/// Sets the base URL for resolving relative links.
	/// </summary>
	public HtmlBuilder WithBaseUrl(string baseUrl)
	{
		_baseUrl = baseUrl;
		return this;
	}

	/// <summary>
	/// Sets the foreground color.
	/// </summary>
	public HtmlBuilder WithForegroundColor(Color color)
	{
		_foregroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets the background color.
	/// </summary>
	public HtmlBuilder WithBackgroundColor(Color color)
	{
		_backgroundColor = color;
		return this;
	}

	/// <summary>
	/// Sets both foreground and background colors.
	/// </summary>
	public HtmlBuilder WithColors(Color foreground, Color background)
	{
		_foregroundColor = foreground;
		_backgroundColor = background;
		return this;
	}

	/// <summary>
	/// Sets the color used for unvisited links.
	/// </summary>
	public HtmlBuilder WithLinkColor(Color color)
	{
		_linkColor = color;
		return this;
	}

	/// <summary>
	/// Sets the color used for visited links.
	/// </summary>
	public HtmlBuilder WithVisitedLinkColor(Color color)
	{
		_visitedLinkColor = color;
		return this;
	}

	/// <summary>
	/// Sets whether images are rendered (as alt text placeholders).
	/// </summary>
	public HtmlBuilder WithShowImages(bool show = true)
	{
		_showImages = show;
		return this;
	}

	/// <summary>
	/// Sets whether bullet points are rendered for lists.
	/// </summary>
	public HtmlBuilder WithShowBulletPoints(bool show = true)
	{
		_showBulletPoints = show;
		return this;
	}

	/// <summary>
	/// Sets the tab size in spaces.
	/// </summary>
	public HtmlBuilder WithTabSize(int tabSize)
	{
		_tabSize = Math.Clamp(tabSize, 1, 8);
		return this;
	}

	/// <summary>
	/// Sets the spacing between block elements.
	/// </summary>
	public HtmlBuilder WithBlockSpacing(int spacing)
	{
		_blockSpacing = Math.Max(0, spacing);
		return this;
	}

	/// <summary>
	/// Sets when the vertical scrollbar should be displayed.
	/// </summary>
	public HtmlBuilder WithScrollbarVisibility(ScrollbarVisibility visibility)
	{
		_scrollbarVisibility = visibility;
		return this;
	}

	/// <summary>
	/// Sets the text displayed while content is loading.
	/// </summary>
	public HtmlBuilder WithLoadingText(string loadingText)
	{
		_loadingText = loadingText;
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	public HtmlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin.
	/// </summary>
	public HtmlBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the width.
	/// </summary>
	public HtmlBuilder WithWidth(int width)
	{
		_width = width;
		return this;
	}

	/// <summary>
	/// Sets the height.
	/// </summary>
	public HtmlBuilder WithHeight(int height)
	{
		_height = height;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	public HtmlBuilder WithHorizontalAlignment(HorizontalAlignment alignment)
	{
		_horizontalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	public HtmlBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup.
	/// </summary>
	public HtmlBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object.
	/// </summary>
	public HtmlBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	public HtmlBuilder WithVisible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the LinkClicked event handler.
	/// </summary>
	public HtmlBuilder OnLinkClicked(EventHandler<LinkClickedEventArgs> handler)
	{
		_linkClickedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LinkHover event handler.
	/// </summary>
	public HtmlBuilder OnLinkHover(EventHandler<LinkHoverEventArgs> handler)
	{
		_linkHoverHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the ContentLoaded event handler. Fires after the text layout of a fetched
	/// page has been committed — before images finish loading. Use
	/// <see cref="OnLoadingCompleted"/> to wait for everything.
	/// </summary>
	public HtmlBuilder OnContentLoaded(EventHandler handler)
	{
		_contentLoadedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LoadingCompleted event handler. Fires exactly once per LoadUrlAsync call
	/// when every phase of loading has finished (including progressive image loading) —
	/// on success, cancellation, or error.
	/// </summary>
	public HtmlBuilder OnLoadingCompleted(EventHandler handler)
	{
		_loadingCompletedHandler = handler;
		return this;
	}

	/// <summary>
	/// Sets the LoadError event handler.
	/// </summary>
	public HtmlBuilder OnLoadError(EventHandler<LoadErrorEventArgs> handler)
	{
		_loadErrorHandler = handler;
		return this;
	}

	/// <summary>
	/// Builds the HtmlControl.
	/// </summary>
	public HtmlControl Build()
	{
		var control = new HtmlControl
		{
			HorizontalAlignment = _horizontalAlignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Width = _width,
			Height = _height,
			Name = _name,
			Tag = _tag,
			Visible = _visible,
			ShowImages = _showImages,
			ShowBulletPoints = _showBulletPoints,
			TabSize = _tabSize,
			BlockSpacing = _blockSpacing,
			ScrollbarVisibility = _scrollbarVisibility,
			LoadingText = _loadingText,
		};

		if (_foregroundColor.HasValue)
			control.ForegroundColor = _foregroundColor.Value;
		if (_backgroundColor.HasValue)
			control.BackgroundColor = _backgroundColor.Value;
		if (_linkColor.HasValue)
			control.LinkColor = _linkColor.Value;
		if (_visitedLinkColor.HasValue)
			control.VisitedLinkColor = _visitedLinkColor.Value;

		// Wire events
		if (_linkClickedHandler != null)
			control.LinkClicked += _linkClickedHandler;
		if (_linkHoverHandler != null)
			control.LinkHover += _linkHoverHandler;
		if (_contentLoadedHandler != null)
			control.ContentLoaded += _contentLoadedHandler;
		if (_loadingCompletedHandler != null)
			control.LoadingCompleted += _loadingCompletedHandler;
		if (_loadErrorHandler != null)
			control.LoadError += _loadErrorHandler;

		// Set content after wiring events so ContentLoaded fires if needed
		if (_content != null)
		{
			if (_baseUrl != null)
				control.SetContent(_content, _baseUrl);
			else
				control.SetContent(_content);
		}

		BindingHelper.ApplyDeferredBindings(this, control);
		return control;
	}

	/// <summary>
	/// Implicit conversion to HtmlControl.
	/// </summary>
	public static implicit operator HtmlControl(HtmlBuilder builder) => builder.Build();
}
