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
/// The Tab order, with each stop's geometry beside it.
/// </summary>
/// <remarks>
/// <c>reachable</c> gives names in order and nothing else; the tree gives
/// sizes. Joined, they answer the question people actually ask of a focus
/// list: which of these can be seen. A stop that is reachable by Tab and
/// paints nothing is a real and common bug, and it is invisible in either
/// source alone.
/// </remarks>
internal sealed class FocusSource : ITableDataSource
{
	private readonly List<Stop> _stops = new();

	private readonly record struct Stop(
		string Order, string Control, string Size, string Note, bool Focused, bool Invisible);

	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	/// <summary>Takes the Tab order and looks each stop up in the tree.</summary>
	/// <param name="order">The control names, in Tab order.</param>
	/// <param name="focused">The control that has the keyboard.</param>
	/// <param name="model">The tree, for the geometry.</param>
	internal void SetOrder(IReadOnlyList<string> order, string? focused, ControlTreeModel model)
	{
		_stops.Clear();

		for (int i = 0; i < order.Count; i++)
		{
			string name = order[i];
			var node = model.FindByType(name);

			string size = "-";
			bool invisible = false;
			string note = "";

			if (node != null)
			{
				var b = node.Raw["bounds"];
				int w = b?["w"]?.GetValue<int>() ?? -1;
				int h = b?["h"]?.GetValue<int>() ?? -1;

				size = $"{w} x {h}";
				invisible = w == 0 || h == 0;

				if (invisible) note = "[red]Tab reaches it, you cannot see it[/]";
			}
			else
			{
				// A focus stop the tree does not contain is in a portal or a
				// detached surface: reachable by Tab, and not part of the
				// window's own layout. Worth saying, because "not in the tree"
				// on its own reads as a bug in the lookup.
				note = "[dim]in a portal, not in the window's tree[/]";
			}

			bool hasFocus = string.Equals(name, focused, StringComparison.Ordinal);
			if (hasFocus && note.Length == 0) note = "[dim]it has the keyboard[/]";

			_stops.Add(new Stop($"{i + 1}", Escape(name), size, note, hasFocus, invisible));
		}

		CollectionChanged?.Invoke(this,
			new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
	}

	public int RowCount => _stops.Count;

	public int ColumnCount => 4;

	public string GetColumnHeader(int columnIndex) => columnIndex switch
	{
		0 => "tab",
		1 => "control",
		2 => "size",
		3 => "",
		_ => "",
	};

	public int? GetColumnWidth(int columnIndex) => columnIndex switch
	{
		0 => 5,
		1 => 28,
		2 => 10,
		_ => null,
	};

	public string GetCellValue(int rowIndex, int columnIndex)
	{
		if (rowIndex < 0 || rowIndex >= _stops.Count) return "";

		var s = _stops[rowIndex];

		return columnIndex switch
		{
			0 => s.Focused ? $"[green]{s.Order}[/]" : s.Order,
			1 => s.Focused ? $"[green]{s.Control}[/]"
				: s.Invisible ? $"[red]{s.Control}[/]"
				: s.Control,
			2 => s.Invisible ? $"[red]{s.Size}[/]" : s.Size,
			3 => s.Note,
			_ => "",
		};
	}

	private static string Escape(string text) => text.Replace("[", "[[");
}
