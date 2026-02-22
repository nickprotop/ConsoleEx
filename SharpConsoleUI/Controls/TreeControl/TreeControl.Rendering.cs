// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using System.Text;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	public partial class TreeControl
	{
		/// <summary>
		/// Build the tree prefix for a node based on its depth and position
		/// </summary>
		private string BuildTreePrefix(int depth, bool isLast, (string cross, string corner, string tee, string vertical, string horizontal) guides)
		{
			if (depth == 0)
				return "";

			StringBuilder prefix = new StringBuilder();

			// Add indentation based on depth
			for (int i = 0; i < depth - 1; i++)
			{
				prefix.Append(_indent);
			}

			// Add appropriate connector for the current node
			string connector = isLast ? guides.corner : guides.tee;
			string horizontalLine = guides.horizontal;

			prefix.Append(connector);
			prefix.Append(horizontalLine);
			prefix.Append(" ");

			return prefix.ToString();
		}

		private (string cross, string corner, string tee, string vertical, string horizontal) GetGuideChars()
		{
			switch (_guide)
			{
				case var _ when _guide == TreeGuide.Ascii:
					return ("+", "\\", "+", "|", "-");

				case var _ when _guide == TreeGuide.DoubleLine:
					return ("╬", "╚", "╚", "║", "═");

				case var _ when _guide == TreeGuide.BoldLine:
					return ("┿", "┗", "┗", "┃", "━");

				case var _ when _guide == TreeGuide.Line:
				default:
					return ("┼", "└", "└", "│", "─");
			}
		}

		/// <summary>
		/// Returns the display-column width of the expand/collapse indicator for a node at the given index.
		/// Used by the mouse handler to detect clicks on the indicator.
		/// </summary>
		internal int GetIndicatorStartColumn(int nodeIndex)
		{
			if (nodeIndex < 0 || nodeIndex >= _flattenedNodes.Count) return -1;
			var node = _flattenedNodes[nodeIndex];
			if (node.Children.Count == 0) return -1;

			var guideChars = GetGuideChars();
			int depth = GetNodeDepth(node);
			bool isLast = IsLastChildInParent(node);
			string prefix = BuildTreePrefix(depth, isLast, guideChars);
			return _margin.Left + GetCachedTextLength(prefix);
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			UpdateFlattenedNodes();

			int contentWidth = constraints.MaxWidth - _margin.Left - _margin.Right;

			// Calculate max item width
			int maxItemWidth = 0;
			var guideChars = GetGuideChars();
			foreach (var node in _flattenedNodes)
			{
				int depth = GetNodeDepth(node);
				string prefix = BuildTreePrefix(depth, IsLastChildInParent(node), guideChars);
				string displayText = node.Text ?? string.Empty;
				string expandIndicator = node.Children.Count > 0 ? "[-] " : "";
				int itemWidth = GetCachedTextLength(prefix + expandIndicator + displayText);
				if (itemWidth > maxItemWidth) maxItemWidth = itemWidth;
			}

			// Calculate width based on content or explicit width
			int contentBasedWidth = (_width ?? maxItemWidth) + _margin.Left + _margin.Right;

			// For Stretch alignment, request full available width
			// For other alignments, request only what content needs
			int width = _horizontalAlignment == HorizontalAlignment.Stretch
				? constraints.MaxWidth
				: contentBasedWidth;

			// Calculate height based on visible items
			int effectiveMaxVisibleItems;
			if (MaxVisibleItems.HasValue)
			{
				effectiveMaxVisibleItems = MaxVisibleItems.Value;
			}
			else if (_height.HasValue)
			{
				effectiveMaxVisibleItems = _height.Value - _margin.Top - _margin.Bottom;
			}
			else
			{
				effectiveMaxVisibleItems = Math.Min(_flattenedNodes.Count, constraints.MaxHeight - _margin.Top - _margin.Bottom - 1);
			}

			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;

			bool hasScrollIndicator = _flattenedNodes.Count > effectiveMaxVisibleItems;
			int contentHeight = Math.Min(_flattenedNodes.Count, effectiveMaxVisibleItems);
			int height = contentHeight + _margin.Top + _margin.Bottom + (hasScrollIndicator ? 1 : 0);

			if (_height.HasValue)
			{
				height = _height.Value;
			}

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

			var bgColor = BackgroundColor;
			var fgColor = ForegroundColor;
			int contentWidth = bounds.Width - _margin.Left - _margin.Right;
			int contentHeight = bounds.Height - _margin.Top - _margin.Bottom;

			if (contentWidth <= 0 || contentHeight <= 0) return;

			int startX = bounds.X + _margin.Left;
			int startY = bounds.Y + _margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, bgColor);

			// Update flattened nodes
			UpdateFlattenedNodes();

			// Determine visible items
			// For VerticalAlignment.Fill controls, use the actual content height from bounds, not the cached measurement
			int effectiveMaxVisibleItems;
			if (_verticalAlignment == VerticalAlignment.Fill)
			{
				// VerticalAlignment.Fill: use all available space
				effectiveMaxVisibleItems = MaxVisibleItems ?? contentHeight;
			}
			else
			{
				// Normal: use cached measurement or content height
				effectiveMaxVisibleItems = _calculatedMaxVisibleItems ?? MaxVisibleItems ?? contentHeight;
			}
			bool hasScrollIndicator = _flattenedNodes.Count > effectiveMaxVisibleItems;
			int visibleItemsHeight = hasScrollIndicator ? contentHeight - 1 : contentHeight;
			effectiveMaxVisibleItems = Math.Min(effectiveMaxVisibleItems, visibleItemsHeight);
			_calculatedMaxVisibleItems = effectiveMaxVisibleItems;

			// Get and validate scroll offset
			int scrollOffset = CurrentScrollOffset;
			int maxScrollOffset = Math.Max(0, _flattenedNodes.Count - effectiveMaxVisibleItems);
			if (scrollOffset < 0 || scrollOffset > maxScrollOffset)
			{
				scrollOffset = Math.Max(0, Math.Min(scrollOffset, maxScrollOffset));
				_scrollOffset = scrollOffset;
			}

			// Get guide characters
			var guideChars = GetGuideChars();
			int selectedIndex = CurrentSelectedIndex;

			// Render visible nodes
			int endIndex = Math.Min(scrollOffset + effectiveMaxVisibleItems, _flattenedNodes.Count);
			int paintRow = 0;
			for (int i = scrollOffset; i < endIndex && paintRow < visibleItemsHeight; i++, paintRow++)
			{
				var node = _flattenedNodes[i];
				int paintY = startY + paintRow;

				if (paintY < clipRect.Y || paintY >= clipRect.Bottom || paintY >= bounds.Bottom)
					continue;

				// Calculate node depth and build prefix
				int depth = GetNodeDepth(node);
				bool isLast = IsLastChildInParent(node);
				string prefix = BuildTreePrefix(depth, isLast, guideChars);

				// Get node text and colors
				string displayText = node.Text ?? string.Empty;
				Color textColor;
				Color nodeBgColor;

				if (i == selectedIndex && _hasFocus)
				{
					textColor = HighlightForegroundColor;
					nodeBgColor = HighlightBackgroundColor;
				}
				else if (i == _hoveredIndex && _hasFocus)
				{
					// Hover highlight - subtle visual distinction
					textColor = HighlightForegroundColor;
					nodeBgColor = HighlightBackgroundColor;
				}
				else
				{
					textColor = node.TextColor ?? fgColor;
					nodeBgColor = bgColor;
				}

				// Add expand/collapse indicator on the left side
				string expandIndicator = node.Children.Count > 0 ? (node.IsExpanded ? "[-] " : "[+] ") : "";

				// Build full node text
				string nodeText = prefix + expandIndicator + displayText;
				int visibleLength = GetCachedTextLength(nodeText);

				// Truncate if necessary
				if (visibleLength > contentWidth)
				{
					nodeText = TextTruncationHelper.TruncateWithFixedParts(
						prefix + expandIndicator,
						displayText,
						string.Empty,
						contentWidth,
						_textMeasurementCache);
					visibleLength = GetCachedTextLength(nodeText);
				}

				// Fill left margin
				if (_margin.Left > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.X, paintY, _margin.Left, 1), ' ', fgColor, bgColor);
				}

				// Calculate alignment offset
				int alignOffset = 0;
				if (visibleLength < contentWidth)
				{
					switch (_horizontalAlignment)
					{
						case HorizontalAlignment.Center:
							alignOffset = (contentWidth - visibleLength) / 2;
							break;
						case HorizontalAlignment.Right:
							alignOffset = contentWidth - visibleLength;
							break;
					}
				}

				// Fill left alignment padding
				if (alignOffset > 0)
				{
					buffer.FillRect(new LayoutRect(startX, paintY, alignOffset, 1), ' ', textColor, nodeBgColor);
				}

				// Render the node text
				string formattedNode = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					nodeText,
					visibleLength,
					1,
					false,
					nodeBgColor,
					textColor
				).FirstOrDefault() ?? string.Empty;

				var cells = AnsiParser.Parse(formattedNode, textColor, nodeBgColor);
				buffer.WriteCellsClipped(startX + alignOffset, paintY, cells, clipRect);

				// Fill right padding
				int rightPadStart = startX + alignOffset + visibleLength;
				int rightPadWidth = bounds.Right - rightPadStart - _margin.Right;
				if (rightPadWidth > 0)
				{
					buffer.FillRect(new LayoutRect(rightPadStart, paintY, rightPadWidth, 1), ' ', textColor, nodeBgColor);
				}

				// Fill right margin
				if (_margin.Right > 0)
				{
					buffer.FillRect(new LayoutRect(bounds.Right - _margin.Right, paintY, _margin.Right, 1), ' ', fgColor, bgColor);
				}
			}

			// Fill remaining content rows if needed
			for (int row = paintRow; row < visibleItemsHeight; row++)
			{
				int paintY = startY + row;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, paintY, bounds.Width, 1), ' ', fgColor, bgColor);
				}
			}

			// Draw scroll indicator if needed
			if (hasScrollIndicator)
			{
				int scrollY = startY + visibleItemsHeight;
				if (scrollY >= clipRect.Y && scrollY < clipRect.Bottom && scrollY < bounds.Bottom)
				{
					// Fill the scroll indicator line
					buffer.FillRect(new LayoutRect(bounds.X, scrollY, bounds.Width, 1), ' ', fgColor, bgColor);

					// Up arrow
					char upArrow = scrollOffset > 0 ? '▲' : ' ';
					buffer.SetCell(startX, scrollY, upArrow, fgColor, bgColor);

					// Down arrow
					char downArrow = scrollOffset + effectiveMaxVisibleItems < _flattenedNodes.Count ? '▼' : ' ';
					buffer.SetCell(startX + contentWidth - 1, scrollY, downArrow, fgColor, bgColor);
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, bounds.Bottom - _margin.Bottom, fgColor, bgColor);
		}

		#endregion
	}
}
