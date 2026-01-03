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
	/// Represents a control that can receive keyboard input and be focused.
	/// </summary>
	public interface IInteractiveControl
	{
		/// <summary>Gets or sets whether this control currently has keyboard focus.</summary>
		bool HasFocus { get; set; }

		/// <summary>Gets or sets whether this control is enabled and can receive input.</summary>
		bool IsEnabled { get; set; }

		/// <summary>
		/// Processes a keyboard input event.
		/// </summary>
		/// <param name="key">The key information for the pressed key.</param>
		/// <returns>True if the key was handled by this control; otherwise, false.</returns>
		bool ProcessKey(ConsoleKeyInfo key);
	}
}