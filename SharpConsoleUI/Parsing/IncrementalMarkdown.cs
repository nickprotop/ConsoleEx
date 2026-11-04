// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Ozerov Maxim
// Email: ozerov.maxim.vl@ya.ru
// License: MIT
// -----------------------------------------------------------------------

using Markdig.Syntax;
using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Parsing;

// A control-owned document, accessed only under its parse-cache lock. Published layouts own
// immutable text and do not point back here. Each retained syntax tree owns its block string,
// never a slice into an older full SetMarkdown snapshot.
internal sealed class IncrementalMarkdown
{
	private string _source = string.Empty;
	private List<Part> _parts = new();
	private bool _independent;

	private sealed class Part(string source, int start, IEnumerable<Block> document)
	{
		internal readonly string Source = source;
		internal readonly int Start = start;
		internal readonly IEnumerable<Block> Document = document;
		private readonly List<(int Width, int Prefix, string Text)> _rendered = new();

		internal string Render(int width, int prefix, MarkdownStyle style)
		{
			foreach (var item in _rendered)
				if (item.Width == width && item.Prefix == prefix) return item.Text;
			string text = MarkdownToMarkup.RenderFragment(Document, style, width, prefix);
			if (_rendered.Count == ControlDefaults.MarkupParseCacheCapacity) _rendered.RemoveAt(0);
			_rendered.Add((width, prefix, text));
			return text;
		}
	}

	internal void Update(string source)
	{
		if (string.Equals(source, _source, StringComparison.Ordinal)) return;
		// Reference links, footnotes and abbreviation definitions can change earlier blocks.
		// Brackets conservatively disable tail parsing, even for ordinary inline links/code.
		// CR normalization and extension-specific block boundaries use the full parser too.
		bool independent = source.IndexOfAny(['[', '\r']) < 0;
		int keep = independent && _independent && source.StartsWith(_source, StringComparison.Ordinal)
			? Math.Max(0, _parts.Count - 2) : 0;
		int tailStart = keep > 0 ? _parts[keep].Start : 0;
		var updated = new List<Part>();
		for (int i = 0; i < keep; i++) updated.Add(_parts[i]);
		string tail = tailStart == 0 ? source : source[tailStart..];
		var document = MarkdownToMarkup.ParseDocument(tail);
		if (!independent)
		{
			// Resolve document-wide references with the full tree, but render/cache blocks
			// separately. Old trees are never reused on this path; unchanged rendered text is.
			foreach (var block in document)
				updated.Add(new Part(string.Empty, 0, new[] { block }));
		}
		else
		{
			int start = 0;
			for (int i = 0; i < document.Count; i++)
			{
				int end = tail.Length;
				if (i + 1 < document.Count)
				{
					// Include indentation in the next block, and keep intervening blank lines in
					// the previous block. Setext headings, lists, fences and tables stay whole.
					end = Math.Clamp(document[i + 1].Span.Start, start, tail.Length);
					while (end > start && tail[end - 1] != '\n') end--;
				}
				if (end <= start) continue;
				string owned = tail.Substring(start, end - start);
				int absoluteStart = tailStart + start;
				Part? reused = null;
				// Only the last two previous parts were reparsed on the append path.
				for (int old = keep; _independent && old < _parts.Count; old++)
					if (_parts[old].Start == absoluteStart && _parts[old].Source == owned)
					{ reused = _parts[old]; break; }
				updated.Add(reused ?? new Part(owned, absoluteStart,
					start == 0 && end == tail.Length ? document : MarkdownToMarkup.ParseDocument(owned)));
				start = end;
			}
		}
		_parts = updated;
		_source = source;
		_independent = independent;
	}

	internal List<string> RenderChunks(int width, MarkdownStyle style)
	{
		var result = new List<string>(_parts.Count);
		int trailingNewlines = 0;
		bool any = false;
		foreach (var part in _parts)
		{
			int prefix = !any ? 0 : trailingNewlines >= 2 ? 2 : 1;
			string text = part.Render(width, prefix, style);
			if (text.Length == 0) continue;
			result.Add(text);
			any = true;
			// All currently supported block emitters terminate in a newline.
			trailingNewlines = 0;
			for (int i = text.Length - 1; i >= 0 && text[i] == '\n'; i--) trailingNewlines++;
		}
		// Match RenderDocument.TrimEnd once across the whole document, not per block.
		for (int i = result.Count - 1; i >= 0; i--)
		{
			string trimmed = result[i].TrimEnd('\n');
			if (trimmed.Length == 0) { result.RemoveAt(i); continue; }
			result[i] = trimmed;
			break;
		}
		if (result.Count == 0) result.Add(string.Empty);
		return result;
	}
}
