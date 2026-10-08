// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Helpers.Scrollbar;
using SharpConsoleUI.Html;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Sub-region gesture ownership for <see cref="HtmlControl"/> mouse handling. A fresh Button1 press
	/// hit-tests one of these regions and captures it; every subsequent resent press/drag routes to the
	/// captured region without re-hit-testing (SGR re-sends Button1Pressed on motion).
	/// </summary>
	internal enum HtmlGestureRegion
	{
		/// <summary>
		/// No region. Must stay first so <c>default(HtmlGestureRegion)</c> is never a real region:
		/// <see cref="Helpers.MouseGestureCapture{TRegion}.Route"/> returns <c>default</c> together
		/// with <see cref="Helpers.GesturePhase.None"/> for an uncaptured bare click, and a region
		/// worth 0 would make that read as a genuine hit.
		/// </summary>
		None = 0,

		/// <summary>The vertical scrollbar column.</summary>
		Scrollbar,

		/// <summary>The rendered HTML content area.</summary>
		Content
	}

	/// <summary>
	/// A control that renders HTML content in the terminal with scrolling, link interaction, and keyboard navigation.
	/// </summary>
	/// <remarks>
	/// This control does not implement <see cref="IColorRoleableControl"/>, so it has no semantic role and does
	/// not honour them: it has no single themed colour surface (colours come from the HTML/CSS content).
	/// </remarks>
	public partial class HtmlControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		/// <summary>
		/// Creates a new HtmlControl instance (for builder pattern start).
		/// </summary>
		public static HtmlControl Create() => new();

		#region Fields

		private readonly HtmlLayoutEngine _layoutEngine = new();
		private readonly object _contentLock = new();
		private LayoutResult _layoutResult;
		private int _lastLayoutWidth = -1;
		// The fg/bg/link colors baked into the current layout. Layout bakes theme colors into every
		// cell, so a theme switch leaves the cached layout stale until these are seen to differ and a
		// relayout is forced (reload already does this; this makes a live theme switch do it too).
		private Color? _lastLayoutFg;
		private Color? _lastLayoutBg;
		private Color? _lastLayoutLinkColor;
		private string? _rawHtml;
		private string? _baseUrl;
		private string? _currentUrl;
		private bool _isLoading;
		private CancellationTokenSource? _loadCts;
		private static readonly HttpClient _httpClient = new()
		{
			DefaultRequestHeaders =
			{
				{ "User-Agent", "SharpConsoleUI/1.0 (HtmlControl; +https://github.com/nickprotop/ConsoleEx)" }
			}
		};

		// Scroll state
		private int _scrollOffset;

		// Mouse gesture capture: a fresh Button1 press captures one sub-region; every subsequent
		// resent press/drag routes to it without re-hit-testing (SGR re-sends Button1Pressed on
		// motion). Replaces the former _isScrollbarDragging latch, which released only on
		// Button1Released and could leak a captured drag into content when the pointer left the bar.
		private readonly MouseGestureCapture<HtmlGestureRegion> _gesture = new();
		private bool _thumbDragging;
		private int _scrollbarDragStartY;
		private int _scrollbarDragStartOffset;

		// Link hover state
		private int _hoveredLinkLineIndex = -1;
		private int _hoveredLinkIndex = -1;

		// Link keyboard navigation state
		private int _focusedLinkIndex = -1; // index into flattened link list, -1 = no link focused
		private List<(int lineIndex, int linkIndex, LinkRegion link, int lineY)>? _flattenedLinks;
		private bool _flattenedLinksDirty = true;

		// Interaction state
		private bool _isEnabled = true;
		private int _mouseWheelScrollSpeed = ControlDefaults.DefaultScrollWheelLines;

		// Color backing fields
		private Color? _foregroundColorValue;
		private Color? _backgroundColorValue;

		// Property backing fields
		// Null = follow the active theme accent (so links read on light themes); explicit set pins it.
		private Color? _linkColor;
		private Color _visitedLinkColor = HtmlConstants.DefaultVisitedLinkColor;
		private bool _showImages;
		private bool _showBulletPoints = true;
		private int _tabSize = 4;
		private int _blockSpacing = 1;
		private ScrollbarVisibility _scrollbarVisibility = ScrollbarVisibility.Auto;
		private string _loadingText = HtmlConstants.DefaultLoadingText;

		// Progressive loading state
		private string? _loadingStatus;
		private int _imageLoadTotal;
		private int _imageLoadCompleted;
		// Images downloaded for the current URL load (URL → decoded buffer, null = failed).
		// Every relayout of that page reads from it, so a resize, theme switch or CSS reload
		// never downloads the images again. Written only under _contentLock.
		private Dictionary<string, Imaging.PixelBuffer?>? _imageCache;
		// True between LoadUrlAsync start and first content commit — used to dim the
		// previous page and render a banner while fetching, without discarding context.
		private bool _isNavigating;
		// Animated spinner for the loading banner (incremented each paint while navigating).
		private int _spinnerTick;

		// Resize debounce: skip relayout during active resize, catch up after idle
		private System.Timers.Timer? _resizeDebounceTimer;
		private int _pendingLayoutWidth;

		#endregion

		#region Properties

		/// <inheritdoc/>
		public override int? ContentWidth => null;

		/// <summary>
		/// Gets or sets the foreground color of the control.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.HtmlForegroundColor ?? Color.White;
			set => SetProperty(ref _foregroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the background color of the control.
		/// </summary>
		public Color BackgroundColor
		{
			// HTML rendering needs a CONCRETE surface (images/contrast blend against it), so resolve to
			// an opaque color: control bg if set → container's bg (only if opaque) → theme window bg →
			// black. The theme step is what makes it light on light themes (was a hardcoded ?? black).
			get
			{
				if (_backgroundColorValue is { } own)
					return own;
				var containerBg = Container?.BackgroundColor;
				if (containerBg is { } cbg && cbg.A == 255)
					return cbg;
				return Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			}
			set => SetProperty(ref _backgroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the color used for unvisited links. When unset, follows the active theme accent
		/// so links stay readable on light themes (instead of a hardcoded cyan).
		/// </summary>
		public Color LinkColor
		{
			get => _linkColor
				?? Container?.GetConsoleWindowSystem?.Theme?.ActiveBorderForegroundColor
				?? HtmlConstants.DefaultLinkColor;
			set => SetProperty(ref _linkColor, value);
		}

		/// <summary>
		/// Gets or sets the color used for visited links.
		/// </summary>
		public Color VisitedLinkColor
		{
			get => _visitedLinkColor;
			set => SetProperty(ref _visitedLinkColor, value);
		}

		/// <summary>
		/// Gets or sets whether images are rendered inline using half-block characters.
		/// When false, images display alt text placeholders instead.
		/// </summary>
		public bool ShowImages
		{
			get => _showImages;
			set
			{
				if (SetProperty(ref _showImages, value))
					OnShowImagesChanged();
			}
		}

		/// <summary>
		/// Gets or sets whether bullet points are rendered for lists.
		/// </summary>
		public bool ShowBulletPoints
		{
			get => _showBulletPoints;
			set => SetProperty(ref _showBulletPoints, value);
		}

		/// <summary>
		/// Gets or sets the tab size in spaces.
		/// </summary>
		public int TabSize
		{
			get => _tabSize;
			set => SetProperty(ref _tabSize, value, v => Math.Clamp(v, 1, 8));
		}

		/// <summary>
		/// Gets or sets the spacing between block elements.
		/// </summary>
		public int BlockSpacing
		{
			get => _blockSpacing;
			set => SetProperty(ref _blockSpacing, value, v => Math.Max(0, v));
		}

		/// <summary>
		/// Gets or sets the scrollbar visibility mode.
		/// </summary>
		public ScrollbarVisibility ScrollbarVisibility
		{
			get => _scrollbarVisibility;
			set => SetProperty(ref _scrollbarVisibility, value);
		}

		/// <summary>
		/// Gets or sets the text displayed while content is loading.
		/// </summary>
		public string LoadingText
		{
			get => _loadingText;
			set => SetProperty(ref _loadingText, value ?? HtmlConstants.DefaultLoadingText);
		}

		/// <summary>
		/// Gets or sets the scroll offset (number of lines scrolled from the top).
		/// </summary>
		public int ScrollOffset
		{
			get => _scrollOffset;
			set
			{
				int maxScroll = Math.Max(0, _layoutResult.TotalHeight - GetViewportHeight());
				int clamped = Math.Clamp(value, 0, maxScroll);
				if (SetProperty(ref _scrollOffset, clamped))
				{
					Invalidate(Invalidation.Repaint);
				}
			}
		}

		/// <summary>
		/// Gets the total content height in lines.
		/// </summary>
		public int ContentHeight => _layoutResult.TotalHeight;

		/// <summary>
		/// Gets whether content is currently being loaded.
		/// </summary>
		public bool IsLoading => _isLoading;

		/// <summary>
		/// Gets a human-readable loading status (e.g., "Loading images: 3/12").
		/// Null when not loading.
		/// </summary>
		public string? LoadingStatus => _loadingStatus;

		/// <summary>
		/// Gets the URL of the currently loaded content, if any.
		/// </summary>
		public string? CurrentUrl => _currentUrl;

		/// <summary>
		/// Gets the raw HTML content.
		/// </summary>
		public string? RawHtml => _rawHtml;

		/// <summary>
		/// Gets or sets the number of lines to scroll per mouse wheel tick.
		/// </summary>
		public int MouseWheelScrollSpeed
		{
			get => _mouseWheelScrollSpeed;
			set { _mouseWheelScrollSpeed = Math.Max(1, value); OnPropertyChanged(); }
		}

		/// <summary>
		/// Gets whether this control has focus.
		/// </summary>
		public bool HasFocus
		{
			get => ComputeHasFocus();
		}

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <summary>
		/// Resolves the scrollbar thumb/track colors via the shared <see cref="ScrollbarPaletteResolver"/>.
		/// Html already resolved its colors focus-aware from the theme with the same fallback cascade
		/// (Cyan1/Grey thumb, Grey/Grey23 track) that the resolver now applies uniformly, so this is a
		/// non-visible refactor for Html - a check that the shared resolver preserves the existing policy.
		/// </summary>
		private ScrollbarPalette ResolveScrollbarPalette()
		{
			var theme = Container?.GetConsoleWindowSystem?.Theme;
			return ScrollbarPaletteResolver.Resolve(new ScrollbarPaletteRequest(
				ThumbOverride: null,
				TrackOverride: null,
				Theme: theme,
				HasFocus: HasFocus,
				IsEnabled: IsEnabled,
				Background: Color.Transparent));
		}

		#endregion

		#region Events

		/// <summary>
		/// Raised when a link is clicked.
		/// </summary>
		public event EventHandler<LinkClickedEventArgs>? LinkClicked;

		/// <summary>Async counterpart of <see cref="LinkClicked"/>.</summary>
#pragma warning disable CS0067 // No internal raise site; mirrors the sync LinkClicked event.
		public event Core.AsyncEventHandler<LinkClickedEventArgs>? LinkClickedAsync;
#pragma warning restore CS0067

		/// <summary>
		/// Raised when a link is hovered or unhovered.
		/// </summary>
		public event EventHandler<LinkHoverEventArgs>? LinkHover;

		/// <summary>
		/// Raised when the text layout of a fetched page has been committed and is usable.
		/// For pages with images, this fires BEFORE images are loaded — subscribe to
		/// <see cref="LoadingCompleted"/> if you want to wait for images too.
		/// </summary>
		public event EventHandler? ContentLoaded;

		/// <summary>Async counterpart of <see cref="ContentLoaded"/>.</summary>
		public event Core.AsyncEventHandler<EventArgs>? ContentLoadedAsync;

		/// <summary>
		/// Raised when every phase of loading has finished, including progressive image
		/// loading. Fires exactly once per <see cref="LoadUrlAsync(string)"/> call (on success,
		/// cancellation, or error). At the moment this event fires, <see cref="IsLoading"/>
		/// is false and <see cref="LoadingStatus"/> is null.
		/// </summary>
		public event EventHandler? LoadingCompleted;

		/// <summary>Async counterpart of <see cref="LoadingCompleted"/>.</summary>
		public event Core.AsyncEventHandler<EventArgs>? LoadingCompletedAsync;

		/// <summary>
		/// Raised when an error occurs during content loading.
		/// </summary>
		public event EventHandler<LoadErrorEventArgs>? LoadError;

		/// <summary>Async counterpart of <see cref="LoadError"/>.</summary>
		public event Core.AsyncEventHandler<LoadErrorEventArgs>? LoadErrorAsync;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		#endregion
	}
}
