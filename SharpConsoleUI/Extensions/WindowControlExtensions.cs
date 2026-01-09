// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Extensions;

/// <summary>
/// Extension methods for IWindowControl.
/// </summary>
public static class WindowControlExtensions
{
	/// <summary>
	/// Gets the parent Window by walking up the container hierarchy.
	/// Returns null if the control is not attached to a window.
	/// </summary>
	/// <param name="control">The control to find the parent window for.</param>
	/// <returns>The parent Window, or null if not attached to a window.</returns>
	public static Window? GetParentWindow(this IWindowControl control)
	{
		IContainer? container = control.Container;

		while (container != null)
		{
			// If container is a Window, return it
			if (container is Window window)
				return window;

			// If container is a control (e.g., ColumnContainer, ToolbarControl),
			// walk up to its parent
			if (container is IWindowControl parentControl)
				container = parentControl.Container;
			else
				break;
		}

		return null;
	}
}
