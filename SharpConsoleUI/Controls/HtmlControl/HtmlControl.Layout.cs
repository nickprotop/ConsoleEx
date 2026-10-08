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
	/// Turning HTML into laid-out lines, and the link bookkeeping that depends on that layout.
	/// </summary>
	/// <remarks>
	/// The link cache lives here rather than with the keyboard or mouse code that reads it,
	/// because what invalidates it is a re-layout: link positions are a product of layout, so
	/// every path that re-runs layout has to drop them, and keeping the two together is what
	/// makes that hard to forget.
	/// </remarks>
	public partial class HtmlControl
	{
		#region Layout and links


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
			var bg = BackgroundColor; // theme-resolved (was Container?.BackgroundColor ?? Color.Black)
			_lastLayoutFg = fg; _lastLayoutBg = bg; _lastLayoutLinkColor = LinkColor;

			try
			{
				_layoutResult = _layoutEngine.Layout(
					_rawHtml, width, fg, bg, _blockSpacing,
					LinkColor, VisitedLinkColor, _baseUrl,
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
			var bg = BackgroundColor; // theme-resolved (was Container?.BackgroundColor ?? Color.Black)
			_lastLayoutFg = fg; _lastLayoutBg = bg; _lastLayoutLinkColor = LinkColor;

			try
			{
				var gp = Container?.GetConsoleWindowSystem?.ConsoleDriver as Drivers.IGraphicsProtocol;
				_layoutResult = _layoutEngine.Layout(
					_rawHtml,
					width,
					fg,
					bg,
					_blockSpacing,
					LinkColor,
					VisitedLinkColor,
					_baseUrl,
					_showImages,
					// For a URL load, the images downloaded so far — never re-download them
					// synchronously on a resize, theme switch or CSS reload.
					imageCache: _imageCache,
					graphicsProtocol: gp);
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
				Invalidate(Invalidation.Repaint);
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
