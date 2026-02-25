// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Abstract base class for portal content controls (overlay panels used by dropdowns, menus, etc.).
	/// Provides default implementations of <see cref="IWindowControl"/>, <see cref="IDOMPaintable"/>,
	/// <see cref="IMouseAwareControl"/>, and <see cref="IHasPortalBounds"/> to eliminate boilerplate.
	/// </summary>
	public abstract class PortalContentBase : IWindowControl, IDOMPaintable, IMouseAwareControl, IHasPortalBounds
	{
		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;

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
		public IContainer? Container { get; set; }

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
		public Size GetLogicalContentSize()
		{
			var bounds = GetPortalBounds();
			return new Size(bounds.Width, bounds.Height);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			// Portal content is lightweight; nothing to dispose by default
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

			if (BorderStyle is { } border)
			{
				buffer.DrawBox(bounds, border, BorderColor ?? defaultFg,
					BorderBackgroundColor ?? defaultBg);
				var inner = new LayoutRect(bounds.X + 1, bounds.Y + 1,
					Math.Max(0, bounds.Width - 2), Math.Max(0, bounds.Height - 2));
				PaintPortalContent(buffer, inner, clipRect, defaultFg, defaultBg);
			}
			else
			{
				PaintPortalContent(buffer, bounds, clipRect, defaultFg, defaultBg);
			}
		}

		/// <summary>
		/// Paints the portal content. Called by <see cref="PaintDOM"/> after setting actual bounds.
		/// </summary>
		protected abstract void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg);

		#endregion
	}
}
