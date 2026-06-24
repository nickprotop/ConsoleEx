// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Represents a row in a TableControl with cell data and optional styling.
/// </summary>
public class TableRow
{
	/// <summary>
	/// The owning <see cref="TableControl"/>, set when the row is added. Used to route
	/// display-property and cell changes back to the table for cache busting and invalidation.
	/// </summary>
	internal TableControl? Owner;

	private ObservableCollection<string> _cells = new();

	/// <summary>
	/// Gets or sets the observable collection of cell values for this row.
	/// Mutations (assignment, add, remove, clear, indexer) notify the owning table to relayout.
	/// </summary>
	public ObservableCollection<string> Cells
	{
		get => _cells;
		set
		{
			if (_cells != null) _cells.CollectionChanged -= OnCellsChanged;
			_cells = value ?? new ObservableCollection<string>();
			_cells.CollectionChanged += OnCellsChanged;
			Owner?.OnRowDisplayChanged(this, true, Invalidation.Relayout);
		}
	}

	private void OnCellsChanged(object? s, NotifyCollectionChangedEventArgs e)
		=> Owner?.OnRowDisplayChanged(this, true, Invalidation.Relayout);

	/// <summary>
	/// Gets or sets an arbitrary object associated with this row for user data.
	/// </summary>
	public object? Tag { get; set; }

	private Color? _backgroundColor;

	/// <summary>
	/// Gets or sets the background color for this row, overriding table defaults.
	/// </summary>
	public Color? BackgroundColor
	{
		get => _backgroundColor;
		set { if (_backgroundColor == value) return; _backgroundColor = value; Owner?.OnRowDisplayChanged(this, false, Invalidation.Repaint); }
	}

	private Color? _foregroundColor;

	/// <summary>
	/// Gets or sets the foreground color for this row, overriding table defaults.
	/// </summary>
	public Color? ForegroundColor
	{
		get => _foregroundColor;
		set { if (_foregroundColor == value) return; _foregroundColor = value; Owner?.OnRowDisplayChanged(this, false, Invalidation.Repaint); }
	}

	private bool _isEnabled = true;

	/// <summary>
	/// Gets or sets whether this row is enabled for interaction.
	/// </summary>
	public bool IsEnabled
	{
		get => _isEnabled;
		set { if (_isEnabled == value) return; _isEnabled = value; Owner?.OnRowDisplayChanged(this, false, Invalidation.Repaint); }
	}

	private bool _isChecked;

	/// <summary>
	/// Gets or sets whether this row is checked (for multi-select checkbox mode).
	/// </summary>
	public bool IsChecked
	{
		get => _isChecked;
		set { if (_isChecked == value) return; _isChecked = value; Owner?.OnRowDisplayChanged(this, false, Invalidation.Repaint); }
	}

	/// <summary>
	/// Gets or sets the rendered Y position (cached for mouse hit testing).
	/// </summary>
	internal int RenderedY { get; set; }

	/// <summary>
	/// Gets or sets the rendered height (cached for mouse hit testing).
	/// </summary>
	internal int RenderedHeight { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="TableRow"/> class with no cells.
	/// </summary>
	public TableRow()
	{
		Cells = new ObservableCollection<string>();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TableRow"/> class with the specified cells.
	/// </summary>
	public TableRow(params string[] cells)
	{
		Cells = new ObservableCollection<string>(cells);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="TableRow"/> class with the specified cells.
	/// </summary>
	public TableRow(IEnumerable<string> cells)
	{
		Cells = new ObservableCollection<string>(cells);
	}

	/// <summary>
	/// Gets or sets the cell value at the specified index.
	/// </summary>
	public string this[int index]
	{
		get => Cells[index];
		set => Cells[index] = value;
	}
}
