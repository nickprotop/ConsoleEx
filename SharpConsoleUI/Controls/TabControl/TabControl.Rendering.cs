// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using System.Linq;

namespace SharpConsoleUI.Controls
{
	public partial class TabControl
	{
		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			// Layout system handles this via TabLayout
			// This won't be called directly, but provide fallback
			int height = _height ?? (TabHeaderHeight + 10); // Default height
			int width = Width ?? constraints.MaxWidth;
			return new LayoutSize(width, height);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			// Paint tab headers at Y=0
			PaintTabHeaders(buffer, bounds, defaultFg, defaultBg);

			// Tab content painted by layout system
			_isDirty = false;
		}

		private void PaintTabHeaders(CharacterBuffer buffer, LayoutRect bounds,
			Color defaultFg, Color defaultBg)
		{
			List<TabPage> snapshot;
			int activeIdx;
			lock (_tabLock) { snapshot = _tabPages.ToList(); activeIdx = _activeTabIndex; }
			var bgColor = ColorResolver.ResolveBackground(_backgroundColor, Container, defaultBg);
			int headerLeft = bounds.X + Margin.Left;
			int headerRight = bounds.X + bounds.Width - Margin.Right;
			int headerY = bounds.Y + Margin.Top;
			int x = headerLeft;

			int activeTabStartX = -1;
			int activeTabEndX = -1;

			for (int i = 0; i < snapshot.Count; i++)
			{
				bool isActive = i == activeIdx;
				var title = $" {snapshot[i].Title} ";

				// When the tab strip has keyboard focus, render the active tab in reverse-video
				// (black text on cyan background) so the user can see focus and knows that
				// Left/Right arrows will switch tabs.
				Color tileFg, tileBg;
				if (isActive)
				{
					tileFg = _hasFocus ? bgColor    : Color.Cyan1;
					tileBg = _hasFocus ? Color.Cyan1 : bgColor;
				}
				else
				{
					tileFg = Color.Grey;
					tileBg = bgColor;
				}

				if (isActive)
					activeTabStartX = x;

				// Draw tab title with markup support
				var titleCells = MarkupParser.Parse(title, tileFg, tileBg);
				var titleClip = new LayoutRect(headerLeft, headerY, headerRight - headerLeft, 1);
				buffer.WriteCellsClipped(x, headerY, titleCells, titleClip);
				x += titleCells.Count;

				// Draw close button (×) for closable tabs
				if (snapshot[i].IsClosable && x < headerRight)
				{
					buffer.SetNarrowCell(x, headerY, '×', tileFg, tileBg);
					x++;
				}

				if (isActive)
					activeTabEndX = x;

				// Draw separator
				if (x < headerRight && i < snapshot.Count - 1)
				{
					buffer.SetNarrowCell(x, headerY, '│', Color.Grey, bgColor);
					x++;
				}
			}

			const string navHint = " ← → ";
			bool showHint = _hasFocus && snapshot.Count > 1;
			int hintStartX = headerRight - navHint.Length;
			int tabsEndX = x; // capture before fill loops modify x

			if (_headerStyle == TabHeaderStyle.Classic)
			{
				// Fill remaining header space with ─
				while (x < headerRight)
				{
					buffer.SetNarrowCell(x, headerY, '─', Color.Grey, bgColor);
					x++;
				}
			}
			else
			{
				// Fill remaining row 1 space with spaces
				while (x < headerRight)
				{
					buffer.SetNarrowCell(x, headerY, ' ', Color.Grey, bgColor);
					x++;
				}

				// Draw row 2 separator line
				int separatorY = headerY + 1;
				if (_headerStyle == TabHeaderStyle.Separator)
				{
					var sepColor = _hasFocus ? Color.Cyan1 : Color.Grey;
					for (int x2 = headerLeft; x2 < headerRight; x2++)
						buffer.SetNarrowCell(x2, separatorY, '─', sepColor, bgColor);
				}
				else // AccentedSeparator
				{
					// When the tab strip has keyboard focus the entire separator row is
					// drawn in the accent colour so it stands out as a highlighted band.
					var sepColor = _hasFocus ? Color.Cyan1 : Color.Grey;
					var accentColor = Color.Cyan1;

					for (int x2 = headerLeft; x2 < headerRight; x2++)
					{
						char c2 = '─';
						Color c2Color = sepColor;

						if (activeTabStartX >= 0 && activeTabEndX > activeTabStartX)
						{
							bool isLeftBoundary = x2 == activeTabStartX;
							bool isRightBoundary = x2 == activeTabEndX - 1;
							bool isInner = x2 > activeTabStartX && x2 < activeTabEndX - 1;

							if (isLeftBoundary)
							{
								// '╡' connects a ─ on the left to ═ on the right; only valid when
								// there is actually a ─ segment to the left.  At the very left
								// edge of the control there is nothing to connect, so draw '═'.
								c2 = x2 > headerLeft ? '╡' : '═';
								c2Color = accentColor;
							}
							else if (isRightBoundary)
							{
								// '╞' connects ═ on the left to ─ on the right; only valid when
								// there is a ─ segment to the right.
								c2 = x2 < headerRight - 1 ? '╞' : '═';
								c2Color = accentColor;
							}
							else if (isInner)
							{
								c2 = '═';
								c2Color = accentColor;
							}
						}

						buffer.SetNarrowCell(x2, separatorY, c2, c2Color, bgColor);
					}
				}
			}

			// Navigation hint at the right edge of the header row
			if (showHint && hintStartX >= tabsEndX - 1)
			{
				for (int h = 0; h < navHint.Length; h++)
					buffer.SetNarrowCell(hintStartX + h, headerY, navHint[h], Color.Grey, bgColor);
			}
		}

		private int CalculateHeaderWidth()
		{
			List<TabPage> snapshot;
			lock (_tabLock) { snapshot = _tabPages.ToList(); }
			return CalculateHeaderWidth(snapshot);
		}

		private int CalculateHeaderWidth(List<TabPage> tabs)
		{
			int width = 0;
			for (int i = 0; i < tabs.Count; i++)
			{
				width += MarkupParser.StripLength(tabs[i].Title) + 2; // " title "
				if (tabs[i].IsClosable)
					width += 1; // ×
				if (i < tabs.Count - 1)
					width += 1; // separator
			}
			return width;
		}

		#endregion
	}
}
