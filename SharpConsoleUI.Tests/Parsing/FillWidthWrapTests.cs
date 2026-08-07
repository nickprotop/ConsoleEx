using SharpConsoleUI;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Parsing
{
	/// <summary>
	/// The <c>[fillwidth]</c> marker must survive wrapping.
	///
	/// <para>It rides on the LAST CELL of a parsed line, and the painter reads exactly one place —
	/// <c>cellLine[^1].FillToWidth</c> — for each row it draws. Wrapping turns one logical line into
	/// several painted rows, so any row that does not carry the flag paints its trailing background
	/// as the control background instead of the code background: the shaded block stops at the last
	/// character rather than running to the right edge.</para>
	/// </summary>
	public class FillWidthWrapTests
	{
		private static readonly Color Fg = Color.White;
		private static readonly Color Bg = Color.Black;

		[Fact]
		public void FillWidth_SurvivesOnAShortLineThatDoesNotWrap()
		{
			// The path that always worked: under the width, ParseLines skips WrapCellLine entirely.
			var rows = MarkupParser.ParseLines("[white on blue] x [/][fillwidth]", 40, Fg, Bg);

			Assert.Single(rows);
			Assert.True(rows[0][^1].FillToWidth);
		}

		[Fact]
		public void FillWidth_SurvivesTrailingSpaceTrimming()
		{
			// The code-block emitter ends every line with a PAD SPACE, and that space is the cell the
			// flag is attached to. WrapCellLine trims trailing spaces from a wrapped row, which threw
			// the flag away with them.
			var markup = "[white on blue] " + new string('x', 60) + " [/][fillwidth]";
			var rows = MarkupParser.ParseLines(markup, 20, Fg, Bg);

			Assert.True(rows.Count > 1, "expected this line to wrap");
			Assert.True(rows[^1][^1].FillToWidth, "the final wrapped row lost the marker");
		}

		[Fact]
		public void FillWidth_AppliesToEVERYWrappedRowNotJustTheLast()
		{
			// The flag lives on the last cell, so GetRange gives it to the FINAL row only. Every
			// earlier row of a wrapped code line painted an unshaded tail -- visible as a ragged
			// right edge on exactly the long lines in a ``` block.
			var markup = "[white on blue] " + string.Join(' ', Enumerable.Repeat("word", 30)) + " [/][fillwidth]";
			var rows = MarkupParser.ParseLines(markup, 20, Fg, Bg);

			Assert.True(rows.Count > 2, $"expected several wrapped rows, got {rows.Count}");
			for (int i = 0; i < rows.Count; i++)
				Assert.True(rows[i][^1].FillToWidth, $"row {i} of {rows.Count} lost the marker");
		}

		[Fact]
		public void FillWidth_CarriesTheCodeBackgroundOnEveryRow()
		{
			// The painter uses the flagged cell's OWN background. A row whose last cell kept the flag
			// but not the shading would fill the tail with the wrong colour, which is the same visual
			// defect wearing a different mask.
			var markup = "[white on blue] " + string.Join(' ', Enumerable.Repeat("word", 30)) + " [/][fillwidth]";
			var rows = MarkupParser.ParseLines(markup, 20, Fg, Bg);

			foreach (var row in rows)
				Assert.Equal(Color.Blue, row[^1].Background);
		}

		[Fact]
		public void FillWidth_IsAppliedToEveryHardLine_NotJustTheLast()
		{
			// THE ACTUAL BUG. ParseLines parses the WHOLE markup in one Parse call (parse-then-cut,
			// newlines swapped for a row-break sentinel). The marker used to set a single global bool
			// applied once to the last cell of the entire run, so a multi-line code block -- which
			// emits [fillwidth] once PER LINE -- flagged only its FINAL row. Every earlier row painted
			// an unshaded tail stopping at its last character, while the newest line looked correct:
			// exactly the "previous lines lose the background as more arrive" report.
			//
			// Note these lines differ in length, so a row inheriting another row's cell is also caught.
			var markup = "[white on blue] aaaa [/][fillwidth]\n"
					   + "[white on blue] bb [/][fillwidth]\n"
					   + "[white on blue] c [/][fillwidth]";

			var rows = MarkupParser.ParseLines(markup, 40, Fg, Bg);

			Assert.Equal(3, rows.Count);
			for (int i = 0; i < rows.Count; i++)
				Assert.True(rows[i][^1].FillToWidth, $"hard line {i} of {rows.Count} lost the marker");
		}

		[Fact]
		public void FillWidth_OnOneLineDoesNotLeakToUnmarkedNeighbours()
		{
			// The flag must stay scoped to the line that asked for it. Flagging at the marker's own
			// position (rather than at end-of-parse) is what makes this true, so guard it: a marked
			// line between two unmarked ones must be the only row that fills.
			var markup = "plain above\n[white on blue] code [/][fillwidth]\nplain below";

			var rows = MarkupParser.ParseLines(markup, 40, Fg, Bg);

			Assert.Equal(3, rows.Count);
			Assert.False(rows[0][^1].FillToWidth);
			Assert.True(rows[1][^1].FillToWidth);
			Assert.False(rows[2][^1].FillToWidth);
		}

		[Fact]
		public void FillWidth_IsNotInventedForOrdinaryText()
		{
			// The marker must stay opt-in: a wrapped line that never asked for it must not gain a
			// filled tail, or every wrapped paragraph would paint its trailing background solid.
			var rows = MarkupParser.ParseLines(string.Join(' ', Enumerable.Repeat("word", 30)), 20, Fg, Bg);

			Assert.True(rows.Count > 1);
			foreach (var row in rows)
				Assert.False(row[^1].FillToWidth);
		}
	}
}
