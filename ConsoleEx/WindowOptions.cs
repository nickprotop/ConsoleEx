// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx
{
    public class WindowOptions
    {
        public string Title { get; set; } = "Window";
        public int Top { get; set; }
        public int Left { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsResizable { get; set; } = true;
        public bool IsMoveable { get; set; } = true;
        public string BackgroundColor { get; set; } = "black";
        public string ForegroundColor { get; set; } = "white";
    }
}