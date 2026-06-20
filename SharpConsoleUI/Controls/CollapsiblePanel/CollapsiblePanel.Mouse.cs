// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Drawing;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Mouse activation for <see cref="CollapsiblePanel"/>. A left click anywhere on the header
	/// row focuses the panel and toggles the expanded state. Clicks on the body are not handled
	/// here — the layout engine routes those to the child controls.
	/// </summary>
	public partial class CollapsiblePanel
	{
		#region IMouseAwareControl Implementation

		// Body-interaction hover/double-click tracking (mirrors PanelControl). Only used for the
		// non-header body events raised by RaiseBodyMouseEvent.
		private bool _isMouseInside = false;
		private DateTime _lastClickTime = DateTime.MinValue;
		private int _clickCount = 0;

		/// <inheritdoc/>
		public bool WantsMouseEvents => true;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => true;

		/// <summary>
		/// Fired on a left click — on a collapsible header (which also toggles the panel) or on the
		/// expanded body when no body child consumes the click.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <inheritdoc/>
		/// <remarks>
		/// Handles a left click on the header row by focusing the panel and toggling its expanded
		/// state. A click on the expanded body is forwarded to the body child under the cursor
		/// (focusing it when it is focusable) so that mouse focus reaches body controls even when
		/// the panel is hosted inside a self-painting forwarding container (e.g. a
		/// <see cref="ScrollablePanelControl"/>) whose DOM hit-test resolves the panel rather than
		/// the deep body child. When the panel is a direct DOM child of the window the dispatcher
		/// already targets the body child directly, so this forwarding is a harmless no-op there.
		/// </remarks>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!_isEnabled)
				return false;

			int headerTop = Margin.Top;
			int headerBottom = headerTop + HeaderHeight; // exclusive
			bool onHeader = args.Position.Y >= headerTop && args.Position.Y < headerBottom;

			if (_collapsible && onHeader && args.HasFlag(MouseFlags.Button1Clicked))
			{
				(this as IWindowControl).GetParentWindow()?.FocusManager.SetFocus(this, FocusReason.Mouse);
				Toggle();
				MouseClick?.Invoke(this, args);
				args.Handled = true;
				return true;
			}

			// Below the header: when expanded, route the click to the body child under the cursor so
			// clicking a focusable body control focuses it (mirrors ColumnContainer.ProcessMouseEvent).
			// When no body child consumes the event, raise the panel's own mouse event so a hosting
			// facade (or direct subscriber) can react to content-only body interaction.
			if (!onHeader && _isExpanded)
			{
				bool consumed = ForwardClickToBody(args);
				if (!consumed)
					RaiseBodyMouseEvent(args);
				return consumed;
			}

			return false;
		}

		/// <summary>
		/// Raises the panel's own mouse event for a body interaction that no body child consumed.
		/// Mirrors <see cref="PanelControl.ProcessMouseEvent"/> flag handling (enter/leave/right/
		/// double/click/move). Wheel/scroll events are intentionally ignored here so they bubble
		/// untouched to an outer scroll container.
		/// </summary>
		private void RaiseBodyMouseEvent(MouseEventArgs args)
		{
			if (args.Handled)
				return;

			// Mouse leave.
			if (args.HasFlag(MouseFlags.MouseLeave))
			{
				if (_isMouseInside)
				{
					_isMouseInside = false;
					MouseLeave?.Invoke(this, args);
					Container?.Invalidate(true);
				}
				return;
			}

			// Mouse enter (first report-position while outside).
			if (!_isMouseInside && args.HasFlag(MouseFlags.ReportMousePosition))
			{
				_isMouseInside = true;
				MouseEnter?.Invoke(this, args);
				Container?.Invalidate(true);
			}

			// Right-click.
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				args.Handled = true;
				return;
			}

			// Wheel/scroll: never raise a panel event — let it bubble to an outer scroll container.
			if (args.HasFlag(MouseFlags.WheeledUp) || args.HasFlag(MouseFlags.WheeledDown) ||
				args.HasFlag(MouseFlags.WheeledLeft) || args.HasFlag(MouseFlags.WheeledRight))
			{
				return;
			}

			// Driver-provided double-click (preferred).
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				_clickCount = 0;
				_lastClickTime = DateTime.MinValue;
				MouseDoubleClick?.Invoke(this, args);
				args.Handled = true;
				return;
			}

			// Left click with manual double-click detection (fallback).
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				var now = DateTime.UtcNow;
				var timeSince = (now - _lastClickTime).TotalMilliseconds;
				bool isDoubleClick = timeSince <= ControlDefaults.DefaultDoubleClickThresholdMs &&
									_clickCount == 1;

				if (isDoubleClick)
				{
					_clickCount = 0;
					_lastClickTime = DateTime.MinValue;
					MouseDoubleClick?.Invoke(this, args);
				}
				else
				{
					_clickCount = 1;
					_lastClickTime = now;
					MouseClick?.Invoke(this, args);
				}

				args.Handled = true;
				return;
			}

			// Mouse movement.
			if (args.HasFlag(MouseFlags.ReportMousePosition))
			{
				MouseMove?.Invoke(this, args);
			}
		}

		/// <summary>
		/// Routes a body mouse event to the visible body child under the cursor.
		/// <para>
		/// For CLICK events: focuses the child when it (or a focusable descendant) can receive focus,
		/// forwards a child-relative copy of the event, and consumes the event (returns
		/// <see langword="true"/>) even when the child does not react, so a click on a focusable-but-passive
		/// body area still focuses it and does not leak to outer containers.
		/// </para>
		/// <para>
		/// For WHEEL/SCROLL events: forwards a child-relative copy to the child WITHOUT changing focus
		/// (a wheel must not steal focus). Returns the child's result — <see langword="true"/> when the
		/// child consumes the scroll, otherwise <see langword="false"/> with <see cref="MouseEventArgs.Handled"/>
		/// left unset so the event BUBBLES UP to an outer scroll container (e.g. a root
		/// <see cref="ScrollablePanelControl"/>).
		/// </para>
		/// Returns <see langword="false"/> when the cursor falls in body padding with no child underneath
		/// so the host can keep bubbling.
		/// </summary>
		private bool ForwardClickToBody(MouseEventArgs args)
		{
			// Distinguish wheel/scroll from click. WheeledLeft/WheeledRight are composites that include
			// the WheeledUp/WheeledDown bits respectively, so testing those two covers horizontal wheel too.
			bool isWheel = args.HasFlag(MouseFlags.WheeledUp) || args.HasFlag(MouseFlags.WheeledDown);

			bool bordered = IsBordered;
			int sideInset = bordered ? 1 : 0;

			// Body content origin within the panel (matches CollapsibleLayout.ArrangeChildren).
			int bodyTop = Margin.Top + HeaderHeight;
			int bodyLeft = Margin.Left + sideInset;

			IReadOnlyList<IWindowControl> snapshot = GetChildren();

			int y = bodyTop;
			foreach (var child in snapshot)
			{
				if (!child.Visible)
					continue;

				int childHeight = child.ActualHeight > 0
					? child.ActualHeight
					: child.GetLogicalContentSize().Height;

				if (args.Position.Y >= y && args.Position.Y < y + childHeight)
				{
					if (isWheel)
					{
						// Wheel/scroll: forward to the child WITHOUT stealing focus. If the child
						// consumes it, we are done; otherwise let it bubble up to an outer scroll
						// container by returning false and NOT marking the event handled.
						if (child is IMouseAwareControl wheelChild && wheelChild.WantsMouseEvents)
						{
							var wheelPosition = new Point(args.Position.X - bodyLeft, args.Position.Y - y);
							var wheelArgs = args.WithPosition(wheelPosition);
							if (wheelChild.ProcessMouseEvent(wheelArgs))
							{
								args.Handled = true;
								return true;
							}
						}

						return false; // unconsumed scroll bubbles to the panel's container
					}

					// Click: focus the child (or its focusable descendant) before forwarding, so
					// subsequent keyboard/wheel input routes to it. HandleClick walks UP from the hit,
					// so passing the child focuses the child (or the nearest focusable ancestor within it).
					(this as IWindowControl).GetParentWindow()?.FocusManager.HandleClick(child);

					if (child is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
					{
						var childPosition = new Point(args.Position.X - bodyLeft, args.Position.Y - y);
						var childArgs = args.WithPosition(childPosition);
						if (mouseAware.ProcessMouseEvent(childArgs))
						{
							args.Handled = true;
							return true;
						}
					}

					// The child did not consume the click. If it (or a descendant) is a focus
					// target, focus was set above, so report the click as handled. Otherwise the
					// child is passive — leave the event unconsumed so the panel can raise its own
					// body mouse event for a hosting facade or direct subscriber.
					if (HasFocusableTarget(child))
					{
						args.Handled = true;
						return true;
					}

					return false;
				}

				y += childHeight;
			}

			return false; // event fell in body padding with no child underneath
		}

		/// <summary>
		/// Returns <see langword="true"/> when <paramref name="control"/> itself, or any of its
		/// descendants, can currently receive focus. Used to decide whether a body click on a
		/// non-consuming child should still be treated as handled (because focus was taken) or
		/// allowed to surface as the panel's own body mouse event.
		/// </summary>
		private static bool HasFocusableTarget(IWindowControl control)
		{
			if (control is IFocusableControl focusable && focusable.CanReceiveFocus)
				return true;

			if (control is IContainerControl container)
			{
				foreach (var child in container.GetChildren())
				{
					if (child.Visible && HasFocusableTarget(child))
						return true;
				}
			}

			return false;
		}

		#endregion
	}
}
