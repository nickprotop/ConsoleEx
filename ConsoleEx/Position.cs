// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx
{
	public class Position
	{
		public Position(int x, int y)
		{
			this.X = x;
			this.Y = y;
		}

		public int X { get; set; }
		public int Y { get; set; }
	}
}