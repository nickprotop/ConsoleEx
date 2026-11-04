// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Ozerov Maxim
// Email: ozerov.maxim.vl@ya.ru
// License: MIT
// -----------------------------------------------------------------------

using System.Collections;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Parsing;

// Frame-local value. No shared text is mutated by selection or keyboard focus.
internal struct PaintedTextRow(StyledRow row)
{
	private readonly StyledRow _row = row;
	private Highlight _selection;
	private Highlight _focus;
	private readonly record struct Highlight(int From, int To, Color Foreground, Color Background)
	{
		internal void Apply(int column, ref Cell cell)
		{
			if (column < From || column >= To) return;
			cell.Foreground = Foreground;
			cell.Background = Background;
		}
	}

	internal int Count => _row.Count;
	internal void SetSelection(int from, int to, Color fg, Color bg) => _selection = new(from, to, fg, bg);
	internal void SetFocus(int from, int to, Color fg, Color bg) => _focus = new(from, to, fg, bg);
	internal Enumerator GetEnumerator(int from, int to) => new(this, from, to);

	internal struct Enumerator(PaintedTextRow row, int from, int to) : IEnumerator<Cell>
	{
		private int _index = from - 1;
		public Cell Current
		{
			get
			{
				var cell = row._row[_index];
				row._selection.Apply(_index, ref cell);
				row._focus.Apply(_index, ref cell);
				return cell;
			}
		}
		public bool MoveNext() => ++_index < to;
		object IEnumerator.Current => Current;
		public void Reset() => throw new NotSupportedException();
		public void Dispose() { }
	}
}
