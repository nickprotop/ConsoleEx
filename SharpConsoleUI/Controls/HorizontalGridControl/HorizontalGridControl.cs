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
using System.ComponentModel.Design;
using System.Data.Common;
using System.Drawing;

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
	public partial class HorizontalGridControl : BaseControl, IInteractiveControl, IFocusableControl, ILogicalCursorProvider, IMouseAwareControl, ICursorShapeProvider, IDirectionalFocusControl, IContainerControl, IFocusTrackingContainer
	{
		private List<ColumnContainer> _columns = new List<ColumnContainer>();
		private readonly object _gridLock = new();
		private IInteractiveControl? _focusedContent;

		/// <summary>
		/// Gets the currently focused child control within the grid.
		/// </summary>
		public IInteractiveControl? FocusedContent => _focusedContent;
		private bool _hasFocus;
		private Dictionary<IInteractiveControl, ColumnContainer> _interactiveContents = new Dictionary<IInteractiveControl, ColumnContainer>();
		private bool _interactiveContentsDirty = true;
		private bool _focusFromBackward = false;
		private bool _isEnabled = true;
		private Dictionary<IInteractiveControl, int> _splitterControls = new Dictionary<IInteractiveControl, int>();
		private List<SplitterControl> _splitters = new List<SplitterControl>();
		private Dictionary<ColumnContainer, int?> _savedColumnWidths = new Dictionary<ColumnContainer, int?>();

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

		#region Properties

		/// <inheritdoc/>
		public override HorizontalAlignment HorizontalAlignment
		{ get => base.HorizontalAlignment; set { base.HorizontalAlignment = value; } }

		/// <inheritdoc/>
		public override VerticalAlignment VerticalAlignment
		{ get => base.VerticalAlignment; set { base.VerticalAlignment = value; } }

		/// <inheritdoc/>
		public Color? BackgroundColor { get; set; }

		/// <summary>
		/// Gets the list of columns contained in this grid.
		/// </summary>
		public List<ColumnContainer> Columns
		{
			get { lock (_gridLock) { return new List<ColumnContainer>(_columns); } }
		}

		/// <summary>
		/// Gets the list of splitters in this grid.
		/// </summary>
		public IReadOnlyList<SplitterControl> Splitters
		{
			get { lock (_gridLock) { return new List<SplitterControl>(_splitters); } }
		}

		/// <summary>
		/// Gets the index of the column to the left of the specified splitter.
		/// </summary>
		/// <param name="splitter">The splitter to look up.</param>
		/// <returns>The index of the left column, or -1 if not found.</returns>
		public int GetSplitterLeftColumnIndex(SplitterControl splitter)
		{
			lock (_gridLock)
			{
				return _splitterControls.TryGetValue(splitter, out int index) ? index : -1;
			}
		}

		/// <inheritdoc/>
		public override IContainer? Container
		{
			get { return base.Container; }
			set
			{
				base.Container = value;
				OnPropertyChanged();
				List<ColumnContainer> columns;
				List<SplitterControl> splitters;
				lock (_gridLock)
				{
					columns = new List<ColumnContainer>(_columns);
					splitters = new List<SplitterControl>(_splitters);
				}
				foreach (var column in columns)
				{
					column.GetConsoleWindowSystem = value?.GetConsoleWindowSystem;
					column.Invalidate(true);
				}

				// Update container for all splitters as well
				foreach (var splitter in splitters)
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
				OnPropertyChanged();
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
			set => SetProperty(ref _isEnabled, value);
		}

		/// <inheritdoc/>
		public override bool Visible
		{ get => base.Visible; set { base.Visible = value; } }

	/// <inheritdoc/>
	public override int? Width
	{
		get => base.Width;
		set
		{
			var validatedValue = value.HasValue ? Math.Max(0, value.Value) : value;
			base.Width = validatedValue;
		}
	}

		/// <inheritdoc/>
		public bool WantsMouseEvents => IsEnabled;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => IsEnabled;

		/// <inheritdoc/>
		/// <summary>
		/// HorizontalGridControl is a layout container and should not be directly focusable.
		/// Focus should go to the controls within the columns instead.
		/// </summary>
		public bool CanReceiveFocus => false;

		/// <inheritdoc/>
		public CursorShape? PreferredCursorShape =>
			(_focusedContent as ICursorShapeProvider)?.PreferredCursorShape;

		#endregion

		#region Events

		/// <inheritdoc/>
		public event EventHandler? GotFocus;

		/// <inheritdoc/>
		public event EventHandler? LostFocus;

		#pragma warning disable CS0067  // Event never raised (interface requirement)
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

		#endregion

		#region Column/Splitter Management

		/// <summary>
		/// Adds a column to the grid.
		/// </summary>
		/// <param name="column">The column container to add.</param>
		public void AddColumn(ColumnContainer column)
		{
			column.GetConsoleWindowSystem = Container?.GetConsoleWindowSystem;
			lock (_gridLock)
			{
				_columns.Add(column);
				_interactiveContentsDirty = true;
			}

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
			bool hasColumns;
			lock (_gridLock) { hasColumns = _columns.Count > 0; }

			if (hasColumns)
			{
				var splitter = new SplitterControl();
				column.GetConsoleWindowSystem = Container?.GetConsoleWindowSystem;
				lock (_gridLock)
				{
					_columns.Add(column);

					// Set up the splitter
					splitter.Container = Container;
					splitter.SetColumns(_columns[_columns.Count - 2], column);
					_splitters.Add(splitter);
					_splitterControls[splitter] = _columns.Count - 2;

					_interactiveContentsDirty = true;
				}

				// Subscribe to splitter's move event
				splitter.SplitterMoved += OnSplitterMoved;

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
			lock (_gridLock)
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

				_interactiveContentsDirty = true;
			}

			// Subscribe to splitter's move event
			splitterControl.SplitterMoved += OnSplitterMoved;

			(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();
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
			int columnIndex;
			lock (_gridLock) { columnIndex = _columns.IndexOf(column); }
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
			int columnIndex;
			lock (_gridLock) { columnIndex = _columns.IndexOf(column); }
			if (columnIndex <= 0)
				return false;

			return AddSplitter(columnIndex - 1, splitter ?? new SplitterControl());
		}

		#endregion

		/// <summary>
		/// Removes a column from the grid along with any associated splitters.
		/// </summary>
		/// <param name="column">The column container to remove.</param>
		public void RemoveColumn(ColumnContainer column)
		{
			List<SplitterControl> splittersToRemove;

			lock (_gridLock)
			{
				int index = _columns.IndexOf(column);
				if (index < 0)
					return;

				// Remove any splitters connected to this column
				splittersToRemove = new List<SplitterControl>();

				foreach (var entry in _splitterControls)
				{
					// If splitter is connected to this column (either left or right)
					if (entry.Value == index || entry.Value == index - 1)
					{
						splittersToRemove.Add((SplitterControl)entry.Key);
					}
				}

				// Remove the identified splitters
				foreach (var s in splittersToRemove)
				{
					_splitters.Remove(s);
					_splitterControls.Remove(s);
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
			}

			// Unsubscribe events outside lock
			foreach (var s in splittersToRemove)
			{
				s.SplitterMoved -= OnSplitterMoved;
			}

			// Force DOM rebuild for runtime removal
			(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();

			Invalidate();
		}

		/// <summary>
		/// Removes all columns and splitters from the grid.
		/// </summary>
		public void ClearColumns()
		{
			List<SplitterControl> splittersSnapshot;
			lock (_gridLock)
			{
				splittersSnapshot = new List<SplitterControl>(_splitters);
				_columns.Clear();
				_splitters.Clear();
				_splitterControls.Clear();
				_savedColumnWidths.Clear();
				_interactiveContentsDirty = true;
			}
			_focusedContent = null;

			foreach (var s in splittersSnapshot)
			{
				s.SplitterMoved -= OnSplitterMoved;
			}

			(this as IWindowControl).GetParentWindow()?.ForceRebuildLayout();
			Invalidate();
		}

		#endregion

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			List<SplitterControl> splitters;
			lock (_gridLock) { splitters = new List<SplitterControl>(_splitters); }
			// Clean up event handlers from splitters
			foreach (var splitter in splitters)
			{
				splitter.SplitterMoved -= OnSplitterMoved;
			}
		}

		private void OnSplitterMoved(object? sender, SplitterMovedEventArgs e)
		{
			if (sender is SplitterControl splitter)
			{
				// Find the index of the left column for this splitter
				// Note: Column widths are already set by SplitterControl.MoveSplitter()
				// This block is kept for potential future use
			}

			// Invalidate the entire grid when a splitter moves
			List<ColumnContainer> columns;
			lock (_gridLock) { columns = new List<ColumnContainer>(_columns); }
			foreach (var column in columns)
			{
				column.Invalidate(true);
			}

			Invalidate();
		}
	}
}
