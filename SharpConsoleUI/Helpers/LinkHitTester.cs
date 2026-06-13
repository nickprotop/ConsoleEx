// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Resolves a clickable <see cref="LinkSpan"/> under a control-relative mouse position.
	/// Generalizes the per-row link lookup so any markup-rendering control can hit-test links
	/// against a per-row list of spans (display-column coordinates).
	/// </summary>
	public static class LinkHitTester
	{
		/// <summary>
		/// Returns the span on <paramref name="rowSpans"/> covering the display column derived from
		/// <paramref name="relativeX"/> minus <paramref name="originX"/> (the row's painted left
		/// origin, i.e. margin + alignment offset), or <c>null</c> if none covers it.
		/// </summary>
		/// <param name="rowSpans">The link spans on the hit row (display-column coordinates). May be null/empty.</param>
		/// <param name="originX">The row's painted left origin (margin + alignment offset).</param>
		/// <param name="relativeX">The control-relative mouse X position.</param>
		/// <returns>The covering <see cref="LinkSpan"/>, or <c>null</c> if none covers the column.</returns>
		public static LinkSpan? FindAt(IReadOnlyList<LinkSpan> rowSpans, int originX, int relativeX)
		{
			if (rowSpans == null || rowSpans.Count == 0) return null;
			int col = relativeX - originX;
			foreach (var s in rowSpans)
				if (s.Contains(col)) return s;
			return null;
		}
	}
}
