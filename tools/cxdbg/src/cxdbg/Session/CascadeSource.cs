// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Specialized;
using System.Text.Json.Nodes;
using SharpConsoleUI.Controls;

namespace Cxdbg.Session;

/// <summary>
/// The containers that decided a control's size, as a table.
/// </summary>
/// <remarks>
/// A table because that is what a cascade is: one row per container, with what
/// it offered and whether it was the one that decided. Writing it as lines of
/// markup made a table out of spaces, which is the thing the library has
/// controls to avoid.
/// </remarks>
internal sealed class CascadeSource : ITableDataSource
{
	private readonly List<Link> _links = new();

	private readonly record struct Link(
		string Who, string Layout, string Offered, string Decides, bool IsDeciding);

	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	/// <summary>What the control itself wanted and was offered.</summary>
	internal string Summary { get; private set; } = "";

	/// <summary>Takes a fresh <c>measure</c> reply.</summary>
	internal void SetCascade(JsonNode? reply, string id)
	{
		_links.Clear();

		var controls = reply?["controls"]?.AsArray();

		if (controls == null || controls.Count == 0)
		{
			Summary = $"no cascade for {id}";
			Changed();
			return;
		}

		var c = controls[0];

		Summary = $"[bold]{Escape(c?["control"]?.GetValue<string>() ?? id)}[/]"
			+ $"   wants [yellow]{c?["desiredWidth"]}x{c?["desiredHeight"]}[/]"
			+ $"   offered {c?["offeredWidth"]}x{c?["offeredHeight"]}";

		foreach (var link in c?["cascade"]?.AsArray() ?? [])
		{
			// setsWidth / setsHeight is the SERVER saying which link decided.
			// cxdbg does not work it out from the numbers.
			bool setsW = link?["setsWidth"]?.GetValue<bool>() == true;
			bool setsH = link?["setsHeight"]?.GetValue<bool>() == true;

			string decides = (setsW, setsH) switch
			{
				(true, true) => "[yellow]size[/]",
				(true, false) => "[yellow]width[/]",
				(false, true) => "[yellow]height[/]",
				_ => "",
			};

			_links.Add(new Link(
				Escape(link?["control"]?.GetValue<string>() ?? "-"),
				Escape(link?["layout"]?.GetValue<string>() ?? "-"),
				$"{link?["offeredWidth"]} x {link?["offeredHeight"]}",
				decides,
				setsW || setsH));
		}

		Changed();
	}

	public int RowCount => _links.Count;

	public int ColumnCount => 4;

	public string GetColumnHeader(int columnIndex) => columnIndex switch
	{
		0 => "container",
		1 => "layout",
		2 => "offered",
		3 => "decided",
		_ => "",
	};

	public int? GetColumnWidth(int columnIndex) => columnIndex switch
	{
		2 => 12,
		3 => 9,
		_ => null,
	};

	public string GetCellValue(int rowIndex, int columnIndex)
	{
		if (rowIndex < 0 || rowIndex >= _links.Count) return "";

		var l = _links[rowIndex];

		return columnIndex switch
		{
			0 => l.IsDeciding ? $"[yellow]{l.Who}[/]" : l.Who,
			1 => l.Layout,
			2 => l.Offered,
			3 => l.Decides,
			_ => "",
		};
	}

	private void Changed() =>
		CollectionChanged?.Invoke(this,
			new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

	private static string Escape(string text) => text.Replace("[", "[[");
}
