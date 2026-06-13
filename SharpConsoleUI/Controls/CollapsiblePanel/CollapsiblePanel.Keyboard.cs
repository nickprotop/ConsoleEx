// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Keyboard activation for <see cref="CollapsiblePanel"/>. When the header is focused,
	/// Enter or Space toggles the expanded state.
	/// </summary>
	public partial class CollapsiblePanel
	{
		#region IInteractiveControl — Keyboard

		/// <inheritdoc/>
		/// <remarks>
		/// Toggles the expanded state when the panel is focused and Enter or Space is pressed.
		/// Returns <see langword="false"/> for any other key (or when not focused/disabled) so the
		/// key can bubble to focus traversal.
		/// </remarks>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !HasFocus)
				return false;

			if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar)
			{
				Toggle();
				return true;
			}

			return false;
		}

		#endregion
	}
}
