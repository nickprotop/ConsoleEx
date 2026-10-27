// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using SharpConsoleUI.Controls;

namespace Cxdbg.Session;

/// <summary>
/// A list of name-and-value facts, as a table.
/// </summary>
/// <remarks>
/// One of these per section of the details pane, each inside its own panel. A
/// table rather than aligned markup, because a column of names beside a column
/// of values is a table and writing it with padding is simulating one.
/// </remarks>
internal sealed class FactsSource : ITableDataSource
{
	private readonly List<(string Name, string Value, string? Note)> _facts = new();

	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	/// <summary>Whether this section has anything to show.</summary>
	internal bool HasFacts => _facts.Count > 0;

	/// <summary>Empties the section.</summary>
	internal void Clear() => _facts.Clear();

	/// <summary>Adds one fact.</summary>
	/// <param name="name">What it is called.</param>
	/// <param name="value">What it is.</param>
	/// <param name="note">Why it matters, when it does.</param>
	internal void Add(string name, string? value, string? note = null) =>
		_facts.Add((name, value ?? "-", note));

	/// <summary>Tells the table the section has changed.</summary>
	internal void Done() =>
		CollectionChanged?.Invoke(this,
			new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

	public int RowCount => _facts.Count;

	public int ColumnCount => 3;

	public string GetColumnHeader(int columnIndex) => "";

	public int? GetColumnWidth(int columnIndex) => columnIndex switch
	{
		0 => 12,
		1 => 16,
		_ => null,
	};

	public string GetCellValue(int rowIndex, int columnIndex)
	{
		if (rowIndex < 0 || rowIndex >= _facts.Count) return "";

		var (name, value, note) = _facts[rowIndex];

		return columnIndex switch
		{
			0 => $"[dim]{name}[/]",
			1 => value,
			2 => note ?? "",
			_ => "",
		};
	}
}
