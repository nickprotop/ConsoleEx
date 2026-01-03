// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides helper methods for content layout and positioning calculations.
	/// </summary>
	public static class ContentHelper
	{
		/// <summary>
		/// Calculates the starting position to center content within a given width.
		/// </summary>
		/// <param name="availableWidth">The total available width for positioning.</param>
		/// <param name="contentWidth">The width of the content to be centered.</param>
		/// <returns>
		/// The starting position (left offset) to center the content.
		/// Returns 0 if the content is wider than the available width.
		/// </returns>
		public static int GetCenter(int availableWidth, int contentWidth)
		{
			int center = (availableWidth - contentWidth) / 2;
			if (center < 0) center = 0;
			return center;
		}
	}
}