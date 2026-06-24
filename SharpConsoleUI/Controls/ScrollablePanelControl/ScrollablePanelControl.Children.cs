// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class ScrollablePanelControl
	{
		#region Child Control Management

		/// <summary>
		/// Adds a child control to the panel.
		/// This method is not thread-safe and must be called from the UI thread.
		/// For multi-threaded scenarios, queue additions and process them during paint.
		/// </summary>
		public void AddControl(IWindowControl control)
		{
			lock (_childrenLock)
			{
				_children.Add(control);
			}
			control.Container = this;
			// If the panel has focus but no focused child yet, focus the new control
			// if it's focusable. Restores focus routing after ClearContents.
			if (HasFocus && GetFocusedChildFromCoordinator() == null &&
				control.Visible &&
				control is IFocusableControl fc && fc.CanReceiveFocus)
			{
				var w = this.GetParentWindow();
				if (w != null)
					w.FocusManager.SetFocus(fc, FocusReason.Programmatic);
				else
					fc.Container?.Invalidate(Invalidation.Repaint);
			}
			// SPC is a layout-tree participant: a new child must be built into the DOM tree
			// (a plain Invalidate only re-measures the existing tree, which would omit it).
			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Inserts a child control at the specified index in the panel.
		/// This method is not thread-safe and must be called from the UI thread.
		/// </summary>
		/// <param name="index">The zero-based index at which to insert the control.</param>
		/// <param name="control">The control to insert.</param>
		public void InsertControl(int index, IWindowControl control)
		{
			lock (_childrenLock)
			{
				index = Math.Clamp(index, 0, _children.Count);
				_children.Insert(index, control);
			}
			control.Container = this;
			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Removes a child control from the panel.
		/// This method is not thread-safe and must be called from the UI thread.
		/// </summary>
		public void RemoveControl(IWindowControl control)
		{
			// If removing the focused child, clear focus
			var focusedChild = GetFocusedChildFromCoordinator();
			if (focusedChild != null && focusedChild == control as IInteractiveControl)
			{
				var w1 = this.GetParentWindow();
				if (w1 != null) w1.FocusManager.SetFocus(null, FocusReason.Programmatic);
			}

			// Clear remembered child if it's being removed
			if (_lastInternalFocusedChild == control as IInteractiveControl)
				_lastInternalFocusedChild = null;

			bool removed;
			lock (_childrenLock)
			{
				removed = _children.Remove(control);
			}

			if (removed)
			{
				control.Container = null;

				// If we're no longer focusable (lost all children and no scrolling), lose focus
				if (HasFocus && !CanReceiveFocus)
				{
					this.GetParentWindow()?.FocusManager.SetFocus(null, FocusReason.Programmatic);
				}

				this.GetParentWindow()?.ForceRebuildLayout();
				Invalidate(Invalidation.Relayout);
			}
		}

		/// <summary>
		/// Removes all child controls from the panel.
		/// </summary>
		public void ClearContents()
		{
			// If this panel or a child has focus, retain focus on the panel itself
			// (children are being removed, but the panel is still a valid focus target)
			var window = this.GetParentWindow();
			if (window?.FocusManager?.IsInFocusPath(this) == true)
				window.FocusManager.SetFocus(this, FocusReason.Programmatic);
			_lastInternalFocusedChild = null;

			List<IWindowControl> snapshot;
			lock (_childrenLock)
			{
				snapshot = new List<IWindowControl>(_children);
				_children.Clear();
			}

			foreach (var child in snapshot)
			{
				child.Container = null;
				child.Dispose();
			}

			this.GetParentWindow()?.ForceRebuildLayout();
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Gets the collection of child controls.
		/// </summary>
		public IReadOnlyList<IWindowControl> Children
		{
			get { lock (_childrenLock) { return new List<IWindowControl>(_children); } }
		}

		/// <inheritdoc />
		void IControlHost.ClearControls() => ClearContents();

		#endregion

		#region Shared Child Layout

		/// <summary>
		/// Measures a single child at the given constraints and returns its desired height. Uses the
		/// persistent layout node (when ScrollLayout has registered a resolver for the current pass)
		/// so the child's already-built subtree is reused instead of rebuilt from scratch via
		/// <see cref="LayoutNodeFactory.CreateSubtree"/>. Falls back to building a throwaway subtree
		/// when no persistent node is available (detached / unit-test direct calls), preserving the
		/// original behavior of those paths. The measurement (constraints, width re-measure) is
		/// identical either way — only the subtree-build is avoided.
		/// </summary>
		private int MeasureChildHeight(IWindowControl child, LayoutConstraints constraints)
		{
			var persistent = _childNodeResolver?.Invoke(child);
			if (persistent != null)
			{
				persistent.IsVisible = true;
				persistent.Measure(constraints);
				return persistent.DesiredSize.Height;
			}

			var node = LayoutNodeFactory.CreateSubtree(child);
			node.IsVisible = true;
			node.Measure(constraints);
			return node.DesiredSize.Height;
		}


		/// <summary>
		/// A visible child together with its content-space vertical position and the height
		/// it occupies in the panel's stacked layout.
		/// </summary>
		internal readonly struct ChildSlot
		{
			public ChildSlot(IWindowControl control, int top, int height)
			{
				Control = control;
				Top = top;
				Height = height;
			}

			/// <summary>The child control.</summary>
			public IWindowControl Control { get; }

			/// <summary>The child's top edge in content-space (before scroll offset is applied).</summary>
			public int Top { get; }

			/// <summary>The vertical space the child occupies in the stack.</summary>
			public int Height { get; }
		}

		/// <summary>
		/// Runs the first Fill pass: measures non-Fill children to get their total fixed
		/// height, counts Fill children, and derives the per-Fill-child height. This is the
		/// single definition of the Fill metrics shared by the paint pass and the hit-test
		/// layout, so the two can never disagree.
		/// </summary>
		/// <param name="snapshot">The children to measure.</param>
		/// <param name="contentWidth">Width children are measured at.</param>
		/// <param name="viewportHeight">
		/// The available height to distribute. Pass a value &gt; 0 to override
		/// <see cref="_viewportHeight"/> (used by the content-height measure when re-measuring at a
		/// reduced width for the scrollbar); pass -1 to use the current <see cref="_viewportHeight"/>.
		/// </param>
		internal (int fixedHeight, int fillCount, int perFillHeight) ComputeFillMetrics(
			IReadOnlyList<IWindowControl> snapshot, int contentWidth, int viewportHeight = -1)
		{
			int vh = viewportHeight >= 0 ? viewportHeight : _viewportHeight;

			// PERF: the per-Fill height is only derived from fixedHeight when there is at least one
			// Fill child; with zero Fill children perFillHeight == vh and fixedHeight is unused. So
			// scan for Fill children first (cheap, no measure) and only run the O(children) measuring
			// loop when it actually contributes. Callers all ignore fixedHeight/fillCount unless a
			// Fill child exists, so this is byte-identical (fixedHeight reported as 0 when no Fill).
			int fillCount = 0;
			foreach (var child in snapshot)
			{
				if (child.Visible && child.VerticalAlignment == VerticalAlignment.Fill)
					fillCount++;
			}

			int fixedHeight = 0;
			if (vh > 0 && fillCount > 0)
			{
				foreach (var child in snapshot)
				{
					if (!child.Visible || child.VerticalAlignment == VerticalAlignment.Fill) continue;
					fixedHeight += MeasureChildHeight(child, new LayoutConstraints(1, contentWidth, 1, int.MaxValue));
				}
			}

			int perFillHeight = (vh > 0 && fillCount > 0)
				? Math.Max(0, (vh - fixedHeight) / fillCount) : vh;

			return (fixedHeight, fillCount, perFillHeight);
		}

		/// <summary>
		/// The height a single child occupies given the shared Fill metrics. A Fill child fills
		/// its allocated slot (<paramref name="perFillHeight"/>) even when its content is shorter;
		/// a content-sized child uses its measured DesiredSize so it can overflow and be scrolled.
		/// </summary>
		/// <param name="child">The child to measure.</param>
		/// <param name="contentWidth">Width the child is measured at.</param>
		/// <param name="perFillHeight">The per-Fill-child slot height from <see cref="ComputeFillMetrics"/>.</param>
		/// <param name="viewportHeight">Override for <see cref="_viewportHeight"/>; -1 uses the current value.</param>
		internal int ComputeChildHeight(IWindowControl child, int contentWidth, int perFillHeight, int viewportHeight = -1)
		{
			int vh = viewportHeight >= 0 ? viewportHeight : _viewportHeight;
			bool isFillChild = vh > 0 && child.VerticalAlignment == VerticalAlignment.Fill;
			int maxChildHeight = isFillChild ? perFillHeight : int.MaxValue;

			int measuredHeight = MeasureChildHeight(child, new LayoutConstraints(1, contentWidth, 1, maxChildHeight));

			return isFillChild
				? Math.Max(measuredHeight, perFillHeight)
				: measuredHeight;
		}

		/// <summary>
		/// Computes the laid-out slot (top + height) of every visible child using the SAME
		/// two-pass Fill algorithm the paint pass uses. This is the single source of truth for
		/// child heights: paint, hit-testing, coordinate translation and scroll-into-view all
		/// consume it, so they can never disagree about where a child is.
		/// </summary>
		/// <param name="contentWidth">The content width children are measured at (must match paint).</param>
		internal List<ChildSlot> GetVisibleChildLayout(int contentWidth)
		{
			List<IWindowControl> snapshot;
			lock (_childrenLock) { snapshot = new List<IWindowControl>(_children); }

			var (_, _, perFillHeight) = ComputeFillMetrics(snapshot, contentWidth);

			// Assign each visible child its slot.
			var result = new List<ChildSlot>();
			int currentY = 0;
			foreach (var child in snapshot)
			{
				if (!child.Visible) continue;

				int childHeight = ComputeChildHeight(child, contentWidth, perFillHeight);
				result.Add(new ChildSlot(child, currentY, childHeight));
				currentY += childHeight;
			}

			return result;
		}

		#endregion

	}
}
