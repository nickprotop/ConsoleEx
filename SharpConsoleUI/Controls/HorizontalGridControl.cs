// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Core;
using Spectre.Console;
using System.ComponentModel.Design;
using System.Data.Common;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Controls
{
	public class HorizontalGridControl : IWindowControl, IInteractiveControl, IFocusableControl, ILogicalCursorProvider, IMouseAwareControl
	{
		private Alignment _alignment = Alignment.Left;
		private readonly ThreadSafeCache<List<string>> _contentCache;
		private List<ColumnContainer> _columns = new List<ColumnContainer>();
		private IContainer? _container;
		private IInteractiveControl? _focusedContent;
		private bool _hasFocus;
		private Dictionary<IInteractiveControl, ColumnContainer> _interactiveContents = new Dictionary<IInteractiveControl, ColumnContainer>();
		private bool _invalidated = true;
		private bool _isEnabled = true;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private Dictionary<IInteractiveControl, int> _splitterControls = new Dictionary<IInteractiveControl, int>();
		private List<SplitterControl> _splitters = new List<SplitterControl>();
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		public HorizontalGridControl()
		{
			_contentCache = this.CreateThreadSafeCache<List<string>>();
		}

		public int? ActualWidth
		{
			get
			{
				var cachedContent = _contentCache.Content;
				if (cachedContent == null) return 0;
				int maxLength = 0;
				foreach (var line in cachedContent)
				{
					int length = AnsiConsoleHelper.StripAnsiStringLength(line);
					if (length > maxLength) maxLength = length;
				}
				return maxLength;
			}
		}

		public Alignment Alignment
		{ get => _alignment; set { _alignment = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); } }

		public Color? BackgroundColor { get; set; }
		public List<ColumnContainer> Columns => _columns;

		public IContainer? Container
		{
			get { return _container; }
			set
			{
				_container = value;
				_invalidated = true;
				_contentCache.Invalidate(InvalidationReason.PropertyChanged);
				foreach (var column in _columns)
				{
					column.GetConsoleWindowSystem = value?.GetConsoleWindowSystem;
					column.Invalidate(true);
				}

				// Update container for all splitters as well
				foreach (var splitter in _splitters)
				{
					splitter.Container = value;
				}
			}
		}

		public Color? ForegroundColor { get; set; }

		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				var hadFocus = _hasFocus;
				_hasFocus = value;
				FocusChanged();
				this.SafeInvalidate(InvalidationReason.FocusChanged);
				
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

		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_contentCache.Invalidate(InvalidationReason.StateChanged);
				_isEnabled = value;
				Container?.Invalidate(false);
			}
		}

		public Margin Margin
		{ get => _margin; set { _margin = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); } }

		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set
			{
				_stickyPosition = value;
				this.SafeInvalidate(InvalidationReason.PropertyChanged);
			}
		}

		public object? Tag { get; set; }

		public bool Visible
		{ get => _visible; set { _visible = value; _contentCache.Invalidate(InvalidationReason.PropertyChanged); } }

	public int? Width
	{ 
		get => _width; 
		set 
		{ 
			var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
			if (_width != validatedValue)
			{
				_width = validatedValue; 
				_contentCache.Invalidate(InvalidationReason.SizeChanged); 
			}
		} 
	}		public void AddColumn(ColumnContainer column)
		{
			column.GetConsoleWindowSystem = Container?.GetConsoleWindowSystem;
			_columns.Add(column);
			Invalidate();
		}

		public SplitterControl? AddColumnWithSplitter(ColumnContainer column)
		{
			// Only add a splitter if there's at least one column already
			if (_columns.Count > 0)
			{
				var splitter = new SplitterControl();
				column.GetConsoleWindowSystem = Container?.GetConsoleWindowSystem;
				_columns.Add(column);

				// Set up the splitter
				splitter.Container = Container;
				splitter.SetColumns(_columns[_columns.Count - 2], column);
				_splitters.Add(splitter);
				_splitterControls[splitter] = _columns.Count - 2;

				// Subscribe to splitter's move event
				splitter.SplitterMoved += OnSplitterMoved;

				Invalidate();
				return splitter;
			}
			else
			{
				// Just add the column without a splitter
				AddColumn(column);
				return null;
			}
		}

		public bool AddSplitter(int leftColumnIndex, SplitterControl splitterControl)
		{
			// Verify the column indices are valid
			if (leftColumnIndex < 0 || leftColumnIndex >= _columns.Count - 1)
				return false;

			// Set the columns that this splitter will control
			splitterControl.Container = Container;
			splitterControl.SetColumns(_columns[leftColumnIndex], _columns[leftColumnIndex + 1]);

			// Add the splitter and register it for key handling
			_splitters.Add(splitterControl);
			_splitterControls[splitterControl] = leftColumnIndex;

			// Subscribe to splitter's move event
			splitterControl.SplitterMoved += OnSplitterMoved;

			Invalidate();
			return true;
		}

		// Update the Dispose method to clean up event handlers
		public void Dispose()
		{
			// Clean up event handlers from splitters
			foreach (var splitter in _splitters)
			{
				splitter.SplitterMoved -= OnSplitterMoved;
			}
			Container = null;
		}

		// ILogicalCursorProvider implementation
		public Point? GetLogicalCursorPosition()
		{
			return null; // Grids don't have a visible cursor
		}

		public System.Drawing.Size GetLogicalContentSize()
		{
			// Use reasonable maximum dimensions instead of int.MaxValue to avoid memory issues
			var content = RenderContent(10000, 10000);
			return new System.Drawing.Size(
				content.FirstOrDefault()?.Length ?? 0,
				content.Count
			);
		}

		public void SetLogicalCursorPosition(Point position)
		{
			// Grids don't have cursor positioning
		}

		public void Invalidate()
		{
			_invalidated = true;
			_contentCache.Invalidate(InvalidationReason.ContentChanged);

			foreach (var column in _columns)
			{
				column.InvalidateOnlyColumnContents(this);
			}

			foreach (var splitter in _splitters)
			{
				splitter.Invalidate();
			}

			// Don't directly call Container.Invalidate - let the InvalidationManager handle it
		}

		public bool ProcessKey(ConsoleKeyInfo key)
		{
			// Check if key is tab
			if (key.Key == ConsoleKey.Tab)
			{
				// Build a properly ordered list of interactive controls
				var orderedInteractiveControls = new List<IInteractiveControl>();

				// Start by collecting all the interactive controls from columns and their associated splitters
				var columnControls = new Dictionary<int, List<IInteractiveControl>>();

				// First, gather all interactive controls by column
				for (int i = 0; i < _columns.Count; i++)
				{
					var column = _columns[i];
					var interactiveContents = column.GetInteractiveContents();

					if (!columnControls.ContainsKey(i))
					{
						columnControls[i] = new List<IInteractiveControl>();
					}

					columnControls[i].AddRange(interactiveContents);

					// Find if this column has a splitter to the right
					var splitter = _splitters.FirstOrDefault(s => _splitterControls[s] == i);
					if (splitter != null)
					{
						// Add the splitter right after this column's controls
						columnControls[i].Add(splitter);
					}
				}

				// Now flatten the dictionary into a single ordered list
				for (int i = 0; i < _columns.Count; i++)
				{
					if (columnControls.ContainsKey(i))
					{
						orderedInteractiveControls.AddRange(columnControls[i]);
					}
				}

				// If we have no interactive controls, exit
				if (orderedInteractiveControls.Count == 0)
				{
					return false;
				}

				// Handle tabbing through the ordered list
				if (_focusedContent == null)
				{
					_focusedContent = orderedInteractiveControls.First();
				}
				else
				{
					// Unfocus current control
					_focusedContent.HasFocus = false;

					// If it's from columns dictionary, invalidate its container
					if (_interactiveContents.ContainsKey(_focusedContent))
					{
						_interactiveContents[_focusedContent].Invalidate(true);
					}

					int index = orderedInteractiveControls.IndexOf(_focusedContent);

					// Determine the next control based on tab direction
					if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
					{
						if (index == 0)
						{
							return false; // Exit control backward
						}
						index = index == 0 ? orderedInteractiveControls.Count - 1 : index - 1;
					}
					else
					{
						if (index == orderedInteractiveControls.Count - 1)
						{
							return false; // Exit control forward
						}
						index = index == orderedInteractiveControls.Count - 1 ? 0 : index + 1;
					}

					_focusedContent = orderedInteractiveControls[index];
				}

				// Set focus on the new control
				_focusedContent.HasFocus = true;

				// If it's from columns dictionary, invalidate its container
				if (_interactiveContents.ContainsKey(_focusedContent))
				{
					_interactiveContents[_focusedContent].Invalidate(true);
				}

				Container?.Invalidate(true);
				return true;
			}

			// Process key in the focused control
			return _focusedContent?.ProcessKey(key) ?? false;
		}

		public void RemoveColumn(ColumnContainer column)
		{
			int index = _columns.IndexOf(column);
			if (index >= 0)
			{
				// Remove any splitters connected to this column
				var splittersToRemove = new List<SplitterControl>();

				foreach (var entry in _splitterControls)
				{
					// If splitter is connected to this column (either left or right)
					if (entry.Value == index || entry.Value == index - 1)
					{
						splittersToRemove.Add((SplitterControl)entry.Key);
					}
				}

				// Remove the identified splitters
				foreach (var splitter in splittersToRemove)
				{
					_splitters.Remove(splitter);
					_splitterControls.Remove(splitter);
					splitter.SplitterMoved -= OnSplitterMoved;
				}

				// Now remove the column
				_columns.Remove(column);

				// Update remaining splitter indices
				var updatedSplitters = new Dictionary<IInteractiveControl, int>();
				foreach (var entry in _splitterControls)
				{
					int leftColIndex = entry.Value;
					if (leftColIndex > index)
					{
						// Decrement index for splitters that were after the removed column
						updatedSplitters[entry.Key] = leftColIndex - 1;
					}
					else
					{
						updatedSplitters[entry.Key] = leftColIndex;
					}
				}

				_splitterControls = updatedSplitters;

				Invalidate();
			}
		}

		public List<string> RenderContent(int? availableWidth, int? availableHeight)
		{
			// Use thread-safe cache with lazy rendering
			return _contentCache.GetOrRender(() => RenderContentInternal(availableWidth, availableHeight));
		}

		private List<string> RenderContentInternal(int? availableWidth, int? availableHeight)
		{
			BackgroundColor = BackgroundColor ?? Container?.GetConsoleWindowSystem?.Theme.WindowBackgroundColor ?? Color.Black;
			ForegroundColor = ForegroundColor ?? Container?.GetConsoleWindowSystem?.Theme.WindowForegroundColor ?? Color.White;

			var renderedContent = new List<string>();
			int? maxHeight = 0;

			// Create combined list of columns and splitters in their display order
			var displayControls = new List<(bool IsSplitter, object Control, int Width)>();

			// Calculate total specified width and count columns with null width
			int totalSpecifiedWidth = 0;
			int nullWidthCount = 0;

			// First, add all columns and calculate width requirements
			for (int i = 0; i < _columns.Count; i++)
			{
				var column = _columns[i];
				displayControls.Add((false, column, column.Width ?? 0));

				if (column.Width != null)
				{
					totalSpecifiedWidth += column.Width ?? 0;
				}
				else
				{
					nullWidthCount += 1;
				}

				// If there's a splitter after this column, add it too
				var splitter = _splitters.FirstOrDefault(s => _splitterControls[s] == i);
				if (splitter != null)
				{
					displayControls.Add((true, splitter, splitter.Width ?? 1));
					totalSpecifiedWidth += splitter.Width ?? 1;
				}
			}

			// Calculate remaining width to be distributed among auto-width columns
			int remainingWidth = (availableWidth ?? 0) - totalSpecifiedWidth;
			int distributedWidth = nullWidthCount > 0 ? remainingWidth / nullWidthCount : 0;

			// First render all columns to determine the maximum height
			var columnContents = new Dictionary<int, List<string>>();
			int columnIndex = 0;

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, control, _) = displayControls[i];

				if (!isSplitter)
				{
					var column = (ColumnContainer)control;
					int columnWidth = column.Width ?? distributedWidth;
					var content = column.RenderContent(columnWidth, availableHeight);
					columnContents[i] = content;

					if (content.Count > maxHeight)
					{
						maxHeight = content.Count;
					}

					columnIndex++;
				}
			}

			// Now render splitters with the proper height
			var renderedControls = new List<List<string>>();

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, control, controlWidth) = displayControls[i];

				if (isSplitter)
				{
					// Render splitter with the maxHeight of columns instead of availableHeight
					var splitter = (SplitterControl)control;
					renderedControls.Add(splitter.RenderContent(splitter.Width ?? 1, maxHeight ?? 1));
				}
				else
				{
					// For columns, use the already rendered content
					renderedControls.Add(columnContents[i]);
				}
			}

			// Combine the rendered controls horizontally
			for (int i = 0; i < maxHeight; i++)
			{
				string line = string.Empty;

				for (int controlIndex = 0; controlIndex < renderedControls.Count; controlIndex++)
				{
					var controlContent = renderedControls[controlIndex];
					var controlInfo = displayControls[controlIndex];

					int controlActualWidth;

					if (controlInfo.IsSplitter)
					{
						controlActualWidth = ((SplitterControl)controlInfo.Control).Width ?? 1;
					}
					else
					{
						controlActualWidth = ((ColumnContainer)controlInfo.Control).GetActualWidth() ?? 0;
					}

					// Make sure we don't access beyond the bounds of controlContent
					string contentLine = i < controlContent.Count
						? controlContent[i]
						: AnsiConsoleHelper.AnsiEmptySpace(controlActualWidth, BackgroundColor ?? Color.Black);

					// Add the control content to the line
					line += contentLine;
				}

				// Apply alignment to the combined line
				if (availableWidth.HasValue && _alignment != Alignment.Left)
				{
					int lineLength = AnsiConsoleHelper.StripAnsiStringLength(line);
					int padding = availableWidth.Value - lineLength;

					if (padding > 0)
					{
						switch (_alignment)
						{
							case Alignment.Center:
								int leftPadding = padding / 2;
								line = AnsiConsoleHelper.AnsiEmptySpace(leftPadding, BackgroundColor ?? Color.Black) + line;
								break;

							case Alignment.Right:
								line = AnsiConsoleHelper.AnsiEmptySpace(padding, BackgroundColor ?? Color.Black) + line;
								break;

							case Alignment.Stretch:
								// For stretch, we don't add padding here as the content already fills the available width
								break;
						}
					}
				}

				renderedContent.Add(line);
			}

			_invalidated = false;
			return renderedContent;
		}

		// IFocusableControl implementation
		public bool CanReceiveFocus => IsEnabled;
		
		public event EventHandler? GotFocus;
		public event EventHandler? LostFocus;
		
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			HasFocus = focus;
		}

		// IMouseAwareControl implementation
		public bool WantsMouseEvents => IsEnabled;
		public bool CanFocusWithMouse => IsEnabled;

		public event EventHandler<MouseEventArgs>? MouseClick;
		public event EventHandler<MouseEventArgs>? MouseEnter;
		public event EventHandler<MouseEventArgs>? MouseLeave;
		public event EventHandler<MouseEventArgs>? MouseMove;

		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Find the column and control that was clicked
			var clickedControl = GetControlAtPosition(args.Position);
			if (clickedControl != null)
			{
				// Handle focus management for mouse clicks
				if (args.HasAnyFlag(MouseFlags.Button1Pressed, MouseFlags.Button1Clicked))
				{
					HandleControlFocusFromMouse(clickedControl);
				}

				// Propagate mouse event to the clicked control if it supports mouse events
				if (clickedControl is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
				{
					// Calculate control-relative coordinates
					var controlPosition = GetControlRelativePosition(clickedControl, args.Position);
					var controlArgs = args.WithPosition(controlPosition);
					
					return mouseAware.ProcessMouseEvent(controlArgs);
				}
				
				// Event was handled by focus change even if control doesn't support mouse
				return true;
			}

			// No control was clicked, but we might want to handle grid-level events
			if (args.HasFlag(MouseFlags.Button1Clicked))
			{
				MouseClick?.Invoke(this, args);
				return true;
			}

			return false;
		}

		private void FocusChanged(bool backward = false)
		{
			if (_hasFocus)
			{
				_interactiveContents.Clear();
				foreach (var column in _columns)
				{
					foreach (var interactiveContent in column.GetInteractiveContents())
					{
						_interactiveContents.Add(interactiveContent, column);
					}
				}

				if (_interactiveContents.Count == 0 && _splitterControls.Count == 0) return;

				if (_focusedContent == null)
				{
					if (backward)
					{
						if (_interactiveContents.Count > 0)
						{
							_interactiveContents.Keys.Last().HasFocus = true;
							_focusedContent = _interactiveContents.Keys.Last();
						}
						else if (_splitterControls.Count > 0)
						{
							var lastSplitter = _splitterControls.Keys.Last();
							lastSplitter.HasFocus = true;
							_focusedContent = lastSplitter;
						}
					}
					else
					{
						if (_interactiveContents.Count > 0)
						{
							_interactiveContents.Keys.First().HasFocus = true;
							_focusedContent = _interactiveContents.Keys.First();
						}
						else if (_splitterControls.Count > 0)
						{
							var firstSplitter = _splitterControls.Keys.First();
							firstSplitter.HasFocus = true;
							_focusedContent = firstSplitter;
						}
					}
				}

				if (_focusedContent != null && _interactiveContents.ContainsKey(_focusedContent))
				{
					_interactiveContents[_focusedContent].Invalidate(true);
				}
			}
			else
			{
				// Remove focus from all interactive controls
				if (_interactiveContents.Count > 0 && _focusedContent != null && _interactiveContents.ContainsKey(_focusedContent))
				{
					_interactiveContents[_focusedContent]?.Invalidate(true);
				}

				foreach (var control in _interactiveContents.Keys)
				{
					control.HasFocus = false;
				}

				foreach (var splitterControl in _splitterControls.Keys)
				{
					splitterControl.HasFocus = false;
				}

				_focusedContent = null;
			}
		}

		/// <summary>
		/// Finds the control at the specified position within the grid
		/// </summary>
		/// <param name="position">Position relative to the grid</param>
		/// <returns>The control at the position, or null if no control found</returns>
		private IInteractiveControl? GetControlAtPosition(Point position)
		{
			// We need to find which column and which control within that column contains this position
			// Use thread-safe cache access to prevent race conditions
			var cachedContent = _contentCache.Content;
			if (cachedContent == null)
			{
				// Force a render to ensure we have current layout information
				// Use reasonable maximum dimensions instead of int.MaxValue to avoid memory issues
				cachedContent = RenderContent(10000, 10000);
				
				// Verify content was rendered
				if (cachedContent == null)
				{
					return null;
				}
			}

			// Calculate column positions based on the rendered layout
			var displayControls = BuildDisplayControlsList();
			int currentX = 0;

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, control, controlWidth) = displayControls[i];
				
				if (isSplitter)
				{
					// Check if click is on splitter
					if (position.X >= currentX && position.X < currentX + controlWidth)
					{
						return control as IInteractiveControl;
					}
					currentX += controlWidth;
				}
				else
				{
					// Check if click is within this column
					var column = (ColumnContainer)control;
					int actualColumnWidth = column.GetActualWidth() ?? controlWidth;
					
					if (position.X >= currentX && position.X < currentX + actualColumnWidth)
					{
						// Find the control within this column at the relative position
						var relativePosition = new Point(position.X - currentX, position.Y);
						return column.GetControlAtPosition(relativePosition);
					}
					currentX += actualColumnWidth;
				}
			}

			return null;
		}

		/// <summary>
		/// Calculates the position relative to a specific control
		/// </summary>
		/// <param name="control">The target control</param>
		/// <param name="gridPosition">Position relative to the grid</param>
		/// <returns>Position relative to the control</returns>
		private Point GetControlRelativePosition(IInteractiveControl control, Point gridPosition)
		{
			// Find the column that contains this control
			foreach (var column in _columns)
			{
				if (column.ContainsControl(control))
				{
					// Calculate the column's offset within the grid
					var columnOffset = GetColumnOffset(column);
					var columnRelativePosition = new Point(gridPosition.X - columnOffset, gridPosition.Y);
					
					// Get the control's position within the column
					return column.GetControlRelativePosition(control, columnRelativePosition);
				}
			}

			// If control not found in any column, check splitters
			var displayControls = BuildDisplayControlsList();
			int currentX = 0;

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, displayControl, controlWidth) = displayControls[i];
				
				if (isSplitter && displayControl == control)
				{
					return new Point(gridPosition.X - currentX, gridPosition.Y);
				}
				
				currentX += isSplitter ? controlWidth : ((ColumnContainer)displayControl).GetActualWidth() ?? controlWidth;
			}

			return gridPosition; // Fallback
		}

		/// <summary>
		/// Gets the X offset of a column within the grid
		/// </summary>
		/// <param name="targetColumn">The column to find the offset for</param>
		/// <returns>X offset of the column</returns>
		private int GetColumnOffset(ColumnContainer targetColumn)
		{
			var displayControls = BuildDisplayControlsList();
			int currentX = 0;

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, control, controlWidth) = displayControls[i];
				
				if (!isSplitter && control == targetColumn)
				{
					return currentX;
				}
				
				currentX += isSplitter ? controlWidth : ((ColumnContainer)control).GetActualWidth() ?? controlWidth;
			}

			return 0; // Fallback
		}

		/// <summary>
		/// Builds the display controls list similar to what's done in RenderContent
		/// </summary>
		/// <returns>List of display controls with their metadata</returns>
		private List<(bool IsSplitter, object Control, int Width)> BuildDisplayControlsList()
		{
			var displayControls = new List<(bool IsSplitter, object Control, int Width)>();

			// Add all columns and their splitters
			for (int i = 0; i < _columns.Count; i++)
			{
				var column = _columns[i];
				displayControls.Add((false, column, column.Width ?? 0));

				// If there's a splitter after this column, add it
				var splitter = _splitters.FirstOrDefault(s => _splitterControls[s] == i);
				if (splitter != null)
				{
					displayControls.Add((true, splitter, splitter.Width ?? 1));
				}
			}

			return displayControls;
		}

		/// <summary>
		/// Handles focus management when a control is clicked
		/// </summary>
		/// <param name="control">The control that was clicked</param>
		private void HandleControlFocusFromMouse(IInteractiveControl control)
		{
			// Check if control can receive focus
			if (control is IFocusableControl focusable && focusable.CanReceiveFocus)
			{
				// Remove focus from current control
				if (_focusedContent != null && _focusedContent != control && _focusedContent is IFocusableControl currentFocused)
				{
					currentFocused.SetFocus(false, FocusReason.Mouse);
				}
				
				// Set focus to new control
				focusable.SetFocus(true, FocusReason.Mouse);
				
				// Update focused content
				_focusedContent = control;
				
				// Invalidate the container that contains this control
				if (_interactiveContents.ContainsKey(control))
				{
					_interactiveContents[control].Invalidate(true);
				}
				
				Container?.Invalidate(true);
			}
		}

		private void OnSplitterMoved(object? sender, SplitterMovedEventArgs e)
		{
			if (sender is SplitterControl splitter)
			{
				// Find the index of the left column for this splitter
				int leftColumnIndex = -1;
				if (_splitterControls.TryGetValue(splitter, out leftColumnIndex))
				{
					// Make sure the column indices are valid
					if (leftColumnIndex >= 0 && leftColumnIndex < _columns.Count - 1)
					{
						// Update column widths explicitly
						_columns[leftColumnIndex].Width = e.LeftColumnWidth;
						_columns[leftColumnIndex + 1].Width = e.RightColumnWidth;

						// Log width changes for debugging
						System.Diagnostics.Debug.WriteLine($"Splitter moved: Left col width={e.LeftColumnWidth}, Right col width={e.RightColumnWidth}");
					}
				}
			}

			// Invalidate the entire grid when a splitter moves
			foreach (var column in _columns)
			{
				column.Invalidate(true);
			}

			Invalidate();
		}
	}
}