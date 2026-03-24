// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A horizontal splitter control that allows users to resize vertically-stacked controls by dragging up/down.
	/// Renders as a thin horizontal bar (═══) and supports keyboard and mouse interaction.
	/// Works in any container: Window, ColumnContainer, ScrollablePanelControl.
	/// </summary>
	public class HorizontalSplitterControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		#region Fields

		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;
		private Color? _draggingBackgroundColorValue;
		private Color? _draggingForegroundColorValue;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;

		private IContainer? _container;
		private Window? _subscribedWindow;
		private bool _isDragging;
		private bool _isEnabled = true;
		private bool _isMouseDragging;
		private int _lastMouseY;

		private IWindowControl? _aboveControl;
		private IWindowControl? _belowControl;
		private bool _neighborsResolved;
		private bool _autoHidden;

		private int _minHeightAbove = ControlDefaults.HorizontalSplitterMinControlHeight;
		private int _minHeightBelow = ControlDefaults.HorizontalSplitterMinControlHeight;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="HorizontalSplitterControl"/> class.
		/// Neighbors are auto-discovered from the parent container's children list.
		/// </summary>
		public HorizontalSplitterControl()
		{
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Top;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HorizontalSplitterControl"/> class with explicit neighbors.
		/// </summary>
		/// <param name="aboveControl">The control above the splitter.</param>
		/// <param name="belowControl">The control below the splitter.</param>
		public HorizontalSplitterControl(IWindowControl aboveControl, IWindowControl belowControl)
		{
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Top;
			_aboveControl = aboveControl;
			_belowControl = belowControl;
			_neighborsResolved = true;
		}

		#endregion

		#region Events

		/// <summary>
		/// Occurs when the splitter is moved and control heights are adjusted.
		/// </summary>
		public event EventHandler<HorizontalSplitterMovedEventArgs>? SplitterMoved;

		#endregion

		#region Properties

		/// <inheritdoc/>
		public override int? ContentWidth => null;

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				// Only reset resolved neighbors if they weren't explicitly set via constructor/SetNeighbors.
				// Explicit neighbors remain valid regardless of container changes.
				if (_aboveControl == null && _belowControl == null)
					_neighborsResolved = false;
				Container?.Invalidate(true);
				var newWindow = this.GetParentWindow();
				if (!ReferenceEquals(newWindow, _subscribedWindow))
				{
					if (_subscribedWindow != null)
						_subscribedWindow.FocusManager.FocusChanged -= OnFocusChanged;
					_subscribedWindow = newWindow;
					if (_subscribedWindow != null)
						_subscribedWindow.FocusManager.FocusChanged += OnFocusChanged;
				}
			}
		}

		/// <summary>
		/// Gets or sets the background color of the splitter in normal state.
		/// </summary>
		public Color? BackgroundColor
		{
			get => _backgroundColorValue;
			set
			{
				_backgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the splitter in normal state.
		/// </summary>
		public Color ForegroundColor
		{
			get => ColorResolver.ResolveForeground(_foregroundColorValue, Container);
			set
			{
				_foregroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color of the splitter when being dragged.
		/// </summary>
		public Color DraggingBackgroundColor
		{
			get => _draggingBackgroundColorValue
				?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor
				?? Color.Yellow;
			set
			{
				_draggingBackgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the splitter when being dragged.
		/// </summary>
		public Color DraggingForegroundColor
		{
			get => _draggingForegroundColorValue
				?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor
				?? Color.Black;
			set
			{
				_draggingForegroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color of the splitter when focused.
		/// </summary>
		public Color FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue
				?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor
				?? Color.Blue;
			set
			{
				_focusedBackgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the splitter when focused.
		/// </summary>
		public Color FocusedForegroundColor
		{
			get => _focusedForegroundColorValue
				?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor
				?? Color.White;
			set
			{
				_focusedForegroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => this.GetParentWindow()?.FocusManager.IsFocused(this) ?? false;
		}

		/// <summary>
		/// Gets a value indicating whether the splitter is currently being dragged.
		/// </summary>
		public bool IsDragging => _isDragging;

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <summary>
		/// Gets or sets the minimum height for the control above the splitter.
		/// </summary>
		public int MinHeightAbove
		{
			get => _minHeightAbove;
			set => _minHeightAbove = Math.Max(ControlDefaults.HorizontalSplitterMinControlHeight, value);
		}

		/// <summary>
		/// Gets or sets the minimum height for the control below the splitter.
		/// </summary>
		public int MinHeightBelow
		{
			get => _minHeightBelow;
			set => _minHeightBelow = Math.Max(ControlDefaults.HorizontalSplitterMinControlHeight, value);
		}

		/// <summary>
		/// Gets the control above this splitter (resolved lazily).
		/// </summary>
		public IWindowControl? AboveControl => ResolveAndGetAbove();

		/// <summary>
		/// Gets the control below this splitter (resolved lazily).
		/// </summary>
		public IWindowControl? BelowControl => ResolveAndGetBelow();

		/// <inheritdoc/>
		public override bool Visible
		{
			get => base.Visible;
			set
			{
				if (!value)
					_autoHidden = false; // user explicitly hiding
				base.Visible = value;
			}
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => true;

		#endregion

		#region Neighbor Discovery

		/// <summary>
		/// Sets the controls that this splitter will resize.
		/// </summary>
		/// <param name="aboveControl">Control above the splitter.</param>
		/// <param name="belowControl">Control below the splitter.</param>
		public void SetControls(IWindowControl aboveControl, IWindowControl belowControl)
		{
			_aboveControl = aboveControl;
			_belowControl = belowControl;
			_neighborsResolved = true;
			Container?.Invalidate(true);
		}

		private void ResolveNeighbors()
		{
			if (_neighborsResolved) return;

			// Try to find neighbors from container's children list
			IReadOnlyList<IWindowControl>? children = null;

			if (Container is IContainerControl containerControl)
			{
				children = containerControl.GetChildren();
			}
			else if (Container is Window window)
			{
				children = window.GetControls();
			}

			if (children == null) return;

			int myIndex = -1;
			for (int i = 0; i < children.Count; i++)
			{
				if (ReferenceEquals(children[i], this))
				{
					myIndex = i;
					break;
				}
			}

			if (myIndex < 0) return;

			// Scan outward, skipping invisible controls
			_aboveControl = null;
			for (int i = myIndex - 1; i >= 0; i--)
			{
				if (children[i].Visible)
				{
					_aboveControl = children[i];
					break;
				}
			}

			_belowControl = null;
			for (int i = myIndex + 1; i < children.Count; i++)
			{
				if (children[i].Visible)
				{
					_belowControl = children[i];
					break;
				}
			}

			// Only mark resolved if we found both visible neighbors
			_neighborsResolved = _aboveControl != null && _belowControl != null;

			UpdateAutoVisibility();
		}

		private IWindowControl? ResolveAndGetAbove()
		{
			if (!_neighborsResolved) ResolveNeighbors();
			return _aboveControl;
		}

		private IWindowControl? ResolveAndGetBelow()
		{
			if (!_neighborsResolved) ResolveNeighbors();
			return _belowControl;
		}

		private void UpdateAutoVisibility()
		{
			bool hasVisibleAbove = _aboveControl?.Visible ?? false;
			bool hasVisibleBelow = _belowControl?.Visible ?? false;
			bool shouldBeVisible = hasVisibleAbove && hasVisibleBelow;

			if (!shouldBeVisible)
			{
				_autoHidden = true;
				base.Visible = false;
				_neighborsResolved = false; // re-resolve next time
			}
			else if (_autoHidden && shouldBeVisible)
			{
				_autoHidden = false;
				base.Visible = true;
			}
		}

		#endregion

		#region Resize Logic

		private static bool IsHeightSettable(IWindowControl control)
		{
			// A control is height-settable if it already has an explicit Height,
			// or if it uses VerticalAlignment.Fill (can accept an explicit height)
			return control.Height != null || control.VerticalAlignment == VerticalAlignment.Fill;
		}

		private static int GetCurrentHeight(IWindowControl control)
		{
			return control.Height ?? (control.ActualHeight > 0 ? control.ActualHeight : ControlDefaults.HorizontalSplitterMinControlHeight);
		}

		/// <summary>
		/// Moves the splitter by the specified delta, adjusting neighbor heights.
		/// Positive delta moves the splitter down (above grows, below shrinks).
		/// </summary>
		/// <param name="delta">Rows to move (positive = down, negative = up).</param>
		public void MoveSplitter(int delta)
		{
			var above = ResolveAndGetAbove();
			var below = ResolveAndGetBelow();

			if (above == null && below == null) return;
			if (delta == 0) return;

			bool aboveSettable = above != null && above.Visible && IsHeightSettable(above);
			bool belowSettable = below != null && below.Visible && IsHeightSettable(below);

			if (!aboveSettable && !belowSettable) return;

			if (aboveSettable && belowSettable)
			{
				// Both can be resized
				int aboveHeight = GetCurrentHeight(above!);
				int belowHeight = GetCurrentHeight(below!);

				int newAboveHeight = aboveHeight + delta;
				int newBelowHeight = belowHeight - delta;

				// Clamp
				newAboveHeight = Math.Max(_minHeightAbove, newAboveHeight);
				newBelowHeight = Math.Max(_minHeightBelow, newBelowHeight);

				// Recalculate if clamping changed the delta
				int actualDelta = newAboveHeight - aboveHeight;
				if (belowHeight - actualDelta < _minHeightBelow)
					actualDelta = belowHeight - _minHeightBelow;
				newAboveHeight = aboveHeight + actualDelta;
				newBelowHeight = belowHeight - actualDelta;

				if (actualDelta == 0) return;

				// If both were Fill, set above to explicit and leave below as Fill
				if (above!.Height == null && below!.Height == null)
				{
					above.Height = newAboveHeight;
					// Leave below as Fill — it flexes
				}
				else if (above.Height != null && below!.Height == null)
				{
					// Above has explicit height, below is Fill
					above.Height = newAboveHeight;
				}
				else if (above.Height == null && below!.Height != null)
				{
					// Above is Fill, below has explicit height
					below.Height = newBelowHeight;
				}
				else
				{
					// Both have explicit heights
					above.Height = newAboveHeight;
					below!.Height = newBelowHeight;
				}

				SplitterMoved?.Invoke(this, new HorizontalSplitterMovedEventArgs(actualDelta, newAboveHeight, newBelowHeight));
			}
			else if (aboveSettable && !belowSettable)
			{
				// Only above can be resized
				int aboveHeight = GetCurrentHeight(above!);
				int newAboveHeight = Math.Max(_minHeightAbove, aboveHeight + delta);
				int actualDelta = newAboveHeight - aboveHeight;
				if (actualDelta == 0) return;

				above!.Height = newAboveHeight;
				SplitterMoved?.Invoke(this, new HorizontalSplitterMovedEventArgs(actualDelta, newAboveHeight, 0));
			}
			else
			{
				// Only below can be resized (delta is negated since moving splitter down means below shrinks)
				int belowHeight = GetCurrentHeight(below!);
				int newBelowHeight = Math.Max(_minHeightBelow, belowHeight - delta);
				int actualDelta = belowHeight - newBelowHeight;
				if (actualDelta == 0) return;

				below!.Height = newBelowHeight;
				SplitterMoved?.Invoke(this, new HorizontalSplitterMovedEventArgs(delta, 0, newBelowHeight));
			}

			Invalidate();
		}

		#endregion

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int width = constraints.MaxWidth;
			int height = 1 + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			// Re-check neighbor visibility in case it changed since last resolve
			if (_neighborsResolved)
				UpdateAutoVisibility();

			SetActualBounds(bounds);

			Color bgColor, fgColor;

			const char splitterChar = '═';

			if (_isDragging)
			{
				bgColor = DraggingBackgroundColor;
				fgColor = DraggingForegroundColor;
			}
			else if (this.GetParentWindow()?.FocusManager.IsFocused(this) ?? false)
			{
				bgColor = FocusedBackgroundColor;
				fgColor = FocusedForegroundColor;
			}
			else
			{
				bgColor = ColorResolver.ResolveBackground(_backgroundColorValue, Container);
				fgColor = ForegroundColor;
			}

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int splitterWidth = bounds.Width - Margin.Left - Margin.Right;

			var effectiveBg = _backgroundColorValue == null ? Color.Transparent : ColorResolver.ResolveBackground(_backgroundColorValue, Container);

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, effectiveBg);

			// Paint the splitter line
			if (startY >= clipRect.Y && startY < clipRect.Bottom && startY < bounds.Bottom)
			{
				// Fill left margin
				if (Margin.Left > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(bounds.X, startY, Margin.Left, 1), fgColor, effectiveBg);
				}

				// Paint splitter characters
				var splitterBg = bgColor;
				for (int x = 0; x < splitterWidth; x++)
				{
					int paintX = startX + x;
					if (paintX >= clipRect.X && paintX < clipRect.Right)
					{
						buffer.SetNarrowCell(paintX, startY, splitterChar, fgColor, splitterBg);
					}
				}

				// Fill right margin
				if (Margin.Right > 0)
				{
					ControlRenderingHelpers.FillRect(buffer, new LayoutRect(startX + splitterWidth, startY, Margin.Right, 1), fgColor, effectiveBg);
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + 1, fgColor, effectiveBg);
		}

		#endregion

		#region IInteractiveControl Implementation

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !(this.GetParentWindow()?.FocusManager.IsFocused(this) ?? false)) return false;

			int delta = 0;

			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					delta = key.Modifiers.HasFlag(ConsoleModifiers.Shift)
						? -ControlDefaults.HorizontalSplitterKeyboardJumpSize
						: -1;
					break;
				case ConsoleKey.DownArrow:
					delta = key.Modifiers.HasFlag(ConsoleModifiers.Shift)
						? ControlDefaults.HorizontalSplitterKeyboardJumpSize
						: 1;
					break;
				default:
					return false;
			}

			if (!_isDragging)
			{
				_isDragging = true;
				Container?.Invalidate(true);
			}

			MoveSplitter(delta);
			return true;
		}

		#endregion

		#region IFocusableControl Implementation

		private void OnFocusChanged(object? sender, Core.FocusChangedEventArgs e)
		{
			if (ReferenceEquals(e.Previous, this) && _isDragging)
			{
				_isDragging = false;
				_isMouseDragging = false;
			}
		}

		#endregion

		#region IMouseAwareControl Implementation

#pragma warning disable CS0067
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <summary>
		/// Occurs when the control is right-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;
#pragma warning restore CS0067

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled) return false;

			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			if (args.HasAnyFlag(MouseFlags.MouseEnter, MouseFlags.MouseLeave))
				return false;

			int mouseY = args.WindowPosition.Y;

			// Handle drag-in-progress
			if (_isMouseDragging && args.HasAnyFlag(MouseFlags.Button1Dragged, MouseFlags.Button1Pressed))
			{
				int deltaY = mouseY - _lastMouseY;
				if (deltaY != 0)
				{
					MoveSplitter(deltaY);
					_lastMouseY = mouseY;
				}
				args.Handled = true;
				return true;
			}

			// Handle drag end
			if (args.HasFlag(MouseFlags.Button1Released) && _isMouseDragging)
			{
				_isMouseDragging = false;
				_isDragging = false;
				Container?.Invalidate(true);
				args.Handled = true;
				return true;
			}

			// Handle press to start drag
			if (args.HasFlag(MouseFlags.Button1Pressed) && !_isMouseDragging)
			{
				_isMouseDragging = true;
				_lastMouseY = mouseY;
				_isDragging = true;
				Container?.Invalidate(true);
				args.Handled = true;
				return true;
			}

			return false;
		}

		#endregion

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			return new System.Drawing.Size(1, 1);
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			if (_subscribedWindow != null)
			{
				_subscribedWindow.FocusManager.FocusChanged -= OnFocusChanged;
				_subscribedWindow = null;
			}
		}
	}

	/// <summary>
	/// Provides data for the <see cref="HorizontalSplitterControl.SplitterMoved"/> event.
	/// </summary>
	public class HorizontalSplitterMovedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="HorizontalSplitterMovedEventArgs"/> class.
		/// </summary>
		/// <param name="delta">The amount the splitter was moved (positive = down).</param>
		/// <param name="aboveControlHeight">The new height of the control above the splitter.</param>
		/// <param name="belowControlHeight">The new height of the control below the splitter.</param>
		public HorizontalSplitterMovedEventArgs(int delta, int aboveControlHeight, int belowControlHeight)
		{
			Delta = delta;
			AboveControlHeight = aboveControlHeight;
			BelowControlHeight = belowControlHeight;
		}

		/// <summary>
		/// Gets the amount the splitter was moved (positive = down, negative = up).
		/// </summary>
		public int Delta { get; }

		/// <summary>
		/// Gets the new height of the control above the splitter.
		/// </summary>
		public int AboveControlHeight { get; }

		/// <summary>
		/// Gets the new height of the control below the splitter.
		/// </summary>
		public int BelowControlHeight { get; }
	}
}
