// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	public interface IInteractiveControl
	{
		public bool HasFocus { get; set; }
		public bool IsEnabled { get; set; }

		public (int Left, int Top)? GetCursorPosition();

		public bool ProcessKey(ConsoleKeyInfo key);

		public void SetFocus(bool focus, bool backward);
	}
}