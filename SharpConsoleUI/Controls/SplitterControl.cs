// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A vertical splitter control that allows users to resize adjacent columns in a <see cref="HorizontalGridControl"/>.
	/// Supports keyboard-based resizing with arrow keys and provides visual feedback during focus and dragging.
	/// </summary>
	public class SplitterControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		private const int DEFAULT_WIDTH = 1;
		private const float MIN_COLUMN_PERCENTAGE = 0.1f; // Minimum 10% width for any column

		private Color? _backgroundColorValue;
		private Color _borderColor = Color.White;
		private IContainer? _container;
		private Color? _draggingBackgroundColorValue;
		private Color? _draggingForegroundColorValue;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus;
		private bool _isDragging;
		private bool _isEnabled = true;
		private bool _isMouseDragging = false;
		private int _lastMouseX = 0;

		// References to the columns on either side of this splitter
		private ColumnContainer? _leftColumn;

		private HorizontalGridControl? _parentGrid;
		private ColumnContainer? _rightColumn;

		/// <summary>
		/// Initializes a new instance of the <see cref="SplitterControl"/> class.
		/// </summary>
		public SplitterControl()
		{
			VerticalAlignment = VerticalAlignment.Fill;
			Width = DEFAULT_WIDTH;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SplitterControl"/> class with specified adjacent columns.
		/// </summary>
		/// <param name="leftColumn">The column to the left of the splitter.</param>
		/// <param name="rightColumn">The column to the right of the splitter.</param>
		public SplitterControl(ColumnContainer leftColumn, ColumnContainer rightColumn)
		{
			VerticalAlignment = VerticalAlignment.Fill;
			Width = DEFAULT_WIDTH;
			_leftColumn = leftColumn;
			_rightColumn = rightColumn;
		}

		/// <summary>
		/// Occurs when the splitter is moved and column widths are adjusted.
		/// </summary>
		public event EventHandler<SplitterMovedEventArgs>? SplitterMoved;

		/// <inheritdoc/>
		public override int? ContentWidth => Width;

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color of the splitter in normal state.
		/// </summary>
		public Color BackgroundColor
		{
			get => ColorResolver.ResolveBackground(_backgroundColorValue, Container);
			set
			{
				_backgroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the border color of the splitter.
		/// </summary>
		public Color BorderColor
		{
			get => _borderColor;
			set
			{
				_borderColor = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color of the splitter when being dragged.
		/// </summary>
		public Color DraggingBackgroundColor
		{
			get => _draggingBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Yellow;
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
			get => _draggingForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.Black;
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
			get => _focusedBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Blue;
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
			get => _focusedForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.White;
			set
			{
				_focusedForegroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the foreground color of the splitter in normal state.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				if (_isDragging && !value) _isDragging = false;
				var hadFocus = _hasFocus;
				_hasFocus = value;


				Container?.Invalidate(true);

				// Fire focus events
				if (value && !hadFocus)
				{
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
				else if (!value && hadFocus)
				{
					LostFocus?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether the splitter is currently being dragged.
		/// </summary>
		public bool IsDragging => _isDragging;

		/// <summary>
		/// Gets a value indicating whether the splitter is currently being dragged by the mouse.
		/// </summary>
		public bool IsMouseDragging => _isMouseDragging;

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <inheritdoc/>
		public override int? Width
		{
			get => base.Width;
			set
			{
				var validatedValue = value.HasValue ? Math.Max(1, value.Value) : value;
				base.Width = validatedValue;
			}
		}

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
		}

		/// <inheritdoc/>
		public override System.Drawing.Size GetLogicalContentSize()
		{
			return new System.Drawing.Size(Width ?? 1, 1);
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			bool handled = false;
			int delta = 0;

			// When focused, immediately respond to arrow keys (no Enter needed)
			switch (key.Key)
			{
				case ConsoleKey.LeftArrow:
					// Allow faster movement when holding Shift
					delta = key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? -5 : -1;
					handled = true;
					break;

				case ConsoleKey.RightArrow:
					// Allow faster movement when holding Shift
					delta = key.Modifiers.HasFlag(ConsoleModifiers.Shift) ? 5 : 1;
					handled = true;
					break;
			}

			// If we have a movement delta and both columns are set
			if (delta != 0 && _leftColumn != null && _rightColumn != null)
			{

				// Set dragging state for visual feedback
				if (!_isDragging)
				{
					_isDragging = true;
					Container?.Invalidate(true);  // Force redraw with dragging colors
				}

				MoveSplitter(delta);
			}
			else if (delta != 0)
			{
			}

			return handled;
		}

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
		public override LayoutSize MeasureDOM(LayoutConstraints constraints)
		{
			int splitterWidth = Width ?? 1;
			int width = splitterWidth + Margin.Left + Margin.Right;
			// Report minimal height during measurement.
			// The splitter will be given full height during arrangement.
			// This prevents integer overflow when measured with unbounded height.
			int height = 1 + Margin.Top + Margin.Bottom;

			return new LayoutSize(
				Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
				Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight)
			);
		}

		/// <inheritdoc/>
		public override void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			SetActualBounds(bounds);

			Color bgColor, fgColor;

			// Always use double vertical line - color indicates focus/dragging state
			const char splitterChar = '║';

			if (_isDragging)
			{
				// Use dragging colors when in dragging mode
				bgColor = DraggingBackgroundColor;
				fgColor = DraggingForegroundColor;
			}
			else if (_hasFocus)
			{
				// Use focused colors when focused
				bgColor = FocusedBackgroundColor;
				fgColor = FocusedForegroundColor;
			}
			else
			{
				// Use normal colors
				bgColor = BackgroundColor;
				fgColor = ForegroundColor;
			}

			int startX = bounds.X + Margin.Left;
			int startY = bounds.Y + Margin.Top;
			int splitterHeight = bounds.Height - Margin.Top - Margin.Bottom;

			// Fill margins with background color
			Color windowBackground = Container?.BackgroundColor ?? defaultBg;

			// Fill top margin
			ControlRenderingHelpers.FillTopMargin(buffer, bounds, clipRect, startY, fgColor, windowBackground);

			// Paint the splitter lines
			for (int y = 0; y < splitterHeight; y++)
			{
				int paintY = startY + y;
				if (paintY >= clipRect.Y && paintY < clipRect.Bottom && paintY < bounds.Bottom)
				{
					// Fill left margin
					if (Margin.Left > 0)
					{
						buffer.FillRect(new LayoutRect(bounds.X, paintY, Margin.Left, 1), ' ', fgColor, windowBackground);
					}

					// Paint splitter character
					if (startX >= clipRect.X && startX < clipRect.Right)
					{
						buffer.SetCell(startX, paintY, splitterChar, fgColor, bgColor);
					}

					// Fill right margin
					if (Margin.Right > 0)
					{
						buffer.FillRect(new LayoutRect(startX + 1, paintY, Margin.Right, 1), ' ', fgColor, windowBackground);
					}
				}
			}

			// Fill bottom margin
			ControlRenderingHelpers.FillBottomMargin(buffer, bounds, clipRect, startY + splitterHeight, fgColor, windowBackground);
		}

		#endregion

		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => true;

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

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Ignore synthetic enter/leave events — they carry the original press/drag
			// flags and would incorrectly start or process a drag.
			if (args.HasAnyFlag(MouseFlags.MouseEnter, MouseFlags.MouseLeave))
				return false;

			// Use WindowPosition for delta computation because the splitter physically
			// moves as it resizes, making control-relative Position unreliable.
			int mouseX = args.WindowPosition.X;

			// Handle drag-in-progress
			if (_isMouseDragging && args.HasAnyFlag(MouseFlags.Button1Dragged, MouseFlags.Button1Pressed))
			{
				int deltaX = mouseX - _lastMouseX;
				if (deltaX != 0 && _leftColumn != null && _rightColumn != null)
				{
					MoveSplitter(deltaX);
					_lastMouseX = mouseX;
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
				_lastMouseX = mouseX;
				_isDragging = true;
				Container?.Invalidate(true);
				args.Handled = true;
				return true;
			}

			return false;
		}

		#endregion

		/// <summary>
		/// Sets the columns that this splitter will resize
		/// </summary>
		/// <param name="leftColumn">Column to the left of the splitter</param>
		/// <param name="rightColumn">Column to the right of the splitter</param>
		/// <param name="parentGrid">The parent HorizontalGridControl that contains this splitter</param>
		public void SetColumns(ColumnContainer leftColumn, ColumnContainer rightColumn, HorizontalGridControl? parentGrid = null)
		{
			_leftColumn = leftColumn;
			_rightColumn = rightColumn;
			_parentGrid = parentGrid;
			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			bool hadFocus = HasFocus;
			HasFocus = focus;

			// When focus is lost, exit drag mode
			if (!focus && _isDragging)
			{
				_isDragging = false;
				_isMouseDragging = false;
				Container?.Invalidate(true);  // Force redraw with normal colors
			}

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		/// <summary>
		/// Moves the splitter by adjusting the widths of adjacent columns.
		/// </summary>
		/// <param name="delta">The amount to move the splitter (positive = right, negative = left).</param>
		private void MoveSplitter(int delta)
		{
			if (_leftColumn == null || _rightColumn == null)
				return;

			// Get the current column widths:
			// - Prefer explicit Width (always fresh, set by previous MoveSplitter calls)
			// - Fall back to ActualWidth (rendered size, correct for flexing columns with no explicit Width)
			// - Last resort: GetContentWidth or default
			int leftColumnWidth = _leftColumn.Width ?? (_leftColumn.ActualWidth > 0 ? _leftColumn.ActualWidth : (_leftColumn.GetContentWidth() ?? 10));
			int rightColumnWidth = _rightColumn.Width ?? (_rightColumn.ActualWidth > 0 ? _rightColumn.ActualWidth : (_rightColumn.GetContentWidth() ?? 10));

			// Calculate new left width
			int newLeftWidth = leftColumnWidth + delta;

			// Total available width is the combined space of the two adjacent columns
			// Using actual rendered widths ensures correctness when columns flex or when other columns are hidden
			int totalAvailableWidth = leftColumnWidth + rightColumnWidth;


			// Enforce minimum widths (at least 10% of total available or 5 characters)
			int minWidth = Math.Max(5, (int)(totalAvailableWidth * MIN_COLUMN_PERCENTAGE));
			int maxLeftWidth = totalAvailableWidth - minWidth; // Leave minimum space for right column

			// Constrain the new left width
			newLeftWidth = Math.Clamp(newLeftWidth, minWidth, maxLeftWidth);

			// Only apply changes if width is valid and different
			if (newLeftWidth > 0 && newLeftWidth != leftColumnWidth)
			{

				// Apply the new widths
				// FIX: Only set the left column's explicit width.
				// The right column should flex to fill the remaining space in HorizontalLayout.
				_leftColumn.Width = newLeftWidth;
				_rightColumn.Width = null;  // Clear right column width - let it flex


				// Calculate the actual delta that was applied
				int actualDelta = newLeftWidth - leftColumnWidth;

				// Raise the SplitterMoved event
				SplitterMoved?.Invoke(this, new SplitterMovedEventArgs(actualDelta, newLeftWidth, 0));

				// Invalidate to ensure redraw
				Invalidate();

			}
		}
	}

	/// <summary>
	/// Provides data for the <see cref="SplitterControl.SplitterMoved"/> event.
	/// </summary>
	public class SplitterMovedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SplitterMovedEventArgs"/> class.
		/// </summary>
		/// <param name="delta">The amount the splitter was moved.</param>
		/// <param name="leftColumnWidth">The new width of the left column.</param>
		/// <param name="rightColumnWidth">The new width of the right column.</param>
		public SplitterMovedEventArgs(int delta, int leftColumnWidth, int rightColumnWidth)
		{
			Delta = delta;
			LeftColumnWidth = leftColumnWidth;
			RightColumnWidth = rightColumnWidth;
		}

		/// <summary>
		/// Gets the amount the splitter was moved (positive = right, negative = left).
		/// </summary>
		public int Delta { get; }

		/// <summary>
		/// Gets the new width of the column to the left of the splitter.
		/// </summary>
		public int LeftColumnWidth { get; }

		/// <summary>
		/// Gets the new width of the column to the right of the splitter.
		/// </summary>
		public int RightColumnWidth { get; }
	}
}