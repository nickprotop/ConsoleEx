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

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A grid control that arranges child columns horizontally with optional splitters between them.
	/// Supports keyboard and mouse navigation, focus management, and dynamic column resizing.
	///
	/// <para><b>SIMPLE USAGE (Factory Methods):</b></para>
	/// <code>
	/// // Button row (common pattern)
	/// var buttons = HorizontalGridControl.ButtonRow(
	///     new ButtonControl { Text = "OK" },
	///     new ButtonControl { Text = "Cancel" }
	/// );
	///
	/// // Any controls
	/// var grid = HorizontalGridControl.FromControls(control1, control2, control3);
	/// </code>
	///
	/// <para><b>FLUENT USAGE (For Complex Layouts):</b></para>
	/// <code>
	/// var grid = HorizontalGridControl.Create()
	///     .Column(col => col.Width(48).Add(control1))
	///     .Column(col => col.Flex(2.0).Add(control2))
	///     .WithSplitterAfter(0)
	///     .WithAlignment(HorizontalAlignment.Stretch)
	///     .Build();
	/// </code>
	///
	/// <para><b>SPLITTER API:</b></para>
	/// <code>
	/// // Add splitters using column references (more intuitive than indices)
	/// grid.AddSplitterAfter(column1);  // Adds splitter between column1 and column2
	/// grid.AddSplitterBefore(column2); // Same result as above
	///
	/// // Or add columns with automatic splitters
	/// grid.AddColumn(column1);
	/// grid.AddColumnWithSplitter(column2); // Creates splitter automatically
	/// </code>
	///
	/// <para><b>TRADITIONAL USAGE (Still Supported):</b></para>
	/// <code>
	/// var grid = new HorizontalGridControl();
	/// var column = new ColumnContainer(grid);
	/// column.AddContent(control);
	/// grid.AddColumn(column);
	/// grid.AddSplitter(0, new SplitterControl()); // Add splitter by index
	/// </code>
	///
	/// <para><b>ARCHITECTURE NOTE:</b></para>
	/// <para>
	/// This control uses <see cref="HorizontalLayout"/> internally for measuring
	/// and arranging columns. The layout algorithm is assigned automatically by
	/// Window.cs during tree building. Users don't interact with HorizontalLayout directly.
	/// </para>
	/// </summary>
	public class HorizontalGridControl : IWindowControl, IInteractiveControl, IFocusableControl, ILogicalCursorProvider, IMouseAwareControl, ICursorShapeProvider, IDirectionalFocusControl, IDOMPaintable, IContainerControl, IFocusTrackingContainer
	{
		private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;
		private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
		private List<ColumnContainer> _columns = new List<ColumnContainer>();
		private IContainer? _container;
		private IInteractiveControl? _focusedContent;
		private bool _hasFocus;
		private Dictionary<IInteractiveControl, ColumnContainer> _interactiveContents = new Dictionary<IInteractiveControl, ColumnContainer>();
		private bool _interactiveContentsDirty = true;
		private bool _focusFromBackward = false;
		private bool _isEnabled = true;
		private Margin _margin = new Margin(0, 0, 0, 0);
		private Dictionary<IInteractiveControl, int> _splitterControls = new Dictionary<IInteractiveControl, int>();
		private List<SplitterControl> _splitters = new List<SplitterControl>();
		private Dictionary<ColumnContainer, int?> _savedColumnWidths = new Dictionary<ColumnContainer, int?>();
		private StickyPosition _stickyPosition = StickyPosition.None;
		private bool _visible = true;
		private int? _width;

		private int _actualX;
		private int _actualY;
		private int _actualWidth;
		private int _actualHeight;

		/// <summary>
		/// Initializes a new instance of the <see cref="HorizontalGridControl"/> class.
		/// </summary>
		public HorizontalGridControl()
		{
		}

		#region Factory Methods

		/// <summary>
		/// Creates a horizontal grid with buttons, commonly used for dialog button rows.
		/// Each button is automatically wrapped in a column.
		/// </summary>
		/// <param name="buttons">The buttons to add to the grid.</param>
		/// <returns>A new HorizontalGridControl containing the buttons, centered horizontally.</returns>
		public static HorizontalGridControl ButtonRow(params ButtonControl[] buttons)
		{
			return ButtonRow(buttons, HorizontalAlignment.Center);
		}

		/// <summary>
		/// Creates a horizontal grid with buttons.
		/// Each button is automatically wrapped in a column.
		/// </summary>
		/// <param name="buttons">The buttons to add to the grid.</param>
		/// <param name="alignment">The horizontal alignment of the grid.</param>
		/// <returns>A new HorizontalGridControl containing the buttons.</returns>
		public static HorizontalGridControl ButtonRow(
			IEnumerable<ButtonControl> buttons,
			HorizontalAlignment alignment = HorizontalAlignment.Center)
		{
			var grid = new HorizontalGridControl { HorizontalAlignment = alignment };

			foreach (var button in buttons)
			{
				var column = new ColumnContainer(grid);
				column.AddContent(button);
				grid.AddColumn(column);
			}

			return grid;
		}

		/// <summary>
		/// Creates a horizontal grid with arbitrary controls.
		/// Each control is automatically wrapped in a column.
		/// </summary>
		/// <param name="controls">The controls to add to the grid.</param>
		/// <param name="alignment">The horizontal alignment of the grid.</param>
		/// <returns>A new HorizontalGridControl containing the controls.</returns>
		public static HorizontalGridControl FromControls(
			IEnumerable<IWindowControl> controls,
			HorizontalAlignment alignment = HorizontalAlignment.Left)
		{
			var grid = new HorizontalGridControl { HorizontalAlignment = alignment };

			foreach (var control in controls)
			{
				var column = new ColumnContainer(grid);
				column.AddContent(control);
				grid.AddColumn(column);
			}

			return grid;
		}

		/// <summary>
		/// Creates a horizontal grid from controls using params syntax.
		/// Each control is automatically wrapped in a column.
		/// </summary>
		/// <param name="controls">The controls to add to the grid.</param>
		/// <returns>A new HorizontalGridControl containing the controls.</returns>
		public static HorizontalGridControl FromControls(params IWindowControl[] controls)
		{
			return FromControls(controls, HorizontalAlignment.Left);
		}

		/// <summary>
		/// Creates a fluent builder for constructing a HorizontalGridControl.
		/// Provides a concise, chainable API for complex grid layouts.
		/// </summary>
		/// <returns>A new HorizontalGridBuilder instance.</returns>
		/// <example>
		/// <code>
		/// var grid = HorizontalGridControl.Create()
		///     .Column(col => col.Width(48).Add(control1))
		///     .Column(col => col.Flex(2.0).Add(control2))
		///     .WithSplitterAfter(0)
		///     .WithAlignment(HorizontalAlignment.Stretch)
		///     .Build();
		/// </code>
		/// </example>
		public static Builders.HorizontalGridBuilder Create()
		{
			return new Builders.HorizontalGridBuilder();
		}

		#endregion

		/// <inheritdoc/>
		public int? ContentWidth
		{
			get
			{
				int totalWidth = _margin.Left + _margin.Right;
				foreach (var column in _columns)
				{
					if (!column.Visible) continue;
					totalWidth += column.ContentWidth ?? column.Width ?? 0;
				}
				foreach (var splitter in _splitters)
				{
					if (!splitter.Visible) continue;
					totalWidth += splitter.Width ?? 1;
				}
				return totalWidth;
			}
	}

	/// <inheritdoc/>
	public int ActualX => _actualX;

	/// <inheritdoc/>
	public int ActualY => _actualY;

	/// <inheritdoc/>
	public int ActualWidth => _actualWidth;

	/// <inheritdoc/>
	public int ActualHeight => _actualHeight;

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
		/// Gets the children of this container for Tab navigation traversal.
		/// Required by IContainerControl interface.
		/// </summary>
		public IReadOnlyList<IWindowControl> GetChildren()
		{
			var children = new List<IWindowControl>();

		for (int i = 0; i < _columns.Count; i++)
		{
			if (!_columns[i].Visible) continue;

			// Add the column
			children.Add(_columns[i]);

			// Add splitter after this column if it exists
			var splitter = _splitters.FirstOrDefault(s => _splitterControls[s] == i);
			if (splitter != null && splitter.Visible)
			{
				children.Add(splitter);
			}
		}

		return children.AsReadOnly();
		}

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
			set => PropertySetterHelper.SetBoolProperty(ref _isEnabled, value, Container);
		}

		/// <inheritdoc/>
		public Margin Margin
		{
			get => _margin;
			set => PropertySetterHelper.SetProperty(ref _margin, value, Container);
		}

		/// <inheritdoc/>
		public StickyPosition StickyPosition
		{
			get => _stickyPosition;
			set => PropertySetterHelper.SetEnumProperty(ref _stickyPosition, value, Container);
		}

		/// <inheritdoc/>
		public string? Name { get; set; }

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

			// Force DOM rebuild for runtime addition
			(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();

			Invalidate();
		}

		/// <summary>
		/// Adds a column to the grid and automatically creates a splitter before it.
		/// Convenience method - equivalent to calling AddSplitter() then AddColumn().
		/// If this is the first column, no splitter is added.
		/// </summary>
		/// <param name="column">The column container to add.</param>
		/// <returns>The created splitter control, or null if this is the first column.</returns>
		/// <example>
		/// <code>
		/// var grid = new HorizontalGridControl();
		/// grid.AddColumn(column1);  // First column - no splitter
		/// grid.AddColumnWithSplitter(column2); // Adds splitter between column1 and column2
		/// grid.AddColumnWithSplitter(column3); // Adds splitter between column2 and column3
		/// </code>
		/// </example>
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

				// Force DOM rebuild for runtime addition
				(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();

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

		#region Splitter API Convenience Methods

		/// <summary>
		/// Adds a splitter after the specified column.
		/// More intuitive than AddSplitter(index) - you specify the column, not an index.
		/// </summary>
		/// <param name="column">The column after which to add the splitter.</param>
		/// <param name="splitter">The splitter control to add. If null, a new SplitterControl is created.</param>
		/// <returns>True if the splitter was added successfully; false if the column is not found or is the last column.</returns>
		/// <example>
		/// <code>
		/// var col1 = new ColumnContainer(grid);
		/// grid.AddColumn(col1);
		/// var col2 = new ColumnContainer(grid);
		/// grid.AddColumn(col2);
		/// grid.AddSplitterAfter(col1); // Adds splitter between col1 and col2
		/// </code>
		/// </example>
		public bool AddSplitterAfter(ColumnContainer column, SplitterControl? splitter = null)
		{
			int columnIndex = _columns.IndexOf(column);
			if (columnIndex < 0)
				return false;

			return AddSplitter(columnIndex, splitter ?? new SplitterControl());
		}

		/// <summary>
		/// Adds a splitter before the specified column.
		/// More intuitive than AddSplitter(index) - you specify the column, not an index.
		/// </summary>
		/// <param name="column">The column before which to add the splitter.</param>
		/// <param name="splitter">The splitter control to add. If null, a new SplitterControl is created.</param>
		/// <returns>True if the splitter was added successfully; false if the column is not found or is the first column.</returns>
		/// <example>
		/// <code>
		/// var col1 = new ColumnContainer(grid);
		/// grid.AddColumn(col1);
		/// var col2 = new ColumnContainer(grid);
		/// grid.AddColumn(col2);
		/// grid.AddSplitterBefore(col2); // Adds splitter between col1 and col2
		/// </code>
		/// </example>
		public bool AddSplitterBefore(ColumnContainer column, SplitterControl? splitter = null)
		{
			int columnIndex = _columns.IndexOf(column);
			if (columnIndex <= 0)
				return false;

			return AddSplitter(columnIndex - 1, splitter ?? new SplitterControl());
		}

		#endregion

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

				if (childPosition.HasValue && _focusedContent is IWindowControl focusedControl)
				{
					// For now, just return child position as-is
					// The proper offset will be accumulated in TranslateLogicalCursorToWindow
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
				if (!column.Visible) continue;
				var size = column.GetLogicalContentSize();
				totalWidth += size.Width;
				maxHeight = Math.Max(maxHeight, size.Height);
			}

			foreach (var splitter in _splitters)
			{
				if (!splitter.Visible) continue;
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
			AdjustColumnWidthsForVisibility();

			foreach (var column in _columns)
			{
				if (!column.Visible) continue;
				column.InvalidateOnlyColumnContents(this);
			}

			foreach (var splitter in _splitters)
			{
				if (!splitter.Visible) continue;
				splitter.Invalidate();
			}

			Container?.Invalidate(true);
		}

		/// <summary>
		/// When a column adjacent to a splitter is hidden, the other column may have an
		/// explicit Width set by a previous splitter drag. Clear it so the column can flex
		/// to fill the freed space. Restore the width when both columns become visible again.
		/// </summary>
		private void AdjustColumnWidthsForVisibility()
		{
			foreach (var entry in _splitterControls)
			{
				var splitter = (SplitterControl)entry.Key;
				int leftIndex = entry.Value;
				int rightIndex = leftIndex + 1;

				if (leftIndex < 0 || rightIndex >= _columns.Count)
					continue;

				var leftCol = _columns[leftIndex];
				var rightCol = _columns[rightIndex];

				if (!leftCol.Visible && rightCol.Visible)
				{
					// Left column hidden — release right column's explicit width
					if (rightCol.Width.HasValue && !_savedColumnWidths.ContainsKey(rightCol))
					{
						_savedColumnWidths[rightCol] = rightCol.Width;
						rightCol.Width = null;
					}
				}
				else if (leftCol.Visible && !rightCol.Visible)
				{
					// Right column hidden — release left column's explicit width
					if (leftCol.Width.HasValue && !_savedColumnWidths.ContainsKey(leftCol))
					{
						_savedColumnWidths[leftCol] = leftCol.Width;
						leftCol.Width = null;
					}
				}
				else if (leftCol.Visible && rightCol.Visible)
				{
					// Both visible — restore any saved widths
					if (_savedColumnWidths.TryGetValue(leftCol, out var savedLeft))
					{
						leftCol.Width = savedLeft;
						_savedColumnWidths.Remove(leftCol);
					}
					if (_savedColumnWidths.TryGetValue(rightCol, out var savedRight))
					{
						rightCol.Width = savedRight;
						_savedColumnWidths.Remove(rightCol);
					}
				}
			}
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
					if (!column.Visible) continue;

					var interactiveContents = column.GetInteractiveContents();

					if (!columnControls.ContainsKey(i))
					{
						columnControls[i] = new List<IInteractiveControl>();
					}

					columnControls[i].AddRange(interactiveContents);

					// Find if this column has a splitter to the right
					var splitter = _splitters.FirstOrDefault(s => _splitterControls[s] == i);
					if (splitter != null && splitter.Visible)
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

				// Filter to only include controls that can actually receive focus
				orderedInteractiveControls = orderedInteractiveControls
					.Where(c => c is not IFocusableControl fc || fc.CanReceiveFocus)
					.ToList();

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

				// Force DOM rebuild for runtime removal
				(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();

				Invalidate();
			}
		}

		/// <summary>
		/// Removes all columns and splitters from the grid.
		/// </summary>
		public void ClearColumns()
		{
			foreach (var splitter in _splitters)
			{
				splitter.SplitterMoved -= OnSplitterMoved;
			}

			_columns.Clear();
			_splitters.Clear();
			_splitterControls.Clear();
			_interactiveContentsDirty = true;
			_focusedContent = null;

			(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();
			Invalidate();
		}

		/// <inheritdoc/>
		/// <summary>
		/// HorizontalGridControl is a layout container and should not be directly focusable.
		/// Focus should go to the controls within the columns instead.
		/// </summary>
		public bool CanReceiveFocus => false;

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			// Note: _focusFromBackward should be set before calling this method
			// if backward focus selection is needed
			bool hadFocus = HasFocus;
			HasFocus = focus;

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
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

		#pragma warning disable CS0067  // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

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
			if (!IsEnabled || !WantsMouseEvents)
				return false;

			// Find the column and control that was clicked
			var clickedControl = GetControlAtPosition(args.Position);
			if (clickedControl != null)
			{
				// Window now handles focus via DOM tree - just forward mouse event to child

				// Propagate mouse event to the clicked control if it supports mouse events
				if (clickedControl is IMouseAwareControl mouseAware && mouseAware.WantsMouseEvents)
				{
					// Calculate control-relative coordinates
					var controlPosition = GetControlRelativePosition(clickedControl, args.Position);
					var controlArgs = args.WithPosition(controlPosition);
					
					return mouseAware.ProcessMouseEvent(controlArgs);
				}

				return false;
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
					int actualColumnWidth = column.GetContentWidth() ?? controlWidth;
					
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
				
				currentX += isSplitter ? controlWidth : ((ColumnContainer)displayControl).GetContentWidth() ?? controlWidth;
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
				
				currentX += isSplitter ? controlWidth : ((ColumnContainer)control).GetContentWidth() ?? controlWidth;
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
				
				currentX += isSplitter ? controlWidth : ((ColumnContainer)control).GetContentWidth() ?? controlWidth;
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

			// Add all columns and their splitters (skip hidden ones)
			for (int i = 0; i < _columns.Count; i++)
			{
				var column = _columns[i];
				if (!column.Visible) continue;

				// Use GetContentWidth for accurate position calculations after rendering
				int columnWidth = column.GetContentWidth() ?? column.Width ?? 0;
				displayControls.Add((false, column, columnWidth));

				// If there's a splitter after this column, add it
				var splitter = _splitters.FirstOrDefault(s => _splitterControls[s] == i);
				if (splitter != null && splitter.Visible)
				{
					displayControls.Add((true, splitter, splitter.Width ?? 1));
				}
			}

			return displayControls;
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

		#region IFocusTrackingContainer Implementation

		/// <inheritdoc/>
		public void NotifyChildFocusChanged(IInteractiveControl child, bool hasFocus)
		{
			if (hasFocus)
			{
				if (_focusedContent != null && _focusedContent != child)
				{
					if (_focusedContent is IFocusableControl oldFc)
						oldFc.HasFocus = false;
					else
						_focusedContent.HasFocus = false;
				}

				_focusedContent = child;

				if (!_hasFocus)
				{
					_hasFocus = true;
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
			}
			else if (_focusedContent == child)
			{
				_focusedContent = null;
			}

			Container?.Invalidate(true);
		}

		#endregion

		private void OnSplitterMoved(object? sender, SplitterMovedEventArgs e)
		{
			if (sender is SplitterControl splitter)
			{
				// Find the index of the left column for this splitter
				// Note: Column widths are already set by SplitterControl.MoveSplitter()
				// This block is kept for potential future use
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
		/// <remarks>
		/// This method measures content-based size (sum of actual child sizes), consistent with
		/// how HorizontalLayout.MeasureChildren works in the DOM system. Space distribution
		/// happens during arrangement, not measurement.
		/// </remarks>
        public LayoutSize MeasureDOM(LayoutConstraints constraints)
        {
            int totalWidth = _margin.Left + _margin.Right;
            int maxHeight = 0;

            // Measure each column and sum actual widths (skip hidden ones)
            for (int i = 0; i < _columns.Count; i++)
            {
                var column = _columns[i];
                if (!column.Visible) continue;

                int columnWidth;
                int columnHeight = 0;

                if (column.Width.HasValue)
                {
                    // Column has explicit width - use it, but still measure for height
                    columnWidth = column.Width.Value;
                    if (column is IDOMPaintable paintable)
                    {
                        var childSize = paintable.MeasureDOM(
                            LayoutConstraints.Loose(columnWidth, constraints.MaxHeight));
                        columnHeight = childSize.Height;
                    }
                    else
                    {
                        columnHeight = column.GetLogicalContentSize().Height;
                    }
                }
                else if (column is IDOMPaintable paintable)
                {
                    // Measure with loose constraints to get natural content size
                    var childSize = paintable.MeasureDOM(
                        LayoutConstraints.Loose(constraints.MaxWidth, constraints.MaxHeight));
                    columnWidth = childSize.Width;
                    columnHeight = childSize.Height;
                }
                else
                {
                    var size = column.GetLogicalContentSize();
                    columnWidth = size.Width;
                    columnHeight = size.Height;
                }

                totalWidth += columnWidth;
                maxHeight = Math.Max(maxHeight, columnHeight);

                // Add splitter width if present after this column
                var splitter = _splitters.FirstOrDefault(s => _splitterControls[s] == i);
                if (splitter != null && splitter.Visible)
                {
                    totalWidth += splitter.Width ?? 1;
                }
            }

            return new LayoutSize(
                Math.Clamp(totalWidth, constraints.MinWidth, constraints.MaxWidth),
                Math.Clamp(maxHeight + _margin.Top + _margin.Bottom, constraints.MinHeight, constraints.MaxHeight)
            );
        }


		/// <inheritdoc/>
		public void PaintDOM(CharacterBuffer buffer, LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			_actualX = bounds.X;
			_actualY = bounds.Y;
			_actualWidth = bounds.Width;
			_actualHeight = bounds.Height;

			// NOTE: Container controls should NOT paint their children here.
			// Children (columns, splitters) are painted by the DOM tree's child LayoutNodes.
			// This method only paints the container's own content (background, margins).

			var bgColor = ColorResolver.ResolveBackground(BackgroundColor, Container, defaultBg);
			var fgColor = ColorResolver.ResolveForeground(ForegroundColor, Container, defaultFg);

			// Fill the entire bounds with background color
			for (int y = bounds.Y; y < bounds.Bottom; y++)
			{
				if (y >= clipRect.Y && y < clipRect.Bottom)
				{
					buffer.FillRect(new LayoutRect(bounds.X, y, bounds.Width, 1), ' ', fgColor, bgColor);
				}
			}

			}

		#endregion
	}
}