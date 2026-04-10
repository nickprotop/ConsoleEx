// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Html;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace SharpConsoleUI.Controls
{
	public partial class HtmlControl
	{
		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// Use a sane width for measurement:
			// - If Width is explicitly set, use it.
			// - If constraints have a bounded MaxWidth, use that.
			// - Otherwise, use the currently-cached layout width (or a default).
			// Critically: do NOT re-layout at unbounded constraints.MaxWidth (which can be int.MaxValue).
			int targetWidth;
			if (Width.HasValue)
			{
				targetWidth = Width.Value;
			}
			else if (constraints.MaxWidth < 10000)
			{
				targetWidth = constraints.MaxWidth;
			}
			else
			{
				// Unbounded measure — return the current layout size without re-laying out
				targetWidth = _lastLayoutWidth > 0 ? _lastLayoutWidth + Margin.Left + Margin.Right : 80;
			}

			int contentWidth = targetWidth - Margin.Left - Margin.Right;
			if (contentWidth < 1) contentWidth = 1;

			int layoutWidth = ComputeLayoutWidth(contentWidth);

			// Re-layout only if width actually changed AND we have a real width (not huge)
			if (layoutWidth != _lastLayoutWidth && _rawHtml != null && layoutWidth < 10000)
			{
				// Debounce: defer relayout during rapid width changes (e.g., window resize).
				// The cached DOM parse means re-flow is cheaper, but for large documents
				// even the flow pass can be expensive. Schedule relayout after 150ms idle.
				_pendingLayoutWidth = layoutWidth;
				if (_resizeDebounceTimer == null)
				{
					_resizeDebounceTimer = new System.Timers.Timer(150) { AutoReset = false };
					_resizeDebounceTimer.Elapsed += (_, _) =>
					{
						lock (_contentLock)
						{
							if (_pendingLayoutWidth > 0 && _pendingLayoutWidth != _lastLayoutWidth)
								RunLayout(_pendingLayoutWidth);
						}
						Container?.Invalidate(true);
					};
				}
				_resizeDebounceTimer.Stop();
				_resizeDebounceTimer.Start();
			}

			int measuredWidth = targetWidth;

			int measuredHeight;
			if (Height.HasValue)
			{
				measuredHeight = Height.Value;
			}
			else
			{
				measuredHeight = _layoutResult.TotalHeight + Margin.Top + Margin.Bottom;
			}

			return new LayoutSize(
				Math.Clamp(measuredWidth, Math.Min(constraints.MinWidth, constraints.MaxWidth), constraints.MaxWidth),
				Math.Clamp(measuredHeight, Math.Min(constraints.MinHeight, constraints.MaxHeight), constraints.MaxHeight));
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultForeground, Color defaultBackground)
		{
			SetActualBounds(bounds);

			var bg = Container?.BackgroundColor ?? defaultBackground;
			var fg = ForegroundColor;

			int contentWidth = bounds.Width - Margin.Left - Margin.Right;
			if (contentWidth < 1) return;

			int viewportHeight = bounds.Height - Margin.Top - Margin.Bottom;
			if (viewportHeight < 1) return;

			// Use the same layout width formula as MeasureDOM to avoid relayout ping-pong
			// between measure and paint on large documents (see ComputeLayoutWidth).
			int layoutWidth = ComputeLayoutWidth(contentWidth);

			// Note: relayout on width change is handled by MeasureDOM with debouncing.
			// PaintDOM renders from the current (possibly stale) layout snapshot.

			int contentAreaX = bounds.X + Margin.Left;
			int contentAreaY = bounds.Y + Margin.Top;

			// Read layout result (struct read — no lock needed for performance; background
			// image loader uses atomic struct assignment so at worst we render an old snapshot)
			var linesSnapshot = _layoutResult.Lines;
			int totalHeight = _layoutResult.TotalHeight;

			// Render loading state
			if (_isLoading && totalHeight == 0)
			{
				string text = _loadingStatus ?? _loadingText;
				int textX = contentAreaX + Math.Max(0, (contentWidth - text.Length) / 2);
				int textY = contentAreaY + viewportHeight / 2;
				for (int i = 0; i < text.Length && textX + i < contentAreaX + contentWidth; i++)
				{
					buffer.SetNarrowCell(textX + i, textY, text[i], fg, bg);
				}
				return;
			}

			// Determine scrollbar visibility
			bool needsScrollbar = _scrollbarVisibility switch
			{
				ScrollbarVisibility.Always => true,
				ScrollbarVisibility.Never => false,
				_ => totalHeight > viewportHeight // Auto
			};

			int scrollbarWidth = needsScrollbar ? 1 : 0;
			int renderWidth = contentWidth - scrollbarWidth;
			if (renderWidth < 1) renderWidth = 1;

			// Clamp scroll offset
			int maxScroll = Math.Max(0, totalHeight - viewportHeight);
			if (_scrollOffset > maxScroll) _scrollOffset = maxScroll;

			// Clear content area before rendering (prevents stale content when scrolling)
			// Use Black as fallback if bg is transparent to ensure cells are actually cleared
			// (FillRect uses alpha blending; a transparent bg won't replace existing content)
			var clearBg = bg.A == 0 ? Color.Black : bg;
			buffer.FillRect(new LayoutRect(contentAreaX, contentAreaY, contentWidth, viewportHeight), ' ', fg, clearBg);

			// Compute a tight clip rect: intersection of passed clipRect and our content area
			// This is critical — we must NEVER write outside our own bounds regardless of what clipRect says
			int tightX = Math.Max(contentAreaX, clipRect.X);
			int tightY = Math.Max(contentAreaY, clipRect.Y);
			int tightRight = Math.Min(contentAreaX + contentWidth, clipRect.Right);
			int tightBottom = Math.Min(contentAreaY + viewportHeight, clipRect.Bottom);
			var tightClip = (tightRight > tightX && tightBottom > tightY)
				? new LayoutRect(tightX, tightY, tightRight - tightX, tightBottom - tightY)
				: new LayoutRect(contentAreaX, contentAreaY, 0, 0);

			// Render visible layout lines from snapshot
			if (linesSnapshot != null)
			{
				for (int i = 0; i < linesSnapshot.Length; i++)
				{
					ref var line = ref linesSnapshot[i];
					if (line.Y < _scrollOffset) continue;
					if (line.Y >= _scrollOffset + viewportHeight) continue;

					int screenY = contentAreaY + (line.Y - _scrollOffset);
					int screenX = contentAreaX + line.X;

					// Apply alignment offset
					if (line.Alignment == TextAlignment.Center && line.Width < renderWidth)
					{
						screenX += (renderWidth - line.Width) / 2;
					}
					else if (line.Alignment == TextAlignment.Right && line.Width < renderWidth)
					{
						screenX += renderWidth - line.Width;
					}

					buffer.WriteCellsClipped(screenX, screenY, line.Cells, tightClip);

					// Highlight links on this line (focused and hovered)
					if (line.Links != null)
					{
						HighlightLinksOnLine(buffer, i, ref line, screenX, screenY, tightClip);
					}
				}
			}

			// Draw scrollbar
			if (needsScrollbar)
			{
				int scrollbarX = contentAreaX + contentWidth - 1;
				var thumbColor = HasFocus ? Color.Cyan1 : Color.Grey;
				var trackColor = HasFocus ? Color.Grey : Color.Grey23;

				ScrollbarHelper.DrawVerticalScrollbar(
					buffer, scrollbarX, contentAreaY, viewportHeight,
					totalHeight, viewportHeight, _scrollOffset,
					thumbColor, trackColor, bg);
			}

			// Loading overlay: paint a top banner while anything is loading. During navigation
			// we also dim the previous page (it's stale and about to be replaced); during
			// background image loading we leave content untouched so the user can keep reading.
			if (_isNavigating)
			{
				DrawLoadingOverlay(buffer, contentAreaX, contentAreaY, contentWidth, viewportHeight, dim: true);
			}
			else if (_loadingStatus != null)
			{
				DrawLoadingOverlay(buffer, contentAreaX, contentAreaY, contentWidth, viewportHeight, dim: false);
			}
		}

		/// <summary>
		/// Paints the loading banner across the top row of the content area and, if
		/// <paramref name="dim"/> is true, applies a half-alpha black wash over the rest of
		/// the content so stale page content reads as inactive. Shared between navigation
		/// and background image loading — same visual language, different intensity.
		/// </summary>
		private void DrawLoadingOverlay(CharacterBuffer buffer, int areaX, int areaY, int areaW, int areaH, bool dim)
		{
			if (areaW < 1 || areaH < 1) return;

			if (dim)
			{
				// Dim pass: blend every cell's fg and bg with 50% black. Preserves characters
				// so the user can still see the previous page structure behind the banner.
				var veil = new Color(0, 0, 0, 128);
				for (int y = areaY; y < areaY + areaH; y++)
				{
					for (int x = areaX; x < areaX + areaW; x++)
					{
						var c = buffer.GetCell(x, y);
						var newBg = Color.Blend(veil, c.Background);
						var newFg = Color.Blend(veil, c.Foreground);
						buffer.SetCellColors(x, y, newFg, newBg);
					}
				}
			}

			// Spinner animation: advance one frame per paint.
			_spinnerTick++;
			const string spinnerFrames = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";
			var spinnerRunes = spinnerFrames.EnumerateRunes().ToArray();
			var spinner = spinnerRunes[(_spinnerTick / 2) % spinnerRunes.Length];

			// Build banner text: spinner + status, truncated to fit area width.
			var status = _loadingStatus ?? "Loading...";
			var bannerText = $" {spinner} {status} ";
			var bannerBg = new Color(20, 40, 80);
			var bannerFg = Color.White;

			// Paint banner background across the full top row (provides a solid bar even
			// if status is short), then write the text.
			for (int x = areaX; x < areaX + areaW; x++)
			{
				buffer.SetNarrowCell(x, areaY, ' ', bannerFg, bannerBg);
			}

			int col = areaX;
			foreach (var rune in bannerText.EnumerateRunes())
			{
				int rw = Helpers.UnicodeWidth.GetRuneWidth(rune);
				if (rw <= 0) continue;
				if (col + rw > areaX + areaW) break;
				if (rw == 2)
				{
					buffer.SetCell(col, areaY, new Cell(rune, bannerFg, bannerBg));
					buffer.SetCell(col + 1, areaY, new Cell(' ', bannerFg, bannerBg) { IsWideContinuation = true });
					col += 2;
				}
				else
				{
					buffer.SetNarrowCell(col, areaY, rune, bannerFg, bannerBg);
					col++;
				}
			}
		}
		/// <summary>
		/// Highlights focused and hovered links on a rendered line by overriding cell colors.
		/// </summary>
		private void HighlightLinksOnLine(CharacterBuffer buffer, int lineIndex, ref LayoutLine line,
			int screenX, int screenY, LayoutRect clipRect)
		{
			int focusedLinkId = GetFocusedLinkId();

			for (int j = 0; j < line.Links!.Length; j++)
			{
				ref var link = ref line.Links[j];

				// Check if this segment belongs to the focused link (by LinkId — covers all segments)
				bool isFocused = focusedLinkId >= 0 && link.LinkId == focusedLinkId;
				bool isHovered = lineIndex == _hoveredLinkLineIndex && j == _hoveredLinkIndex;

				if (!isFocused && !isHovered) continue;

				// Apply highlight colors
				for (int x = link.StartX; x < link.EndX; x++)
				{
					int cellX = screenX + x;
					if (cellX < clipRect.X || cellX >= clipRect.Right) continue;
					if (screenY < clipRect.Y || screenY >= clipRect.Bottom) continue;

					var cell = buffer.GetCell(cellX, screenY);
					if (isFocused)
					{
						// Focused link: inverted colors for clear visibility
						buffer.SetCellColors(cellX, screenY, cell.Background, cell.Foreground);
					}
					else if (isHovered)
					{
						// Hovered link: subtle underline-like highlight (brighter foreground)
						var brightFg = new Color(
							(byte)Math.Min(255, cell.Foreground.R + 60),
							(byte)Math.Min(255, cell.Foreground.G + 60),
							(byte)Math.Min(255, cell.Foreground.B + 60));
						buffer.SetCellColors(cellX, screenY, brightFg, cell.Background);
					}
				}
			}
		}
	}
}
