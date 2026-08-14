// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

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
			if (_showTabHeader)
				PaintTabHeaders(buffer, bounds, defaultFg, defaultBg);

			// Tab content painted by layout system
		}

		private void PaintTabHeaders(CharacterBuffer buffer, LayoutRect bounds,
			Color defaultFg, Color defaultBg)
		{
			List<TabPage> snapshot;
			int activeIdx;
			lock (_tabLock) { snapshot = _tabPages.ToList(); activeIdx = _activeTabIndex; }
			var bgColor = ColorResolver.ResolveBackground(_backgroundColor, Container);

			// Resolve per-state header colors once per paint. The active tab is the role surface: when a
			// ColorRole is set its fill/text-on-fill take precedence over the theme defaults (explicit overrides
			// still win). For ColorRole=Default the role helpers return null, keeping the no-role path identical.
			var activeFocFg = _activeFocusedForeground
				?? ColorResolver.ColorRoleTextOnBackground(ColorRole, Container, Outline, Themes.ColorRoleState.Focused, mode: ColorRoleMode)
				?? ColorResolver.ResolveTabHeaderActiveFocusedForeground(null, Container);
			var activeFocBg = _activeFocusedBackground
				?? ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, Themes.ColorRoleState.Focused, mode: ColorRoleMode)
				?? ColorResolver.ResolveTabHeaderActiveFocusedBackground(null, Container);
			var activeUnfocFg = _activeUnfocusedForeground
				?? ColorResolver.ColorRoleTextOnBackground(ColorRole, Container, Outline, Themes.ColorRoleState.Normal, mode: ColorRoleMode)
				?? ColorResolver.ResolveTabHeaderActiveForeground(null, Container);
			var activeUnfocBg = _activeUnfocusedBackground
				?? ColorResolver.ColorRoleBackground(ColorRole, Container, Outline, Themes.ColorRoleState.Normal, mode: ColorRoleMode)
				?? ColorResolver.ResolveTabHeaderActiveBackground(null, Container);
			var inactFocFg = ColorResolver.ResolveTabHeaderFocusedForeground(_inactiveFocusedForeground, Container);
			var inactFocBg = ColorResolver.ResolveTabHeaderFocusedBackground(_inactiveFocusedBackground, Container);
			var inactUnfocFg = ColorResolver.ResolveTabHeaderForeground(_inactiveUnfocusedForeground, Container);
			var inactUnfocBg = ColorResolver.ResolveTabHeaderBackground(_inactiveUnfocusedBackground, Container);

			int headerLeft = bounds.X + Margin.Left;
			int headerRight = bounds.X + bounds.Width - Margin.Right;
			int headerY = bounds.Y + Margin.Top;
			int x = headerLeft;

			int activeTabStartX = -1;
			int activeTabEndX = -1;

			// Which tabs fit, positioned so the active one is among them. Without this the row drew from
			// index 0 and clipped, so a strip narrower than its tabs simply stopped — with the active tab
			// itself off the end whenever it sat late enough in the order.
			var strip = LayoutStrip(snapshot, activeIdx, headerRight - headerLeft);

			// MoreLeft/MoreRight are facts about the strip; the property decides whether to say them out
			// loud. With it off no cell was reserved for them either, so nothing here has room to draw.
			if (strip.MoreLeft && _showTabScrollIndicators)
			{
				buffer.SetNarrowCell(x, headerY, ControlDefaults.TabScrollLeftGlyph, Color.Grey, bgColor);
				x++;
			}

			for (int drawn = 0; drawn < strip.Count; drawn++)
			{
				int i = strip.First + drawn;
				bool isActive = i == activeIdx;
				var title = $" {snapshot[i].Title} ";

				Color tileFg, tileBg;
				if (isActive)
				{
					tileFg = HasFocus ? activeFocFg : activeUnfocFg;
					tileBg = HasFocus ? activeFocBg : activeUnfocBg;
				}
				else
				{
					tileFg = HasFocus ? inactFocFg : inactUnfocFg;
					tileBg = HasFocus ? inactFocBg : inactUnfocBg;
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

				// Draw separator — between the tabs actually drawn, not between every tab there is.
				if (x < headerRight && drawn < strip.Count - 1)
				{
					buffer.SetNarrowCell(x, headerY, '│', Color.Grey, bgColor);
					x++;
				}
			}

			const string navHint = " ← → ";
			bool showHint = HasFocus && snapshot.Count > 1;
			int navHintDisplayWidth = UnicodeWidth.GetStringWidth(navHint);
			int hintStartX = headerRight - navHintDisplayWidth;
			int tabsEndX = x; // capture before fill loops modify x

			// Drawn after the tabs and before the fill, so the cell it needs is one the fill will not
			// take back. It sits at the row's own right edge rather than after the last tab: it marks
			// where the row ends, and a strip a cell short of full would otherwise strand it mid-row.
			int fillRight = headerRight;
			if (strip.MoreRight && _showTabScrollIndicators && headerRight - 1 >= headerLeft)
			{
				buffer.SetNarrowCell(headerRight - 1, headerY, ControlDefaults.TabScrollRightGlyph, Color.Grey, bgColor);
				fillRight--;
			}

			if (_headerStyle == TabHeaderStyle.Classic)
			{
				// Fill remaining header space with ─
				while (x < fillRight)
				{
					buffer.SetNarrowCell(x, headerY, '─', Color.Grey, bgColor);
					x++;
				}
			}
			else
			{
				// Fill remaining row 1 space with spaces
				while (x < fillRight)
				{
					buffer.SetNarrowCell(x, headerY, ' ', Color.Grey, bgColor);
					x++;
				}

				// Draw row 2 separator line
				int separatorY = headerY + 1;
				if (_headerStyle == TabHeaderStyle.Separator)
				{
					var sepColor = HasFocus ? Color.Cyan1 : Color.Grey;
					for (int x2 = headerLeft; x2 < headerRight; x2++)
						buffer.SetNarrowCell(x2, separatorY, '─', sepColor, bgColor);
				}
				else // AccentedSeparator
				{
					// When the tab strip has keyboard focus the entire separator row is
					// drawn in the accent colour so it stands out as a highlighted band.
					var sepColor = HasFocus ? Color.Cyan1 : Color.Grey;
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
				var navHintCells = Parsing.MarkupParser.Parse(navHint, Color.Grey, bgColor);
				for (int h = 0; h < navHintCells.Count; h++)
					buffer.SetCell(hintStartX + h, headerY, navHintCells[h]);
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
