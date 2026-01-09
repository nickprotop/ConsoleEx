// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Layout
{
	/// <summary>
	/// Represents a node in the layout DOM tree.
	/// Wraps an IWindowControl and manages layout state (measure/arrange/paint).
	/// </summary>
	public class LayoutNode
	{
		private readonly List<LayoutNode> _children = new();
		private readonly List<LayoutNode> _portalChildren = new();

		/// <summary>
		/// Gets the parent node, if any.
		/// </summary>
		public LayoutNode? Parent { get; internal set; }

		/// <summary>
		/// Gets the children of this node.
		/// </summary>
		public IReadOnlyList<LayoutNode> Children => _children;

		/// <summary>
		/// Gets the portal children (render on top, for dropdowns/overlays).
		/// </summary>
		public IReadOnlyList<LayoutNode> PortalChildren => _portalChildren;

		/// <summary>
		/// Gets the control this node represents.
		/// </summary>
		public IWindowControl? Control { get; }

		/// <summary>
		/// Gets or sets the layout container that determines how children are arranged.
		/// </summary>
		public ILayoutContainer? Layout { get; set; }

		/// <summary>
		/// Gets a unique identifier for this node.
		/// </summary>
		public Guid Id { get; } = Guid.NewGuid();

		/// <summary>
		/// Gets or sets the vertical alignment within the parent container.
		/// </summary>
		public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

		/// <summary>
		/// Gets or sets the flex factor for proportional sizing (default 1.0).
		/// </summary>
		public double FlexFactor { get; set; } = 1.0;

		/// <summary>
		/// Gets or sets an explicit width for this node (null = auto).
		/// </summary>
		public int? ExplicitWidth { get; set; }

		/// <summary>
		/// Gets or sets an explicit height for this node (null = auto).
		/// </summary>
		public int? ExplicitHeight { get; set; }

		/// <summary>
		/// Gets the desired size calculated during the measure pass.
		/// </summary>
		public LayoutSize DesiredSize { get; private set; }

		/// <summary>
		/// Gets the bounds relative to the parent node.
		/// </summary>
		public LayoutRect Bounds { get; private set; }

		/// <summary>
		/// Gets the absolute bounds (screen coordinates) for rendering and hit testing.
		/// </summary>
		public LayoutRect AbsoluteBounds { get; private set; }

		/// <summary>
		/// Gets whether this node needs to be measured.
		/// </summary>
		public bool NeedsMeasure { get; set; } = true;

		/// <summary>
		/// Gets whether this node needs to be arranged.
		/// </summary>
		public bool NeedsArrange { get; set; } = true;

		/// <summary>
		/// Gets whether this node needs to be painted.
		/// </summary>
		public bool NeedsPaint { get; set; } = true;

		/// <summary>
		/// Gets or sets whether this node is visible (participates in layout).
		/// </summary>
		public bool IsVisible { get; set; } = true;

		/// <summary>
		/// Creates a new layout node for the specified control.
		/// </summary>
		public LayoutNode(IWindowControl? control, ILayoutContainer? layout = null)
		{
			Control = control;
			Layout = layout;

			// Initialize from control properties if available
			if (control != null)
			{
				ExplicitWidth = control.Width;
				IsVisible = control.Visible;
				VerticalAlignment = control.VerticalAlignment;
			}
		}

		/// <summary>
		/// Creates a new container layout node (no control, just layout).
		/// </summary>
		public LayoutNode(ILayoutContainer layout) : this(null, layout)
		{
		}

		/// <summary>
		/// Adds a child node.
		/// </summary>
		public void AddChild(LayoutNode child)
		{
			child.Parent = this;
			_children.Add(child);
			InvalidateMeasure();
		}

		/// <summary>
		/// Inserts a child node at the specified index.
		/// </summary>
		public void InsertChild(int index, LayoutNode child)
		{
			child.Parent = this;
			_children.Insert(index, child);
			InvalidateMeasure();
		}

		/// <summary>
		/// Removes a child node.
		/// </summary>
		public bool RemoveChild(LayoutNode child)
		{
			if (_children.Remove(child))
			{
				child.Parent = null;
				InvalidateMeasure();
				return true;
			}
			return false;
		}

		/// <summary>
		/// Clears all children.
		/// </summary>
		public void ClearChildren()
		{
			foreach (var child in _children)
			{
				child.Parent = null;
			}
			_children.Clear();
			InvalidateMeasure();
		}

		/// <summary>
		/// Adds a portal child (renders on top, for dropdowns/overlays).
		/// </summary>
		public void AddPortalChild(LayoutNode child)
		{
			child.Parent = this;
			_portalChildren.Add(child);
			InvalidatePaint();
		}

		/// <summary>
		/// Removes a portal child.
		/// </summary>
		public bool RemovePortalChild(LayoutNode child)
		{
			if (_portalChildren.Remove(child))
			{
				child.Parent = null;
				InvalidatePaint();
				return true;
			}
			return false;
		}

		/// <summary>
		/// Measures this node and all its children.
		/// Returns the desired size based on content and constraints.
		/// </summary>
		public LayoutSize Measure(LayoutConstraints constraints)
		{
			if (!IsVisible)
			{
				DesiredSize = LayoutSize.Zero;
				NeedsMeasure = false;
				return DesiredSize;
			}

			// Apply explicit sizes to constraints
			var effectiveConstraints = constraints;
			if (ExplicitWidth.HasValue)
			{
				var clampedWidth = Math.Clamp(ExplicitWidth.Value, effectiveConstraints.MinWidth, effectiveConstraints.MaxWidth);
				effectiveConstraints = effectiveConstraints
					.WithMinWidth(clampedWidth)
					.WithMaxWidth(clampedWidth);
			}
			if (ExplicitHeight.HasValue)
			{
				var clampedHeight = Math.Clamp(ExplicitHeight.Value, effectiveConstraints.MinHeight, effectiveConstraints.MaxHeight);
				effectiveConstraints = effectiveConstraints
					.WithMinHeight(clampedHeight)
					.WithMaxHeight(clampedHeight);
			}

			LayoutSize desiredSize;

			if (Layout != null && _children.Count > 0)
			{
				// Container with children - use layout algorithm
				desiredSize = Layout.MeasureChildren(this, effectiveConstraints);
			}
			else if (Control != null)
			{
				// Leaf node - measure control content
				desiredSize = MeasureControl(effectiveConstraints);
			}
			else
			{
				// Empty container
				desiredSize = LayoutSize.Zero;
			}

			// Constrain to bounds
			DesiredSize = effectiveConstraints.Constrain(desiredSize);
			NeedsMeasure = false;

			return DesiredSize;
		}

		/// <summary>
		/// Measures the control's content to determine its desired size.
		/// </summary>
		protected virtual LayoutSize MeasureControl(LayoutConstraints constraints)
		{
			if (Control == null)
				return LayoutSize.Zero;

			// Use native DOM measurement if available
			if (Control is IDOMMeasurable measurable)
			{
				return measurable.MeasureDOM(constraints);
			}

			// Use native DOM painting interface if available (it also measures)
			if (Control is IDOMPaintable paintable)
			{
				return paintable.MeasureDOM(constraints);
			}

			// All controls must implement IDOMPaintable
			throw new NotSupportedException($"Control {Control.GetType().Name} must implement IDOMPaintable or IDOMMeasurable for DOM-based layout.");
		}

		/// <summary>
		/// Arranges this node and all its children within the given bounds.
		/// </summary>
		public void Arrange(LayoutRect finalRect)
		{
			if (!IsVisible)
			{
				Bounds = LayoutRect.Empty;
				AbsoluteBounds = LayoutRect.Empty;
				NeedsArrange = false;
				return;
			}

			Bounds = finalRect;

			// Calculate absolute bounds
			if (Parent != null)
			{
				AbsoluteBounds = new LayoutRect(
					Parent.AbsoluteBounds.X + finalRect.X,
					Parent.AbsoluteBounds.Y + finalRect.Y,
					finalRect.Width,
					finalRect.Height);
			}
			else
			{
				AbsoluteBounds = finalRect;
			}

			// Arrange children using layout algorithm
			if (Layout != null && _children.Count > 0)
			{
				Layout.ArrangeChildren(this, new LayoutRect(0, 0, finalRect.Width, finalRect.Height));
			}

			NeedsArrange = false;
			NeedsPaint = true;
		}

		/// <summary>
		/// Paints this node and all its children to the buffer.
		/// </summary>
		public void Paint(CharacterBuffer buffer, LayoutRect clipRect)
		{
			if (!IsVisible)
				return;

			// Calculate visible area (intersection of our bounds with clip rect)
			var visibleBounds = AbsoluteBounds.Intersect(clipRect);
			if (visibleBounds.IsEmpty)
				return;

			// Paint control content
			if (Control != null)
			{
				PaintControl(buffer, visibleBounds);
			}

			// Paint children
			foreach (var child in _children)
			{
				child.Paint(buffer, visibleBounds);
			}

			// Paint portal children last (on top)
			foreach (var portal in _portalChildren)
			{
				portal.Paint(buffer, clipRect); // Portals clip to full area, not our bounds
			}

			NeedsPaint = false;
		}

		/// <summary>
		/// Paints the control's content to the buffer.
		/// </summary>
		protected virtual void PaintControl(CharacterBuffer buffer, LayoutRect clipRect)
		{
			PaintControl(buffer, clipRect, Color.White, Color.Black);
		}

		/// <summary>
		/// Paints the control's content to the buffer with specified default colors.
		/// </summary>
		protected virtual void PaintControl(CharacterBuffer buffer, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			if (Control == null)
				return;

			// Use native DOM painting if available
			if (Control is IDOMPaintable paintable)
			{
				paintable.PaintDOM(buffer, AbsoluteBounds, clipRect, defaultFg, defaultBg);
				return;
			}

			// All controls must implement IDOMPaintable
			throw new NotSupportedException($"Control {Control.GetType().Name} must implement IDOMPaintable for DOM-based layout.");
		}

		/// <summary>
		/// Invalidates measure for this node and propagates up to ancestors.
		/// </summary>
		public void InvalidateMeasure()
		{
			NeedsMeasure = true;
			NeedsArrange = true;
			NeedsPaint = true;
			Parent?.InvalidateMeasure();
		}

		/// <summary>
		/// Invalidates arrange for this node and propagates down to descendants.
		/// </summary>
		public void InvalidateArrange()
		{
			NeedsArrange = true;
			NeedsPaint = true;
			foreach (var child in _children)
			{
				child.InvalidateArrange();
			}
		}

		/// <summary>
		/// Invalidates paint for this node only.
		/// </summary>
		public void InvalidatePaint()
		{
			NeedsPaint = true;
		}

		/// <summary>
		/// Finds a node by its control.
		/// </summary>
		public LayoutNode? FindByControl(IWindowControl control)
		{
			if (Control == control)
				return this;

			foreach (var child in _children)
			{
				var found = child.FindByControl(control);
				if (found != null)
					return found;
			}

			foreach (var portal in _portalChildren)
			{
				var found = portal.FindByControl(control);
				if (found != null)
					return found;
			}

			return null;
		}

		/// <summary>
		/// Performs hit testing to find the node at the specified absolute position.
		/// </summary>
		public LayoutNode? HitTest(int x, int y)
		{
			if (!IsVisible || !AbsoluteBounds.Contains(x, y))
				return null;

			// Check portal children first (they're on top)
			for (int i = _portalChildren.Count - 1; i >= 0; i--)
			{
				var hit = _portalChildren[i].HitTest(x, y);
				if (hit != null)
					return hit;
			}

			// Check children in reverse order (last painted = on top)
			for (int i = _children.Count - 1; i >= 0; i--)
			{
				var hit = _children[i].HitTest(x, y);
				if (hit != null)
					return hit;
			}

			// Return this node if we have a control
			return Control != null ? this : null;
		}

		/// <summary>
		/// Gets the depth of this node in the tree (root = 0).
		/// </summary>
		public int GetDepth()
		{
			int depth = 0;
			var current = Parent;
			while (current != null)
			{
				depth++;
				current = current.Parent;
			}
			return depth;
		}

		/// <summary>
		/// Gets the root node of this tree.
		/// </summary>
		public LayoutNode GetRoot()
		{
			var current = this;
			while (current.Parent != null)
			{
				current = current.Parent;
			}
			return current;
		}

		/// <summary>
		/// Visits all nodes in the tree (depth-first).
		/// </summary>
		public void Visit(Action<LayoutNode> action)
		{
			action(this);
			foreach (var child in _children)
			{
				child.Visit(action);
			}
			foreach (var portal in _portalChildren)
			{
				portal.Visit(action);
			}
		}

		/// <summary>
		/// Returns a debug string representation of the node tree.
		/// </summary>
		public string ToDebugString(int indent = 0)
		{
			var prefix = new string(' ', indent * 2);
			var name = Control?.GetType().Name ?? "Container";
			var result = $"{prefix}{name} desired={DesiredSize.Width}x{DesiredSize.Height} bounds=({AbsoluteBounds.X},{AbsoluteBounds.Y} {AbsoluteBounds.Width}x{AbsoluteBounds.Height})";

			foreach (var child in _children)
			{
				result += "\n" + child.ToDebugString(indent + 1);
			}

			foreach (var portal in _portalChildren)
			{
				result += "\n" + $"{prefix}  [Portal]" + portal.ToDebugString(indent + 2);
			}

			return result;
		}
	}
}
