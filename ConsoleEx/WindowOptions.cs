// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;

namespace ConsoleEx
{
	public class WindowOptions
	{
		public Color? BackgroundColor { get; set; }
		public Color? ForegroundColor { get; set; }
		public int Height { get; set; }
		public bool IsMoveable { get; set; } = true;
		public bool IsResizable { get; set; } = true;
		public bool IsScrollable { get; set; } = true;
		public int Left { get; set; }
		public string Title { get; set; } = "Window";
		public int Top { get; set; }
		public int Width { get; set; }
		public WindowState WindowState { get; set; } = WindowState.Normal;
	}
}