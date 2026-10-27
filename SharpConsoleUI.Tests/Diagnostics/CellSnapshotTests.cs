// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using SharpConsoleUI.Diagnostics.Socket;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using SharpConsoleUI.Surfaces;
using Spectre.Console;
using Xunit;

namespace SharpConsoleUI.Tests.CellSnapshot
{
	/// <summary>
	/// Reading a screen as cells rather than as text.
	/// </summary>
	/// <remarks>
	/// Text is the wrong carrier for the questions a debugger asks about width.
	/// Both text formats skip continuation cells and fold combining marks into
	/// the character before them, so by the time a row is a string a wide glyph
	/// is one character with no width and the cell it also occupies is gone.
	/// That is exactly the information a caller is looking for when the columns
	/// do not add up.
	/// </remarks>
	public class CellSnapshotTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;

		/// <summary>A painted surface with known content.</summary>
		private static ICellSurface Surface(string markup, int width = 12)
		{
			var buffer = new CharacterBuffer(width, 1, Bg);
			var cells = MarkupParser.Parse(markup, Fg, Bg);

			for (int i = 0; i < cells.Count && i < width; i++)
				buffer.SetCell(i, 0, cells[i]);

			return buffer.CreateSnapshot().AsSurface();
		}

		private static JsonArray Row(string markup, int width = 12) =>
			DiagnosticsSerializer.CellGrid(Surface(markup, width), DiagnosticsSerializer.Region.All)["rows"]!
				.AsArray()[0]!.AsArray();

		[Fact]
		public void ANarrowCharacterIsOneCellOfWidthOne()
		{
			var cells = Row("AB");

			Assert.Equal("A", cells[0]!["ch"]!.GetValue<string>());
			Assert.Equal(1, cells[0]!["w"]!.GetValue<int>());
			Assert.Equal("B", cells[1]!["ch"]!.GetValue<string>());
		}

		[Fact]
		public void AWideCharacterOccupiesTwoCellsAndSaysSo()
		{
			// The whole point of the format. In text this is one character and
			// the second column has vanished.
			var cells = Row("日A");

			Assert.Equal("日", cells[0]!["ch"]!.GetValue<string>());
			Assert.Equal(2, cells[0]!["w"]!.GetValue<int>());

			Assert.True(cells[1]!["cont"]!.GetValue<bool>(),
				"the second column of a wide glyph is a continuation cell");

			// And the next real character is where the columns say it is.
			Assert.Equal("A", cells[2]!["ch"]!.GetValue<string>());
		}

		[Fact]
		public void EveryRowHasOneEntryPerColumn()
		{
			// Continuation cells are PRESENT, not skipped: a caller counting
			// columns must be able to trust the index.
			var rows = DiagnosticsSerializer.CellGrid(
				Surface("日本語"), DiagnosticsSerializer.Region.All)["rows"]!.AsArray();

			Assert.All(rows, row => Assert.Equal(12, row!.AsArray().Count));
		}

		[Fact]
		public void ColoursComeBackAsNumbersNotEscapes()
		{
			var cell = Row("[red]A[/]")[0]!;

			Assert.Equal(3, cell["fg"]!.AsArray().Count);
			Assert.Equal(3, cell["bg"]!.AsArray().Count);
		}

		[Fact]
		public void DecorationsAreNamed()
		{
			var cell = Row("[bold]A[/]")[0]!;

			Assert.Contains("Bold", cell["dec"]!.GetValue<string>());
		}

		[Fact]
		public void AnUndecoratedCellOmitsTheDecorationField()
		{
			// Most cells carry no decoration and no combiners. Emitting nulls
			// for every one of 1200 cells is noise on the wire.
			var cell = Row("A")[0]!.AsObject();

			Assert.False(cell.ContainsKey("dec"));
			Assert.False(cell.ContainsKey("comb"));
		}

		[Fact]
		public void ARegionReadsOnlyThatRectangle()
		{
			var reply = DiagnosticsSerializer.CellGrid(
				Surface("ABCDEFGH"), new DiagnosticsSerializer.Region(2, 0, 3, 1));

			Assert.Equal(2, reply["x"]!.GetValue<int>());
			Assert.Equal(3, reply["width"]!.GetValue<int>());

			var cells = reply["rows"]!.AsArray()[0]!.AsArray();
			Assert.Equal(3, cells.Count);
			Assert.Equal("C", cells[0]!["ch"]!.GetValue<string>());
		}
	}
}
