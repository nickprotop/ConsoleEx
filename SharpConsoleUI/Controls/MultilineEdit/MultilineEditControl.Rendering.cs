// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	public partial class MultilineEditControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int baseWidth = _width ?? constraints.MaxWidth - _margin.Left - _margin.Right;

			// When VerticalAlignment is Fill, use the max available height from constraints
			int effectiveViewport = _verticalAlignment == VerticalAlignment.Fill
				? Math.Max(_viewportHeight, constraints.MaxHeight - _margin.Top - _margin.Bottom)
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

			int width = baseWidth + _margin.Left + _margin.Right;
			int height = contentHeight + _margin.Top + _margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			Color bgColor = _hasFocus ? FocusedBackgroundColor : BackgroundColor;
			Color fgColor = _hasFocus ? FocusedForegroundColor : ForegroundColor;
			Color selBgColor = SelectionBackgroundColor;
			Color selFgColor = SelectionForegroundColor;
			Color windowBgColor = Container?.BackgroundColor ?? defaultBg;

			int targetWidth = bounds.Width - _margin.Left - _margin.Right;
			if (targetWidth <= 0) { _skipUpdateScrollPositionsInRender = false; return; }

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// When VerticalAlignment is Fill, use the actual layout bounds instead of fixed viewport height
			int effectiveViewport = _verticalAlignment == VerticalAlignment.Fill
				? Math.Max(_viewportHeight, bounds.Height - _margin.Top - _margin.Bottom)
				: _viewportHeight;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, windowBgColor);

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
			int availableHeight = bounds.Height - _margin.Top - _margin.Bottom - (needsHorizontalScrollbar ? 1 : 0);
			int linesToPaint = Math.Min(effectiveViewport, availableHeight);

			// Determine if placeholder should be shown
			bool showPlaceholder = !string.IsNullOrEmpty(_placeholderText) && !_isEditing &&
				_lines.Count == 1 && _lines[0].Length == 0;

			// Pre-compute vertical scrollbar thumb position (avoid per-row recalculation)
			int vThumbHeight = 0, vThumbPos = 0;
			int totalWrappedLineCount = 0;
			if (needsVerticalScrollbar)
			{
				totalWrappedLineCount = GetTotalWrappedLineCount();
				vThumbHeight = Math.Max(1, (effectiveViewport * effectiveViewport) / Math.Max(1, totalWrappedLineCount));
				int maxThumbPos = effectiveViewport - vThumbHeight;
				vThumbPos = totalWrappedLineCount > effectiveViewport
					? (int)Math.Round((double)_verticalScrollOffset / (totalWrappedLineCount - effectiveViewport) * maxThumbPos)
					: 0;
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
					if (_margin.Left > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, windowBgColor);
					}

					int wrappedIndex = i + _verticalScrollOffset;

					// Render gutter (line numbers)
					if (gutterWidth > 0)
					{
						Color gutterBg = bgColor;
						Color gutterFg = LineNumberColor;
						string gutterText;

						if (wrappedIndex < wrappedLines.Count)
						{
							var gwl = wrappedLines[wrappedIndex];
							bool isFirstWrappedSegment = gwl.SourceCharOffset == 0;
							bool isCurrentLineGutter = _highlightCurrentLine && _isEditing && gwl.SourceLineIndex == _cursorY;
							if (isCurrentLineGutter)
								gutterFg = fgColor;

							if (isFirstWrappedSegment)
								gutterText = (gwl.SourceLineIndex + 1).ToString().PadLeft(gutterWidth - ControlDefaults.LineNumberGutterPadding).PadRight(gutterWidth);
							else
								gutterText = new string(' ', gutterWidth);
						}
						else
						{
							gutterText = new string(' ', gutterWidth);
						}

						for (int g = 0; g < gutterWidth && startX + g < clipRect.Right; g++)
						{
							if (startX + g >= clipRect.X)
								buffer.SetCell(startX + g, paintY, gutterText[g], gutterFg, gutterBg);
						}
					}

					if (wrappedIndex < wrappedLines.Count)
					{
						// Render placeholder text on first visible line when content is empty
						if (showPlaceholder && i == 0)
						{
							string placeholderLine = _placeholderText!.Length > effectiveWidth
								? _placeholderText.Substring(0, effectiveWidth)
								: _placeholderText.PadRight(effectiveWidth);
							Color dimFg = Color.Grey;
							for (int charPos = 0; charPos < effectiveWidth; charPos++)
							{
								int cellX = contentStartX + charPos;
								if (cellX >= clipRect.X && cellX < clipRect.Right)
									buffer.SetCell(cellX, paintY, placeholderLine[charPos], dimFg, bgColor);
							}

							// Fill right margin and scrollbar area
							int rightMarginStart = contentStartX + effectiveWidth;
							int rightFill = bounds.Right - rightMarginStart;
							if (rightFill > 0)
								buffer.FillRect(new LayoutRect(rightMarginStart, paintY, rightFill, 1), ' ', fgColor, windowBgColor);

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

						// Pad or truncate to effective width
						if (visibleLine.Length < effectiveWidth)
							visibleLine = visibleLine.PadRight(effectiveWidth);
						else if (visibleLine.Length > effectiveWidth)
							visibleLine = visibleLine.Substring(0, effectiveWidth);

						int hScrollForCalc = (_wrapMode == WrapMode.NoWrap) ? _horizontalScrollOffset : 0;

						// Determine current line highlight
						bool isCurrentLine = _highlightCurrentLine && _isEditing && wl.SourceLineIndex == _cursorY;
						Color lineBg = isCurrentLine ? CurrentLineHighlightColor : bgColor;

						// Paint each character with selection, syntax, and whitespace handling
						for (int charPos = 0; charPos < effectiveWidth; charPos++)
						{
							int actualCharPos = charPos + wl.SourceCharOffset + hScrollForCalc;
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

							char c = charPos < visibleLine.Length ? visibleLine[charPos] : ' ';
							bool isContentChar = charPos + hScrollForCalc < wl.DisplayText.Length;

							// Color priority: Selection > Syntax > Visible whitespace > Default
							Color charFg;
							Color charBg;
							if (isSelected)
							{
								charFg = selFgColor;
								charBg = selBgColor;
							}
							else
							{
								charBg = lineBg;

								if (_showWhitespace && c == ' ' && isContentChar)
								{
									c = ControlDefaults.WhitespaceSpaceChar;
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

							int cellX = contentStartX + charPos;
							if (cellX >= clipRect.X && cellX < clipRect.Right)
							{
								buffer.SetCell(cellX, paintY, c, charFg, charBg);
							}
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
							if (i == 0 && _verticalScrollOffset > 0)
							{
								scrollChar = '▲';
								scrollFg = activeThumbColor;
							}
							else if (i == effectiveViewport - 1 && _verticalScrollOffset < totalWrappedLineCount - effectiveViewport)
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
							buffer.SetCell(scrollX, paintY, scrollChar, scrollFg, scrollbarBg);
						}
					}

					// Fill right margin
					if (_margin.Right > 0)
					{
						int rightMarginX = contentStartX + effectiveWidth + scrollbarWidth;
						buffer.FillRect(new LayoutRect(rightMarginX, paintY, _margin.Right, 1), ' ', fgColor, windowBgColor);
					}
				}
			}

			// Fill remaining viewport height with empty lines
			for (int i = linesToPaint; i < effectiveViewport && startY + i < bounds.Bottom; i++)
			{
				int paintY = startY + i;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom)
				{
					if (_margin.Left > 0)
						buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, windowBgColor);

					// Fill gutter area for empty rows
					if (gutterWidth > 0)
						buffer.FillRect(new LayoutRect(startX, paintY, gutterWidth, 1), ' ', fgColor, bgColor);

					buffer.FillRect(new LayoutRect(contentStartX, paintY, effectiveWidth, 1), ' ', fgColor, bgColor);

					if (needsVerticalScrollbar)
					{
						int scrollX = contentStartX + effectiveWidth;
						if (scrollX >= clipRect.X && scrollX < clipRect.Right)
						{
							char scrollChar;
							Color scrollFg;
							if (i == 0 && _verticalScrollOffset > 0)
							{
								scrollChar = '▲';
								scrollFg = activeThumbColor;
							}
							else if (i == effectiveViewport - 1 && _verticalScrollOffset < totalWrappedLineCount - effectiveViewport)
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
							buffer.SetCell(scrollX, paintY, scrollChar, scrollFg, scrollbarBg);
						}
					}

					if (_margin.Right > 0)
					{
						int rightMarginX = contentStartX + effectiveWidth + scrollbarWidth;
						buffer.FillRect(new LayoutRect(rightMarginX, paintY, _margin.Right, 1), ' ', fgColor, windowBgColor);
					}
				}
			}

			// Paint horizontal scrollbar
			if (needsHorizontalScrollbar)
			{
				int scrollY = startY + effectiveViewport;
				if (scrollY >= clipRect.Y && scrollY < clipRect.Bottom && scrollY < bounds.Bottom)
				{
					if (_margin.Left > 0)
						buffer.FillRect(new LayoutRect(bounds.X, scrollY, _margin.Left, 1), ' ', fgColor, windowBgColor);

					// Fill gutter area under horizontal scrollbar
					if (gutterWidth > 0)
						buffer.FillRect(new LayoutRect(startX, scrollY, gutterWidth, 1), ' ', fgColor, bgColor);

					int maxLineLength = GetMaxLineLength();
					int hMaxScroll = Math.Max(0, maxLineLength - effectiveWidth);
					int thumbWidth = Math.Max(1, (effectiveWidth * effectiveWidth) / Math.Max(1, maxLineLength));
					int maxThumbPos = effectiveWidth - thumbWidth;
					int thumbPos = maxLineLength > effectiveWidth
						? (int)Math.Round((double)_horizontalScrollOffset / (maxLineLength - effectiveWidth) * maxThumbPos)
						: 0;

					for (int x = 0; x < effectiveWidth; x++)
					{
						int cellX = contentStartX + x;
						if (cellX >= clipRect.X && cellX < clipRect.Right)
						{
							char scrollChar;
							Color scrollFg;
							if (x == 0 && _horizontalScrollOffset > 0)
							{
								scrollChar = '◄';
								scrollFg = activeThumbColor;
							}
							else if (x == effectiveWidth - 1 && _horizontalScrollOffset < hMaxScroll)
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
							buffer.SetCell(cellX, scrollY, scrollChar, scrollFg, scrollbarBg);
						}
					}

					if (needsVerticalScrollbar)
					{
						int cornerX = contentStartX + effectiveWidth;
						if (cornerX >= clipRect.X && cornerX < clipRect.Right)
						{
							buffer.SetCell(cornerX, scrollY, '┘', activeTrackColor, scrollbarBg);
						}
					}

					if (_margin.Right > 0)
					{
						int rightMarginX = contentStartX + effectiveWidth + scrollbarWidth;
						buffer.FillRect(new LayoutRect(rightMarginX, scrollY, _margin.Right, 1), ' ', fgColor, windowBgColor);
					}
				}
			}

			// Render editing mode hint overlay at bottom-right of viewport
			if (_showEditingHints && _hasFocus)
			{
				string hintText = _isEditing
					? ControlDefaults.EditingModeHint
					: ControlDefaults.BrowseModeHint;

				int lastVisibleRow = startY + linesToPaint - 1;
				if (hintText.Length <= effectiveWidth && lastVisibleRow >= clipRect.Y && lastVisibleRow < clipRect.Bottom)
				{
					int hintStartX = contentStartX + effectiveWidth - hintText.Length;
					Color hintFg = Color.Grey50;
					Color hintBg = bgColor;

					for (int c = 0; c < hintText.Length; c++)
					{
						int cellX = hintStartX + c;
						if (cellX >= clipRect.X && cellX < clipRect.Right)
							buffer.SetCell(cellX, lastVisibleRow, hintText[c], hintFg, hintBg);
					}
				}
			}

			// Fill bottom margin
			int contentEndY = startY + effectiveViewport + (needsHorizontalScrollbar ? 1 : 0);
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, contentEndY, fgColor, windowBgColor);
		}

		private int GetGutterWidth()
		{
			if (!_showLineNumbers) return 0;
			int digits = Math.Max(1, (int)Math.Floor(Math.Log10(Math.Max(1, _lines.Count))) + 1);
			return digits + ControlDefaults.LineNumberGutterPadding;
		}

		/// <summary>
		/// Computes vertical scrollbar geometry for hit-testing and rendering.
		/// Returns (trackHeight, thumbY, thumbHeight) relative to the viewport top.
		/// </summary>
		private (int trackHeight, int thumbY, int thumbHeight) GetVerticalScrollbarGeometry()
		{
			int effectiveViewport = _effectiveViewportHeight > 0 ? _effectiveViewportHeight : GetEffectiveViewportHeight();
			int totalLines = GetTotalWrappedLineCount();
			int thumbHeight = Math.Max(1, (effectiveViewport * effectiveViewport) / Math.Max(1, totalLines));
			int maxThumbPos = effectiveViewport - thumbHeight;
			int thumbY = totalLines > effectiveViewport
				? (int)Math.Round((double)_verticalScrollOffset / (totalLines - effectiveViewport) * maxThumbPos)
				: 0;
			return (effectiveViewport, thumbY, thumbHeight);
		}

		/// <summary>
		/// Computes horizontal scrollbar geometry for hit-testing and rendering.
		/// Returns (trackWidth, thumbX, thumbWidth) relative to the content start.
		/// </summary>
		private (int trackWidth, int thumbX, int thumbWidth) GetHorizontalScrollbarGeometry()
		{
			int maxLineLength = GetMaxLineLength();
			int thumbWidth = Math.Max(1, (_effectiveWidth * _effectiveWidth) / Math.Max(1, maxLineLength));
			int maxThumbPos = _effectiveWidth - thumbWidth;
			int thumbX = maxLineLength > _effectiveWidth
				? (int)Math.Round((double)_horizontalScrollOffset / (maxLineLength - _effectiveWidth) * maxThumbPos)
				: 0;
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
			_syntaxTokenCache ??= new Dictionary<int, IReadOnlyList<SyntaxToken>>();
			_lineStateCache   ??= new Dictionary<int, SyntaxLineState>();

			if (_syntaxTokenCache.TryGetValue(lineIndex, out var cached))
				return cached;

			EnsureStateUpToLine(lineIndex);
			return _syntaxTokenCache.TryGetValue(lineIndex, out var result)
				? result : Array.Empty<SyntaxToken>();
		}

		private void EnsureStateUpToLine(int lineIndex)
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

		#endregion
	}
}
