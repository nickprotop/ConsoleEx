// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>Whether a grid splitter resizes columns (vertical handle) or rows (horizontal handle).</summary>
	internal enum GridSplitterOrientation
	{
		/// <summary>A vertical handle on a COLUMN boundary; drag left/right resizes the two columns.</summary>
		Column,
		/// <summary>A horizontal handle on a ROW boundary; drag up/down resizes the two rows.</summary>
		Row
	}

	/// <summary>
	/// One draggable boundary in a <see cref="GridControl"/>. A column splitter "after N" sits between
	/// column track N and N+1; a row splitter "after N" between row N and N+1. The grid renders it in the
	/// gap at that boundary and routes mouse/keyboard resize to it. Transient hover/drag flags drive
	/// the visual state (idle / focused / dragging) mirroring SplitterControl.
	/// </summary>
	/// <remarks>
	/// A splitter is a real <see cref="IFocusableControl"/> so it can be a Tab stop in the grid's focus scope,
	/// but it is NOT a DOM/layout node: the grid paints it as chrome and the <see cref="Core.FocusManager"/>
	/// only tracks it + routes keys. Most <see cref="IWindowControl"/> members are therefore trivial — the
	/// splitter has no measured layout of its own; its on-screen rectangle lives in <see cref="Bounds"/>.
	/// </remarks>
	internal sealed class GridSplitter : IFocusableControl, IInteractiveControl
	{
		public GridSplitterOrientation Orientation { get; }

		/// <summary>The track index this splitter sits AFTER (boundary between Index and Index+1).</summary>
		public int AfterIndex { get; }

		/// <summary>The grid that owns this splitter; set when the splitter is created. Acts as its container.</summary>
		public GridControl? OwnerGrid { get; set; }

		public bool IsHovered { get; set; }
		public bool IsDragging { get; set; }

		/// <summary>The rectangle of the splitter handle in the gap, set during paint. Used for hit-testing.
		/// Recorded in GRID-CONTROL-RELATIVE coordinates (grid content top-left = 0,0), so it can be compared
		/// directly against the control-relative <c>args.Position</c> delivered to the grid's mouse handler.</summary>
		public Rectangle Bounds { get; set; }

		public GridSplitter(GridSplitterOrientation orientation, int afterIndex)
		{
			Orientation = orientation;
			AfterIndex = afterIndex;
		}

		public bool Matches(GridSplitterOrientation orientation, int afterIndex)
			=> Orientation == orientation && AfterIndex == afterIndex;

		// ---- IFocusableControl: real focus participation, derived from the grid's FocusManager ----

		/// <summary>Whether this splitter currently holds focus, derived from the owning grid's FocusManager.</summary>
		public bool HasFocus => OwnerGrid != null && OwnerGrid.IsSplitterFocused(this);

		/// <inheritdoc/>
		/// <remarks>
		/// A visible splitter can receive focus. Bounds emptiness is NOT gated here: a splitter declared before
		/// its first paint has empty <see cref="Bounds"/>, yet programmatic focus (FocusColumnSplitter) must
		/// still take. The owning grid filters out-of-range / un-paintable handles from the Tab list separately
		/// (see GridControl.GetFocusableChildren's splitter interleaving), and an out-of-range splitter never resolves a target.
		/// </remarks>
		public bool CanReceiveFocus => Visible;

		// ---- IInteractiveControl: a focused splitter resizes itself via arrow keys ----

		/// <inheritdoc/>
		public bool IsEnabled { get; set; } = true;

		/// <summary>
		/// Processes a key while this splitter is the focused control. A focused splitter resizes itself on
		/// arrow keys (column handle: Left/Right; row handle: Up/Down; Shift = larger step), delegating to the
		/// owner grid's resize core. Mirrors <see cref="SplitterControl"/>'s own ProcessKey. Tab is NOT wanted
		/// (<see cref="IInteractiveControl.WantsTabKey"/> stays the default <c>false</c>) so Tab traverses focus
		/// away from the splitter normally.
		/// </summary>
		public bool ProcessKey(System.ConsoleKeyInfo key)
		{
			if (OwnerGrid == null || !IsEnabled) return false;
			return OwnerGrid.HandleSplitterKey(this, key);
		}

		// ---- IWindowControl: trivial — the splitter is chrome, not a measured DOM node ----

		/// <inheritdoc/>
		public IContainer? Container { get => OwnerGrid; set { /* owned by the grid; ignore external sets */ } }

		/// <inheritdoc/>
		public bool Visible { get; set; } = true;

		/// <inheritdoc/>
		public int ActualX => Bounds.X;

		/// <inheritdoc/>
		public int ActualY => Bounds.Y;

		/// <inheritdoc/>
		public int ActualWidth => Bounds.Width;

		/// <inheritdoc/>
		public int ActualHeight => Bounds.Height;

		/// <inheritdoc/>
		public Size GetLogicalContentSize() => new Size(Bounds.Width, Bounds.Height);

		/// <inheritdoc/>
		public void Invalidate() => OwnerGrid?.Invalidate(true);

		/// <inheritdoc/>
		public int? ContentWidth => Bounds.Width;

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

		/// <inheritdoc/>
		public Margin Margin { get; set; }

		/// <inheritdoc/>
		public StickyPosition StickyPosition { get; set; } = StickyPosition.None;

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public int? Width { get; set; }

		/// <inheritdoc/>
		public int? Height { get; set; }

		/// <inheritdoc/>
		public void Dispose() { }
	}
}
