// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	public partial class CanvasControl
	{
		#region IInteractiveControl

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!IsEnabled || !HasFocus || (CanvasKeyPressed == null && CanvasKeyPressedAsync == null))
				return false;

			Core.AsyncEvent.Raise(CanvasKeyPressed, CanvasKeyPressedAsync, this, key, Container?.GetConsoleWindowSystem?.LogService);
			return true;
		}

		#endregion
	}
}
