// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class MultilineEditControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int baseWidth = Width ?? constraints.MaxWidth - Margin.Left - Margin.Right;

			// When VerticalAlignment is Fill, use the max available height from constraints
			int effectiveViewport = VerticalAlignment == VerticalAlignment.Fill
				? Math.Max(_viewportHeight, constraints.MaxHeight - Margin.Top - Margin.Bottom)
				: _viewportHeight;
			int contentHeight = effectiveViewport;

			// Account for vertical scrollbar and gutter taking columns from content area
			bool needsVerticalScrollbar = _verticalScrollbarVisibility == ScrollbarVisibility.Always ||
				(_verticalScrollbarVisibility == ScrollbarVisibility.Auto &&
				 GetTotalWrappedLineCount() > effectiveViewport);
			int scrollbarWidth = needsVerticalScrollbar ? 1 : 0;
			int gutterWidth = GetGutterWidth();
			int contentWidth = baseWidth - scrollbarWidth - gutterWidth;

			// Account for horizontal scrollbar if needed (using content width after scrollbar and gutter)
			bool needsHorizontalScrollbar = _wrapMode == WrapMode.NoWrap &&
				(_horizontalScrollbarVisibility == ScrollbarVisibility.Always ||
				 (_horizontalScrollbarVisibility == ScrollbarVisibility.Auto &&
				  GetMaxLineLength() > contentWidth));
			if (needsHorizontalScrollbar)
			{
				contentHeight++; // extra row for horizontal scrollbar

				// Horizontal scrollbar reduces viewport by 1, which may trigger vertical scrollbar
				if (!needsVerticalScrollbar &&
					_verticalScrollbarVisibility == ScrollbarVisibility.Auto &&
					GetTotalWrappedLineCount() > effectiveViewport - 1)
				{
					needsVerticalScrollbar = true;
					scrollbarWidth = 1;
					contentWidth = baseWidth - scrollbarWidth - gutterWidth;
				}
			}

			int width = baseWidth + Margin.Left + Margin.Right;
			int height = contentHeight + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			// Snapshot collections under lock for thread safety
			List<string> linesSnapshot;
			List<IGutterRenderer> gutterSnapshot;
			lock (_contentLock)
			{
				linesSnapshot = _lines.ToList();
				gutterSnapshot = _gutterRenderers.ToList();
			}

			Color bgColor = _hasFocus ? FocusedBackgroundColor : BackgroundColor;
			Color fgColor = _hasFocus ? FocusedForegroundColor : ForegroundColor;
			Color selBgColor = SelectionBackgroundColor;
			Color selFgColor = SelectionForegroundColor;
			Color windowBgColor = Container?.BackgroundColor ?? defaultBg;
			var effectiveBg = Container?.HasGradientBackground == true ? Color.Transparent : windowBgColor;

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) { _skipUpdateScrollPositionsInRender = false; return; }

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// When VerticalAlignment is Fill, use the actual layout bounds instead of fixed viewport height
			int effectiveViewport = VerticalAlignment == VerticalAlignment.Fill
				? Math.Max(_viewportHeight, bounds.Height - Margin.Top - Margin.Bottom)
				: _viewportHeight;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);

			// Determine if scrollbars will be shown
			int gutterWidth = GetGutterWidth();

			bool needsVerticalScrollbar = _verticalScrollbarVisibility == ScrollbarVisibility.Always ||
				(_verticalScrollbarVisibility == ScrollbarVisibility.Auto &&
				 GetTotalWrappedLineCount() > effectiveViewport);

			int scrollbarWidth = needsVerticalScrollbar ? 1 : 0;
			int effectiveWidth = targetWidth - scrollbarWidth - gutterWidth;

			bool needsHorizontalScrollbar = _wrapMode == WrapMode.NoWrap &&
				(_horizontalScrollbarVisibility == ScrollbarVisibility.Always ||
				 (_horizontalScrollbarVisibility == ScrollbarVisibility.Auto &&
				  GetMaxLineLength() > effectiveWidth));

			// Horizontal scrollbar takes 1 row from viewport, which may trigger vertical scrollbar
			if (needsHorizontalScrollbar)
			{
				effectiveViewport = Math.Max(1, effectiveViewport - 1);

				if (!needsVerticalScrollbar &&
					_verticalScrollbarVisibility == ScrollbarVisibility.Auto &&
					GetTotalWrappedLineCount() > effectiveViewport)
				{
					needsVerticalScrollbar = true;
					scrollbarWidth = 1;
					effectiveWidth = targetWidth - scrollbarWidth - gutterWidth;
				}
			}

			_effectiveWidth = effectiveWidth;
			_effectiveViewportHeight = effectiveViewport;
			_needsHorizontalScrollbar = needsHorizontalScrollbar;
			_needsVerticalScrollbar = needsVerticalScrollbar;

			if (effectiveWidth <= 0) { _skipUpdateScrollPositionsInRender = false; return; }

			// Use shared wrapping infrastructure
			var wrappedLines = GetWrappedLines(effectiveWidth);

			// Find wrapped line with cursor and adjust scroll
			int wrappedLineWithCursor = (_wrapMode != WrapMode.NoWrap)
				? FindWrappedLineForCursor(wrappedLines)
				: _cursorY;

			if (wrappedLineWithCursor >= 0 && !_skipUpdateScrollPositionsInRender && !_scrollbarInteracted)
			{
				if (wrappedLineWithCursor < _verticalScrollOffset)
					_verticalScrollOffset = wrappedLineWithCursor;
				else if (wrappedLineWithCursor >= _verticalScrollOffset + effectiveViewport)
					_verticalScrollOffset = wrappedLineWithCursor - effectiveViewport + 1;
			}
			_skipUpdateScrollPositionsInRender = false;

			// Get selection bounds
			var (selStartX, selStartY, selEndX, selEndY) = GetOrderedSelectionBounds();

			// Paint visible lines
			int availableHeight = bounds.Height - Margin.Top - Margin.Bottom - (needsHorizontalScrollbar ? 1 : 0);
			int linesToPaint = Math.Min(effectiveViewport, availableHeight);

			// Determine if placeholder should be shown
			bool showPlaceholder = !string.IsNullOrEmpty(_placeholderText) && !_isEditing &&
				linesSnapshot.Count == 1 && linesSnapshot[0].Length == 0;

			// Pre-compute vertical scrollbar thumb position (avoid per-row recalculation)
			int vThumbHeight = 0, vThumbPos = 0;
			int totalWrappedLineCount = 0;
			if (needsVerticalScrollbar)
			{
				totalWrappedLineCount = GetTotalWrappedLineCount();
				// Reserve arrow positions so thumb never overlaps them
				int arrowSlots = effectiveViewport >= 3 ? 2 : 0;
				int thumbTrackHeight = effectiveViewport - arrowSlots;
				vThumbHeight = Math.Clamp((int)(thumbTrackHeight * ((double)effectiveViewport / Math.Max(1, totalWrappedLineCount))), 1, thumbTrackHeight);
				vThumbPos = arrowSlots > 0 ? 1 : 0; // start after top arrow
				if (totalWrappedLineCount > effectiveViewport)
				{
					int maxThumbPos = thumbTrackHeight - vThumbHeight;
					double scrollRatio = (double)_verticalScrollOffset / (totalWrappedLineCount - effectiveViewport);
					vThumbPos += Math.Min((int)Math.Round(maxThumbPos * scrollRatio), maxThumbPos);
				}
			}

			// Focus-aware scrollbar colors (matching ScrollablePanelControl)
			Color scrollbarBg = bgColor;
			Color activeThumbColor = _hasFocus ? Color.Cyan1 : Color.Grey;
			Color activeTrackColor = _hasFocus ? Color.Grey : Color.Grey23;

				int contentStartX = startX + gutterWidth;

			for (int i = 0; i < linesToPaint; i++)
			{
				int paintY = startY + i;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
				{
					// Fill left margin
					if (Margin.Left > 0)
					{
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, paintY, Margin.Left, 1), fgColor, effectiveBg);
					}

					int wrappedIndex = i + _verticalScrollOffset;

					// Render gutter via pluggable renderers
					if (gutterWidth > 0)
					{
						int gutterX = startX;
						int sourceLineIndex;
						bool isFirstSegment;
						bool isCursorLine;

						if (wrappedIndex < wrappedLines.Count)
						{
							var gwl = wrappedLines[wrappedIndex];
							sourceLineIndex = gwl.SourceLineIndex;
							isFirstSegment = gwl.SourceCharOffset == 0;
							isCursorLine = _highlightCurrentLine && _isEditing && gwl.SourceLineIndex == _cursorY;
						}
						else
						{
							sourceLineIndex = -1;
							isFirstSegment = false;
							isCursorLine = false;
						}

						for (int r = 0; r < gutterSnapshot.Count; r++)
						{
							int rWidth = gutterSnapshot[r].GetWidth(linesSnapshot.Count);
							if (rWidth <= 0) continue;
							var ctx = new GutterRenderContext
							{
								Buffer = buffer,
								X = gutterX,
								Y = paintY,
								SourceLineIndex = sourceLineIndex,
								IsFirstWrappedSegment = isFirstSegment,
								IsCursorLine = isCursorLine,
								HasFocus = _hasFocus,
								ForegroundColor = fgColor,
								BackgroundColor = bgColor,
								TotalLineCount = linesSnapshot.Count
							};
							gutterSnapshot[r].Render(in ctx, rWidth);
							gutterX += rWidth;
						}
					}

					if (wrappedIndex < wrappedLines.Count)
					{
						// Render placeholder text on first visible line when content is empty
						if (showPlaceholder && i == 0)
						{
							Color dimFg = Color.Grey;
							var placeholderCells = Parsing.MarkupParser.Parse(_placeholderText!, dimFg, bgColor);
							int phLen = placeholderCells.Count;

							for (int charPos = 0; charPos < effectiveWidth; charPos++)
							{
								int cellX = contentStartX + charPos;
								if (cellX >= clipRect.X && cellX < clipRect.Right)
								{
									if (charPos < phLen)
										buffer.SetCell(cellX, paintY, placeholderCells[charPos]);
									else
										buffer.SetNarrowCell(cellX, paintY, ' ', dimFg, bgColor);
								}
							}

							// Fill right margin and scrollbar area
							int rightMarginStart = contentStartX + effectiveWidth;
							int rightFill = bounds.Right - rightMarginStart;
							if (rightFill > 0)
								ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightMarginStart, paintY, rightFill, 1), fgColor, effectiveBg);

							continue;
						}

						var wl = wrappedLines[wrappedIndex];
						string line = wl.DisplayText;
						string visibleLine = line;

						// Apply horizontal scrolling (only in NoWrap mode)
						if (_wrapMode == WrapMode.NoWrap && _horizontalScrollOffset > 0)
						{
							if (_horizontalScrollOffset < line.Length)
								visibleLine = line.Substring(_horizontalScrollOffset);
							else
								visibleLine = string.Empty;
						}

						int hScrollForCalc = (_wrapMode == WrapMode.NoWrap) ? _horizontalScrollOffset : 0;

						// Determine current line highlight (custom line highlights take precedence)
						bool isCurrentLine = _highlightCurrentLine && _isEditing && wl.SourceLineIndex == _cursorY;
						Color lineBg;
						if (_lineHighlights.TryGetValue(wl.SourceLineIndex, out var customLineBg))
							lineBg = customLineBg;
						else if (isCurrentLine)
							lineBg = CurrentLineHighlightColor;
						else
							lineBg = bgColor;

						// Paint each character with selection, syntax, and whitespace handling
						// Use Rune-aware iteration to properly handle surrogate pairs,
						// wide characters, and zero-width combining/variation selectors
						int col = 0;        // display column
						int srcCharIdx = 0; // char index into visibleLine
						Rune? lastRenderedRune = null; // track for VS16 widening

						while (col < effectiveWidth)
						{
							Rune rune;
							int runeCharLen;
							int runeDisplayWidth;
							bool isContentChar;
							int actualCharPos;

							if (srcCharIdx < visibleLine.Length)
							{
								// Decode a Rune from the string (handles surrogate pairs)
								if (char.IsHighSurrogate(visibleLine[srcCharIdx]) &&
									srcCharIdx + 1 < visibleLine.Length &&
									char.IsLowSurrogate(visibleLine[srcCharIdx + 1]))
								{
									rune = new Rune(visibleLine[srcCharIdx], visibleLine[srcCharIdx + 1]);
									runeCharLen = 2;
								}
								else if (char.IsSurrogate(visibleLine[srcCharIdx]))
								{
									rune = new Rune('\uFFFD');
									runeCharLen = 1;
								}
								else
								{
									rune = new Rune(visibleLine[srcCharIdx]);
									runeCharLen = 1;
								}

								runeDisplayWidth = UnicodeWidth.GetRuneWidth(rune);
								isContentChar = true;
								actualCharPos = srcCharIdx + wl.SourceCharOffset + hScrollForCalc;
								srcCharIdx += runeCharLen;

								// Handle zero-width characters (combining marks, variation selectors)
								if (runeDisplayWidth == 0)
								{
									if (col > 0 && UnicodeWidth.IsVS16(rune) &&
										lastRenderedRune.HasValue &&
										UnicodeWidth.IsVs16Widened(lastRenderedRune.Value) &&
										!UnicodeWidth.IsWideRune(lastRenderedRune.Value))
									{
										// VS16 widens previous character from 1→2 columns
										int prevCellX = contentStartX + col - 1;
										if (prevCellX >= clipRect.X && prevCellX < clipRect.Right)
										{
											var prev = buffer.GetCell(prevCellX, paintY);
											prev.AppendCombiner(rune);
											buffer.SetCell(prevCellX, paintY, prev);
										}
										// Place continuation cell at current column
										int cellX = contentStartX + col;
										if (cellX >= clipRect.X && cellX < clipRect.Right)
										{
											var prev = buffer.GetCell(contentStartX + col - 1, paintY);
											buffer.SetCell(cellX, paintY, new Cell(' ', prev.Foreground, prev.Background) { IsWideContinuation = true });
										}
										lastRenderedRune = null;
										col++;
									}
									else if (col > 0)
									{
										// Regular combining mark — attach to previous cell
										int prevCellX = contentStartX + col - 1;
										if (prevCellX >= clipRect.X && prevCellX < clipRect.Right)
										{
											var prev = buffer.GetCell(prevCellX, paintY);
											prev.AppendCombiner(rune);
											buffer.SetCell(prevCellX, paintY, prev);
										}
									}
									// Don't advance col for zero-width chars (except VS16 widening above)
									continue;
								}
							}
							else
							{
								rune = new Rune(' ');
								runeDisplayWidth = 1;
								isContentChar = false;
								actualCharPos = col + wl.SourceCharOffset + hScrollForCalc;
							}

							// Selection check
							bool isSelected = false;
							if (_hasSelection && wl.SourceLineIndex >= selStartY && wl.SourceLineIndex <= selEndY)
							{
								if (wl.SourceLineIndex == selStartY && wl.SourceLineIndex == selEndY)
									isSelected = actualCharPos >= selStartX && actualCharPos < selEndX;
								else if (wl.SourceLineIndex == selStartY)
									isSelected = actualCharPos >= selStartX;
								else if (wl.SourceLineIndex == selEndY)
									isSelected = actualCharPos < selEndX;
								else
									isSelected = true;
							}

							// Color priority: Selection > Search Match > Syntax > Visible whitespace > Default
							Color charFg;
							Color charBg;
							if (isSelected)
							{
								charFg = selFgColor;
								charBg = selBgColor;
							}
							else
							{
								// Check search match highlighting
								var (isSearchMatch, isCurrentSearchMatch) = GetSearchMatchState(wl.SourceLineIndex, actualCharPos);
								if (isSearchMatch)
								{
									charBg = isCurrentSearchMatch ? Color.Orange1 : Color.Yellow;
									charFg = Color.Black;
								}
								else
								{
									charBg = lineBg;

									if (_showWhitespace && rune.Value == ' ' && isContentChar)
									{
										rune = new Rune(ControlDefaults.WhitespaceSpaceChar);
										charFg = Color.Grey37;
									}
									else if (_syntaxHighlighter != null)
									{
										charFg = GetSyntaxColor(wl.SourceLineIndex, actualCharPos, fgColor);
									}
									else
									{
										charFg = fgColor;
									}
								}
							}

							int cellX2 = contentStartX + col;

							if (runeDisplayWidth == 2 && col + 1 < effectiveWidth)
							{
								// Wide character — write base cell + continuation cell
								if (cellX2 >= clipRect.X && cellX2 < clipRect.Right)
									buffer.SetCell(cellX2, paintY, new Cell(rune, charFg, charBg));
								if (cellX2 + 1 >= clipRect.X && cellX2 + 1 < clipRect.Right)
									buffer.SetCell(cellX2 + 1, paintY, new Cell(' ', charFg, charBg) { IsWideContinuation = true });
								col += 2;
							}
							else if (runeDisplayWidth == 2)
							{
								// Wide character at right edge — can't fit, show space
								if (cellX2 >= clipRect.X && cellX2 < clipRect.Right)
									buffer.SetNarrowCell(cellX2, paintY, ' ', charFg, charBg);
								col++;
							}
							else
							{
								// Narrow character (BMP or non-BMP single-width)
								if (cellX2 >= clipRect.X && cellX2 < clipRect.Right)
									buffer.SetNarrowCell(cellX2, paintY, rune, charFg, charBg);
								col++;
							}

							lastRenderedRune = rune;
						}
					}
					else
					{
						// Empty line beyond content
						buffer.FillRect(new LayoutRect(contentStartX, paintY, effectiveWidth, 1), ' ', fgColor, bgColor);
					}

					// Paint vertical scrollbar
					if (needsVerticalScrollbar)
					{
						int scrollX = contentStartX + effectiveWidth;
						if (scrollX >= clipRect.X && scrollX < clipRect.Right)
						{
							char scrollChar;
							Color scrollFg;
							if (i == 0)
							{
								scrollChar = '▲';
								scrollFg = activeThumbColor;
							}
							else if (i == effectiveViewport - 1)
							{
								scrollChar = '▼';
								scrollFg = activeThumbColor;
							}
							else if (i >= vThumbPos && i < vThumbPos + vThumbHeight)
							{
								scrollChar = '█';
								scrollFg = activeThumbColor;
							}
							else
							{
								scrollChar = '│';
								scrollFg = activeTrackColor;
							}
							buffer.SetNarrowCell(scrollX, paintY, scrollChar, scrollFg, scrollbarBg);
						}
					}

					// Fill right margin
					if (Margin.Right > 0)
					{
						int rightMarginX = contentStartX + effectiveWidth + scrollbarWidth;
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightMarginX, paintY, Margin.Right, 1), fgColor, effectiveBg);
					}
				}
			}

			// Fill remaining viewport height with empty lines
			for (int i = linesToPaint; i < effectiveViewport && startY + i < bounds.Bottom; i++)
			{
				int paintY = startY + i;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
				{
					if (Margin.Left > 0)
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, paintY, Margin.Left, 1), fgColor, effectiveBg);

					// Render gutter for empty rows via pluggable renderers
					if (gutterWidth > 0)
					{
						int gutterX = startX;
						for (int r = 0; r < gutterSnapshot.Count; r++)
						{
							int rWidth = gutterSnapshot[r].GetWidth(linesSnapshot.Count);
							if (rWidth <= 0) continue;
							var ctx = new GutterRenderContext
							{
								Buffer = buffer,
								X = gutterX,
								Y = paintY,
								SourceLineIndex = -1,
								IsFirstWrappedSegment = false,
								IsCursorLine = false,
								HasFocus = _hasFocus,
								ForegroundColor = fgColor,
								BackgroundColor = bgColor,
								TotalLineCount = linesSnapshot.Count
							};
							gutterSnapshot[r].Render(in ctx, rWidth);
							gutterX += rWidth;
						}
					}

					buffer.FillRect(new LayoutRect(contentStartX, paintY, effectiveWidth, 1), ' ', fgColor, bgColor);

					if (needsVerticalScrollbar)
					{
						int scrollX = contentStartX + effectiveWidth;
						if (scrollX >= clipRect.X && scrollX < clipRect.Right)
						{
							char scrollChar;
							Color scrollFg;
							if (i == 0)
							{
								scrollChar = '▲';
								scrollFg = activeThumbColor;
							}
							else if (i == effectiveViewport - 1)
							{
								scrollChar = '▼';
								scrollFg = activeThumbColor;
							}
							else if (i >= vThumbPos && i < vThumbPos + vThumbHeight)
							{
								scrollChar = '█';
								scrollFg = activeThumbColor;
							}
							else
							{
								scrollChar = '│';
								scrollFg = activeTrackColor;
							}
							buffer.SetNarrowCell(scrollX, paintY, scrollChar, scrollFg, scrollbarBg);
						}
					}

					if (Margin.Right > 0)
					{
						int rightMarginX = contentStartX + effectiveWidth + scrollbarWidth;
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightMarginX, paintY, Margin.Right, 1), fgColor, effectiveBg);
					}
				}
			}

			// Paint horizontal scrollbar
			if (needsHorizontalScrollbar)
			{
				int scrollY = startY + effectiveViewport;
				if (scrollY >= clipRect.Y && scrollY < clipRect.Bottom && scrollY < bounds.Bottom)
				{
					if (Margin.Left > 0)
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, scrollY, Margin.Left, 1), fgColor, effectiveBg);

					// Fill gutter area under horizontal scrollbar
					if (gutterWidth > 0)
						buffer.FillRect(new LayoutRect(startX, scrollY, gutterWidth, 1), ' ', fgColor, bgColor);

					int maxLineLength = GetMaxLineLength();
					int hMaxScroll = Math.Max(0, maxLineLength - effectiveWidth);
					// Reserve arrow positions so thumb never overlaps them
					int hArrowSlots = effectiveWidth >= 3 ? 2 : 0;
					int hThumbTrackWidth = effectiveWidth - hArrowSlots;
					int thumbWidth = Math.Clamp((hThumbTrackWidth * hThumbTrackWidth) / Math.Max(1, maxLineLength), 1, hThumbTrackWidth);
					int thumbPos = hArrowSlots > 0 ? 1 : 0;
					if (maxLineLength > effectiveWidth)
					{
						int maxThumbPos = hThumbTrackWidth - thumbWidth;
						thumbPos += Math.Min((int)Math.Round((double)_horizontalScrollOffset / (maxLineLength - effectiveWidth) * maxThumbPos), maxThumbPos);
					}

					for (int x = 0; x < effectiveWidth; x++)
					{
						int cellX = contentStartX + x;
						if (cellX >= clipRect.X && cellX < clipRect.Right)
						{
							char scrollChar;
							Color scrollFg;
							if (x == 0)
							{
								scrollChar = '◄';
								scrollFg = activeThumbColor;
							}
							else if (x == effectiveWidth - 1)
							{
								scrollChar = '►';
								scrollFg = activeThumbColor;
							}
							else if (x >= thumbPos && x < thumbPos + thumbWidth)
							{
								scrollChar = '▬';
								scrollFg = activeThumbColor;
							}
							else
							{
								scrollChar = '─';
								scrollFg = activeTrackColor;
							}
							buffer.SetNarrowCell(cellX, scrollY, scrollChar, scrollFg, scrollbarBg);
						}
					}

					if (needsVerticalScrollbar)
					{
						int cornerX = contentStartX + effectiveWidth;
						if (cornerX >= clipRect.X && cornerX < clipRect.Right)
						{
							buffer.SetNarrowCell(cornerX, scrollY, '┘', activeTrackColor, scrollbarBg);
						}
					}

					if (Margin.Right > 0)
					{
						int rightMarginX = contentStartX + effectiveWidth + scrollbarWidth;
						ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightMarginX, scrollY, Margin.Right, 1), fgColor, effectiveBg);
					}
				}
			}

			// Render editing mode hint overlay at bottom-right of viewport
			if (_showEditingHints && _hasFocus)
			{
				string hintText = _isEditing
					? ControlDefaults.EditingModeHint
					: ControlDefaults.BrowseModeHint;

				int hintDisplayWidth = UnicodeWidth.GetStringWidth(hintText);
				int lastVisibleRow = startY + linesToPaint - 1;
				if (hintDisplayWidth <= effectiveWidth && lastVisibleRow >= clipRect.Y && lastVisibleRow < clipRect.Bottom)
				{
					int hintStartX = contentStartX + effectiveWidth - hintDisplayWidth;
					Color hintFg = Color.Grey50;
					Color hintBg = bgColor;

					var hintCells = Parsing.MarkupParser.Parse(hintText, hintFg, hintBg);
					for (int c = 0; c < hintCells.Count; c++)
					{
						int cellX = hintStartX + c;
						if (cellX >= clipRect.X && cellX < clipRect.Right)
							buffer.SetCell(cellX, lastVisibleRow, hintCells[c]);
					}
				}
			}

			// Fill bottom margin
			int contentEndY = startY + effectiveViewport + (needsHorizontalScrollbar ? 1 : 0);
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, contentEndY, fgColor, effectiveBg);
		}

		private int GetGutterWidth()
		{
			lock (_contentLock)
			{
				if (_gutterRenderers.Count == 0) return 0;
				int total = 0;
				for (int r = 0; r < _gutterRenderers.Count; r++)
					total += _gutterRenderers[r].GetWidth(_lines.Count);
				return total;
			}
		}

		/// <summary>
		/// Computes vertical scrollbar geometry for hit-testing and rendering.
		/// Returns (trackHeight, thumbY, thumbHeight) relative to the viewport top.
		/// </summary>
		private (int trackHeight, int thumbY, int thumbHeight) GetVerticalScrollbarGeometry()
		{
			int effectiveViewport = _effectiveViewportHeight > 0 ? _effectiveViewportHeight : GetEffectiveViewportHeight();
			int totalLines = GetTotalWrappedLineCount();
			// Reserve arrow positions so thumb never overlaps them
			int arrowSlots = effectiveViewport >= 3 ? 2 : 0;
			int thumbTrackHeight = effectiveViewport - arrowSlots;
			int thumbHeight = Math.Clamp((int)(thumbTrackHeight * ((double)effectiveViewport / Math.Max(1, totalLines))), 1, thumbTrackHeight);
			int thumbY = arrowSlots > 0 ? 1 : 0;
			if (totalLines > effectiveViewport)
			{
				int maxThumbPos = thumbTrackHeight - thumbHeight;
				thumbY += Math.Min((int)Math.Round((double)_verticalScrollOffset / (totalLines - effectiveViewport) * maxThumbPos), maxThumbPos);
			}
			return (effectiveViewport, thumbY, thumbHeight);
		}

		/// <summary>
		/// Computes horizontal scrollbar geometry for hit-testing and rendering.
		/// Returns (trackWidth, thumbX, thumbWidth) relative to the content start.
		/// </summary>
		private (int trackWidth, int thumbX, int thumbWidth) GetHorizontalScrollbarGeometry()
		{
			int maxLineLength = GetMaxLineLength();
			// Reserve arrow positions so thumb never overlaps them
			int arrowSlots = _effectiveWidth >= 3 ? 2 : 0;
			int thumbTrackWidth = _effectiveWidth - arrowSlots;
			int thumbWidth = Math.Clamp((thumbTrackWidth * thumbTrackWidth) / Math.Max(1, maxLineLength), 1, thumbTrackWidth);
			int thumbX = arrowSlots > 0 ? 1 : 0;
			if (maxLineLength > _effectiveWidth)
			{
				int maxThumbPos = thumbTrackWidth - thumbWidth;
				thumbX += Math.Min((int)Math.Round((double)_horizontalScrollOffset / (maxLineLength - _effectiveWidth) * maxThumbPos), maxThumbPos);
			}
			return (_effectiveWidth, thumbX, thumbWidth);
		}

		private Color GetSyntaxColor(int lineIndex, int charIndex, Color defaultColor)
		{
			if (_syntaxHighlighter == null) return defaultColor;
			var tokens = GetOrComputeTokens(lineIndex);
			foreach (var token in tokens)
				if (charIndex >= token.StartIndex && charIndex < token.StartIndex + token.Length)
					return token.ForegroundColor;
			return defaultColor;
		}

		private IReadOnlyList<SyntaxToken> GetOrComputeTokens(int lineIndex)
		{
			lock (_contentLock)
			{
				_syntaxTokenCache ??= new Dictionary<int, IReadOnlyList<SyntaxToken>>();
				_lineStateCache   ??= new Dictionary<int, SyntaxLineState>();

				if (_syntaxTokenCache.TryGetValue(lineIndex, out var cached))
					return cached;
			}

			EnsureStateUpToLine(lineIndex);

			lock (_contentLock)
			{
				return _syntaxTokenCache != null && _syntaxTokenCache.TryGetValue(lineIndex, out var result)
					? result : Array.Empty<SyntaxToken>();
			}
		}

		private void EnsureStateUpToLine(int lineIndex)
		{
			lock (_contentLock)
			{
				_syntaxTokenCache ??= new Dictionary<int, IReadOnlyList<SyntaxToken>>();
				_lineStateCache   ??= new Dictionary<int, SyntaxLineState>();

				// Find the furthest line whose start-state is already known
				int startFrom = lineIndex;
				while (startFrom > 0 && !_lineStateCache.ContainsKey(startFrom))
					startFrom--;

				for (int i = startFrom; i <= lineIndex; i++)
				{
					if (_syntaxTokenCache.ContainsKey(i))
						continue; // tokens already computed; end-state is stored as cache[i+1]

					var startState = (i == 0)
						? SyntaxLineState.Initial
						: (_lineStateCache.TryGetValue(i, out var s) ? s : SyntaxLineState.Initial);

					var lineText = i < _lines.Count ? _lines[i] : string.Empty;
					var (tokens, endState) = _syntaxHighlighter!.Tokenize(lineText, i, startState);

					_syntaxTokenCache[i]   = tokens;
					_lineStateCache[i + 1] = endState; // state at the START of the next line
				}
			}
		}

		#endregion
	}
}
