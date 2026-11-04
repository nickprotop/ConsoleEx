// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Ozerov Maxim
// Email: ozerov.maxim.vl@ya.ru
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Parsing;

public static partial class MarkupParser
{
	private readonly struct StyledTextSink : ICellSink
	{
		public StyledTextSink() => Builder = new();
		public StyledText.Builder Builder { get; }
		public int Count => Builder.Count;
		public Cell this[int index] { get => Builder[index]; set => Builder[index] = value; }
		public void Add(Cell cell) => Builder.Add(cell);
	}

	internal static StyledText ParseStyledText(string expanded, Color fg, Color bg)
	{
		var sink = new StyledTextSink();
		ParseCore(SentinelizeLineBreaks(expanded), fg, bg, ref sink, out var links);
		return sink.Builder.Build(links);
	}

	private readonly struct RangeRowSink(StyledText text, int lineStart, List<StyledRow> rows) : IWrappedRowSink
	{
		public void Add(int start, int count, bool fillToWidth)
			=> rows.Add(new StyledRow(text, lineStart + start, count, fillToWidth));
	}

	internal static List<StyledRow> WrapStyledText(StyledText text, int width,
		out List<List<LinkSpan>> rowLinks, out List<bool> soft)
	{
		var rows = new List<StyledRow>(text.LineStarts.Length);
		soft = new(text.LineStarts.Length);
		rowLinks = new(text.LineStarts.Length);
		for (int line = 0; line < text.LineStarts.Length; line++)
		{
			int start = text.LineStarts[line];
			int end = line + 1 < text.LineStarts.Length ? text.LineStarts[line + 1] - 1 : text.Count;
			int before = rows.Count;
			if (end - start <= width || width <= 0)
				rows.Add(new StyledRow(text, start, end - start));
			else
			{
				var sink = new RangeRowSink(text, start, rows);
				WrapCellRanges(new CellSlice(text, start, end - start), width, ref sink);
			}
			for (int row = before; row < rows.Count; row++)
			{
				soft.Add(row != before);
				var range = rows[row];
				// Exact offsets from the wrapper also preserve leading spaces in link hit-testing.
				List<LinkSpan>? links = null;
				foreach (var link in text.Links)
				{
					int a = Math.Max(range.Start, link.StartCol);
					int b = Math.Min(range.Start + range.Count, link.EndCol);
					if (b > a)
						(links ??= new()).Add(new LinkSpan(a - range.Start, b - range.Start,
							link.Url, link.Text, link.LinkIndex));
				}
				rowLinks.Add(links ?? EmptyStyledRowLinks);
			}
		}
		return rows;
	}

	// Internal consumers never mutate published link lists.
	private static readonly List<LinkSpan> EmptyStyledRowLinks = new();
}
