// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class DropdownControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int dropdownWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch
				? constraints.MaxWidth - Margin.Left - Margin.Right
				: calculateOptimalWidth(constraints.MaxWidth));

			// Ensure width can accommodate content
			int maxItemWidth = 0;
			List<DropdownItem> measureSnapshot;
			lock (_dropdownLock) { measureSnapshot = _items.ToList(); }
			foreach (var item in measureSnapshot)
			{
				int itemLength = Parsing.MarkupParser.StripLength(item.Text) + 4;
				if (itemLength > maxItemWidth) maxItemWidth = itemLength;
			}

			if (_autoAdjustWidth)
				dropdownWidth = Math.Max(dropdownWidth, maxItemWidth + 4);

			int promptLength = Parsing.MarkupParser.StripLength(_prompt);
			int minWidth = Math.Max(promptLength + 5, maxItemWidth + 4);
			dropdownWidth = Math.Max(dropdownWidth, minWidth);

			// Calculate height - constant (header only), dropdown items render via portal
			int height = 1 + Margin.Top + Margin.Bottom;

			int width = dropdownWidth + Margin.Left + Margin.Right;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			// Store bounds for portal positioning
			_lastLayoutBounds = bounds;

			Color backgroundColor;
			Color foregroundColor;
			Color windowBackground = Container?.BackgroundColor ?? defaultBg;

			if (!_isEnabled)
			{
				backgroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor ?? Color.Grey;
				foregroundColor = Container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor ?? Color.DarkSlateGray1;
			}
			else if (_hasFocus)
			{
				backgroundColor = FocusedBackgroundColor;
				foregroundColor = FocusedForegroundColor;
			}
			else
			{
				backgroundColor = BackgroundColor;
				foregroundColor = ForegroundColor;
			}

			bool preserveBg = Container?.HasGradientBackground ?? false;

			int targetWidth = bounds.Width - Margin.Left - Margin.Right;
			if (targetWidth <= 0) return;

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, foregroundColor, windowBackground, preserveBg);

			// Calculate dropdown width
			int dropdownWidth = Width ?? (HorizontalAlignment == HorizontalAlignment.Stretch ? targetWidth : calculateOptimalWidth(targetWidth));
			int maxItemWidth = 0;
			List<DropdownItem> paintSnapshot;
			lock (_dropdownLock) { paintSnapshot = _items.ToList(); }
			foreach (var item in paintSnapshot)
			{
				int itemLength = Parsing.MarkupParser.StripLength(item.Text) + 4;
				if (itemLength > maxItemWidth) maxItemWidth = itemLength;
			}
			if (_autoAdjustWidth)
				dropdownWidth = Math.Max(dropdownWidth, maxItemWidth + 4);

			int promptLength = Parsing.MarkupParser.StripLength(_prompt);
			int minWidth = Math.Max(promptLength + 5, maxItemWidth + 4);
			dropdownWidth = Math.Min(Math.Max(dropdownWidth, minWidth), targetWidth);

			// Calculate alignment offset
			int alignOffset = 0;
			if (dropdownWidth < targetWidth)
			{
				switch (HorizontalAlignment)
				{
					case HorizontalAlignment.Center:
						alignOffset = (targetWidth - dropdownWidth) / 2;
						break;
					case HorizontalAlignment.Right:
						alignOffset = targetWidth - dropdownWidth;
						break;
				}
			}

			int selectedIdx = CurrentSelectedIndex;
			int highlightedIdx = CurrentHighlightedIndex;
			int dropdownScroll = CurrentDropdownScrollOffset;

			// Render header
			string selectedText = selectedIdx >= 0 && selectedIdx < paintSnapshot.Count ? paintSnapshot[selectedIdx].Text : "(None)";
			// Arrow shows direction: ▲ when open upward, ▼ when closed or open downward
			string arrow = _isDropdownOpen && _opensUpward ? "▲" : "▼";
			int maxSelectedTextLength = dropdownWidth - promptLength - 5;
			if (maxSelectedTextLength > 0 && selectedText.Length > maxSelectedTextLength)
				selectedText = selectedText.Substring(0, Math.Max(0, maxSelectedTextLength - 3)) + "...";

			string headerContent = $"{_prompt} {selectedText} {arrow}";
			int headerVisibleLength = Parsing.MarkupParser.StripLength(headerContent);
			if (headerVisibleLength < dropdownWidth)
				headerContent += new string(' ', dropdownWidth - headerVisibleLength);

			int paintY = startY;

			if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
			{
				if (Margin.Left > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, paintY, Margin.Left, 1), foregroundColor, windowBackground, preserveBg);

				if (alignOffset > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX, paintY, alignOffset, 1), foregroundColor, windowBackground, preserveBg);

				var cells = Parsing.MarkupParser.Parse(headerContent, foregroundColor, backgroundColor);
				buffer.WriteCellsClipped(startX + alignOffset, paintY, cells, clipRect);

				int rightFillStart = startX + alignOffset + dropdownWidth;
				int rightFillWidth = bounds.Right - rightFillStart - Margin.Right;
				if (rightFillWidth > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(rightFillStart, paintY, rightFillWidth, 1), foregroundColor, windowBackground, preserveBg);

				if (Margin.Right > 0)
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.Right - Margin.Right, paintY, Margin.Right, 1), foregroundColor, windowBackground, preserveBg);
			}
			paintY++;

			// Note: Dropdown items are rendered via portal overlay (PaintDropdownListInternal)
			// This keeps the control height constant and allows the list to extend beyond parent bounds

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, paintY, foregroundColor, windowBackground, preserveBg);
		}

		#endregion
	}
}
