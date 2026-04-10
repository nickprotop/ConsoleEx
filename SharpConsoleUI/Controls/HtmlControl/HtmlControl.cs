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
using SharpConsoleUI.Html;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control that renders HTML content in the terminal with scrolling, link interaction, and keyboard navigation.
	/// </summary>
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

		// Scrollbar drag state
		private bool _isScrollbarDragging;
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
		private Color _linkColor = HtmlConstants.DefaultLinkColor;
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
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonForegroundColor ?? Color.White;
			set => SetProperty(ref _foregroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the background color of the control.
		/// </summary>
		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.BackgroundColor ?? Color.Black;
			set => SetProperty(ref _backgroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the color used for unvisited links.
		/// </summary>
		public Color LinkColor
		{
			get => _linkColor;
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
			set => SetProperty(ref _showImages, value);
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
					Container?.Invalidate(true);
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

		#endregion

		#region Events

		/// <summary>
		/// Raised when a link is clicked.
		/// </summary>
		public event EventHandler<LinkClickedEventArgs>? LinkClicked;

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

		/// <summary>
		/// Raised when every phase of loading has finished, including progressive image
		/// loading. Fires exactly once per <see cref="LoadUrlAsync(string)"/> call (on success,
		/// cancellation, or error). At the moment this event fires, <see cref="IsLoading"/>
		/// is false and <see cref="LoadingStatus"/> is null.
		/// </summary>
		public event EventHandler? LoadingCompleted;

		/// <summary>
		/// Raised when an error occurs during content loading.
		/// </summary>
		public event EventHandler<LoadErrorEventArgs>? LoadError;

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

		#region Public Methods

		/// <summary>
		/// Sets the HTML content to display.
		/// </summary>
		/// <param name="html">The HTML string to render.</param>
		public void SetContent(string html)
		{
			lock (_contentLock)
			{
				_rawHtml = html;
				_baseUrl = null;
				_currentUrl = null;
				_scrollOffset = 0;
				_hoveredLinkLineIndex = -1;
				_hoveredLinkIndex = -1;
				int layoutWidth = _lastLayoutWidth > 0 ? _lastLayoutWidth : 80;
				RunLayout(layoutWidth);
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets the HTML content to display with a base URL for resolving relative links.
		/// </summary>
		/// <param name="html">The HTML string to render.</param>
		/// <param name="baseUrl">The base URL for resolving relative links.</param>
		public void SetContent(string html, string baseUrl)
		{
			lock (_contentLock)
			{
				_rawHtml = html;
				_baseUrl = baseUrl;
				_currentUrl = null;
				_scrollOffset = 0;
				_hoveredLinkLineIndex = -1;
				_hoveredLinkIndex = -1;
				int layoutWidth = _lastLayoutWidth > 0 ? _lastLayoutWidth : 80;
				RunLayout(layoutWidth);
			}
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Loads HTML content from a URL asynchronously.
		/// </summary>
		/// <param name="url">The URL to load content from.</param>
		public async Task LoadUrlAsync(string url)
		{
			await LoadUrlAsync(url, CancellationToken.None);
		}

		/// <summary>
		/// Loads HTML content from a URL asynchronously with cancellation support.
		/// </summary>
		/// <param name="url">The URL to load content from.</param>
		/// <param name="ct">External cancellation token.</param>
		public async Task LoadUrlAsync(string url, CancellationToken ct)
		{
			// Cancel any previous load
			_loadCts?.Cancel();
			_loadCts?.Dispose();
			_loadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var linkedToken = _loadCts.Token;

			_isLoading = true;
			_isNavigating = true;
			_currentUrl = url;
			_loadingStatus = $"Fetching {url}";
			_imageLoadTotal = 0;
			_imageLoadCompleted = 0;
			// Reset scroll + hover state immediately so stray input during the fetch
			// doesn't apply to the page we're about to replace.
			_scrollOffset = 0;
			_hoveredLinkLineIndex = -1;
			_hoveredLinkIndex = -1;
			Container?.Invalidate(true);

			// Animate the loading-banner spinner for the full loading lifecycle
			// (fetch → render → background image load). Fire-and-forget, tied to the same
			// cancellation token; stops as soon as _loadingStatus becomes null.
			_ = AnimateLoadingSpinnerAsync(linkedToken);

			try
			{
				// Phase 1: Fetch HTML
				var html = await _httpClient.GetStringAsync(url, linkedToken);
				linkedToken.ThrowIfCancellationRequested();

				_loadingStatus = "Rendering...";
				Container?.Invalidate(true);

				// Phase 2: Render text immediately (no images) — progressive rendering
				lock (_contentLock)
				{
					_rawHtml = html;
					_baseUrl = url;
					int layoutWidth = _lastLayoutWidth > 0 ? _lastLayoutWidth : 80;
					RunLayoutWithoutImages(layoutWidth);
				}

				_isLoading = false;
				_isNavigating = false;
				// Leave _loadingStatus set only if Phase 3 will pick it up — otherwise clear it
				// so the spinner animation loop exits. We hand off the status to the image
				// loader before clearing navigation state to avoid a one-frame blank gap.
				bool handingOffToImages = _showImages;
				if (!handingOffToImages)
					_loadingStatus = null;
				else
					_loadingStatus = "Preparing images...";
				Container?.Invalidate(true);
				ContentLoaded?.Invoke(this, EventArgs.Empty);

				// Phase 3: If ShowImages, re-layout with images in background
				if (handingOffToImages)
				{
					_ = LoadImagesProgressivelyAsync(linkedToken);
				}
				else
				{
					// No image phase — this is the terminal state for a successful load.
					LoadingCompleted?.Invoke(this, EventArgs.Empty);
				}
			}
			catch (OperationCanceledException)
			{
				_isLoading = false;
				_isNavigating = false;
				_loadingStatus = null;
				LoadingCompleted?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				_isLoading = false;
				_isNavigating = false;
				_loadingStatus = null;

				// Show error as HTML content
				var errorHtml = $"""
					<h1 style="color: red">Error Loading Page</h1>
					<p><b>URL:</b> {System.Net.WebUtility.HtmlEncode(url)}</p>
					<p><b>Error:</b> {System.Net.WebUtility.HtmlEncode(ex.Message)}</p>
					<hr>
					<p style="color: gray">Press Home or type a new address to navigate elsewhere.</p>
					""";
				lock (_contentLock)
				{
					_rawHtml = errorHtml;
					_baseUrl = null;
					_scrollOffset = 0;
					int layoutWidth = _lastLayoutWidth > 0 ? _lastLayoutWidth : 80;
					RunLayout(layoutWidth);
				}

				LoadError?.Invoke(this, new LoadErrorEventArgs(url, ex));
				LoadingCompleted?.Invoke(this, EventArgs.Empty);
			}

			Container?.Invalidate(true);
		}

		private async Task AnimateLoadingSpinnerAsync(CancellationToken ct)
		{
			try
			{
				// Keep animating as long as there is a loading status to show — covers both
				// the navigation overlay (_isNavigating) and the background image-load banner.
				while (_loadingStatus != null && !ct.IsCancellationRequested)
				{
					Container?.Invalidate(true);
					await Task.Delay(100, ct);
				}
			}
			catch (OperationCanceledException)
			{
				// Expected on navigation cancel — nothing to do.
			}
		}

		private async Task LoadImagesProgressivelyAsync(CancellationToken ct)
		{
			bool raiseCompleted = true;
			try
			{
				if (string.IsNullOrEmpty(_rawHtml))
				{
					_loadingStatus = null;
					return;
				}

				// Collect all image URLs from the HTML
				var imageUrls = _layoutEngine.GetImageUrls(_rawHtml, _baseUrl);
				if (imageUrls.Count == 0)
				{
					_loadingStatus = null;
					Container?.Invalidate(true);
					return;
				}

				_imageLoadTotal = imageUrls.Count;
				_imageLoadCompleted = 0;

				// Capture state ONCE at start
				int capturedWidth;
				string? capturedHtml;
				string? capturedBaseUrl;
				lock (_contentLock)
				{
					capturedWidth = _lastLayoutWidth > 0 ? _lastLayoutWidth : 80;
					capturedHtml = _rawHtml;
					capturedBaseUrl = _baseUrl;
				}
				if (capturedHtml == null)
				{
					_loadingStatus = null;
					return;
				}

				// Image cache: URL → PixelBuffer (or null for failed)
				var imageCache = new Dictionary<string, Imaging.PixelBuffer?>();

				// Throttled re-layout: commit the partial image cache to the layout at most
				// once every ThrottleMs. A full re-layout of a large page (e.g. Wikipedia Cat)
				// can take ~1s, so re-laying out per image (20+ times) would stall the UI.
				// Throttling gives the user visible progress — images pop in in batches of
				// whatever arrived during the interval — without burning CPU.
				const int ThrottleMs = 750;
				long lastCommitTicks = 0;
				bool htmlChanged = false;

				// Fetch images one-by-one; after each, if the throttle has elapsed, re-layout
				// with whatever images are cached so far. Missing images still render as alt
				// text (HtmlBlockFlow.ProcessImage handles partial caches cleanly).
				foreach (var url in imageUrls)
				{
					ct.ThrowIfCancellationRequested();

					_loadingStatus = $"Loading images ({_imageLoadCompleted + 1}/{_imageLoadTotal})...";
					Container?.Invalidate(true);

					try
					{
						var bytes = await HtmlImageLoader.HttpClient.GetByteArrayAsync(url, ct);
						using var stream = new System.IO.MemoryStream(bytes);
						var buffer = Imaging.PixelBuffer.FromStream(stream);
						imageCache[url] = buffer;
					}
					catch
					{
						imageCache[url] = null;
					}

					_imageLoadCompleted++;

					// Throttled commit — only re-layout if ThrottleMs elapsed since last commit
					// and there are still images pending (the final commit after the loop
					// guarantees the last batch lands regardless of timing).
					long nowTicks = Environment.TickCount64;
					bool isLast = _imageLoadCompleted >= _imageLoadTotal;
					if (!isLast && (nowTicks - lastCommitTicks) >= ThrottleMs)
					{
						if (!TryCommitPartialImageLayout(capturedHtml, capturedWidth, capturedBaseUrl, imageCache))
						{
							htmlChanged = true;
							break; // page was replaced under us — abort image loading
						}
						lastCommitTicks = nowTicks;
						Container?.Invalidate(true);
					}
				}

				// Final commit: guarantees the last batch of images reaches the screen even if
				// they arrived inside the throttle window.
				if (!htmlChanged)
				{
					ct.ThrowIfCancellationRequested();
					_loadingStatus = "Rendering images...";
					Container?.Invalidate(true);

					if (!TryCommitPartialImageLayout(capturedHtml, capturedWidth, capturedBaseUrl, imageCache))
						htmlChanged = true;
				}

				_loadingStatus = null;
				Container?.Invalidate(true);
				if (htmlChanged)
				{
					// Caller is already navigating to new content — it owns its own completion
					// event lifecycle. Suppress ours to avoid a double-fire.
					raiseCompleted = false;
				}
			}
			catch (OperationCanceledException)
			{
				_loadingStatus = null;
				// A new LoadUrlAsync() cancels the previous one and will fire its own
				// LoadingCompleted on its terminal state. Don't double-fire here.
				raiseCompleted = false;
			}
			catch
			{
				_loadingStatus = null;
			}
			finally
			{
				if (raiseCompleted)
					LoadingCompleted?.Invoke(this, EventArgs.Empty);
			}
		}

		#endregion

		#region Private Helpers

		/// <summary>
		/// Re-lays out the captured HTML with the current (possibly partial) image cache,
		/// committing the result atomically to <c>_layoutResult</c>. Returns <c>false</c> if
		/// the page was replaced under us (caller should abort the image-load loop).
		/// </summary>
		private bool TryCommitPartialImageLayout(
			string capturedHtml,
			int capturedWidth,
			string? capturedBaseUrl,
			Dictionary<string, Imaging.PixelBuffer?> imageCache)
		{
			lock (_contentLock)
			{
				if (_rawHtml != capturedHtml)
					return false;

				var fg = ForegroundColor;
				var bg = Container?.BackgroundColor ?? Color.Black;

				try
				{
					_layoutResult = _layoutEngine.Layout(
						capturedHtml,
						capturedWidth, fg, bg,
						_blockSpacing, _linkColor, _visitedLinkColor,
						capturedBaseUrl,
						showImages: true,
						imageCache: imageCache);
				}
				catch
				{
					// Layout failed — keep the previous result rather than blanking the view.
				}

				InvalidateLinkCache();
			}
			return true;
		}

		private void RunLayoutWithoutImages(int width)
		{
			if (width <= 0) width = 80;
			if (string.IsNullOrEmpty(_rawHtml))
			{
				_layoutResult = new LayoutResult(Array.Empty<LayoutLine>(), 0);
				_lastLayoutWidth = width;
				InvalidateLinkCache();
				return;
			}

			var fg = ForegroundColor;
			var bg = Container?.BackgroundColor ?? Color.Black;

			try
			{
				_layoutResult = _layoutEngine.Layout(
					_rawHtml, width, fg, bg, _blockSpacing,
					_linkColor, _visitedLinkColor, _baseUrl,
					showImages: false);
			}
			catch (Exception ex)
			{
				var errorHtml = $"""
					<h1 style="color: red">Rendering Error</h1>
					<p><b>Error:</b> {System.Net.WebUtility.HtmlEncode(ex.Message)}</p>
					""";
				try { _layoutResult = _layoutEngine.Layout(errorHtml, width, fg, bg, _blockSpacing, showImages: false); }
				catch { _layoutResult = new LayoutResult(Array.Empty<LayoutLine>(), 0); }
			}

			_lastLayoutWidth = width;
			InvalidateLinkCache();
		}

		private void RunLayout(int width)
		{
			if (width <= 0) width = 80;
			if (string.IsNullOrEmpty(_rawHtml))
			{
				_layoutResult = new LayoutResult(Array.Empty<LayoutLine>(), 0);
				_lastLayoutWidth = width;
				return;
			}

			var fg = ForegroundColor;
			var bg = Container?.BackgroundColor ?? Color.Black;

			try
			{
				_layoutResult = _layoutEngine.Layout(
					_rawHtml,
					width,
					fg,
					bg,
					_blockSpacing,
					_linkColor,
					_visitedLinkColor,
					_baseUrl,
					_showImages);
			}
			catch (Exception ex)
			{
				// Render the error as visible HTML so the user sees it instead of a blank page
				var errorHtml = $"""
					<h1 style="color: red">Rendering Error</h1>
					<p><b>Error:</b> {System.Net.WebUtility.HtmlEncode(ex.Message)}</p>
					<hr>
					<p style="color: gray">The page was loaded but could not be rendered.</p>
					""";
				try
				{
					// Layout the error page (without images to avoid recursion)
					_layoutResult = _layoutEngine.Layout(errorHtml, width, fg, bg, _blockSpacing, showImages: false);
				}
				catch
				{
					// Last resort — truly can't render anything
					_layoutResult = new LayoutResult(Array.Empty<LayoutLine>(), 0);
				}
			}

			_lastLayoutWidth = width;
			InvalidateLinkCache();
		}

		private int GetViewportHeight()
		{
			int h = Height ?? ActualHeight;
			return Math.Max(1, h - Margin.Top - Margin.Bottom);
		}

		/// <summary>
		/// Computes the width at which the document should be laid out, given the current
		/// control content width. Reserves one column for the scrollbar whenever scrollbar
		/// visibility is not <see cref="ScrollbarVisibility.Never"/> so that cell content
		/// (notably HTML table right borders) never collides with the scrollbar column.
		/// This must be deterministic and consistent between <c>MeasureDOM</c> and
		/// <c>PaintDOM</c> to avoid relayout ping-pong on large documents.
		/// </summary>
		private int ComputeLayoutWidth(int contentWidth)
		{
			if (contentWidth <= 1)
				return Math.Max(1, contentWidth);
			if (_scrollbarVisibility == ScrollbarVisibility.Never)
				return contentWidth;
			return contentWidth - 1;
		}

		/// <summary>
		/// Returns a flattened list of all links across all layout lines, cached and
		/// invalidated when layout changes. Each entry contains the line index, link
		/// index within that line, the LinkRegion data, and the line's Y position.
		/// </summary>
		/// <summary>
		/// Returns a deduplicated list of links (one entry per LinkId, using the first segment).
		/// Used for Tab navigation — multi-line links are a single Tab stop.
		/// </summary>
		internal List<(int lineIndex, int linkIndex, LinkRegion link, int lineY)> GetAllLinks()
		{
			if (!_flattenedLinksDirty && _flattenedLinks != null)
				return _flattenedLinks;

			_flattenedLinks ??= new List<(int, int, LinkRegion, int)>();
			_flattenedLinks.Clear();

			var seenLinkIds = new HashSet<int>();

			if (_layoutResult.Lines != null)
			{
				for (int i = 0; i < _layoutResult.Lines.Length; i++)
				{
					var line = _layoutResult.Lines[i];
					if (line.Links == null) continue;
					for (int j = 0; j < line.Links.Length; j++)
					{
						// Only add the first segment of each LinkId
						if (seenLinkIds.Add(line.Links[j].LinkId))
						{
							_flattenedLinks.Add((i, j, line.Links[j], line.Y));
						}
					}
				}
			}

			_flattenedLinksDirty = false;
			return _flattenedLinks;
		}

		/// <summary>
		/// Returns the LinkId of the currently focused link, or -1 if none.
		/// </summary>
		internal int GetFocusedLinkId()
		{
			if (_focusedLinkIndex < 0) return -1;
			var links = GetAllLinks();
			if (_focusedLinkIndex >= links.Count) return -1;
			return links[_focusedLinkIndex].link.LinkId;
		}

		/// <summary>
		/// Invalidates the cached flattened link list. Called when layout changes.
		/// </summary>
		internal void InvalidateLinkCache()
		{
			// Save the focused link's identity before invalidating
			int savedLinkId = -1;
			if (_focusedLinkIndex >= 0 && _flattenedLinks != null && _focusedLinkIndex < _flattenedLinks.Count)
			{
				savedLinkId = _flattenedLinks[_focusedLinkIndex].link.LinkId;
			}

			_flattenedLinksDirty = true;

			if (savedLinkId >= 0)
			{
				// Rebuild and try to restore focus to the same logical link
				var links = GetAllLinks();
				_focusedLinkIndex = -1;
				for (int i = 0; i < links.Count; i++)
				{
					if (links[i].link.LinkId == savedLinkId)
					{
						_focusedLinkIndex = i;
						break;
					}
				}
			}
			else if (_focusedLinkIndex >= 0)
			{
				var links = GetAllLinks();
				if (_focusedLinkIndex >= links.Count)
					_focusedLinkIndex = links.Count > 0 ? 0 : -1;
			}
		}

		/// <summary>
		/// Scrolls the viewport to ensure the given line Y position is visible.
		/// </summary>
		private void EnsureLinkVisible(int lineY)
		{
			var viewportHeight = GetViewportHeight();
			if (lineY < _scrollOffset)
				ScrollOffset = lineY;
			else if (lineY >= _scrollOffset + viewportHeight)
				ScrollOffset = lineY - viewportHeight + 1;
		}

		/// <summary>
		/// Gets or sets the currently focused link index (-1 = none).
		/// Setting this scrolls the viewport to make the link visible.
		/// </summary>
		internal int FocusedLinkIndex
		{
			get => _focusedLinkIndex;
			set
			{
				var links = GetAllLinks();
				if (links.Count == 0)
				{
					_focusedLinkIndex = -1;
					return;
				}

				_focusedLinkIndex = value;
				if (_focusedLinkIndex >= 0 && _focusedLinkIndex < links.Count)
				{
					EnsureLinkVisible(links[_focusedLinkIndex].lineY);
				}
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			_loadCts?.Cancel();
			_loadCts?.Dispose();
			_loadCts = null;
			_resizeDebounceTimer?.Stop();
			_resizeDebounceTimer?.Dispose();
			_resizeDebounceTimer = null;
		}

		#endregion
	}
}
