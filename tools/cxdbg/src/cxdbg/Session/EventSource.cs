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
/// The input the target has recorded, most recent first.
/// </summary>
/// <remarks>
/// Only when the target was built with <c>EnableDiagnostics</c>: the recorder
/// is fixed at construction and cannot be switched on over the socket. When it
/// is off the reply carries a hint rather than an empty list, and this source
/// reports that instead of showing an empty table that looks like silence.
/// </remarks>
internal sealed class EventSource : ITableDataSource
{
	private readonly List<(string Flags, string At)> _events = new();

	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	/// <summary>Whether the target is recording, and what to say when it is not.</summary>
	internal string Status { get; private set; } = "";

	/// <summary>Takes a fresh <c>events</c> reply.</summary>
	internal void SetEvents(JsonNode? reply)
	{
		_events.Clear();

		if (reply?["recording"]?.GetValue<bool>() != true)
		{
			Status = reply?["hint"]?.GetValue<string>()
				?? "this application is not recording input";

			Changed();
			return;
		}

		int count = reply["count"]?.GetValue<int>() ?? 0;
		Status = count == 0
			? "recording, nothing yet"
			: $"recording, {count} events";

		// Most recent first: the thing you are looking for is what just
		// happened, not what happened when the application started.
		foreach (var e in (reply["events"]?.AsArray() ?? []).Reverse())
		{
			_events.Add((
				e?["flags"]?.GetValue<string>() ?? "?",
				$"{e?["x"]},{e?["y"]}"));
		}

		Changed();
	}

	public int RowCount => _events.Count;

	public int ColumnCount => 2;

	public string GetColumnHeader(int columnIndex) => columnIndex switch
	{
		0 => "at",
		1 => "event",
		_ => "",
	};

	public int? GetColumnWidth(int columnIndex) => columnIndex == 0 ? 10 : null;

	public string GetCellValue(int rowIndex, int columnIndex)
	{
		if (rowIndex < 0 || rowIndex >= _events.Count) return "";

		var (flags, at) = _events[rowIndex];

		return columnIndex switch
		{
			0 => at,
			1 => flags.Replace("[", "[["),
			_ => "",
		};
	}

	private void Changed() =>
		CollectionChanged?.Invoke(this,
			new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
}
