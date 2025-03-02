// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace ConsoleEx.Controls
{
	public interface IContainer
	{
		public Color BackgroundColor { get; set; }
		public Color ForegroundColor { get; set; }

		public ConsoleWindowSystem? GetConsoleWindowSystem { get; }

		public bool IsDirty { get; set; }

		public void Invalidate(bool redrawAll);
	}
}