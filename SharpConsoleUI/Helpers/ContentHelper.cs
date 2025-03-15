// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	public static class ContentHelper
	{
		public static int GetCenter(int availableWidth, int contentWidth)
		{
			int center = (availableWidth - contentWidth) / 2;
			if (center < 0) center = 0;
			return center;
		}
	}
}