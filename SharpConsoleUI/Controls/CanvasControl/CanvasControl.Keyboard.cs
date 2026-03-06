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
			if (!IsEnabled || !HasFocus || CanvasKeyPressed == null)
				return false;

			CanvasKeyPressed.Invoke(this, key);
			return true;
		}

		#endregion
	}
}
