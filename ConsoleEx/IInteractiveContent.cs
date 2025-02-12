// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx
{
    public interface IInteractiveContent
    {
        public bool IsEnabled { get; set; }
		public bool ProcessKey(ConsoleKeyInfo key);
        public (int Left, int Top) GetCursorPosition();

	}
}
