// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls
{
	/// <summary>
	/// A column with a very long value used to squeeze its neighbours down to a couple of
	/// characters. MinWidth is the floor that stops it; when every floor cannot fit, the table
	/// overflows and relies on horizontal scrolling instead of crushing a column.
	/// </summary>
	public class TableColumnMinWidthTests
	{
		private const string Huge = "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX";

		private static (TableControl table, List<TableColumn> cols, List<TableRow> rows) Build(params int?[] minWidths)
		{
			var table = new TableControl();
			var cols = new List<TableColumn>();
			for (int i = 0; i < minWidths.Length; i++)
			{
				cols.Add(new TableColumn($"Col{i}", TextJustification.Left, null) { MinWidth = minWidths[i] });
			}

			var rows = new List<TableRow>
			{
				new TableRow(new List<string> { "RUNNING", "16:29:00", Huge }),
				new TableRow(new List<string> { "RUNNING", "16:29:01", "short" }),
			};
			return (table, cols, rows);
		}

		[Fact]
		public void WithoutMinWidth_LongColumnCrushesItsNeighbours()
		{
			// The behaviour MinWidth exists to fix — kept as the baseline so a regression is visible.
			var (table, cols, rows) = Build(null, null, null);

			var widths = table.ComputeColumnWidths(80, cols, rows);

			Assert.True(widths[0] < 8, $"expected col0 to be crushed, was {widths[0]}");
			Assert.True(widths[1] < 8, $"expected col1 to be crushed, was {widths[1]}");
		}

		[Fact]
		public void MinWidth_IsHonouredWhenItFits()
		{
			var (table, cols, rows) = Build(8, 8, null);

			var widths = table.ComputeColumnWidths(80, cols, rows);

			Assert.True(widths[0] >= 8, $"col0 below its floor: {widths[0]}");
			Assert.True(widths[1] >= 8, $"col1 below its floor: {widths[1]}");
		}

		[Fact]
		public void MinWidth_OverflowsViewportRatherThanCrushing()
		{
			// Floors sum past the viewport: the table must exceed it so a horizontal scrollbar can
			// pan, rather than shrinking a column below its floor.
			var (table, cols, rows) = Build(30, 30, 30);

			var widths = table.ComputeColumnWidths(40, cols, rows);

			Assert.All(widths, w => Assert.True(w >= 30, $"a column fell below its floor: {w}"));
			Assert.True(widths.Sum() > 40, "expected total width to overflow the viewport");
		}

		[Fact]
		public void FixedWidthColumns_IgnoreMinWidth()
		{
			// A fixed column is already explicit; its floor stays 1 regardless of MinWidth.
			var table = new TableControl();
			var cols = new List<TableColumn>
			{
				new TableColumn("Fixed", TextJustification.Left, 20) { MinWidth = 50 },
				new TableColumn("Auto", TextJustification.Left, null),
			};
			var rows = new List<TableRow> { new TableRow(new List<string> { "x", Huge }) };

			var widths = table.ComputeColumnWidths(30, cols, rows);

			Assert.Equal(20, widths[0]);
		}

		[Fact]
		public void MinWidthOfZeroOrNegative_IsClampedToOne()
		{
			var (table, cols, rows) = Build(0, -5, null);

			var widths = table.ComputeColumnWidths(80, cols, rows);

			Assert.All(widths, w => Assert.True(w >= 1, $"width below 1: {w}"));
		}

		[Fact]
		public void MinWidth_SetterInvalidates_AndIsChangeGuarded()
		{
			var col = new TableColumn("C", TextJustification.Left, null);
			Assert.Null(col.MinWidth);

			col.MinWidth = 12;
			Assert.Equal(12, col.MinWidth);

			col.MinWidth = 12;   // no-op path must not throw
			Assert.Equal(12, col.MinWidth);

			col.MinWidth = null; // clearing restores "no floor"
			Assert.Null(col.MinWidth);
		}
	}
}
