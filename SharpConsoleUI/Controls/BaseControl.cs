// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Abstract base class for all UI controls, providing shared layout fields, properties,
	/// and default implementations of <see cref="IWindowControl"/> and <see cref="IDOMPaintable"/>.
	/// Implements <see cref="INotifyPropertyChanged"/> for MVVM data binding support.
	/// </summary>
	public abstract class BaseControl : IWindowControl, IDOMPaintable, INotifyPropertyChanged
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
		private BindingCollection? _bindings;

		/// <inheritdoc/>
		public event PropertyChangedEventHandler? PropertyChanged;

		/// <summary>
		/// Gets the binding collection for this control. Lazily allocated on first access.
		/// </summary>
		public BindingCollection Bindings => _bindings ??= new BindingCollection();

		/// <summary>
		/// Raises the <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">The name of the property that changed.</param>
		protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		/// <summary>
		/// Sets a property value with change detection, notification, and automatic invalidation.
		/// </summary>
		/// <typeparam name="T">The property type.</typeparam>
		/// <param name="field">Reference to the backing field.</param>
		/// <param name="value">The new value.</param>
		/// <param name="propertyName">The property name (auto-filled by compiler).</param>
		/// <returns>True if the value changed.</returns>
		protected bool SetProperty<T>(ref T field, T value,
			[CallerMemberName] string? propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value)) return false;
			field = value;
			OnPropertyChanged(propertyName);
			Container?.Invalidate(true);
			return true;
		}

		/// <summary>
		/// Sets a property value with validation, change detection, notification, and automatic invalidation.
		/// </summary>
		/// <typeparam name="T">The property type.</typeparam>
		/// <param name="field">Reference to the backing field.</param>
		/// <param name="value">The new value.</param>
		/// <param name="validate">Validation/transformation function applied before setting.</param>
		/// <param name="propertyName">The property name (auto-filled by compiler).</param>
		/// <returns>True if the value changed.</returns>
		protected bool SetProperty<T>(ref T field, T value, Func<T, T> validate,
			[CallerMemberName] string? propertyName = null)
		{
			var validated = validate(value);
			if (EqualityComparer<T>.Default.Equals(field, validated)) return false;
			field = validated;
			OnPropertyChanged(propertyName);
			Container?.Invalidate(true);
			return true;
		}

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
			set => SetProperty(ref _horizontalAlignment, value);
		}

		/// <inheritdoc/>
		public virtual VerticalAlignment VerticalAlignment
		{
			get => _verticalAlignment;
			set => SetProperty(ref _verticalAlignment, value);
		}

		/// <inheritdoc/>
		public virtual IContainer? Container { get; set; }

		/// <inheritdoc/>
		public virtual Margin Margin
		{
			get => _margin;
			set => SetProperty(ref _margin, value);
		}

		/// <inheritdoc/>
		public virtual StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => SetProperty(ref _stickyPosition, value);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public virtual bool Visible
		{
			get => _visible;
			set => SetProperty(ref _visible, value);
		}

		/// <inheritdoc/>
		public virtual int? Width
		{
			get => _width;
			set => SetProperty(ref _width, value, v => v.HasValue ? Math.Max(0, v.Value) : v);
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
			_bindings?.Dispose();
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
