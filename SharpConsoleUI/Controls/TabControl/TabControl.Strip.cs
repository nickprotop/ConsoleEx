// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Which tabs the header row can show, and where each of them sits on it.
	/// </summary>
	/// <param name="First">Index of the first tab drawn.</param>
	/// <param name="Count">How many tabs are drawn, starting at <paramref name="First"/>.</param>
	/// <param name="MoreLeft">Whether tabs exist before <paramref name="First"/>.</param>
	/// <param name="MoreRight">Whether tabs exist after the last drawn one.</param>
	/// <param name="Offsets">
	/// Each drawn tab's X, relative to the left edge of the header area (that is, past
	/// <see cref="BaseControl.Margin"/>'s left). A separator, where there is one, sits immediately after
	/// the tab at <c>Offsets[i] + Widths[i]</c>.
	/// </param>
	/// <param name="Widths">Each drawn tab's width: its title, the space either side, and its close button.</param>
	internal readonly record struct TabStripLayout(
		int First,
		int Count,
		bool MoreLeft,
		bool MoreRight,
		IReadOnlyList<int> Offsets,
		IReadOnlyList<int> Widths);

	public partial class TabControl
	{
		/// <summary>
		/// Lays the header row out: the run of tabs that fits, positioned so the active one is always
		/// among them, and whether there are tabs past either end.
		/// <para>
		/// The scroll position is <b>derived, never stored</b>: it is the smallest starting index that
		/// still leaves room for the active tab. That makes the strip a pure function of its tabs, its
		/// active index and its width, so painting and hit-testing cannot disagree about it, and moving
		/// back towards the first tab always returns the strip to its unscrolled state.
		/// </para>
		/// <para>
		/// A width of zero or less means the control has not been painted yet and its arranged width is
		/// not known. Everything is reported as drawn in that case, which is what callers assumed before
		/// there was any scrolling at all.
		/// </para>
		/// </summary>
		/// <param name="tabs">The tab pages, in strip order.</param>
		/// <param name="activeIndex">The active tab, which the returned run always contains.</param>
		/// <param name="available">Cells the header row has for tabs and indicators.</param>
		internal TabStripLayout LayoutStrip(IReadOnlyList<TabPage> tabs, int activeIndex, int available)
		{
			if (tabs.Count == 0)
				return new TabStripLayout(0, 0, false, false, Array.Empty<int>(), Array.Empty<int>());

			var widths = new int[tabs.Count];
			for (int i = 0; i < tabs.Count; i++)
				widths[i] = TabWidth(tabs[i]);

			if (available <= 0)
				return Unscrolled(tabs.Count, widths);

			int active = Math.Clamp(activeIndex, 0, tabs.Count - 1);
			int indicator = _showTabScrollIndicators ? ControlDefaults.TabScrollIndicatorWidth : 0;

			// The smallest first index that still reaches the end of the active tab. Monotone in `first`,
			// so the first one that fits is the least one — and when even the active tab alone is wider
			// than the row, the loop lands on it and it is drawn clipped, as it would have been anyway.
			int first = 0;
			while (first < active && !Reaches(first, active, widths, tabs.Count, indicator, available))
				first++;

			var offsets = new List<int>(tabs.Count - first);
			var drawn = new List<int>(tabs.Count - first);
			int x = first > 0 ? indicator : 0;
			int last = first;

			for (int i = first; i < tabs.Count; i++)
			{
				int separator = i > first ? 1 : 0;

				// Room for the indicator has to be kept back while any tab still follows this one, or the
				// strip fills to its last cell and has nowhere to say that it was truncated.
				int reserve = i < tabs.Count - 1 ? indicator : 0;
				if (i > first && x + separator + widths[i] + reserve > available)
					break;

				x += separator;
				offsets.Add(x);
				drawn.Add(widths[i]);
				x += widths[i];
				last = i;
			}

			return new TabStripLayout(first, drawn.Count, first > 0, last < tabs.Count - 1, offsets, drawn);
		}

		/// <summary>One tab's width: its title, the space either side of it, and its close button.</summary>
		private static int TabWidth(TabPage tab) =>
			MarkupParser.StripLength(tab.Title) + 2 + (tab.IsClosable ? 1 : 0);

		/// <summary>
		/// Whether a strip starting at <paramref name="first"/> reaches the end of the tab at
		/// <paramref name="active"/> — counting the separators between them and whichever scroll
		/// indicators that run would need.
		/// </summary>
		private static bool Reaches(
			int first, int active, IReadOnlyList<int> widths, int count, int indicator, int available)
		{
			int needed = first > 0 ? indicator : 0;
			for (int i = first; i <= active; i++)
				needed += widths[i] + (i < active ? 1 : 0);
			if (active < count - 1)
				needed += indicator;

			return needed <= available;
		}

		private static TabStripLayout Unscrolled(int count, IReadOnlyList<int> widths)
		{
			var offsets = new int[count];
			int x = 0;
			for (int i = 0; i < count; i++)
			{
				offsets[i] = x;
				x += widths[i] + 1; // + separator
			}

			return new TabStripLayout(0, count, false, false, offsets, widths);
		}
	}
}
