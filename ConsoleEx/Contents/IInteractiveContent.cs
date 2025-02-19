// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx.Contents
{
	public interface IInteractiveContent
	{
		public bool HasFocus { get; set; }
		public bool IsEnabled { get; set; }

		public (int Left, int Top)? GetCursorPosition();

		public bool ProcessKey(ConsoleKeyInfo key);
	}
}