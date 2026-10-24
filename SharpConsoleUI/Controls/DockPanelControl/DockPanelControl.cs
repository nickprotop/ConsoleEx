// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls;

/// <summary>
/// A container whose children claim edges: each docked child takes a strip from the remaining
/// rectangle in declaration order, and the last undocked child fills what is left.
/// </summary>
/// <remarks>
/// <para>
/// This is how docking reaches everywhere rather than only the window root. A
/// <see cref="GridControl"/> cell holds exactly one control, so a cell cannot itself dock several
/// children - but it can hold one of these, and then the cell docks. The same applies inside a
/// scrollable panel, a tab page, or another dock panel.
/// </para>
/// <para>
/// Children are placed with <see cref="IWindowControl.Dock"/>, and the four edges are also
/// exposed as surfaces (<see cref="DockLeft"/> and friends) for the cases where naming the region
/// reads better than tagging the child.
/// </para>
/// </remarks>
public class DockPanelControl : BaseControl, IContainer, IContainerControl, IControlHost,
	IDockSurfaceProvider
{
	private readonly List<IWindowControl> _children = new();
	private readonly object _childrenLock = new();

	private IDockSurface? _left, _top, _right, _bottom, _body;

	/// <summary>
	/// Whether the last undocked child expands into the remaining rectangle. Default true.
	/// </summary>
	public bool LastChildFill
	{
		get => _lastChildFill;
		set => SetProperty(ref _lastChildFill, value);
	}

	private bool _lastChildFill = true;

	#region Children

	/// <summary>The panel's children, in declaration order.</summary>
	public IReadOnlyList<IWindowControl> Children
	{
		get
		{
			lock (_childrenLock)
				return _children.ToList();
		}
	}

	/// <summary>Adds a child. Set its <see cref="IWindowControl.Dock"/> to choose an edge.</summary>
	public void AddControl(IWindowControl control)
	{
		ArgumentNullException.ThrowIfNull(control);

		lock (_childrenLock)
			_children.Add(control);

		control.Container = this;

		// A new child must be built into the DOM tree; a plain Invalidate would only re-measure
		// the tree that already exists and would leave the child out of it.
		this.GetParentWindow()?.ForceRebuildLayout();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>Inserts a child at <paramref name="index"/> in declaration order.</summary>
	public void InsertControl(int index, IWindowControl control)
	{
		ArgumentNullException.ThrowIfNull(control);

		lock (_childrenLock)
		{
			index = Math.Clamp(index, 0, _children.Count);
			_children.Insert(index, control);
		}

		control.Container = this;
		this.GetParentWindow()?.ForceRebuildLayout();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>Removes a child.</summary>
	public void RemoveControl(IWindowControl control)
	{
		bool removed;
		lock (_childrenLock)
			removed = _children.Remove(control);

		if (!removed)
			return;

		control.Container = null;
		this.GetParentWindow()?.ForceRebuildLayout();
		Invalidate(Invalidation.Relayout);
	}

	/// <summary>Removes every child.</summary>
	public void ClearControls()
	{
		List<IWindowControl> removed;
		lock (_childrenLock)
		{
			removed = _children.ToList();
			_children.Clear();
		}

		foreach (var c in removed)
			c.Container = null;

		this.GetParentWindow()?.ForceRebuildLayout();
		Invalidate(Invalidation.Relayout);
	}

	/// <inheritdoc/>
	public IReadOnlyList<IWindowControl> GetChildren() => Children;

	#endregion

	#region Dock surfaces

	/// <summary>Children docked to the left, in declaration order.</summary>
	public IDockSurface DockLeft => _left ??= new DockSurface(this, Dock.Left);

	/// <summary>Children docked to the top, in declaration order.</summary>
	public IDockSurface DockTop => _top ??= new DockSurface(this, Dock.Top);

	/// <summary>Children docked to the right, in declaration order.</summary>
	public IDockSurface DockRight => _right ??= new DockSurface(this, Dock.Right);

	/// <summary>Children docked to the bottom, in declaration order.</summary>
	public IDockSurface DockBottom => _bottom ??= new DockSurface(this, Dock.Bottom);

	/// <summary>Undocked children; the last of them fills the remainder.</summary>
	public IDockSurface DockBody => _body ??= new DockSurface(this, Dock.None);

	/// <summary>The surface for an edge, with <see cref="Dock.None"/> giving <see cref="DockBody"/>.</summary>
	public IDockSurface Surface(Dock edge) => edge switch
	{
		Dock.Left => DockLeft,
		Dock.Top => DockTop,
		Dock.Right => DockRight,
		Dock.Bottom => DockBottom,
		_ => DockBody,
	};

	// Named plainly on the interface. The public properties carry a Dock prefix for symmetry with
	// Window, where Left and Top are already the window's screen position.
	IDockSurface IDockSurfaceProvider.Left => DockLeft;

	IDockSurface IDockSurfaceProvider.Top => DockTop;

	IDockSurface IDockSurfaceProvider.Right => DockRight;

	IDockSurface IDockSurfaceProvider.Bottom => DockBottom;

	IDockSurface IDockSurfaceProvider.Body => DockBody;

	#endregion

	#region IContainer

	/// <inheritdoc/>
	/// <remarks>Transparent unless set: the panel is a region, so by default the window shows through.</remarks>
	public Color BackgroundColor
	{
		get => _backgroundColor ?? Color.Transparent;
		set { _backgroundColor = value; Invalidate(Invalidation.Repaint); }
	}

	private Color? _backgroundColor;

	/// <inheritdoc/>
	/// <remarks>Unset resolves from the theme, so a theme switch recolours children that inherit.</remarks>
	public Color ForegroundColor
	{
		get => Helpers.ColorResolver.ResolveForeground(_foregroundColor, Container);
		set { _foregroundColor = value; OnPropertyChanged(); Invalidate(Invalidation.Repaint); }
	}

	private Color? _foregroundColor;

	/// <inheritdoc/>
	public ConsoleWindowSystem? GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;

	/// <inheritdoc/>
	/// <remarks>
	/// The panel does not scroll, so a child is as visible as the panel itself is. Deferring to
	/// the parent keeps the answer honest when the panel sits inside something that does scroll.
	/// </remarks>
	public int? GetVisibleHeightForControl(IWindowControl control)
		=> Container?.GetVisibleHeightForControl(this);

	/// <inheritdoc/>
	public void Invalidate(Invalidation work, IWindowControl? callerControl = null)
		=> Container?.Invalidate(work, callerControl ?? this);

	#endregion

	#region BaseControl

	/// <summary>The panel takes the width it is given; its children divide it.</summary>
	public override int? ContentWidth => null;

	/// <inheritdoc/>
	public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		=> new(constraints.MaxWidth, constraints.MaxHeight);

	/// <summary>
	/// Nothing of its own. The panel is a region; its children paint themselves, and anything
	/// drawn here would land underneath them.
	/// </summary>
	public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
		Color foreground, Color background)
	{
	}

	#endregion
}
