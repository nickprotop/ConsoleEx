// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Color = Spectre.Console.Color;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Abstract base class for all UI controls, providing shared layout fields, properties,
	/// and default implementations of <see cref="IWindowControl"/> and <see cref="IDOMPaintable"/>.
	/// </summary>
	public abstract class BaseControl : IWindowControl, IDOMPaintable
	{
		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;
		private bool _disposed;

		/// <inheritdoc/>
		public abstract int? ContentWidth { get; }

		/// <inheritdoc/>
		public int ActualX => _actualX;

		/// <inheritdoc/>
		public int ActualY => _actualY;

		/// <inheritdoc/>
		public int ActualWidth => _actualWidth;

		/// <inheritdoc/>
		public int ActualHeight => _actualHeight;

		/// <inheritdoc/>
		public virtual HorizontalAlignment HorizontalAlignment
		{
			get => _horizontalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _horizontalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public virtual VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set => PropertySetterHelper.SetEnumProperty(ref _verticalAlignment, value, Container);
		}

		/// <inheritdoc/>
		public virtual IContainer? Container { get; set; }

		/// <inheritdoc/>
		public virtual Margin Margin
		{
			get => _margin;
			set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
		}

		/// <inheritdoc/>
		public virtual StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public virtual bool Visible
		{
			get => _visible;
			set => PropertySetterHelper.SetBoolProperty(ref _visible, value, Container);
		}

		/// <inheritdoc/>
		public virtual int? Width
		{
			get => _width;
			set => PropertySetterHelper.SetDimensionProperty(ref _width, value, Container);
		}

		/// <inheritdoc/>
		public virtual System.Drawing.Size GetLogicalContentSize()
		{
			int width = ContentWidth ?? 0;
			int height = 1 + _margin.Top + _margin.Bottom;
			return new System.Drawing.Size(width, height);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Sets the actual rendered bounds from the layout system.
		/// Call this at the start of <see cref="PaintDOM"/> to record the control's position.
		/// </summary>
		/// <param name="bounds">The layout bounds assigned by the layout engine.</param>
		protected void SetActualBounds(LayoutRect bounds)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			OnDisposing();
			Container = null;
		}

		/// <summary>
		/// Called during <see cref="Dispose"/> before <c>Container</c> is set to null.
		/// Override to perform control-specific cleanup (null events, close portals, clear data, etc.).
		/// </summary>
		protected virtual void OnDisposing() { }

		/// <inheritdoc/>
		public abstract LayoutSize MeasureDOM(LayoutConstraints constraints);

		/// <inheritdoc/>
		public abstract void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultForeground, Color defaultBackground);
	}
}
