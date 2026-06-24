// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Runtime.CompilerServices;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Abstract base class for portal content controls (overlay panels used by dropdowns, menus, etc.).
	/// Provides default implementations of <see cref="IWindowControl"/>, <see cref="IDOMPaintable"/>,
	/// <see cref="IMouseAwareControl"/>, and <see cref="IHasPortalBounds"/> to eliminate boilerplate.
	/// </summary>
	public abstract class PortalContentBase : IWindowControl, IContainer, IDOMPaintable, IMouseAwareControl, IHasPortalBounds
	{
		private static readonly ConditionalWeakTable<IFocusableControl, PortalContentBase> _portalFocusRegistry = new();
		private IFocusableControl? _portalFocusedControl;

		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;

		// The border-shrunk content rect from the last PaintDOM, used for hosted-child mouse hit-testing.
		private LayoutRect _contentRect;

		/// <summary>
		/// Gets or sets the control that is focused within this portal's scope,
		/// independently of the window's FocusManager.
		/// </summary>
		public IFocusableControl? PortalFocusedControl
		{
			get => _portalFocusedControl;
			set
			{
				if (ReferenceEquals(_portalFocusedControl, value)) return;
				var old = _portalFocusedControl;
				if (old != null) _portalFocusRegistry.Remove(old);
				_portalFocusedControl = value;
				if (value != null)
				{
					_portalFocusRegistry.Remove(value);
					_portalFocusRegistry.Add(value, this);
				}
				(old as IWindowControl)?.Container?.Invalidate(Invalidation.Relayout);
				(value as IWindowControl)?.Container?.Invalidate(Invalidation.Relayout);
			}
		}

		/// <summary>
		/// Attempts to find the portal that owns the specified control's portal focus.
		/// Used by controls to determine if they have portal-scoped focus.
		/// </summary>
		internal static bool TryGetPortalOwner(IFocusableControl control, out PortalContentBase? portal)
		{
			return _portalFocusRegistry.TryGetValue(control, out portal);
		}

		/// <summary>
		/// When set, PaintDOM draws a border using these characters and shrinks the
		/// inner bounds by 1 on each side before calling <see cref="PaintPortalContent"/>.
		/// Mouse coordinates are automatically adjusted by (-1,-1) for the border offset.
		/// </summary>
		public BoxChars? BorderStyle { get; set; }

		/// <summary>Foreground color for the border characters. Falls back to the default foreground.</summary>
		public Color? BorderColor { get; set; }

		/// <summary>Background color for the border and fill area. Falls back to the default background.</summary>
		public Color? BorderBackgroundColor { get; set; }

		#region IHasPortalBounds

		/// <summary>
		/// Returns the absolute position and size for this portal overlay.
		/// Subclasses must implement this to provide their bounds.
		/// </summary>
		public abstract Rectangle GetPortalBounds();

		/// <inheritdoc/>
		public bool DismissOnOutsideClick { get; set; }

		/// <summary>
		/// Raised when the portal is about to be dismissed due to an outside click.
		/// Consumers can use this to perform cleanup before the portal is removed.
		/// </summary>
		public event EventHandler? DismissRequested;

		/// <summary>
		/// Raises the <see cref="DismissRequested"/> event.
		/// </summary>
		internal void RaiseDismissRequested() => DismissRequested?.Invoke(this, EventArgs.Empty);

		#endregion

		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public virtual bool WantsMouseEvents => true;

		/// <inheritdoc/>
		public virtual bool CanFocusWithMouse => false;

#pragma warning disable CS0067 // Event never raised (interface requirement)
		/// <inheritdoc/>
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
#pragma warning restore CS0067

		/// <summary>
		/// Explicit interface implementation that adjusts mouse coordinates for the border
		/// offset before delegating to the public virtual <see cref="ProcessMouseEvent"/>.
		/// </summary>
		bool IMouseAwareControl.ProcessMouseEvent(MouseEventArgs args)
		{
			if (BorderStyle.HasValue)
			{
				var adjusted = args.WithPosition(
					new System.Drawing.Point(args.Position.X - 1, args.Position.Y - 1));
				return ProcessMouseEvent(adjusted);
			}
			return ProcessMouseEvent(args);
		}

		/// <summary>
		/// Processes a mouse event. When <see cref="BorderStyle"/> is set, coordinates
		/// are already adjusted for the border offset.
		/// </summary>
		public abstract bool ProcessMouseEvent(MouseEventArgs args);

		#endregion

		#region IWindowControl Implementation

		/// <inheritdoc/>
		public int? ContentWidth => GetPortalBounds().Width;

		/// <inheritdoc/>
		public int ActualX => _actualX;

		/// <inheritdoc/>
		public int ActualY => _actualY;

		/// <inheritdoc/>
		public int ActualWidth => _actualWidth;

		/// <inheritdoc/>
		public int ActualHeight => _actualHeight;

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

		/// <inheritdoc/>
		/// <remarks>
		/// This is the <b>upstream</b> container — the window that hosts this portal (set by
		/// <c>CreatePortal</c>). It is distinct from this portal acting as the container for an
		/// optional hosted <see cref="Content"/> child (see the <c>IContainer</c> implementation):
		/// a hosted child's <c>Container</c> points at this portal, and this portal's
		/// <c>Container</c> points at the window, forming the <c>child → portal → window</c> chain.
		/// </remarks>
		public IContainer? Container { get; set; }

		private IWindowControl? _content;

		/// <summary>
		/// Optional hosted child control. When set, this portal renders the child through the normal control
		/// pipeline (the child's own <see cref="IDOMPaintable.MeasureDOM"/>/<see cref="IDOMPaintable.PaintDOM"/>),
		/// inside this portal's border, and forwards mouse events to it — so the child self-invalidates via its
		/// Container and the consumer never calls Invalidate. When null, the subclass paints manually via
		/// <see cref="PaintPortalContent"/> (the legacy path), so existing subclasses are unaffected.
		/// </summary>
		public IWindowControl? Content
		{
			get => _content;
			set
			{
				if (ReferenceEquals(_content, value)) return;
				if (_content != null) _content.Container = null;
				_content = value;
				if (_content != null) _content.Container = this; // child → this(IContainer) → window
				Container?.Invalidate(Invalidation.Relayout);
			}
		}

		/// <inheritdoc/>
		public Margin Margin { get; set; } = new Margin(0, 0, 0, 0);

		/// <inheritdoc/>
		public StickyPosition StickyPosition { get; set; } = StickyPosition.None;

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public bool Visible { get; set; } = true;

		/// <inheritdoc/>
		public int? Width { get; set; }

		/// <inheritdoc/>
		public int? Height { get; set; }

		/// <inheritdoc/>
		public Size GetLogicalContentSize()
		{
			var bounds = GetPortalBounds();
			return new Size(bounds.Width, bounds.Height);
		}

		/// <inheritdoc/>
		public void Invalidate(Invalidation work)
		{
			Container?.Invalidate(work);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			PortalFocusedControl = null;
		}

		#endregion

		#region IContainer (host for an optional Content child)

		// PortalContentBase already exposes `IContainer? Container` (the window). For IContainer we expose
		// background/foreground/system resolved from that window container, and Invalidate forwards up (already
		// implemented as Invalidate(Invalidation)). The IContainer.Invalidate(work, caller) overload forwards too.

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => BorderBackgroundColor ?? Container?.BackgroundColor ?? Color.Black;
			set => BorderBackgroundColor = value;
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			get => BorderColor ?? Container?.ForegroundColor ?? Color.White;
			set => BorderColor = value;
		}

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem => Container?.GetConsoleWindowSystem;

		/// <inheritdoc/>
		void IContainer.Invalidate(Invalidation work, IWindowControl? callerControl)
			=> Container?.Invalidate(work, callerControl ?? this);

		/// <inheritdoc/>
		/// <remarks>
		/// Returns the height of the INNER content area the hosted <see cref="Content"/> is painted into —
		/// i.e. the portal height minus the border rows — NOT the portal's full <see cref="ActualHeight"/>.
		/// The hosted child uses this for its scroll/ensure-visible math, so it must match the rect the child
		/// is actually painted into (<c>_contentRect</c>), or the child believes it has the border rows too and
		/// scrolls one row late.
		/// </remarks>
		public int? GetVisibleHeightForControl(IWindowControl control)
		{
			if (Content == null) return null;
			// After a paint, _contentRect is the exact inner area. Before the first paint, derive it from the
			// portal bounds minus the border (2 rows when bordered), matching PaintDOM's inner-rect math.
			if (_contentRect.Height > 0) return _contentRect.Height;
			int borderRows = BorderStyle.HasValue ? 2 : 0;
			return Math.Max(0, GetPortalBounds().Height - borderRows);
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			var bounds = GetPortalBounds();
			return new LayoutSize(bounds.Width, bounds.Height);
		}

		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect,
			Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			LayoutRect contentRect;
			LayoutRect contentClip;
			if (BorderStyle is { } border)
			{
				buffer.DrawBox(bounds, border, BorderColor ?? defaultFg,
					BorderBackgroundColor ?? defaultBg);
				contentRect = new LayoutRect(bounds.X + 1, bounds.Y + 1,
					Math.Max(0, bounds.Width - 2), Math.Max(0, bounds.Height - 2));
				contentClip = contentRect.Intersect(clipRect);
			}
			else
			{
				contentRect = bounds;
				contentClip = clipRect;
			}

			// Record the content rect for hosted-child mouse hit-testing.
			_contentRect = contentRect;

			if (Content is IDOMPaintable paintable)
			{
				// Host the child: measure it to the content rect and paint via its own pipeline.
				var constraints = LayoutConstraints.Tight(contentRect.Width, contentRect.Height);
				paintable.MeasureDOM(constraints);
				paintable.PaintDOM(buffer, contentRect, contentClip,
					BorderColor ?? defaultFg, BorderBackgroundColor ?? defaultBg);
			}
			else
			{
				PaintPortalContent(buffer, contentRect, contentClip, defaultFg, defaultBg);
			}
		}

		/// <summary>
		/// Default mouse handling for a hosted <see cref="Content"/> child: forwards the (already border-adjusted)
		/// event to the child. Subclasses that host a child should return this from their ProcessMouseEvent
		/// override (and may inspect the result to fire higher-level events such as item-accepted).
		/// </summary>
		protected bool ProcessHostedMouseEvent(MouseEventArgs args)
		{
			if (Content is IMouseAwareControl mouse)
				return mouse.ProcessMouseEvent(args);
			return false;
		}

		/// <summary>
		/// Paints the portal content. Called by <see cref="PaintDOM"/> after setting actual bounds.
		/// </summary>
		protected abstract void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg);

		#endregion
	}
}
