// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;
using Spectre.Console;

namespace ConsoleEx.Controls
{
	/// <summary>
	/// Represents a vertical splitter control that can be used to resize columns in a HorizontalGridControl
	/// </summary>
	public class SplitterControl : IWIndowControl, IInteractiveControl
	{
		private const int DEFAULT_WIDTH = 1;
		private const float MIN_COLUMN_PERCENTAGE = 0.1f; // Minimum 10% width for any column

		private Alignment _alignment = Alignment.Left;
		private Color? _backgroundColorValue;
		private Color _borderColor = Color.White;
		private List<string>? _cachedContent;
		private IContainer? _container;
		private Color? _draggingBackgroundColorValue;
		private Color? _draggingForegroundColorValue;
		private Color? _focusedBackgroundColorValue;
		private Color? _focusedForegroundColorValue;
		private Color? _foregroundColorValue;
		private bool _hasFocus;
		private bool _invalidated = true;
		private bool _isDragging;
		private bool _isEnabled = true;

		// References to the columns on either side of this splitter
		private ColumnContainer? _leftColumn;

		private Margin _margin = new Margin(0, 0, 0, 0);
		private ColumnContainer? _rightColumn;
		private int _startDragPosition;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width = DEFAULT_WIDTH;

		public SplitterControl()
		{
		}

		public SplitterControl(ColumnContainer leftColumn, ColumnContainer rightColumn)
		{
			_leftColumn = leftColumn;
			_rightColumn = rightColumn;
		}

		// Event raised when the splitter is moved
		public event EventHandler<SplitterMovedEventArgs>? SplitterMoved;

		public int? ActualWidth => _width;

		public Alignment Alignment
		{
			get => _alignment;
			set
			{
				_alignment = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color BorderColor
		{
			get => _borderColor;
			set
			{
				_borderColor = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				_invalidated = true;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color DraggingBackgroundColor
		{
			get => _draggingBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Yellow;
			set
			{
				_draggingBackgroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color DraggingForegroundColor
		{
			get => _draggingForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.Black;
			set
			{
				_draggingForegroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color FocusedBackgroundColor
		{
			get => _focusedBackgroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor ?? Color.Blue;
			set
			{
				_focusedBackgroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color FocusedForegroundColor
		{
			get => _focusedForegroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor ?? Color.White;
			set
			{
				_focusedForegroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? Color.White;
			set
			{
				_foregroundColorValue = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				_hasFocus = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public bool IsDragging => _isDragging;

		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_isEnabled = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public Margin Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		public object? Tag { get; set; }

		public bool Visible
		{
			get => _visible;
			set
			{
				_visible = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public int? Width
		{
			get => _width;
			set
			{
				_width = value;
				_cachedContent = null;
				Container?.Invalidate(true);
			}
		}

		public void Dispose()
		{
			Container = null;
		}

		public (int Left, int Top)? GetCursorPosition()
		{
			return null; // Splitter doesn't have a cursor position
		}

		public void Invalidate()
		{
			_invalidated = true;
			_cachedContent = null;
			Container?.Invalidate(false);
		}

		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_isEnabled || !_hasFocus)
				return false;

			bool handled = false;

			// If we're not dragging, start dragging on Enter
			if (!_isDragging && key.Key == ConsoleKey.Enter)
			{
				_isDragging = true;
				_startDragPosition = 0; // We'll track relative movement
				_cachedContent = null;  // Force redraw with dragging colors
				Container?.Invalidate(true);
				handled = true;
			}
			// If we're dragging, handle left/right arrow keys
			else if (_isDragging)
			{
				int delta = 0;

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

					case ConsoleKey.Enter:
					case ConsoleKey.Escape:
						_isDragging = false;
						_cachedContent = null;  // Force redraw with normal colors
						Container?.Invalidate(true);
						handled = true;
						break;
				}

				// If we have a movement delta and both columns are set
				if (delta != 0 && _leftColumn != null && _rightColumn != null)
				{
					MoveSplitter(delta);
				}
			}

			return handled;
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			if (!_invalidated && _cachedContent != null)
				return _cachedContent;

			_cachedContent = new List<string>();

			Color bgColor, fgColor;
			char splitterChar;

			if (_isDragging)
			{
				// Use dragging colors when in dragging mode
				bgColor = DraggingBackgroundColor;
				fgColor = DraggingForegroundColor;
				splitterChar = '║'; // Double vertical line for dragging state
			}
			else if (_hasFocus)
			{
				// Use focused colors when focused
				bgColor = FocusedBackgroundColor;
				fgColor = FocusedForegroundColor;
				splitterChar = '┃'; // Bold vertical line for focused state
			}
			else
			{
				// Use normal colors
				bgColor = BackgroundColor;
				fgColor = ForegroundColor;
				splitterChar = '│'; // Normal vertical line for default state
			}

			// Create the splitter line
			int height = availableHeight ?? 1;
			for (int i = 0; i < height; i++)
			{
				string line = AnsiConsoleHelper.ConvertSpectreMarkupToAnsi(
					splitterChar.ToString(),
					1,
					1,
					false,
					bgColor,
					fgColor
				)[0];

				_cachedContent.Add(line);
			}

			_invalidated = false;
			return _cachedContent;
		}

		/// <summary>
		/// Sets the columns that this splitter will resize
		/// </summary>
		/// <param name="leftColumn">Column to the left of the splitter</param>
		/// <param name="rightColumn">Column to the right of the splitter</param>
		public void SetColumns(ColumnContainer leftColumn, ColumnContainer rightColumn)
		{
			_leftColumn = leftColumn;
			_rightColumn = rightColumn;
			_invalidated = true;
			_cachedContent = null;
		}

		public void SetFocus(bool focus, bool backward)
		{
			_hasFocus = focus;
			_cachedContent = null;
			Container?.Invalidate(true);
		}

		/// <summary>
		/// Moves the splitter by adjusting the widths of adjacent columns
		/// </summary>
		/// <param name="delta">The amount to move the splitter (positive = right, negative = left)</param>
		private void MoveSplitter(int delta)
		{
			if (_leftColumn == null || _rightColumn == null)
				return;

			// Get the current width of both columns
			int leftColumnWidth = _leftColumn.Width ?? 0;
			int rightColumnWidth = _rightColumn.Width ?? 0;

			// If either column has null (auto) width, we need to calculate their actual widths
			if (leftColumnWidth == 0)
				leftColumnWidth = _leftColumn.GetActualWidth() ?? 0;

			if (rightColumnWidth == 0)
				rightColumnWidth = _rightColumn.GetActualWidth() ?? 0;

			// Calculate new widths
			int newLeftWidth = leftColumnWidth + delta;
			int newRightWidth = rightColumnWidth - delta;

			// Ensure minimum widths (at least 10% of combined width)
			int totalWidth = leftColumnWidth + rightColumnWidth;
			int minWidth = Math.Max(1, (int)(totalWidth * MIN_COLUMN_PERCENTAGE));

			if (newLeftWidth < minWidth)
			{
				delta = minWidth - leftColumnWidth;
				newLeftWidth = minWidth;
				newRightWidth = totalWidth - minWidth;
			}
			else if (newRightWidth < minWidth)
			{
				delta = leftColumnWidth - (totalWidth - minWidth);
				newLeftWidth = totalWidth - minWidth;
				newRightWidth = minWidth;
			}

			// Apply the new widths
			_leftColumn.Width = newLeftWidth;
			_rightColumn.Width = newRightWidth;

			// Raise the SplitterMoved event
			SplitterMoved?.Invoke(this, new SplitterMovedEventArgs(delta, newLeftWidth, newRightWidth));

			// Invalidate to ensure redraw
			Invalidate();
		}
	}

	/// <summary>
	/// Event arguments for the SplitterMoved event
	/// </summary>
	public class SplitterMovedEventArgs : EventArgs
	{
		public SplitterMovedEventArgs(int delta, int leftColumnWidth, int rightColumnWidth)
		{
			Delta = delta;
			LeftColumnWidth = leftColumnWidth;
			RightColumnWidth = rightColumnWidth;
		}

		public int Delta { get; }
		public int LeftColumnWidth { get; }
		public int RightColumnWidth { get; }
	}
}