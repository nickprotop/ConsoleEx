// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx
{
	public class Size
	{
		public Size(int width, int height)
		{
			Width = width;
			Height = height;
		}

		public int Height { get; set; }
		public int Width { get; set; }
	}
}