// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers.Scrollbar
{
	/// <summary>
	/// Everything the scrollbar maths needs, on one axis. Axis-agnostic: "length" and "extent" are
	/// rows for a vertical bar and columns for a horizontal one.
	/// </summary>
	/// <param name="TrackLength">Cells available to the whole bar, arrows included.</param>
	/// <param name="ContentExtent">Total length of the content being scrolled.</param>
	/// <param name="ViewportExtent">Length of the visible window onto that content.</param>
	/// <param name="Offset">Current scroll offset.</param>
	/// <param name="MinThumbLength">Smallest thumb the bar may draw (at least 1).</param>
	public readonly record struct ScrollbarMetrics(
		int TrackLength,
		int ContentExtent,
		int ViewportExtent,
		int Offset,
		int MinThumbLength = 1);
}
