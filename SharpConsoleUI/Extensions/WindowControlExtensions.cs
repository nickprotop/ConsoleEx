// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
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

	/// <summary>
	/// Requests keyboard focus for this control via the owning window's <see cref="FocusManager"/>.
	/// Works for controls inside portals, panels, and any container depth.
	/// No-op if the control is not attached to a window.
	/// </summary>
	/// <param name="control">The control to focus.</param>
	/// <param name="reason">The reason for the focus change. Defaults to <see cref="FocusReason.Programmatic"/>.</param>
	public static void RequestFocus(this IFocusableControl control,
		FocusReason reason = FocusReason.Programmatic)
	{
		(control as IWindowControl)?.GetParentWindow()?.FocusManager.SetFocus(control, reason);
	}

	/// <summary>
	/// Finds the nearest <see cref="PortalContentBase"/> ancestor by walking the
	/// container chain, then falling back to the static portal focus registry
	/// (for PortalContentBase subclasses where the child's Container chain
	/// doesn't include the portal).
	/// </summary>
	public static PortalContentBase? GetPortalAncestor(this IWindowControl control)
	{
		IContainer? container = control.Container;
		while (container != null)
		{
			if (container is PortalContentBase portal) return portal;
			if (container is IWindowControl wc) container = wc.Container;
			else break;
		}

		if (control is IFocusableControl fc
			&& PortalContentBase.TryGetPortalOwner(fc, out var owner))
			return owner;

		return null;
	}

	/// <summary>
	/// Checks portal focus first, then window FocusManager.IsFocused.
	/// Used by leaf controls for their HasFocus property.
	/// </summary>
	internal static bool ComputeHasFocus(this IFocusableControl control)
	{
		if (control is IWindowControl wc)
		{
			var portal = wc.GetPortalAncestor();
			if (portal != null)
				return ReferenceEquals(portal.PortalFocusedControl, control);
			return wc.GetParentWindow()?.FocusManager.IsFocused(control) ?? false;
		}
		return false;
	}

	/// <summary>
	/// Checks portal focus first, then window FocusManager.IsInFocusPath.
	/// Used by container controls for their HasFocus property.
	/// </summary>
	internal static bool ComputeIsInFocusPath(this IFocusableControl control)
	{
		if (control is IWindowControl wc)
		{
			var portal = wc.GetPortalAncestor();
			if (portal != null)
			{
				var focused = portal.PortalFocusedControl;
				if (focused == null) return false;
				if (ReferenceEquals(focused, control)) return true;
				// Check if portal-focused control is a descendant of this container
				IContainer? c = (focused as IWindowControl)?.Container;
				while (c != null)
				{
					if (ReferenceEquals(c, control)) return true;
					if (c is IWindowControl cwc) c = cwc.Container;
					else break;
				}
				return false;
			}
			return wc.GetParentWindow()?.FocusManager.IsInFocusPath(control) ?? false;
		}
		return false;
	}
}
