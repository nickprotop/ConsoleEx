// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Ozerov Maxim
// Email: ozerov.maxim.vl@ya.ru
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Parsing
{
	public static partial class MarkupParser
	{
		// Both sinks use the same parser, including literal tags, sanitization and grapheme rules.
		// Constrained generic calls keep the value-type measurement sink unboxed.
		private interface ICellSink
		{
			int Count { get; }
			Cell this[int index] { get; set; }
			void Add(Cell cell);
		}

		private readonly struct ListCellSink : ICellSink
		{
			public ListCellSink() => Cells = new List<Cell>();
			public List<Cell> Cells { get; }
			public int Count => Cells.Count;
			public Cell this[int index] { get => Cells[index]; set => Cells[index] = value; }
			public void Add(Cell cell) => Cells.Add(cell);
		}

		private struct WidthCellSink : ICellSink
		{
			// The parser only edits the last cell, or its base when the last cell is a wide
			// continuation. Width measurement does not need link text or gradient recoloring.
			private Cell _previous;
			private Cell _last;
			private int _lineWidth;
			public int Count { get; private set; }
			public int MaxWidth { get; private set; }
			public Cell this[int index]
			{
				get => index == Count - 1 ? _last
					: index == Count - 2 ? _previous
					: throw new ArgumentOutOfRangeException(nameof(index));
				set
				{
					if (index == Count - 1) _last = value;
					else if (index == Count - 2) _previous = value;
					else throw new ArgumentOutOfRangeException(nameof(index));
				}
			}

			public void Add(Cell cell)
			{
				_previous = _last;
				_last = cell;
				Count++;
				if (cell.Character.Value == RowBreakSentinelRune)
					_lineWidth = 0;
				else
					MaxWidth = Math.Max(MaxWidth, ++_lineWidth);
			}
		}

		// The caller has already expanded Markdown at the requested width, just as ParseLines
		// does before its flat parse. No cell lists, row lists or link text are retained here.
		internal static int MeasureExpandedWidth(string markup, Color fg, Color bg)
		{
			var sink = new WidthCellSink();
			ParseCore(SentinelizeLineBreaks(markup), fg, bg, ref sink, out _, measureOnly: true);
			return sink.MaxWidth;
		}

		private static string SentinelizeLineBreaks(string markup)
			=> markup.Replace("\r\n", RowBreakSentinel).Replace("\r", RowBreakSentinel).Replace("\n", RowBreakSentinel);
	}
}
