// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Diagnostics.Snapshots;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for issue #78 — <see cref="ITableDataSource.GetColumnAlignment"/> was declared on
/// the interface and implemented by data sources, but never consulted anywhere in rendering.
///
/// <para>
/// In data-source mode <c>colSnapshot</c> was left null (only <c>colCount</c> was read), so the
/// <c>renderCols</c> handed to <c>DrawDataRow</c> was null and its alignment lookup fell through to
/// the <see cref="TextJustification.Left"/> default for every column. <c>SetColumnAlignment</c> could
/// not help either: it writes into <c>_columns</c>, which is empty when a data source is attached.
/// There was therefore no way at all to right-align a column under <c>WithDataSource()</c>.
/// </para>
///
/// <para>
/// These assert on the COMPOSITED SCREEN — where the glyphs actually land — rather than on control
/// state, because the bug was purely in paint: every internal column value was already correct.
/// </para>
/// </summary>
public class TableDataSourceAlignmentTests
{
	/// <summary>A data source that reports a caller-supplied alignment per column.</summary>
	private sealed class AlignedDataSource : ITableDataSource
	{
		private readonly string[] _headers;
		private readonly string[,] _data;
		private readonly TextJustification[] _alignments;
		private readonly int?[]? _widths;

		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		public AlignedDataSource(string[] headers, string[,] data,
			TextJustification[] alignments, int?[]? widths = null)
		{
			_headers = headers;
			_data = data;
			_alignments = alignments;
			_widths = widths;
		}

		public int RowCount => _data.GetLength(0);
		public int ColumnCount => _headers.Length;
		public string GetColumnHeader(int columnIndex) => _headers[columnIndex];
		public string GetCellValue(int rowIndex, int columnIndex) => _data[rowIndex, columnIndex];
		public TextJustification GetColumnAlignment(int columnIndex) => _alignments[columnIndex];
		public int? GetColumnWidth(int columnIndex) => _widths?[columnIndex];
	}

	/// <summary>Renders the system and returns one composited row as text.</summary>
	private static string Row(CharacterBufferSnapshot snap, int y, int width)
	{
		var sb = new StringBuilder(width);
		for (int x = 0; x < width; x++) sb.Append(ChromeGeometry.CharAt(snap, x, y));
		return sb.ToString();
	}

	/// <summary>
	/// Builds a window hosting a borderless table bound to the given data source, renders it, and
	/// returns the composited snapshot plus the screen width.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, TableControl table)
		BuildTable(ITableDataSource source, bool checkboxMode = false)
	{
		var system = ChromeGeometry.CreateSystem(40, 12);

		var table = new TableControl { BorderStyle = BorderStyle.None };
		table.DataSource = source;
		if (checkboxMode) table.CheckboxMode = true;

		var window = new WindowBuilder(system).Frameless().Maximized().Build();
		window.AddControl(table);
		system.WindowStateService.AddWindow(window);

		ChromeGeometry.Render(system);
		return (system, window, table);
	}

	[Fact]
	public void RightAlignedColumn_FromDataSource_RendersRightAligned()
	{
		// Column 0 is fixed-width and Right-aligned; the short value must sit at the column's RIGHT
		// edge. Before the fix it was painted hard against the left edge.
		var source = new AlignedDataSource(
			headers: new[] { "Amount", "Name" },
			data: new[,] { { "42", "widget" } },
			alignments: new[] { TextJustification.Right, TextJustification.Left },
			widths: new int?[] { 10, 10 });

		var (system, _, _) = BuildTable(source);
		var snap = ChromeGeometry.Render(system);

		// Row 0 is the header, row 1 the first data row (no border).
		string dataRow = Row(snap, 1, 40);

		int valueAt = dataRow.IndexOf("42", System.StringComparison.Ordinal);
		Assert.True(valueAt >= 0, $"value not rendered at all. Row: '{dataRow}'");

		// Right-aligned inside a 10-wide first column: "42" ends at the column's right edge, so it
		// cannot start at column 0. This is the assertion that fails before the fix.
		Assert.True(valueAt > 0,
			$"column 0 is Right-aligned but '42' rendered at the left edge. Row: '{dataRow}'");
	}

	[Fact]
	public void CenterAlignedColumn_FromDataSource_RendersCentered()
	{
		var source = new AlignedDataSource(
			headers: new[] { "Mid", "Name" },
			data: new[,] { { "X", "widget" } },
			alignments: new[] { TextJustification.Center, TextJustification.Left },
			widths: new int?[] { 11, 10 });

		var (system, _, _) = BuildTable(source);
		var snap = ChromeGeometry.Render(system);
		string dataRow = Row(snap, 1, 40);

		int valueAt = dataRow.IndexOf('X');
		Assert.True(valueAt >= 0, $"value not rendered. Row: '{dataRow}'");

		// Centered in an 11-wide column: neither flush left nor flush right.
		Assert.True(valueAt > 0 && valueAt < 10,
			$"column 0 is Center-aligned but 'X' landed at index {valueAt}. Row: '{dataRow}'");
	}

	[Fact]
	public void LeftAlignedColumn_FromDataSource_StillRendersLeft()
	{
		// The default path must be unchanged — guards against "fixing" alignment by shifting
		// everything.
		var source = new AlignedDataSource(
			headers: new[] { "Name", "Other" },
			data: new[,] { { "abc", "xyz" } },
			alignments: new[] { TextJustification.Left, TextJustification.Left },
			widths: new int?[] { 10, 10 });

		var (system, _, _) = BuildTable(source);
		var snap = ChromeGeometry.Render(system);
		string dataRow = Row(snap, 1, 40);

		Assert.StartsWith("abc", dataRow.TrimStart('\0'));
	}

	[Fact]
	public void RightAlignedColumn_SurvivesRerender()
	{
		var source = new AlignedDataSource(
			headers: new[] { "Amount", "Name" },
			data: new[,] { { "42", "widget" } },
			alignments: new[] { TextJustification.Right, TextJustification.Left },
			widths: new int?[] { 10, 10 });

		var (system, _, _) = BuildTable(source);

		int firstIndex = Row(ChromeGeometry.Render(system), 1, 40)
			.IndexOf("42", System.StringComparison.Ordinal);

		// Must hold across a re-render, not just the first frame.
		for (int i = 0; i < 3; i++)
		{
			string row = Row(ChromeGeometry.Render(system), 1, 40);
			int idx = row.IndexOf("42", System.StringComparison.Ordinal);
			Assert.True(idx > 0, $"alignment lost on re-render {i}. Row: '{row}'");
			Assert.Equal(firstIndex, idx);
		}
	}

	[Fact]
	public void CheckboxMode_DoesNotShiftColumnAlignment()
	{
		// The checkbox column is prepended at render time. Alignment must still map to the right
		// data column — an off-by-one here would right-align the wrong column.
		var source = new AlignedDataSource(
			headers: new[] { "Amount", "Name" },
			data: new[,] { { "42", "widget" } },
			alignments: new[] { TextJustification.Right, TextJustification.Left },
			widths: new int?[] { 10, 10 });

		var (system, _, _) = BuildTable(source, checkboxMode: true);
		var snap = ChromeGeometry.Render(system);
		string dataRow = Row(snap, 1, 40);

		int checkAt = dataRow.IndexOf("[ ]", System.StringComparison.Ordinal);
		int valueAt = dataRow.IndexOf("42", System.StringComparison.Ordinal);
		int nameAt = dataRow.IndexOf("widget", System.StringComparison.Ordinal);

		Assert.True(checkAt >= 0, $"checkbox not rendered. Row: '{dataRow}'");
		Assert.True(valueAt > checkAt, $"value did not follow the checkbox. Row: '{dataRow}'");
		Assert.True(nameAt > valueAt, $"columns out of order. Row: '{dataRow}'");

		// "42" must be pushed toward the right edge of its own column, not flush against the
		// checkbox column.
		Assert.True(valueAt > checkAt + 4,
			$"column 0 is Right-aligned but '42' sits at the left of its column. Row: '{dataRow}'");
	}
}
