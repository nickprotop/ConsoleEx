// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A portal content control that acts as a proper container for child controls.
	/// Unlike DropdownPortalContent and MenuPortalContent which paint manually,
	/// this container hosts arbitrary child controls (ListControl, ButtonControl,
	/// ScrollablePanelControl, etc.) with layout, mouse routing, keyboard delegation,
	/// and focus tracking.
	/// </summary>
	public class PortalContentContainer : PortalContentBase, IContainer, IContainerControl, IFocusTrackingContainer
	{
		#region Fields

		private readonly object _childrenLock = new();
		private readonly List<IWindowControl> _children = new();
		private IInteractiveControl? _focusedChild;
		private Rectangle _portalBounds;
		private Color? _backgroundColor;
		private Color _foregroundColor = Color.White;
		private int _viewportWidth;
		private int _viewportHeight;

		#endregion

		#region Portal Bounds

		/// <summary>
		/// Gets or sets the absolute screen bounds for this portal overlay.
		/// </summary>
		public Rectangle PortalBounds
		{
			get => _portalBounds;
			set
			{
				_portalBounds = value;
				Invalidate();
			}
		}

		/// <inheritdoc/>
		public override Rectangle GetPortalBounds() => _portalBounds;

		#endregion

		#region Child Management

		/// <summary>
		/// Adds a child control to the portal container.
		/// </summary>
		public void AddChild(IWindowControl child)
		{
			lock (_childrenLock)
			{
				_children.Add(child);
			}
			child.Container = this;
			Invalidate();
		}

		/// <summary>
		/// Removes a child control from the portal container.
		/// </summary>
		public void RemoveChild(IWindowControl child)
		{
			lock (_childrenLock)
			{
				if (!_children.Remove(child))
					return;

				if (_focusedChild == child as IInteractiveControl)
					_focusedChild = null;
			}

			child.Container = null;
			Invalidate();
		}

		/// <summary>
		/// Removes all child controls and disposes them.
		/// </summary>
		public void ClearChildren()
		{
			List<IWindowControl> snapshot;
			lock (_childrenLock)
			{
				_focusedChild = null;
				snapshot = new List<IWindowControl>(_children);
				_children.Clear();
			}
			foreach (var child in snapshot)
			{
				child.Container = null;
				child.Dispose();
			}
			Invalidate();
		}

		/// <summary>
		/// Gets the child controls in this container.
		/// </summary>
		public IReadOnlyList<IWindowControl> Children
		{
			get
			{
				lock (_childrenLock)
				{
					return new List<IWindowControl>(_children);
				}
			}
		}

		#endregion

		#region IContainer Implementation

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveBackground(_backgroundColor, Container);
			set { _backgroundColor = value; Invalidate(); }
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			get => _foregroundColor;
			set { _foregroundColor = value; Invalidate(); }
		}

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;

		/// <inheritdoc/>
		public bool IsDirty
		{
			get => false;
			set { /* Portal invalidation is via parent container */ }
		}

		/// <inheritdoc/>
		public void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public int? GetVisibleHeightForControl(IWindowControl control)
		{
			return _viewportHeight > 0 ? _viewportHeight : _portalBounds.Height;
		}

		#endregion

		#region IContainerControl Implementation

		/// <inheritdoc/>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			lock (_childrenLock)
			{
				return new List<IWindowControl>(_children);
			}
		}

		#endregion

		#region IFocusTrackingContainer Implementation

		/// <inheritdoc/>
		public void NotifyChildFocusChanged(IInteractiveControl child, bool hasFocus)
		{
			if (hasFocus)
			{
				if (_focusedChild != null && _focusedChild != child && _focusedChild is IFocusableControl oldFc)
					oldFc.HasFocus = false;

				_focusedChild = child;
			}
			else if (_focusedChild == child)
			{
				_focusedChild = null;
			}
		}

		#endregion

		#region Keyboard Input

		/// <summary>
		/// Processes keyboard input by delegating to the focused child or cycling focus with Tab.
		/// Returns true if the key was handled.
		/// </summary>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			// Delegate to focused child first
			if (_focusedChild is IInteractiveControl interactive)
			{
				if (interactive.ProcessKey(key))
					return true;
			}

			// Tab/Shift+Tab: cycle focus among focusable children
			if (key.Key == ConsoleKey.Tab)
			{
				bool backward = (key.Modifiers & ConsoleModifiers.Shift) != 0;
				return CycleFocus(backward);
			}

			return false;
		}

		/// <summary>
		/// Sets focus on the first focusable child control.
		/// </summary>
		public void SetFocusOnFirstChild()
		{
			SetFocusOnChild(backward: false);
		}

		/// <summary>
		/// Sets focus on the last focusable child control.
		/// </summary>
		public void SetFocusOnLastChild()
		{
			SetFocusOnChild(backward: true);
		}

		private void SetFocusOnChild(bool backward)
		{
			var focusable = GetFocusableChildren();
			if (focusable.Count == 0) return;

			var target = backward ? focusable[focusable.Count - 1] : focusable[0];
			FocusChild(target);
		}

		private bool CycleFocus(bool backward)
		{
			var focusable = GetFocusableChildren();
			if (focusable.Count == 0) return false;

			int currentIndex = _focusedChild != null
				? focusable.IndexOf(_focusedChild)
				: -1;

			int nextIndex;
			if (currentIndex < 0)
			{
				nextIndex = backward ? focusable.Count - 1 : 0;
			}
			else
			{
				nextIndex = backward ? currentIndex - 1 : currentIndex + 1;
				// Wrap around
				if (nextIndex < 0 || nextIndex >= focusable.Count)
					return false; // Let owner handle (e.g., close portal)
			}

			FocusChild(focusable[nextIndex]);
			return true;
		}

		private void FocusChild(IInteractiveControl child)
		{
			// Unfocus previous
			if (_focusedChild != null && _focusedChild != child && _focusedChild is IFocusableControl oldFc)
				oldFc.SetFocus(false, FocusReason.Programmatic);

			_focusedChild = child;

			if (child is IDirectionalFocusControl dfc)
				dfc.SetFocusWithDirection(true, false);
			else if (child is IFocusableControl fc)
				fc.SetFocus(true, FocusReason.Programmatic);
		}

		private List<IInteractiveControl> GetFocusableChildren()
		{
			List<IWindowControl> snapshot;
			lock (_childrenLock)
			{
				snapshot = new List<IWindowControl>(_children);
			}
			var result = new List<IInteractiveControl>();
			foreach (var child in snapshot)
			{
				if (child.Visible && child is IFocusableControl fc && fc.CanReceiveFocus && child is IInteractiveControl ic)
					result.Add(ic);
			}
			return result;
		}

		#endregion

		#region Mouse Input

		/// <inheritdoc/>
		public override bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (args.Handled) return false;

			// Forward mouse wheel to focused child first
			bool isWheel = args.HasFlag(Drivers.MouseFlags.WheeledUp) || args.HasFlag(Drivers.MouseFlags.WheeledDown);
			if (isWheel && _focusedChild is IMouseAwareControl wheelTarget && wheelTarget.WantsMouseEvents)
			{
				if (wheelTarget.ProcessMouseEvent(args))
					return true;
			}

			// Handle click events
			if (args.HasAnyFlag(
				Drivers.MouseFlags.Button1Clicked, Drivers.MouseFlags.Button1Pressed, Drivers.MouseFlags.Button1Released,
				Drivers.MouseFlags.Button1DoubleClicked, Drivers.MouseFlags.Button1TripleClicked,
				Drivers.MouseFlags.Button2Clicked, Drivers.MouseFlags.Button2Pressed, Drivers.MouseFlags.Button2Released,
				Drivers.MouseFlags.Button3Clicked, Drivers.MouseFlags.Button3Pressed, Drivers.MouseFlags.Button3Released))
			{
				var hitResult = HitTestChild(args.Position);
				if (hitResult.Child != null)
				{
					// Focus clicked child if focusable
					if (hitResult.Child is IFocusableControl focusable && focusable.CanReceiveFocus &&
						hitResult.Child is IInteractiveControl interactive)
					{
						if (_focusedChild != interactive)
						{
							// Unfocus previous
							if (_focusedChild is IFocusableControl oldFc)
								oldFc.SetFocus(false, FocusReason.Mouse);

							focusable.SetFocus(true, FocusReason.Mouse);
							_focusedChild = interactive;
						}
					}

					// Forward mouse event to child
					if (hitResult.Child is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
					{
						var childArgs = args.WithPosition(hitResult.ChildRelativePosition);
						if (mouseAware.ProcessMouseEvent(childArgs))
						{
							args.Handled = true;
							return true;
						}
					}
				}
				else
				{
					// Clicked on empty space - unfocus children
					if (_focusedChild is IFocusableControl fc)
						fc.SetFocus(false, FocusReason.Mouse);
					_focusedChild = null;
					Container?.Invalidate(true);
					return true;
				}
			}

			return false;
		}

		private (IWindowControl? Child, Point ChildRelativePosition) HitTestChild(Point position)
		{
			int contentY = position.Y;
			int currentY = 0;

			List<IWindowControl> snapshot;
			lock (_childrenLock)
			{
				snapshot = new List<IWindowControl>(_children);
			}

			foreach (var child in snapshot)
			{
				if (!child.Visible) continue;

				int childHeight = MeasureChildHeight(child);
				if (contentY >= currentY && contentY < currentY + childHeight)
				{
					var childRelative = new Point(position.X, contentY - currentY);
					return (child, childRelative);
				}
				currentY += childHeight;
			}

			return (null, Point.Empty);
		}

		private int MeasureChildHeight(IWindowControl child)
		{
			int availableWidth = _viewportWidth > 0 ? _viewportWidth : _portalBounds.Width;
			int availableHeight = _viewportHeight > 0 ? _viewportHeight : _portalBounds.Height;
			var childNode = LayoutNodeFactory.CreateSubtree(child);
			childNode.IsVisible = true;
			var constraints = new LayoutConstraints(1, availableWidth, 1, availableHeight);
			childNode.Measure(constraints);
			return childNode.DesiredSize.Height;
		}

		#endregion

		#region Painting

		/// <inheritdoc/>
		protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			var bgColor = BackgroundColor;
			var fgColor = _foregroundColor;

			_viewportWidth = bounds.Width;
			_viewportHeight = bounds.Height;

			// Fill background
			buffer.FillRect(bounds.Intersect(clipRect), ' ', fgColor, bgColor);

			// Get renderer for registering child bounds (needed for cursor position lookups)
			var parentWindow = ((IWindowControl)this).GetParentWindow();
			var renderer = parentWindow?.Renderer;

			List<IWindowControl> childrenSnapshot;
			lock (_childrenLock)
			{
				childrenSnapshot = new List<IWindowControl>(_children);
			}

			int currentY = 0;
			foreach (var child in childrenSnapshot)
			{
				if (!child.Visible) continue;

				var childNode = LayoutNodeFactory.CreateSubtree(child);
				childNode.IsVisible = true;

				// Measure child
				int maxChildHeight = (child.VerticalAlignment == Layout.VerticalAlignment.Fill)
					? _viewportHeight : int.MaxValue;
				var constraints = new LayoutConstraints(1, _viewportWidth, 1, maxChildHeight);
				childNode.Measure(constraints);
				int childHeight = childNode.DesiredSize.Height;

				// Only render if in viewport
				if (currentY + childHeight > 0 && currentY < _viewportHeight)
				{
					var childBounds = new LayoutRect(
						bounds.X,
						bounds.Y + currentY,
						_viewportWidth,
						childHeight);

					childNode.Arrange(childBounds);

					// Register child bounds for cursor position lookups
					renderer?.UpdateChildBounds(child, childBounds);

					var childClipRect = clipRect.Intersect(bounds);
					childNode.Paint(buffer, childClipRect, fgColor, bgColor);
				}

				currentY += childHeight;
			}
		}

		/// <inheritdoc/>
		public new LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			var bounds = GetPortalBounds();
			return new LayoutSize(bounds.Width, bounds.Height);
		}

		#endregion
	}
}
