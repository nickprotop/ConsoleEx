// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Ozerov Maxim
// Email: ozerov.maxim.vl@ya.ru
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	public partial class MarkupControl
	{
		private readonly record struct SourceKey(string? Text, int SourceIndex, Color Fg, Color Bg, MarkdownStyle Md, bool ZwjLigation);

		private sealed class TextChunk
		{
			public StyledText? Text;
			public int? NaturalWidth;
			public int? LinkCount;
			public readonly Dictionary<int, ParsedGroup> Layouts = new();
			public int Revision;
		}

		private sealed class ParsedSource : IParseCacheEntry
		{
			public required MarkupParser.PreparedMarkup Markup { get; init; }
			public required string SourceText { get; init; }
			public int Generation { get; set; }
			public int Revision;
			public int? NaturalWidth;
			public Dictionary<string, TextChunk> Chunks = new(StringComparer.Ordinal);
		}

		private readonly Dictionary<SourceKey, ParsedSource> _sourceCache = new();
		private ParseKey? _naturalWidthKey;
		private int _naturalWidth;

		private ParsedSource GetSource(string text, int sourceIndex, Color fg, Color bg, MarkdownStyle md, bool zwj, bool dynamic)
		{
			// Ordinary repeated log lines share a source by value. Markdown streams keep a
			// position so their document can advance without searching all other sources.
			bool markdown = text.IndexOf(MarkupParser.MarkdownTagOpen, StringComparison.OrdinalIgnoreCase) >= 0;
			var key = new SourceKey(markdown ? null : text, markdown ? sourceIndex : -1, fg, bg, md, zwj);
			ParsedSource? previous = null;
			if (!dynamic && _sourceCache.TryGetValue(key, out previous) && previous.SourceText == text)
			{
				previous.Generation = _groupGeneration;
				return previous;
			}
			var source = new ParsedSource
			{
				Markup = new MarkupParser.PreparedMarkup(text, md, previous?.Markup),
				SourceText = text,
				Generation = _groupGeneration,
				Revision = (previous?.Revision ?? 0) + 1,
				Chunks = previous?.Chunks ?? new(StringComparer.Ordinal)
			};
			if (!dynamic) _sourceCache[key] = source;
			return source;
		}

		private TextChunk GetTextChunk(ParsedSource source, string expanded)
		{
			if (!source.Chunks.TryGetValue(expanded, out var chunk))
			{
				chunk = new TextChunk();
				source.Chunks.Add(expanded, chunk);
			}
			chunk.Revision = source.Revision;
			return chunk;
		}

		private static List<string> RenderChunks(ParsedSource source, int width)
		{
			var chunks = source.Markup.RenderChunks(width);
			if (chunks == null) return new() { source.Markup.Render(width) };
			// Independent block parsing requires a hard break between chunks. Preserve a safe
			// full-parse fallback if an emitter gains a block with no terminating newline.
			for (int i = 0; i + 1 < chunks.Count; i++)
				if (!chunks[i].EndsWith('\n')) return new() { string.Concat(chunks) };
			return chunks;
		}

		private ParsedGroup ParseSource(ParsedSource source, int width, Color fg, Color bg)
		{
			var chunks = RenderChunks(source, width);
			var rows = new List<StyledRow>();
			var links = new List<List<LinkSpan>>();
			var soft = new List<bool>();
			int linkOffset = 0;
			for (int i = 0; i < chunks.Count; i++)
			{
				var chunk = GetTextChunk(source, chunks[i]);
				if (chunk.Text == null)
				{
					CountParse();
					chunk.Text = MarkupParser.ParseStyledText(chunks[i], fg, bg);
				}
				if (!chunk.Layouts.TryGetValue(width, out var layout))
				{
					var wrapped = MarkupParser.WrapStyledText(chunk.Text, width, out var wrappedLinks, out var wrappedSoft);
					layout = new ParsedGroup { Rows = wrapped, Links = wrappedLinks, SoftWrap = wrappedSoft, Generation = _groupGeneration };
					if (chunk.Layouts.Count == ParseCacheCapacity) chunk.Layouts.Remove(chunk.Layouts.Keys.First());
					chunk.Layouts.Add(width, layout);
				}
				// The trailing empty row belongs to the next chunk. Extra blank lines preceding it
				// are real rows and stay. The final chunk was trimmed exactly like RenderDocument.
				int count = layout.Rows.Count - (i + 1 < chunks.Count ? 1 : 0);
				for (int r = 0; r < count; r++)
				{
					rows.Add(layout.Rows[r]);
					soft.Add(layout.SoftWrap[r]);
					var rowLinks = layout.Links[r];
					if (linkOffset != 0 && rowLinks.Count != 0)
					{
						var rebased = new List<LinkSpan>(rowLinks.Count);
						foreach (var link in rowLinks)
							rebased.Add(new LinkSpan(link.StartCol, link.EndCol, link.Url, link.Text, link.LinkIndex + linkOffset));
						rowLinks = rebased;
					}
					links.Add(rowLinks);
				}
				int linkCount = 0;
				foreach (var link in chunk.Text.Links) linkCount = Math.Max(linkCount, link.LinkIndex + 1);
				linkOffset += linkCount;
			}
			RetireTextChunks(source, chunks.Count);
			return new ParsedGroup { Rows = rows, Links = links, SoftWrap = soft, Generation = _groupGeneration };
		}

		private static void RetireTextChunks(ParsedSource source, int liveChunks)
		{
			foreach (var entry in source.Chunks)
				if (entry.Value.Revision != source.Revision) source.Chunks.Remove(entry.Key);
			// Width-dependent Markdown can produce different strings; retain only a bounded set.
			if (source.Chunks.Count > Math.Max(4, liveChunks * ParseCacheCapacity)) source.Chunks.Clear();
		}

		private int MeasureNaturalContentWidth()
		{
			lock (_parseCacheLock)
			{
				var (fg, bg) = ResolveParseColors();
				var md = ResolveMarkdownStyle() ?? MarkdownStyle.Default;
				bool zwj = Helpers.TerminalCapabilities.SupportsZwjLigation;
				int version;
				lock (_contentLock) { version = _contentVersion; }
				var key = new ParseKey(version, int.MaxValue, fg, bg, md, _wrap, zwj);
				if (_naturalWidthKey == key) return _naturalWidth;
				List<string> snapshot;
				lock (_contentLock) { snapshot = _content.ToList(); version = _contentVersion; }
				key = new ParseKey(version, int.MaxValue, fg, bg, md, _wrap, zwj);
				var groups = BuildParseGroups(snapshot);
				_groupGeneration++;
				bool dynamic = false;
				int max = 0;
				foreach (var (text, sourceIndex) in groups)
				{
					bool groupDynamic = IsDynamicLine(text);
					dynamic |= groupDynamic;
					var source = GetSource(text, sourceIndex, fg, bg, md, zwj, groupDynamic);
					if (source.NaturalWidth is not { } width)
					{
						width = 0;
						var chunks = RenderChunks(source, int.MaxValue);
						foreach (var expanded in chunks)
						{
							var chunk = GetTextChunk(source, expanded);
							if (chunk.NaturalWidth == null)
							{
								CountParse();
								chunk.NaturalWidth = MarkupParser.MeasureExpandedWidth(expanded, fg, bg);
							}
							width = Math.Max(width, chunk.NaturalWidth.Value);
						}
						RetireTextChunks(source, chunks.Count);
						source.NaturalWidth = width;
					}
					max = Math.Max(max, width);
				}
				if (dynamic && HasInlineSpinner(snapshot)) MarkupSpinnerClock.RegisterHost(this);
				RetireObsoleteGroups(groups);
				PruneGroupCache(groups.Count);
				_naturalWidthKey = dynamic ? null : key;
				_naturalWidth = max;
				return max;
			}
		}

		private int MeasureSourceLinkCount()
		{
			// Match the parse -> content lock order used by measurement. Focus discovery must not
			// perform its own full Markdown conversion while holding the content lock.
			lock (_parseCacheLock)
			{
				List<string> snapshot;
				int version;
				lock (_contentLock)
				{
					if (_cachedLinkCount.HasValue) return _cachedLinkCount.Value;
					snapshot = _content.ToList();
					version = _contentVersion;
				}
				var (fg, bg) = ResolveParseColors();
				var md = ResolveMarkdownStyle() ?? MarkdownStyle.Default;
				bool zwj = Helpers.TerminalCapabilities.SupportsZwjLigation;
				var groups = BuildParseGroups(snapshot);
				_groupGeneration++;
				int total = 0;
				foreach (var (text, sourceIndex) in groups)
				{
					var source = GetSource(text, sourceIndex, fg, bg, md, zwj, IsDynamicLine(text));
					var chunks = RenderChunks(source, int.MaxValue);
					foreach (var expanded in chunks)
					{
						var chunk = GetTextChunk(source, expanded);
						chunk.LinkCount ??= MarkupParser.CountLinks(expanded);
						total += chunk.LinkCount.Value;
					}
					RetireTextChunks(source, chunks.Count);
				}
				RetireObsoleteGroups(groups);
				PruneGroupCache(groups.Count);
				lock (_contentLock)
				{
					if (_contentVersion == version) _cachedLinkCount = total;
				}
				return total;
			}
		}

		private static List<(string text, int sourceIndex)> BuildParseGroups(List<string> snapshot)
		{
			// One group per entry is the common case (a group only spans several entries when an open
			// [markdown] region does), so size for that and let the rare coalescing case shrink it.
			var groups = new List<(string text, int sourceIndex)>(snapshot.Count);
			for (int g = 0; g < snapshot.Count;)
			{
				int start = g;
				if (!MarkupParser.HasUnclosedMarkdownRegion(snapshot[g]))
				{
					groups.Add((snapshot[g], start));
					g++;
					continue;
				}
				var sb = new System.Text.StringBuilder(snapshot[g]);
				bool open = true;
				while (open && g + 1 < snapshot.Count)
				{
					string next = snapshot[++g];
					sb.Append('\n').Append(next);
					int close = next.IndexOf("[/]", StringComparison.Ordinal);
					if (close >= 0) open = MarkupParser.HasUnclosedMarkdownRegion(next, close + 3);
				}
				groups.Add((sb.ToString(), start));
				g++;
			}
			return groups;
		}
	}
}
