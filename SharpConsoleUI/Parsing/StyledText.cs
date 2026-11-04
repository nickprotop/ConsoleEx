// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Ozerov Maxim
// Email: ozerov.maxim.vl@ya.ru
// License: MIT
// -----------------------------------------------------------------------

using System.Collections;
using System.Text;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Parsing;

// Immutable after publication. Rune/flag pairs and style IDs occupy eight bytes per column;
// colors and decorations are interned once per chunk of text, combiners only where present.
// Bounded owned pages avoid large glyph-buffer growth copies, pooling lifetime hazards and slices
// into historical full-document strings. Cell remains the public screen-buffer representation.
internal sealed class StyledText : IReadOnlyList<Cell>
{
	private const int PageShift = 10;
	private const int PageSize = 1 << PageShift;
	private const int RuneMask = (1 << 21) - 1;
	private const int WideFlag = 1 << 21;
	private const int FillFlag = 1 << 22;
	private const int DirtyFlag = 1 << 23;
	internal readonly record struct Glyph(int RuneAndFlags, int Style);
	internal readonly record struct Style(Color Foreground, Color Background, TextDecoration Decorations);
	private readonly Glyph[][] _pages;
	private readonly Style[] _styles;
	private readonly Dictionary<int, string>? _combiners;
	internal List<LinkSpan> Links { get; }
	internal int[] LineStarts { get; }
	public int Count { get; }

	private StyledText(Builder builder, List<LinkSpan> links)
	{
		Count = builder.Count;
		_pages = builder.Pages.ToArray();
		// Small labels should not retain an entire page.
		if (_pages.Length > 0 && (Count & (PageSize - 1)) is var tail && tail != 0)
			Array.Resize(ref _pages[^1], tail);
		_styles = builder.Styles.ToArray();
		_combiners = builder.Combiners;
		Links = links;
		var starts = new List<int> { 0 };
		for (int i = 0; i < Count; i++)
			if ((_pages[i >> PageShift][i & (PageSize - 1)].RuneAndFlags & RuneMask) == 0xE000)
				starts.Add(i + 1);
		LineStarts = starts.ToArray();
	}

	public Cell this[int index]
	{
		get
		{
			if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
			var glyph = _pages[index >> PageShift][index & (PageSize - 1)];
			return Decode(glyph, _styles[glyph.Style], _combiners, index);
		}
	}

	private static Cell Decode(Glyph glyph, Style style, Dictionary<int, string>? combiners, int index)
	{
		string? combining = null;
		combiners?.TryGetValue(index, out combining);
		return new Cell(new Rune(glyph.RuneAndFlags & RuneMask), style.Foreground, style.Background, style.Decorations)
		{
			IsWideContinuation = (glyph.RuneAndFlags & WideFlag) != 0,
			FillToWidth = (glyph.RuneAndFlags & FillFlag) != 0,
			Dirty = (glyph.RuneAndFlags & DirtyFlag) != 0,
			Combiners = combining
		};
	}

	internal sealed class Builder
	{
		private readonly List<Glyph[]> _pages = new();
		private readonly List<Style> _styles = new();
		private readonly Dictionary<Style, int> _styleIds = new();
		private Dictionary<int, string>? _combiners;
		private Style _lastStyle;
		private int _lastStyleId = -1;
		internal List<Glyph[]> Pages => _pages;
		internal List<Style> Styles => _styles;
		internal Dictionary<int, string>? Combiners => _combiners;
		public int Count { get; private set; }

		public Cell this[int index]
		{
			get
			{
				var glyph = _pages[index >> PageShift][index & (PageSize - 1)];
				return Decode(glyph, _styles[glyph.Style], _combiners, index);
			}
			set
			{
				var style = new Style(value.Foreground, value.Background, value.Decorations);
				if (_lastStyleId < 0 || style != _lastStyle)
				{
					if (!_styleIds.TryGetValue(style, out _lastStyleId))
					{
						_lastStyleId = _styles.Count;
						_styleIds.Add(style, _lastStyleId);
						_styles.Add(style);
					}
					_lastStyle = style;
				}
				int flags = value.Character.Value | (value.IsWideContinuation ? WideFlag : 0)
					| (value.FillToWidth ? FillFlag : 0) | (value.Dirty ? DirtyFlag : 0);
				_pages[index >> PageShift][index & (PageSize - 1)] = new Glyph(flags, _lastStyleId);
				if (value.Combiners != null) (_combiners ??= new())[index] = value.Combiners;
				else _combiners?.Remove(index);
			}
		}

		public void Add(Cell cell)
		{
			if ((Count & (PageSize - 1)) == 0) _pages.Add(new Glyph[_pages.Count == 0 ? 32 : PageSize]);
			else if (_pages[^1].Length <= (Count & (PageSize - 1)))
			{
				var page = _pages[^1];
				Array.Resize(ref page, Math.Min(PageSize, page.Length * 2));
				_pages[^1] = page;
			}
			this[Count++] = cell;
		}

		// The builder must never be used after publishing its storage.
		public StyledText Build(List<LinkSpan> links) => new(this, links);
	}

	public IEnumerator<Cell> GetEnumerator()
	{
		for (int i = 0; i < Count; i++) yield return this[i];
	}
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// A display row is only a range into immutable text. Wrapping at another width never copies glyphs.
internal readonly struct StyledRow(StyledText? source, int start, int count, bool fillToWidth = false) : IReadOnlyList<Cell>
{
	internal StyledText? Source { get; } = source;
	internal int Start { get; } = start;
	public int Count { get; } = count;
	internal bool FillToWidth { get; } = fillToWidth;
	public Cell this[int index]
	{
		get
		{
			if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
			var cell = Source![Start + index];
			if (index == Count - 1 && FillToWidth) cell.FillToWidth = true;
			return cell;
		}
	}

	public IEnumerator<Cell> GetEnumerator()
	{
		for (int i = 0; i < Count; i++) yield return this[i];
	}
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
