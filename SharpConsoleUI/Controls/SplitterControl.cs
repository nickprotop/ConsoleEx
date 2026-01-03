// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Spectre.Console;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A vertical splitter control that allows users to resize adjacent columns in a <see cref="HorizontalGridControl"/>.
	/// Supports keyboard-based resizing with arrow keys and provides visual feedback during focus and dragging.
	/// </summary>
	public class SplitterControl : IWindowControl, IInteractiveControl, IFocusableControl
	{
		private const int DEFAULT_WIDTH = 1;
		private const float MIN_COLUMN_PERCENTAGE = 0.1f; // Minimum 10% width for any column

		private Alignment _alignment = Alignment.Left;
		private Color? _backgroundColorValue;
		private Color _borderColor = Color.White;
		private readonly ThreadSafeCache<List<string>> _contentCache;
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
		private int? _lastRenderHeight;

		// References to the columns on either side of this splitter
		private ColumnContainer? _leftColumn;

		private Margin _margin = new Margin(0, 0, 0, 0);
		private ColumnContainer? _rightColumn;
		private int _startDragPosition;
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width = DEFAULT_WIDTH;

		/// <summary>
		/// Initializes a new instance of the <see cref="SplitterControl"/> class.
		/// </summary>
		public SplitterControl()
		{
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SplitterControl"/> class with specified adjacent columns.
		/// </summary>
		/// <param name="leftColumn">The column to the left of the splitter.</param>
		/// <param name="rightColumn">The column to the right of the splitter.</param>
		public SplitterControl(ColumnContainer leftColumn, ColumnContainer rightColumn)
		{
			_contentCache = this.CreateThreadSafeCache<List<string>>();
			_leftColumn = leftColumn;
			_rightColumn = rightColumn;
		}

		/// <summary>
		/// Occurs when the splitter is moved and column widths are adjusted.
		/// </summary>
		public event EventHandler<SplitterMovedEventArgs>? SplitterMoved;

		/// <inheritdoc/>
		public int? ActualWidth => _width;

		/// <inheritdoc/>
		public Alignment Alignment
		{
			get => _alignment;
			set
			{
				_alignment = value;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				Container?.Invalidate(true);
			}
		}

		/// <summary>
		/// Gets or sets the background color of the splitter in normal state.
		/// </summary>
		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.BackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? Color.Black;
			set
			{
				_backgroundColorValue = value;
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public IContainer? Container
		{
			get => _container;
			set
			{
				_container = value;
				_invalidated = true;
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
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
				_contentCache.Invalidate();
				_invalidated = true;
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

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_isEnabled = value;
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set
			{
				_margin = value;
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public object? Tag { get; set; }

		/// <inheritdoc/>
		public bool Visible
		{
			get => _visible;
			set
			{
				_visible = value;
				_contentCache.Invalidate();
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
	public int? Width
	{
		get => _width;
		set
		{
			var validatedValue = value.HasValue ? Math.Max(1, value.Value) : value;
			if (_width != validatedValue)
			{
				_width = validatedValue;
				_contentCache.Invalidate(InvalidationReason.SizeChanged);
				Container?.Invalidate(true);
			}
		}
	}

		/// <inheritdoc/>
		public void Dispose()
		{
			Container = null;
		}

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			var content = RenderContent(10000, 10000);
			return new System.Drawing.Size(
				content.FirstOrDefault()?.Length ?? 0,
				content.Count
			);
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			_invalidated = true;
			_contentCache.Invalidate();
			Container?.Invalidate(false);
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
					_startDragPosition = 0;
					_contentCache.Invalidate();  // Force redraw with dragging colors
				}
				
				MoveSplitter(delta);
			}

			return handled;
		}

		/// <inheritdoc/>
		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			var layoutService = Container?.GetConsoleWindowSystem?.LayoutStateService;

			// Smart invalidation: check if re-render is needed due to size change
			if (layoutService == null || layoutService.NeedsRerender(this, availableWidth, availableHeight))
			{
				// Dimensions changed - invalidate cache
				_contentCache.Invalidate(InvalidationReason.SizeChanged);
			}
			else
			{
				// Dimensions unchanged - return cached content if available
				var cached = _contentCache.Content;
				if (cached != null) return cached;
			}

			// Update available space tracking
			layoutService?.UpdateAvailableSpace(this, availableWidth, availableHeight, LayoutChangeReason.ContainerResize);

			return _contentCache.GetOrRender(() =>
			{
				var cachedContent = new List<string>();

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

					cachedContent.Add(line);
				}

				_invalidated = false;
				return cachedContent;
			});
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
			_contentCache.Invalidate();
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
			HasFocus = focus;

			// When focus is lost, exit drag mode
			if (!focus && _isDragging)
			{
				_isDragging = false;
				_contentCache.Invalidate();  // Force redraw with normal colors
				Container?.Invalidate(true);
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

			// Get the current effective width of both columns
			// Use actual width for null (auto-sizing) columns, explicit width otherwise
			int leftColumnWidth = _leftColumn.Width ?? _leftColumn.GetActualWidth() ?? 10; // Default to 10 if no content
			int rightColumnWidth = _rightColumn.Width ?? _rightColumn.GetActualWidth() ?? 10; // Default to 10 if no content

			// Calculate new widths
			int newLeftWidth = leftColumnWidth + delta;
			int newRightWidth = rightColumnWidth - delta;

			// Ensure minimum widths (at least 10% of combined width or 5 characters minimum)
			int totalWidth = leftColumnWidth + rightColumnWidth;
			int minWidth = Math.Max(5, (int)(totalWidth * MIN_COLUMN_PERCENTAGE));

			// Constrain to minimum widths and adjust delta accordingly
			if (newLeftWidth < minWidth)
			{
				newLeftWidth = minWidth;
				newRightWidth = totalWidth - minWidth;
			}
			else if (newRightWidth < minWidth)
			{
				newRightWidth = minWidth;
				newLeftWidth = totalWidth - minWidth;
			}

			// Only apply changes if widths are valid and different
			if (newLeftWidth > 0 && newRightWidth > 0 && 
				(newLeftWidth != leftColumnWidth || newRightWidth != rightColumnWidth))
			{
				// Apply the new widths
				_leftColumn.Width = newLeftWidth;
				_rightColumn.Width = newRightWidth;

				// Calculate the actual delta that was applied
				int actualDelta = newLeftWidth - leftColumnWidth;

				// Raise the SplitterMoved event
				SplitterMoved?.Invoke(this, new SplitterMovedEventArgs(actualDelta, newLeftWidth, newRightWidth));

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