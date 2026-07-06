// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Focus, cursor and mouse routing for <see cref="GridControl"/>. The grid is a transparent
	/// focus scope: it does not take focus itself (<see cref="CanReceiveFocus"/> is <c>false</c>),
	/// instead owning Tab traversal across the controls in its cells. Children placed in cells
	/// behave exactly like children of any other container — they focus, receive keys and mouse,
	/// and report a cursor — because the grid forwards to them and composes their cell origin.
	/// </summary>
	/// <remarks>
	/// This mirrors the proven shape of <see cref="ScrollablePanelControl"/> and
	/// <see cref="HorizontalGridControl"/>, minus everything scroll-related: the grid has no scroll
	/// offset and no viewport clipping, so the cursor is simply the focused child's cursor translated
	/// by that child's cell origin, with no offset subtraction and no off-viewport hiding.
	/// </remarks>
	public partial class GridControl : IInteractiveControl, IFocusableControl, IMouseAwareControl, IFocusScope, IFlattenableFocusScope, ILogicalCursorProvider, ICursorShapeProvider
	{
		private bool _isEnabled = true;

		#region Events

		/// <summary>Event fired when the grid is clicked.</summary>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <summary>Event fired when the grid is double-clicked.</summary>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <summary>Event fired when the grid is right-clicked.</summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <summary>Event fired when the mouse enters the grid area.</summary>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <summary>Event fired when the mouse leaves the grid area.</summary>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <summary>Event fired when the mouse moves over the grid.</summary>
		public event EventHandler<MouseEventArgs>? MouseMove;
#pragma warning restore CS0067

		#endregion

		#region IInteractiveControl / IFocusableControl

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// For a transparent container, <c>HasFocus</c> means "this grid or a descendant is focused"
		/// (i.e. the grid is in the focus path), which keeps rendering and key routing correct.
		/// </remarks>
		public bool HasFocus => ComputeIsInFocusPath();

		/// <inheritdoc/>
		/// <remarks>
		/// The grid is a layout container and is never directly focusable: focus goes to the controls
		/// placed in its cells instead. Returning <c>false</c> makes the grid a transparent focus scope,
		/// exactly like <see cref="HorizontalGridControl"/>.
		/// </remarks>
		public bool CanReceiveFocus => false;

		/// <inheritdoc/>
		/// <remarks>
		/// Forwards the preferred cursor shape from the focused cell child (e.g. an editable
		/// <see cref="MultilineEditControl"/> or <see cref="PromptControl"/> reporting an
		/// I-beam), so a control in a cell shows the same cursor it would in any other container.
		/// Returns <c>null</c> (the default shape) when no cell child supplies one.
		/// </remarks>
		public virtual CursorShape? PreferredCursorShape =>
			(GetFocusedChildFromCoordinator() as ICursorShapeProvider)?.PreferredCursorShape;

		/// <summary>
		/// Gets the currently focused direct cell child via the window's <see cref="Core.FocusManager"/>,
		/// or <c>null</c> when no cell child is focused. Uses the focus path so a focused control nested
		/// inside a cell's own container is reported as its owning cell child.
		/// </summary>
		protected virtual IInteractiveControl? GetFocusedChildFromCoordinator()
		{
			var window = (this as IWindowControl).GetParentWindow();
			if (window == null) return null;
			var focused = window.FocusManager.FocusedControl;
			if (focused == null) return null;

			var focusPath = window.FocusManager.FocusPath;

			// Iterate the cached children projection (no per-call allocation — this runs on every
			// keystroke and is read by the render loop per repaint via HasFocus → ProcessKey paths).
			var children = OrderedChildren();
			for (int i = 0; i < children.Count; i++)
			{
				var child = children[i];
				if (ReferenceEquals(child, focused))
					return child as IInteractiveControl;
				// child is an ancestor of the focused control (e.g. a nested scope cell).
				if (focusPath.Contains(child, ReferenceEqualityComparer.Instance))
					return child as IInteractiveControl;
			}
			return null;
		}

		/// <inheritdoc/>
		public virtual bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!IsEnabled) return false;

			// Inert unless a splitter is the FOCUSED control (via the window's FocusManager): TryHandleSplitterKey
			// returns false immediately when no owned splitter is focused, so existing cell Tab/arrow navigation
			// is unaffected. Arrow keys nudge the focused splitter's boundary.
			if (TryHandleSplitterKey(key)) return true;

			var focusedContent = GetFocusedChildFromCoordinator();

			// Delegate to the focused child first (for non-Tab keys and Tab keys it wants).
			if (focusedContent != null && focusedContent.ProcessKey(key))
				return true;

			// Handle Tab / Shift+Tab via IFocusScope.GetNextFocus so navigation across cells works
			// even when ProcessKey is called directly (e.g. by an enclosing scroll panel).
			if (key.Key == ConsoleKey.Tab)
			{
				bool backward = (key.Modifiers & ConsoleModifiers.Shift) != 0;
				var window = (this as IWindowControl).GetParentWindow();
				var focused = window?.FocusManager.FocusedControl;

				// focusedContent is captured BEFORE the child's ProcessKey may clear FocusedControl,
				// so prefer it as the reference for advancing to the next cell child.
				var referenceForNext = (focusedContent as IFocusableControl) ?? focused;

				if (referenceForNext != null)
				{
					var next = GetNextFocus(referenceForNext, backward);
					if (next != null)
					{
						// Entering a nested scope (e.g. an SPC or a nested grid): respect direction so
						// Shift+Tab lands on the scope's last child rather than its first.
						IFocusableControl? target = next;
						if (next is IFocusScope innerScope)
							target = innerScope.GetInitialFocus(backward) ?? next;
						window?.FocusManager.SetFocus(target, FocusReason.Keyboard);
						return true; // Handled within the grid.
					}
					// next == null: traversal exhausted — let the caller advance to the next sibling.
					return false;
				}

				// No prior focus inside the grid: enter the first (or last) cell child.
				var initial = GetInitialFocus(backward);
				if (initial != null)
				{
					window?.FocusManager.SetFocus(initial, FocusReason.Keyboard);
					return true;
				}
			}

			return false;
		}

		#endregion

		#region ILogicalCursorProvider

		/// <inheritdoc/>
		/// <remarks>
		/// Returns the focused cell child's cursor translated into the grid's own coordinate space by
		/// adding that child's cell origin (the child node's top-left relative to the grid's top-left).
		/// There is no scroll offset to subtract and no viewport to clip against, so this is a plain
		/// translation. Returns <c>null</c> when no cell child is focused or the child reports no cursor.
		/// </remarks>
		public virtual Point? GetLogicalCursorPosition()
		{
			var focused = GetFocusedChildFromCoordinator();
			if (focused is ILogicalCursorProvider cursorProvider && focused is IWindowControl wc)
			{
				var childPos = cursorProvider.GetLogicalCursorPosition();
				if (childPos.HasValue && TryGetCellOrigin(wc, out var origin))
				{
					return new Point(childPos.Value.X + origin.X, childPos.Value.Y + origin.Y);
				}
			}
			return null;
		}

		/// <inheritdoc/>
		/// <remarks>The inverse of <see cref="GetLogicalCursorPosition"/>: removes the cell origin and
		/// forwards the child-relative position to the focused cell child.</remarks>
		public virtual void SetLogicalCursorPosition(Point position)
		{
			var focused = GetFocusedChildFromCoordinator();
			if (focused is ILogicalCursorProvider cursorProvider && focused is IWindowControl wc)
			{
				if (TryGetCellOrigin(wc, out var origin))
					cursorProvider.SetLogicalCursorPosition(new Point(position.X - origin.X, position.Y - origin.Y));
			}
		}

		/// <summary>
		/// Resolves the origin of <paramref name="child"/>'s cell relative to the grid's own top-left,
		/// using the child's arranged layout node. The grid's children are real DOM participants, so
		/// both the grid and the child have arranged absolute bounds; the difference is the cell origin
		/// in grid-local coordinates. Returns <c>false</c> when bounds are not yet available.
		/// </summary>
		private bool TryGetCellOrigin(IWindowControl child, out Point origin)
		{
			origin = Point.Empty;
			var window = (this as IWindowControl).GetParentWindow();
			var childNode = window?.GetLayoutNode(child);
			if (childNode == null)
				return false;

			var childBounds = childNode.AbsoluteBounds;
			origin = new Point(childBounds.X - ActualX, childBounds.Y - ActualY);
			return true;
		}

		#endregion

		#region IMouseAwareControl

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <inheritdoc/>
		/// <remarks>
		/// Routes a click to the cell child under the cursor: focuses it (when directly focusable) and
		/// forwards the event in child-relative coordinates. The engine hit-tests child layout-node
		/// bounds, so a click lands on the cell's control exactly as it would in any other container.
		/// There is no scroll wheel handling — the grid does not scroll.
		/// </remarks>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Splitter drag/capture must run BEFORE cell routing: a press on a handle captures this grid
			// (the dispatcher routes the whole drag here), and an active drag consumes every frame so it
			// never bleeds focus/clicks into the cells under the cursor.
			if (TryHandleSplitterMouse(args)) return true;

			var (clickedControl, childRelative) = GetChildAtGridPosition(args.Position);
			if (clickedControl != null)
			{
				// Only update focus for actual button events, not wheel/motion (which can bubble here).
				bool isClickEvent = args.HasAnyFlag(
					MouseFlags.Button1Pressed, MouseFlags.Button1Clicked, MouseFlags.Button1Released,
					MouseFlags.Button1DoubleClicked, MouseFlags.Button1TripleClicked,
					MouseFlags.Button2Pressed, MouseFlags.Button2Clicked, MouseFlags.Button2Released,
					MouseFlags.Button3Pressed, MouseFlags.Button3Clicked, MouseFlags.Button3Released);

				if (isClickEvent && clickedControl is IFocusableControl focusable && focusable.CanReceiveFocus)
					this.GetParentWindow()?.FocusManager.SetFocus(focusable, FocusReason.Mouse);

				if (clickedControl is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
				{
					var childArgs = args.WithPosition(childRelative);
					return mouseAware.ProcessMouseEvent(childArgs);
				}

				return false;
			}

			// No child under the cursor — raise grid-level events for the common buttons.
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}
			if (args.HasFlag(MouseFlags.Button1DoubleClicked))
			{
				MouseDoubleClick?.Invoke(this, args);
				return true;
			}
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				MouseClick?.Invoke(this, args);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Finds the cell child whose arranged bounds contain the grid-relative
		/// <paramref name="gridPosition"/>, returning it together with the position translated into
		/// that child's own coordinate space. Returns <c>(null, gridPosition)</c> when no cell child is
		/// hit. Uses the children's layout-node bounds so hit-testing agrees with paint.
		/// </summary>
		private (IInteractiveControl? control, Point childRelative) GetChildAtGridPosition(Point gridPosition)
		{
			var window = (this as IWindowControl).GetParentWindow();
			if (window == null)
				return (null, gridPosition);

			// gridPosition is relative to the grid's own top-left; convert to absolute window-content
			// space to compare against the children's absolute layout-node bounds.
			int absX = ActualX + gridPosition.X;
			int absY = ActualY + gridPosition.Y;

			// OrderedChildren() is the cached, content-only row-major projection — no per-event alloc.
			var children = OrderedChildren();
			for (int i = 0; i < children.Count; i++)
			{
				var child = children[i];
				if (!child.Visible) continue;
				if (child is not IInteractiveControl interactive) continue;

				var node = window.GetLayoutNode(child);
				if (node == null) continue;

				var b = node.AbsoluteBounds;
				if (absX >= b.X && absX < b.X + b.Width && absY >= b.Y && absY < b.Y + b.Height)
				{
					var childRelative = new Point(absX - b.X, absY - b.Y);
					return (interactive, childRelative);
				}
			}

			return (null, gridPosition);
		}

		#endregion

		#region IFocusScope

		/// <inheritdoc/>
		public IFocusableControl? SavedFocus { get; set; }

		/// <inheritdoc/>
		/// <remarks>
		/// Returns the first focusable cell child in Tab order (row-major), or the last when
		/// <paramref name="backward"/> is <c>true</c>. For forward re-entry, honours
		/// <see cref="SavedFocus"/> so focus resumes where it left off — unless using it would skip a
		/// nested focus-scope cell that appears earlier in Tab order, in which case the saved value is
		/// discarded. The grid has no scroll mode, so there is no self-sentinel branch.
		/// </remarks>
		public virtual IFocusableControl? GetInitialFocus(bool backward)
		{
			// Build the focusable Tab-stop list once; both the SavedFocus skip-check and the
			// first/last selection reuse it (CLAUDE.md rule 11 — avoid the double traversal).
			var children = GetFocusableChildren();

			// SavedFocus is only used for forward entry (resume from where focus left off).
			// Backward entry always goes to the last child for correct Shift+Tab behavior.
			// Discard SavedFocus if using it would skip a nested IFocusScope child that appears
			// before the saved control in Tab order.
			if (!backward && SavedFocus != null)
			{
				var saved = SavedFocus;
				SavedFocus = null;
				if (!WouldSkipNestedScope(saved, children))
					return saved;
			}
			SavedFocus = null;
			return backward ? children.LastOrDefault() : children.FirstOrDefault();
		}

		/// <inheritdoc/>
		public virtual IFocusableControl? GetNextFocus(IFocusableControl current, bool backward)
		{
			var children = GetFocusableChildren();
			var index = children.FindIndex(c => ReferenceEquals(c, current));
			if (index < 0) return GetInitialFocus(backward);
			var nextIndex = backward ? index - 1 : index + 1;
			return (nextIndex >= 0 && nextIndex < children.Count) ? children[nextIndex] : null;
		}

		/// <summary>
		/// Builds the ordered list of focusable Tab stops across all cells, with splitters INTERLEAVED into
		/// the row-major cell order (per the splitter design addendum), each splitter visited exactly once:
		/// <list type="bullet">
		/// <item>COLUMN splitters are interleaved during the FIRST row's pass — a column splitter "after N"
		/// follows the row-0 cell that reaches/ends at column boundary N (it is one handle spanning all rows,
		/// so it is one Tab stop placed in the row-0 pass).</item>
		/// <item>ROW splitters appear once after each row's cells — a row splitter "after N" follows the last
		/// cell of row N.</item>
		/// </list>
		/// Iterates <see cref="OrderedCells"/> (row-major, carrying each cell's <see cref="Layout.GridPlacement"/>),
		/// so Tab flows left-to-right, top-to-bottom and splitters slot in at their boundaries. Only handles
		/// with a valid, visible boundary (non-empty Bounds → CanReceiveFocus) participate.
		/// </summary>
		protected virtual List<IFocusableControl> GetFocusableChildren()
		{
			var result = new List<IFocusableControl>();
			var cells = OrderedCells;

			// Track which column splitters have already been emitted in the row-0 pass (each appears once).
			var emittedColumnSplitters = new HashSet<int>();
			int? currentRow = null;

			for (int i = 0; i < cells.Count; i++)
			{
				var (child, placement) = cells[i];
				if (child == null || !child.Visible) continue;

				// Row transition: we have finished every cell of the previous row(s) — emit their row
				// splitters now (in ascending boundary order) so they sit after that row's last cell.
				if (currentRow.HasValue && placement.Row > currentRow.Value)
				{
					for (int n = currentRow.Value; n < placement.Row; n++)
						AppendRowSplitterAfter(result, n);
				}
				currentRow = placement.Row;

				CollectFocusableChild(child, result);

				// COLUMN splitters interleave only during the FIRST row's pass: emit every not-yet-emitted
				// column splitter whose boundary N is reached/passed by this cell's right edge, right after it.
				if (placement.Row == 0)
				{
					int rightEdge = placement.Col + placement.ColSpan - 1; // inclusive last column this cell covers
					AppendColumnSplittersUpTo(result, rightEdge, emittedColumnSplitters);
				}
			}

			// Any column splitters not yet emitted (e.g. boundaries past the last row-0 cell, or no row-0
			// cells at all) get appended once at the end of the row-0 logical pass.
			AppendRemainingColumnSplitters(result, emittedColumnSplitters);

			// The final row's row splitter (after the last row's cells) and any trailing row boundaries.
			if (currentRow.HasValue)
				AppendRowSplitterAfter(result, currentRow.Value);

			return result;
		}

		/// <summary>Test-only: the ordered list of focusable Tab stops (cells + interleaved splitters).</summary>
		internal List<IFocusableControl> GetFocusableChildrenForTest() => GetFocusableChildren();

		/// <inheritdoc/>
		/// <remarks>
		/// The grid's Tab stops include grid-native splitters that are NOT DOM children (they live in the
		/// grid's own splitter list, not in <see cref="GetChildren"/>). The window's global Tab list calls
		/// this so those splitters — including a column splitter that sorts first in Tab order — are reachable
		/// as ordinary Tab stops instead of being skipped when the root scope recurses into DOM children.
		/// </remarks>
		List<IFocusableControl> IFlattenableFocusScope.GetOrderedTabStops() => GetFocusableChildren();

		/// <summary>
		/// Collects focusable Tab stops from a child control, handling
		/// <see cref="IFocusableContainerWithHeader"/> (header + active-tab children),
		/// <see cref="IFocusScope"/> (opaque single stop), and leaf controls.
		/// </summary>
		private static void CollectFocusableChild(IWindowControl child, List<IFocusableControl> result)
		{
			if (!child.Visible) return;

			// IFocusableContainerWithHeader (e.g. TabControl): header is a Tab stop,
			// then active-tab children are recursively included immediately after.
			if (child is IFocusableContainerWithHeader)
			{
				if (child is IFocusableControl headerFc && headerFc.CanReceiveFocus)
					result.Add(headerFc);
				if (child is IContainerControl headerContainer)
					foreach (var grandchild in headerContainer.GetChildren())
						CollectFocusableChild(grandchild, result);
				return;
			}

			// IFocusScope (e.g. nested grid / scroll panel): opaque single stop
			if (child is IFocusScope && child is IFocusableControl scopeFc
				&& child is IContainerControl scopeContainer)
			{
				if (scopeFc.CanReceiveFocus || HasAnyFocusableDescendant(scopeContainer))
				{
					result.Add(scopeFc);
					return;
				}
			}

			// Leaf focusable control
			if (child is IFocusableControl f && f.CanReceiveFocus)
			{
				result.Add(f);
				return;
			}

			// Transparent container: recurse into children
			if (child is IContainerControl container)
				foreach (var grandchild in container.GetChildren())
					CollectFocusableChild(grandchild, result);
		}

		/// <summary>
		/// Returns true if using <paramref name="saved"/> as the initial focus would skip
		/// a nested <see cref="IFocusScope"/> child that appears earlier in Tab order. The caller
		/// passes the already-built focusable list so it is not traversed twice (CLAUDE.md rule 11).
		/// </summary>
		private static bool WouldSkipNestedScope(IFocusableControl saved, List<IFocusableControl> children)
		{
			foreach (var child in children)
			{
				if (ReferenceEquals(child, saved))
					return false; // reached saved before any scope — safe
				if (child is IFocusScope)
					return true; // a scope appears before saved — would be skipped
			}
			return true; // saved not found in children (stale) — discard it
		}

		/// <summary>
		/// Returns true if the container has at least one focusable descendant (direct or nested).
		/// </summary>
		private static bool HasAnyFocusableDescendant(IContainerControl container)
		{
			foreach (var child in container.GetChildren())
			{
				if (!child.Visible) continue;
				if (child is IFocusableControl f && f.CanReceiveFocus)
					return true;
				if (child is IContainerControl nested && HasAnyFocusableDescendant(nested))
					return true;
			}
			return false;
		}

		#endregion
	}
}
