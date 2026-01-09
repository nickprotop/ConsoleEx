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
	/// <summary>
	/// A grid control that arranges child columns horizontally with optional splitters between them.
	/// Supports keyboard and mouse navigation, focus management, and dynamic column resizing.
	/// </summary>
	public class HorizontalGridControl : IWindowControl, IInteractiveControl, IFocusableControl, ILogicalCursorProvider, IMouseAwareControl, ICursorShapeProvider, IDirectionalFocusControl, IDOMPaintable
	{
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private List<ColumnContainer> _columns = new List<ColumnContainer>();
		private IContainer? _container;
		private IInteractiveControl? _focusedContent;
		private bool _hasFocus;
		private Dictionary<IInteractiveControl, ColumnContainer> _interactiveContents = new Dictionary<IInteractiveControl, ColumnContainer>();
		private bool _interactiveContentsDirty = true;
		private bool _invalidated = true;
		private bool _focusFromBackward = false;
		private bool _isEnabled = true;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private Dictionary<IInteractiveControl, int> _splitterControls = new Dictionary<IInteractiveControl, int>();
		private List<SplitterControl> _splitters = new List<SplitterControl>();
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;
		private int _allocatedWidth; // Width allocated during layout (used by splitters)

		/// <summary>
		/// Initializes a new instance of the <see cref="HorizontalGridControl"/> class.
		/// </summary>
		public HorizontalGridControl()
		{
		}

		/// <inheritdoc/>
		public int? ActualWidth
		{
			get
			{
				int totalWidth = _margin.Left + _margin.Right;
				foreach (var column in _columns)
				{
					totalWidth += column.ActualWidth ?? column.Width ?? 0;
				}
				foreach (var splitter in _splitters)
				{
					totalWidth += splitter.Width ?? 1;
				}
				return totalWidth;
			}
	}

	/// <summary>
	/// Gets the width allocated to this grid during the last layout pass.
	/// This is the actual available width for distributing among columns, used by splitters.
	/// </summary>
	public int AllocatedWidth => _allocatedWidth;

		/// <inheritdoc/>
		public HorizontalAlignment HorizontalAlignment
		{ get => _horizontalAlignment; set { _horizontalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public VerticalAlignment VerticalAlignment
		{ get => _verticalAlignment; set { _verticalAlignment = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
		public Color? BackgroundColor { get; set; }

		/// <summary>
		/// Gets the list of columns contained in this grid.
		/// </summary>
		public List<ColumnContainer> Columns => _columns;

		/// <summary>
		/// Gets the list of splitters in this grid.
		/// </summary>
		public IReadOnlyList<SplitterControl> Splitters => _splitters;

		/// <summary>
		/// Gets the index of the column to the left of the specified splitter.
		/// </summary>
		/// <param name="splitter">The splitter to look up.</param>
		/// <returns>The index of the left column, or -1 if not found.</returns>
		public int GetSplitterLeftColumnIndex(SplitterControl splitter)
		{
			return _splitterControls.TryGetValue(splitter, out int index) ? index : -1;
		}

		/// <inheritdoc/>
		public IContainer? Container
		{
			get { return _container; }
			set
			{
				_container = value;
				_invalidated = true;
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

		/// <inheritdoc/>
		public Color? ForegroundColor { get; set; }

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				var hadFocus = _hasFocus;
				_hasFocus = value;
				FocusChanged();
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

		/// <inheritdoc/>
		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_isEnabled = value;
				Container?.Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public Margin Margin
		{ get => _margin; set { _margin = value; Container?.Invalidate(true); } }

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
		{ get => _visible; set { _visible = value; Container?.Invalidate(true); } }

		/// <inheritdoc/>
	public int? Width
	{
		get => _width;
		set
		{
			var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
			if (_width != validatedValue)
			{
				_width = validatedValue;
				Container?.Invalidate(true);
			}
		}
	}

		/// <summary>
		/// Adds a column to the grid.
		/// </summary>
		/// <param name="column">The column container to add.</param>
		public void AddColumn(ColumnContainer column)
		{
			column.GetConsoleWindowSystem = Container?.GetConsoleWindowSystem;
			_columns.Add(column);
			_interactiveContentsDirty = true;
			Invalidate();
		}

		/// <summary>
		/// Adds a column to the grid with an automatically created splitter between it and the previous column.
		/// </summary>
		/// <param name="column">The column container to add.</param>
		/// <returns>The created splitter control, or null if this is the first column.</returns>
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

				_interactiveContentsDirty = true;
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

		/// <summary>
		/// Adds a splitter control between two adjacent columns.
		/// </summary>
		/// <param name="leftColumnIndex">The index of the column to the left of the splitter.</param>
		/// <param name="splitterControl">The splitter control to add.</param>
		/// <returns>True if the splitter was added successfully; false if the column index is invalid.</returns>
		public bool AddSplitter(int leftColumnIndex, SplitterControl splitterControl)
		{
			// Verify the column indices are valid
			if (leftColumnIndex < 0 || leftColumnIndex >= _columns.Count - 1)
				return false;

			// Set the columns that this splitter will control
			splitterControl.Container = Container;
			splitterControl.SetColumns(_columns[leftColumnIndex], _columns[leftColumnIndex + 1], this);

			// Add the splitter and register it for key handling
			_splitters.Add(splitterControl);
			_splitterControls[splitterControl] = leftColumnIndex;

			// Subscribe to splitter's move event
			splitterControl.SplitterMoved += OnSplitterMoved;

			_interactiveContentsDirty = true;
			Invalidate();
			return true;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			// Clean up event handlers from splitters
			foreach (var splitter in _splitters)
			{
				splitter.SplitterMoved -= OnSplitterMoved;
			}
			Container = null;
		}

		/// <inheritdoc/>
		public Point? GetLogicalCursorPosition()
		{
			if (_focusedContent is ILogicalCursorProvider cursorProvider)
			{
				var childPosition = cursorProvider.GetLogicalCursorPosition();
				if (childPosition.HasValue)
				{
					// Check if focused content is in a column
					if (_interactiveContents.TryGetValue(_focusedContent, out var column))
					{
						var columnOffset = GetColumnOffset(column);
						return new Point(childPosition.Value.X + columnOffset, childPosition.Value.Y);
					}
					
					// Check if focused content is a splitter
					if (_focusedContent is SplitterControl splitter && _splitterControls.ContainsKey(splitter))
					{
						var splitterOffset = GetSplitterOffset(splitter);
						return new Point(childPosition.Value.X + splitterOffset, childPosition.Value.Y);
					}
				}
				return childPosition;
			}
			return null;
		}

		/// <inheritdoc/>
		public CursorShape? PreferredCursorShape =>
			(_focusedContent as ICursorShapeProvider)?.PreferredCursorShape;

		/// <inheritdoc/>
		public System.Drawing.Size GetLogicalContentSize()
		{
			int totalWidth = _margin.Left + _margin.Right;
			int maxHeight = 0;

			foreach (var column in _columns)
			{
				var size = column.GetLogicalContentSize();
				totalWidth += size.Width;
				maxHeight = Math.Max(maxHeight, size.Height);
			}

			foreach (var splitter in _splitters)
			{
				totalWidth += splitter.Width ?? 1;
			}

			return new System.Drawing.Size(totalWidth, maxHeight + _margin.Top + _margin.Bottom);
		}

		/// <inheritdoc/>
		public void SetLogicalCursorPosition(Point position)
		{
			// Grids don't have cursor positioning
		}

		/// <inheritdoc/>
		public void Invalidate()
		{
			_invalidated = true;

			foreach (var column in _columns)
			{
				column.InvalidateOnlyColumnContents(this);
			}

			foreach (var splitter in _splitters)
			{
				splitter.Invalidate();
			}

			Container?.Invalidate(true);
		}

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{

			// Let focused content try to handle the key first (including Tab for nested containers)
			if (_focusedContent != null && _focusedContent.ProcessKey(key))
			{
				return true; // Child handled it (e.g., inner grid's Tab navigation)
			}

			// Child didn't handle it - now handle Tab at this level
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
					// Unfocus current control using SetFocus for consistent focus handling
					if (_focusedContent is IFocusableControl currentFocusable)
					{
						currentFocusable.SetFocus(false, FocusReason.Keyboard);
					}
					else
					{
						_focusedContent.HasFocus = false;
					}

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
						index--;
					}
					else
					{
						if (index == orderedInteractiveControls.Count - 1)
						{
							return false; // Exit control forward
						}
						index++;
					}

					_focusedContent = orderedInteractiveControls[index];
				}

				// Set focus on the new control using SetFocus for consistent focus handling
				if (_focusedContent is IFocusableControl newFocusable)
				{
					newFocusable.SetFocus(true, FocusReason.Keyboard);
				}
				else
				{
					_focusedContent.HasFocus = true;
				}

				// If it's from columns dictionary, invalidate its container
				if (_interactiveContents.ContainsKey(_focusedContent))
				{
					_interactiveContents[_focusedContent].Invalidate(true);
				}

				Container?.Invalidate(true);
				return true;
			}

			return false;
		}

		/// <summary>
		/// Removes a column from the grid along with any associated splitters.
		/// </summary>
		/// <param name="column">The column container to remove.</param>
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

				_interactiveContentsDirty = true;
				Invalidate();
			}
		}

		/// <summary>
		/// Distributes available width among columns using the new layout-aware algorithm.
		/// </summary>
		private (Dictionary<int, int> columnWidths, bool hasOverflow) DistributeColumnWidths(
			int totalAvailableWidth,
			List<(bool IsSplitter, object Control, int Width)> displayControls)
		{
			var columnWidths = new Dictionary<int, int>();
			bool hasOverflow = false;

			// Phase 1: Calculate fixed width and gather flexible columns
			int totalFixedWidth = 0;
			var flexibleColumns = new List<(int Index, ColumnContainer Column, LayoutRequirements Req)>();

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, control, _) = displayControls[i];

				if (isSplitter)
				{
					var splitter = (SplitterControl)control;
					int width = splitter.Width ?? 1;
					columnWidths[i] = width;
					totalFixedWidth += width;
				}
				else
				{
					var column = (ColumnContainer)control;
					var requirements = GetColumnRequirements(column);

					if (column.Width != null)
					{
						// Column has fixed width
						columnWidths[i] = column.Width.Value;
						totalFixedWidth += column.Width.Value;
					}
					else
					{
						// Column is flexible - gather for phase 2
						flexibleColumns.Add((i, column, requirements));
					}
				}
			}

			// Phase 2: Distribute remaining width among flexible columns
			int remainingWidth = totalAvailableWidth - totalFixedWidth;

			if (flexibleColumns.Count > 0)
			{
				if (remainingWidth <= 0)
				{
					// No space - allocate absolute minimums (EffectiveMinWidth defaults to 1)
					hasOverflow = true;
					foreach (var (index, _, req) in flexibleColumns)
						columnWidths[index] = req.EffectiveMinWidth;
				}
				else
				{
					int totalMinRequired = flexibleColumns.Sum(f => f.Req.EffectiveMinWidth);

					if (remainingWidth < totalMinRequired)
					{
						// Insufficient space - scale proportionally below minimums
						hasOverflow = true;
						double scale = (double)remainingWidth / Math.Max(1, totalMinRequired);
						int allocated = 0;

						for (int i = 0; i < flexibleColumns.Count - 1; i++)
						{
							var (index, _, req) = flexibleColumns[i];
							int width = Math.Max(1, (int)(req.EffectiveMinWidth * scale));
							columnWidths[index] = width;
							allocated += width;
						}
						// Last column gets remainder to avoid rounding issues
						columnWidths[flexibleColumns[^1].Index] = Math.Max(1, remainingWidth - allocated);
					}
					else
					{
						// Sufficient space - distribute by flex factors
						double totalFlex = flexibleColumns.Sum(f => f.Req.FlexFactor);
						if (totalFlex <= 0) totalFlex = flexibleColumns.Count;

						// First pass: calculate ideal widths using floor division
						var idealWidths = new int[flexibleColumns.Count];
						int totalIdeal = 0;

						for (int i = 0; i < flexibleColumns.Count; i++)
						{
							var (_, _, req) = flexibleColumns[i];
							double proportion = req.FlexFactor / totalFlex;
							int width = Math.Max(req.EffectiveMinWidth, (int)(remainingWidth * proportion));
							if (req.MaxWidth.HasValue) width = Math.Min(width, req.MaxWidth.Value);
							idealWidths[i] = width;
							totalIdeal += width;
						}

						// Second pass: distribute any remaining pixels due to rounding
						int remainder = remainingWidth - totalIdeal;
						int distributed = 0;

						for (int i = 0; i < flexibleColumns.Count && distributed < remainder; i++)
						{
							var (index, _, req) = flexibleColumns[i];
							// Only add extra if we haven't hit max width
							if (!req.MaxWidth.HasValue || idealWidths[i] < req.MaxWidth.Value)
							{
								idealWidths[i]++;
								distributed++;
							}
						}

						// Apply the calculated widths
						for (int i = 0; i < flexibleColumns.Count; i++)
						{
							columnWidths[flexibleColumns[i].Index] = idealWidths[i];
						}
					}
				}
			}

			return (columnWidths, hasOverflow);
		}

		/// <summary>
		/// Gets the layout requirements for a column, either from ILayoutAware or from properties.
		/// </summary>
		private LayoutRequirements GetColumnRequirements(ColumnContainer column)
		{
			if (column is ILayoutAware layoutAware)
				return layoutAware.GetLayoutRequirements();

			// If column has explicit Width, treat it as fixed
			// Otherwise, it's flexible with no constraints
			return column.Width.HasValue
				? LayoutRequirements.Fixed(column.Width.Value)
				: LayoutRequirements.Default;
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
			// Note: _focusFromBackward should be set before calling this method
			// if backward focus selection is needed
			HasFocus = focus;
		}

		/// <summary>
		/// Sets focus with direction information for proper child control selection.
		/// </summary>
		/// <param name="focus">Whether to set or remove focus.</param>
		/// <param name="backward">If true, focus last child; if false, focus first child.</param>
		public void SetFocusWithDirection(bool focus, bool backward)
		{
			_focusFromBackward = backward;
			HasFocus = focus;
		}

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <inheritdoc/>
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

		private void FocusChanged()
		{
			if (_hasFocus)
			{
				// Only rebuild interactive contents dictionary when columns have changed
				if (_interactiveContentsDirty)
				{
					_interactiveContents.Clear();
					foreach (var column in _columns)
					{
						foreach (var interactiveContent in column.GetInteractiveContents())
						{
							_interactiveContents.Add(interactiveContent, column);
						}
					}
					_interactiveContentsDirty = false;
				}

				if (_interactiveContents.Count == 0 && _splitterControls.Count == 0) return;

				if (_focusedContent == null)
				{
					// Find first or last focusable control based on focus direction
					_focusedContent = _focusFromBackward 
						? FindLastFocusableControl() 
						: FindFirstFocusableControl();
					_focusFromBackward = false; // Reset after use
				}

				// Set focus on the control if it can receive focus
				if (_focusedContent != null)
				{
					SetControlFocus(_focusedContent, true);
					
					if (_interactiveContents.ContainsKey(_focusedContent))
					{
						_interactiveContents[_focusedContent].Invalidate(true);
					}
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
			// Calculate column positions based on the rendered layout
			var displayControls = BuildDisplayControlsList();
			if (displayControls.Count == 0)
			{
				return null;
			}
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
		/// Gets the X offset of a splitter within the grid
		/// </summary>
		/// <param name="targetSplitter">The splitter to find the offset for</param>
		/// <returns>X offset of the splitter</returns>
		private int GetSplitterOffset(SplitterControl targetSplitter)
		{
			var displayControls = BuildDisplayControlsList();
			int currentX = 0;

			for (int i = 0; i < displayControls.Count; i++)
			{
				var (isSplitter, control, controlWidth) = displayControls[i];
				
				if (isSplitter && control == targetSplitter)
				{
					return currentX;
				}
				
				currentX += isSplitter ? controlWidth : ((ColumnContainer)control).GetActualWidth() ?? controlWidth;
			}

			return 0; // Fallback
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
		/// Builds the display controls list for layout calculations.
		/// Uses actual rendered widths for accurate position calculations.
		/// </summary>
		/// <returns>List of display controls with their metadata</returns>
		private List<(bool IsSplitter, object Control, int Width)> BuildDisplayControlsList()
		{
			var displayControls = new List<(bool IsSplitter, object Control, int Width)>();

			// Add all columns and their splitters
			for (int i = 0; i < _columns.Count; i++)
			{
				var column = _columns[i];
				// Use GetActualWidth for accurate position calculations after rendering
				int columnWidth = column.GetActualWidth() ?? column.Width ?? 0;
				displayControls.Add((false, column, columnWidth));

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

		/// <summary>
		/// Finds the first control that can receive focus
		/// </summary>
		private IInteractiveControl? FindFirstFocusableControl()
		{
			// Check interactive contents first
			foreach (var control in _interactiveContents.Keys)
			{
				if (control is IFocusableControl focusable && focusable.CanReceiveFocus)
				{
					return control;
				}
			}

			// Then check splitters
			foreach (var splitter in _splitterControls.Keys)
			{
				if (splitter is IFocusableControl focusable && focusable.CanReceiveFocus)
				{
					return splitter;
				}
			}

			return null;
		}

		/// <summary>
		/// Finds the last control that can receive focus (for backward tab navigation)
		/// </summary>
		private IInteractiveControl? FindLastFocusableControl()
		{
			IInteractiveControl? lastFocusable = null;

			// Check interactive contents
			foreach (var control in _interactiveContents.Keys)
			{
				if (control is IFocusableControl focusable && focusable.CanReceiveFocus)
				{
					lastFocusable = control;
				}
			}

			// Then check splitters (splitters come after column controls in tab order)
			foreach (var splitter in _splitterControls.Keys)
			{
				if (splitter is IFocusableControl focusable && focusable.CanReceiveFocus)
				{
					lastFocusable = splitter;
				}
			}

			return lastFocusable;
		}

		/// <summary>
		/// Sets focus on a control, checking CanReceiveFocus and using SetFocus when available
		/// </summary>
		private void SetControlFocus(IInteractiveControl control, bool focus)
		{
			if (control is IFocusableControl focusable)
			{
				if (focus && !focusable.CanReceiveFocus)
				{
					return; // Don't set focus if control can't receive it
				}
				focusable.SetFocus(focus, FocusReason.Programmatic);
			}
			else
			{
				control.HasFocus = focus;
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
						// Note: Column widths are already set by SplitterControl.MoveSplitter()
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

		#region IDOMPaintable Implementation

		/// <inheritdoc/>
        public LayoutSize MeasureDOM(LayoutConstraints constraints)
        {

            // Create combined list of columns and splitters in their display order
            var displayControls = new List<(bool IsSplitter, object Control, int Width)>();

            for (int i = 0; i < _columns.Count; i++)
            {
                var column = _columns[i];
                displayControls.Add((false, column, column.Width ?? 0));

                var splitter = _splitters.FirstOrDefault(s => _splitterControls[s] == i);
                if (splitter != null)
                {
                    displayControls.Add((true, splitter, splitter.Width ?? 1));
                }
            }

            int availableWidth = constraints.MaxWidth - _margin.Left - _margin.Right;

            // Distribute widths using the layout algorithm
            var (renderedWidths, _) = DistributeColumnWidths(availableWidth, displayControls);

            // Calculate total width and max height
            int totalWidth = _margin.Left + _margin.Right;
            int maxHeight = 0;

            for (int i = 0; i < displayControls.Count; i++)
            {
                var (isSplitter, control, _) = displayControls[i];
                int controlWidth = renderedWidths.TryGetValue(i, out int w) ? w : 0;
                totalWidth += controlWidth;

                if (!isSplitter)
                {
                    var column = (ColumnContainer)control;

                    if (column is IDOMPaintable paintable)
                    {
                        var childConstraints = new LayoutConstraints(0, controlWidth, 0, constraints.MaxHeight);
                        var childSize = paintable.MeasureDOM(childConstraints);
                        maxHeight = Math.Max(maxHeight, childSize.Height);
                    }
                    else
                    {
                        var size = column.GetLogicalContentSize();
                        maxHeight = Math.Max(maxHeight, size.Height);
                    }
                }
            }

            var finalHeight = Math.Clamp(maxHeight + _margin.Top + _margin.Bottom, constraints.MinHeight, constraints.MaxHeight);

            return new LayoutSize(
                Math.Clamp(totalWidth, constraints.MinWidth, constraints.MaxWidth),
                finalHeight
            );
        }


		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			// NOTE: Container controls should NOT paint their children here.
			// Children (columns, splitters) are painted by the DOM tree's child LayoutNodes.
			// This method only paints the container's own content (background, margins).

			var bgColor = BackgroundColor ?? Container?.BackgroundColor
				?? Container?.GetConsoleWindowSystem?.Theme?.WindowBackgroundColor ?? defaultBg;
			var fgColor = ForegroundColor ?? Container?.ForegroundColor
				?? Container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor ?? defaultFg;

			// Fill the entire bounds with background color
			for (int y = bounds.Y; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
				}
			}

			_invalidated = false;
		}

		#endregion
	}
}