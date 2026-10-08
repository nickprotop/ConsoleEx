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
	/// Getting content into the control: setting it directly, fetching it from a URL, and loading
	/// the images it references.
	/// </summary>
	/// <remarks>
	/// Separated from the control proper because it is the only part that is asynchronous and
	/// cancellable. Everything here runs off the UI thread or hands work to the thread pool, and
	/// shares one <see cref="System.Threading.CancellationTokenSource"/> so that new content
	/// abandons whatever the last content was still fetching.
	/// </remarks>
	public partial class HtmlControl
	{
		#region Content and loading

		/// <summary>
		/// Sets the HTML content to display.
		/// </summary>
		/// <param name="html">The HTML string to render.</param>
		public void SetContent(string html) => SetContentCore(html, null);

		/// <summary>
		/// Sets the HTML content to display with a base URL for resolving relative links.
		/// </summary>
		/// <param name="html">The HTML string to render.</param>
		/// <param name="baseUrl">The base URL for resolving relative links.</param>
		public void SetContent(string html, string baseUrl) => SetContentCore(html, baseUrl);

		private void SetContentCore(string html, string? baseUrl)
		{
			// The new content replaces whatever a URL load or an earlier image load was producing
			var ct = RestartLoad(CancellationToken.None);

			lock (_contentLock)
			{
				_rawHtml = html;
				_baseUrl = baseUrl;
				_currentUrl = null;
				// Text shows right away; remote images download in the background and pop in
				// (inline data: images render immediately — HtmlBlockFlow decodes them in place)
				_imageCache = _showImages ? new Dictionary<string, Imaging.PixelBuffer?>() : null;
				_scrollOffset = 0;
				_hoveredLinkLineIndex = -1;
				_hoveredLinkIndex = -1;
				int layoutWidth = _lastLayoutWidth > 0 ? _lastLayoutWidth : 80;
				RunLayout(layoutWidth);
			}
			Invalidate(Invalidation.Relayout);

			if (_showImages)
				StartBackgroundImageLoad(ct);
		}

		/// <summary>
		/// Cancels any in-flight URL or image load and returns the token for the next one.
		/// </summary>
		private CancellationToken RestartLoad(CancellationToken external)
		{
			_loadCts?.Cancel();
			_loadCts?.Dispose();
			_loadCts = CancellationTokenSource.CreateLinkedTokenSource(external);
			return _loadCts.Token;
		}

		/// <summary>
		/// Downloads the current content's remote images in the background (for content set
		/// directly rather than loaded from a URL, so no LoadingCompleted is raised).
		/// </summary>
		private void StartBackgroundImageLoad(CancellationToken ct)
		{
			// NO STATUS SET HERE. The text is already laid out and must stay on screen: a status
			// makes the paint show the loading spinner INSTEAD of the content, so setting one
			// before knowing whether there is anything to download hid the text behind a spinner
			// for however long it took the task below to start — which, under load, is long
			// enough to see. The loader sets it only once it has remote images to fetch, and
			// starts the spinner animation itself at the same time.
			_ = Task.Run(() => LoadImagesProgressivelyAsync(ct, raiseLoadingCompleted: false));
		}

		private void OnShowImagesChanged()
		{
			if (_rawHtml == null)
				return;

			var ct = RestartLoad(CancellationToken.None);
			lock (_contentLock)
			{
				_imageCache = _showImages ? new Dictionary<string, Imaging.PixelBuffer?>() : null;
				_loadingStatus = null;
				if (_lastLayoutWidth > 0)
					RunLayout(_lastLayoutWidth);
			}
			Invalidate(Invalidation.Relayout);

			if (_showImages)
				StartBackgroundImageLoad(ct);
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
			var linkedToken = RestartLoad(ct);

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
			Invalidate(Invalidation.Relayout);

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
				Invalidate(Invalidation.Relayout);

				// Phase 2: Render text immediately (no images) — progressive rendering.
				// Off the UI thread: laying out a large page still takes a noticeable moment.
				await Task.Run(() =>
				{
					lock (_contentLock)
					{
						_rawHtml = html;
						_baseUrl = url;
						_imageCache = _showImages ? new Dictionary<string, Imaging.PixelBuffer?>() : null;
						int layoutWidth = _lastLayoutWidth > 0 ? _lastLayoutWidth : 80;
						RunLayoutWithoutImages(layoutWidth);
					}
				}, linkedToken);

				_isLoading = false;
				_isNavigating = false;
				// Cleared either way, including when Phase 3 follows, which also lets the spinner
				// animation loop exit. A status makes the paint show the spinner INSTEAD of the
				// content, so holding one here to bridge the gap to the image load hid the page
				// we had just laid out. Phase 3 raises its own once it knows there are remote
				// images worth waiting for.
				bool handingOffToImages = _showImages;
				_loadingStatus = null;
				Invalidate(Invalidation.Relayout);
				Core.AsyncEvent.Raise(ContentLoaded, ContentLoadedAsync, this, EventArgs.Empty, Container?.GetConsoleWindowSystem?.LogService);

				// Phase 2b: Load external CSS stylesheets in background.
				// When done, re-layout with CSS-aware computed styles (correct image widths etc.)
				if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
					_ = Task.Run(() => _layoutEngine.LoadCssAsync(html, url)).ContinueWith(_ =>
					{
						_lastLayoutWidth = -1; // force re-layout on next measure
						InvalidateLinkCache();
						Invalidate(Invalidation.Relayout);
					}, TaskScheduler.Default);

				// Phase 3: If ShowImages, re-layout with images in background
				if (handingOffToImages)
				{
					// Run on the thread pool so downloads, decoding and relayouts never land on
					// the UI thread through its SynchronizationContext.
					_ = Task.Run(() => LoadImagesProgressivelyAsync(linkedToken));
				}
				else
				{
					// No image phase — this is the terminal state for a successful load.
					Core.AsyncEvent.Raise(LoadingCompleted, LoadingCompletedAsync, this, EventArgs.Empty, Container?.GetConsoleWindowSystem?.LogService);
				}
			}
			catch (OperationCanceledException)
			{
				_isLoading = false;
				_isNavigating = false;
				_loadingStatus = null;
				Core.AsyncEvent.Raise(LoadingCompleted, LoadingCompletedAsync, this, EventArgs.Empty, Container?.GetConsoleWindowSystem?.LogService);
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

				Core.AsyncEvent.Raise(LoadError, LoadErrorAsync, this, new LoadErrorEventArgs(url, ex), Container?.GetConsoleWindowSystem?.LogService);
				Core.AsyncEvent.Raise(LoadingCompleted, LoadingCompletedAsync, this, EventArgs.Empty, Container?.GetConsoleWindowSystem?.LogService);
			}

			Invalidate(Invalidation.Relayout);
		}

		private async Task AnimateLoadingSpinnerAsync(CancellationToken ct)
		{
			try
			{
				// Keep animating as long as there is a loading status to show — covers both
				// the navigation overlay (_isNavigating) and the background image-load banner.
				while (_loadingStatus != null && !ct.IsCancellationRequested)
				{
					Invalidate(Invalidation.Repaint);
					await Task.Delay(100, ct);
				}
			}
			catch (OperationCanceledException)
			{
				// Expected on navigation cancel — nothing to do.
			}
		}

		private async Task LoadImagesProgressivelyAsync(CancellationToken ct, bool raiseLoadingCompleted = true)
		{
			bool raiseCompleted = raiseLoadingCompleted;
			try
			{
				if (string.IsNullOrEmpty(_rawHtml))
				{
					_loadingStatus = null;
					return;
				}

				// Collect the remote image URLs from the HTML (a page often repeats an image — fetch
				// once). Inline data: images need no download; layout decodes them in place.
				var imageUrls = _layoutEngine.GetImageUrls(_rawHtml, _baseUrl)
					.Where(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
								u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
					.Distinct().ToList();
				if (imageUrls.Count == 0)
				{
					_loadingStatus = null;
					Invalidate(Invalidation.Relayout);
					return;
				}

				_imageLoadTotal = imageUrls.Count;
				_imageLoadCompleted = 0;

				// Capture state ONCE at start
				int capturedWidth;
				string? capturedHtml;
				string? capturedBaseUrl;
				Dictionary<string, Imaging.PixelBuffer?> imageCache;
				lock (_contentLock)
				{
					capturedWidth = _lastLayoutWidth > 0 ? _lastLayoutWidth : 80;
					capturedHtml = _rawHtml;
					capturedBaseUrl = _baseUrl;
					// Image cache: URL → PixelBuffer (or null for failed). Shared with RunLayout so
					// relayouts during and after loading reuse the downloaded images.
					imageCache = _imageCache ??= new Dictionary<string, Imaging.PixelBuffer?>();
				}
				if (capturedHtml == null)
				{
					_loadingStatus = null;
					return;
				}

				// Throttled re-layout: commit the partial image cache to the layout at most
				// once every ThrottleMs, so images pop in in batches as they arrive instead of
				// re-laying out the whole page once per image.
				const int ThrottleMs = 750;
				long lastCommitTicks = 0;
				bool htmlChanged = false;

				// First point at which a spinner is justified: there are remote images and they
				// will take time. Restart the animation, since nothing is driving it now.
				_loadingStatus = $"Loading images (0/{_imageLoadTotal})...";
				_ = AnimateLoadingSpinnerAsync(ct);
				Invalidate(Invalidation.Relayout);

				// Fetch a few images at a time (like a browser does per host; more gets
				// rate-limited by hosts such as Wikimedia). After each arrives, if the throttle
				// has elapsed, re-layout with whatever is cached so far. Missing images still
				// render as alt text (HtmlBlockFlow.ProcessImage handles partial caches cleanly).
				var gate = new SemaphoreSlim(HtmlConstants.MaxConcurrentImageFetches);
				var pending = imageUrls.Select(u => FetchImageAsync(u, gate, ct)).ToList();
				while (pending.Count > 0)
				{
					var finished = await Task.WhenAny(pending).ConfigureAwait(false);
					pending.Remove(finished);
					var (url, buffer) = await finished.ConfigureAwait(false);

					lock (_contentLock)
						imageCache[url] = buffer;

					_imageLoadCompleted++;
					_loadingStatus = $"Loading images ({_imageLoadCompleted}/{_imageLoadTotal})...";
					Invalidate(Invalidation.Relayout);

					// Throttled commit — only re-layout if ThrottleMs elapsed since last commit
					// and there are still images pending (the final commit after the loop
					// guarantees the last batch lands regardless of timing).
					long nowTicks = Environment.TickCount64;
					if (pending.Count > 0 && (nowTicks - lastCommitTicks) >= ThrottleMs)
					{
						if (!TryCommitPartialImageLayout(capturedHtml, capturedWidth, capturedBaseUrl, imageCache))
						{
							htmlChanged = true;
							break; // page was replaced under us — abort image loading
						}
						lastCommitTicks = nowTicks;
						Invalidate(Invalidation.Relayout);
					}
				}

				// Final commit: guarantees the last batch of images reaches the screen even if
				// they arrived inside the throttle window.
				if (!htmlChanged)
				{
					ct.ThrowIfCancellationRequested();
					_loadingStatus = "Rendering images...";
					Invalidate(Invalidation.Relayout);

					if (!TryCommitPartialImageLayout(capturedHtml, capturedWidth, capturedBaseUrl, imageCache))
						htmlChanged = true;
				}

				_loadingStatus = null;
				Invalidate(Invalidation.Relayout);
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
					Core.AsyncEvent.Raise(LoadingCompleted, LoadingCompletedAsync, this, EventArgs.Empty, Container?.GetConsoleWindowSystem?.LogService);
			}
		}

		/// <summary>
		/// Downloads and decodes one image, waiting on <paramref name="gate"/> for a fetch slot.
		/// Returns a null buffer for anything that isn't a decodable raster image; throws only
		/// when <paramref name="ct"/> is cancelled.
		/// </summary>
		private static async Task<(string Url, Imaging.PixelBuffer? Buffer)> FetchImageAsync(
			string url, SemaphoreSlim gate, CancellationToken ct)
		{
			await gate.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				using var response = await HtmlImageLoader.HttpClient.GetAsync(url, ct).ConfigureAwait(false);
				var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
				if (!response.IsSuccessStatusCode ||
					!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
					contentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
					return (url, null);

				var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
				using var stream = new System.IO.MemoryStream(bytes);
				return (url, Imaging.PixelBuffer.FromStream(stream));
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				throw;
			}
			catch
			{
				return (url, null);
			}
			finally
			{
				gate.Release();
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
				var bg = BackgroundColor; // theme-resolved (was Container?.BackgroundColor ?? Color.Black)
				_lastLayoutFg = fg; _lastLayoutBg = bg; _lastLayoutLinkColor = LinkColor;

				try
				{
					var gp = Container?.GetConsoleWindowSystem?.ConsoleDriver as Drivers.IGraphicsProtocol;
					_layoutResult = _layoutEngine.Layout(
						capturedHtml,
						capturedWidth, fg, bg,
						_blockSpacing, LinkColor, VisitedLinkColor,
						capturedBaseUrl,
						showImages: true,
						imageCache: imageCache,
						graphicsProtocol: gp);
				}
				catch
				{
					// Layout failed — keep the previous result rather than blanking the view.
				}

				InvalidateLinkCache();
			}
			return true;
		}
		#endregion
	}
}
