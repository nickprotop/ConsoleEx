// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Parsing;

namespace SharpConsoleUI.Controls
{
	public partial class MarkupControl : MarkupSpinnerClock.IInlineSpinnerHost
	{
		/// <summary>
		/// Repaints this control because an inline <c>[spinner]</c> advanced a frame. Repaint (not
		/// Relayout): a spinner's reserved width is constant per style, so the glyph can never reflow.
		/// </summary>
		void MarkupSpinnerClock.IInlineSpinnerHost.InvalidateForInlineSpinner()
			=> Invalidate(Invalidation.Repaint);

		// Process-wide count of full line-parses performed by MarkupParser via this control's
		// paint/measure paths. Test-only: lets a test assert the cache eliminated re-parsing.
		private static long _parseCount = 0;

		/// <summary>Test-only: total markup line-parses performed across all MarkupControls.</summary>
		internal static long GetParseCountForTest() => Interlocked.Read(ref _parseCount);

		/// <summary>Process-wide count of markup line-parses performed by all MarkupControls. A diagnostic:
		/// it stops climbing once the parse cache is warm (scrolling/drag-selecting static content) and only
		/// advances on a real content/width/colour change.</summary>
		public static long TotalParseCount => Interlocked.Read(ref _parseCount);

		/// <summary>Increment the parse counter once per logical line parsed.</summary>
		private static void CountParse() => Interlocked.Increment(ref _parseCount);

		// Bumped on every content mutation; part of the parse-cache key so any content change is a miss.
		// Interlocked/Volatile so a background-thread content mutation (CLAUDE.md rule 13) cannot tear or
		// be reordered against the UI-thread cache-key read in EnsureParsed.
		private int _contentVersion = 0;

		/// <summary>Test-only: current content version (bumped on each content mutation).</summary>
		internal int GetContentVersionForTest() => Volatile.Read(ref _contentVersion);

		/// <summary>Test-only: a snapshot of the current logical content lines (one per <c>_content</c> entry).
		/// Lets a test assert that a multi-line <c>[markdown]…[/]</c> region is kept as ONE entry (issue #59).</summary>
		internal IReadOnlyList<string> GetContentLinesForTest()
		{
			lock (_contentLock) { return _content.ToList(); }
		}

		/// <summary>Invalidate the parse cache by advancing the content version. Call from every content mutator.</summary>
		private void BumpContentVersion() => Interlocked.Increment(ref _contentVersion);

		/// <summary>The inputs a parse depends on. Equality of two keys ⇒ identical parse output ⇒ cache hit.</summary>
		private readonly record struct ParseKey(
			int ContentVersion,
			int RenderWidth,
			Color Fg,
			Color Bg,
			MarkdownStyle? MdStyle,
			bool Wrap,
			bool ZwjLigation);

		/// <summary>
		/// The cached result of parsing the whole content: one cell-row per display row, the source
		/// (logical-line) index of each display row, the link spans per display row, and per-logical-line
		/// row counts with their total (a prefix-sum gives O(log N) display-row → logical-line mapping).
		/// </summary>
		internal sealed class ParsedContent
		{
			public required List<List<Cell>> Rows { get; init; }
			public required List<int> RowSourceLine { get; init; }
			public required List<bool> RowIsSoftWrapContinuation { get; init; }
			public required List<List<Parsing.LinkSpan>> RowLinks { get; init; }
			public required int[] LineRowCounts { get; init; }
			public required int[] RowPrefix { get; init; } // RowPrefix[i] = sum(LineRowCounts[0..i)); length = LineRowCounts.Length + 1
			public int TotalRows => RowPrefix.Length > 0 ? RowPrefix[^1] : 0;
		}

		// Small bounded LRU. The SPC overflow probe measures children at TWO widths per layout pass
		// (full viewport and viewport-minus-scrollbar) before arranging at one of them, so a single
		// slot is evicted on every paint. A 4-slot ring keeps every recently-used width hot, so the
		// full measure→narrow-measure→paint sequence completes with three hits after warmup.
		private const int ParseCacheCapacity = 4;
		private readonly ParseKey?[] _cacheKeys = new ParseKey?[ParseCacheCapacity];
		private readonly ParsedContent?[] _cacheEntries = new ParsedContent?[ParseCacheCapacity];
		private int _cacheNextSlot = 0;

		/// <summary>Most-recently produced parse, used by Selection.GetRowCellsForCopy to resolve
		/// off-screen rows during a multi-row copy. Holds the same reference as the newest LRU slot.</summary>
		private ParsedContent? _cached;

		// ---- Second cache level: parsed rows per GROUP ----
		//
		// The LRU above is keyed on the content VERSION, which every mutator bumps, so appending one
		// line to a scrollback invalidated the parse of the entire buffer: at 20,000 lines that was
		// 40,002 line-parses and ~269 MB of allocation because one line arrived. (40,002 rather than
		// 20,001 because the scroll-panel overflow probe measures children at two widths — full
		// viewport and viewport-minus-scrollbar — so a bumped version missed at both before the paint
		// hit, and NaturalContentWidth probes a third width.)
		//
		// Keying a second level on the group's own TEXT fixes that without changing what the LRU does:
		// an L1 miss still rebuilds the flat row list, but every group whose text did not change is
		// assembled from rows that are already parsed. Only genuinely new text reaches the parser.
		// The two levels answer different questions — L1 "is this whole frame unchanged?" (O(1)), L2
		// "has this line been parsed before, at this width?" — and both are needed: without L1 an
		// unchanged frame would walk every group, and without L2 a one-line append re-parses all of
		// them.
		//
		// Accessed without a lock, matching the LRU beside it: only _content is guarded, and both
		// caches are read and written on the render path.
		private readonly record struct GroupKey(
			string Text,
			int ParseWidth,
			Color Fg,
			Color Bg,
			MarkdownStyle? Md,
			bool ZwjLigation);

		/// <summary>One group's parse output: the rows it produced, their links, and their soft-wrap flags.</summary>
		private sealed class ParsedGroup
		{
			public required List<List<Cell>> Rows { get; init; }
			public required List<List<Parsing.LinkSpan>> Links { get; init; }
			public required List<bool> SoftWrap { get; init; }
			/// <summary>Build number this entry was last used by; drives eviction.</summary>
			public int Generation;
		}

		private readonly Dictionary<GroupKey, ParsedGroup> _groupCache = new();
		private int _groupGeneration;

		// Whether the content holds dynamic markup, cached against the version that produced it. Without
		// this an unchanged frame rescanned every line for "[spinner"/"[gradient" before it was allowed
		// to consult the cache, which made a pure hit O(N).
		private int _dynamicVersion = -1;
		private bool _dynamicFlag;

		// Whether any line opens a [markdown] region, cached the same way and for the same reason:
		// ComputeParseWidth asks on every measure and every paint.
		private int _markdownVersion = -1;
		private bool _markdownFlag;

		/// <summary>
		/// True when the content contains a <c>[markdown]</c> region, answered from a per-version cache
		/// so a repaint that changed nothing does not copy and rescan the whole buffer.
		/// </summary>
		private bool HasMarkdownRegion()
		{
			int version;
			lock (_contentLock) { version = Volatile.Read(ref _contentVersion); }
			if (_markdownVersion == version)
				return _markdownFlag;

			List<string> snapshot;
			lock (_contentLock) { snapshot = _content.ToList(); version = Volatile.Read(ref _contentVersion); }

			bool found = false;
			for (int i = 0; i < snapshot.Count && !found; i++)
				found = snapshot[i].Contains("[markdown]", StringComparison.Ordinal);

			_markdownFlag = found;
			_markdownVersion = version;
			return found;
		}

		/// <summary>True if a logical line carries inline dynamic markup that must re-parse every frame.</summary>
		private static bool IsDynamicLine(string line)
			=> line.Contains("[spinner", StringComparison.Ordinal)
			|| line.Contains("[gradient", StringComparison.Ordinal);

		/// <summary>True if ANY content line carries an inline <c>[spinner]</c> (animated, unlike [gradient]).</summary>
		private static bool HasInlineSpinner(List<string> snapshot)
		{
			for (int i = 0; i < snapshot.Count; i++)
				if (snapshot[i].Contains("[spinner", StringComparison.Ordinal)) return true;
			return false;
		}

		/// <summary>True if ANY content line is dynamic — such content can never serve from the cache.</summary>
		private static bool HasDynamicContent(List<string> snapshot)
		{
			for (int i = 0; i < snapshot.Count; i++)
				if (IsDynamicLine(snapshot[i])) return true;
			return false;
		}

		/// <summary>
		/// Returns the parsed content for the given render inputs, reusing the cache on an exact key match
		/// (and when the content has no dynamic lines). On a miss it parses the whole content, captures the
		/// row counts, and stores the result. The parse increments the global parse counter once per
		/// logical line (so a hit shows zero new parses).
		/// </summary>
		private ParsedContent EnsureParsed(int renderWidth, Color fg, Color bg, MarkdownStyle? md, bool wrap)
		{
			bool zwj = Helpers.TerminalCapabilities.SupportsZwjLigation;

			// Fast path: take only the version under the lock. A frame that changed nothing must not
			// copy the content list nor rescan it for dynamic markup — at 20,000 lines those two steps
			// alone cost ~650 KB of garbage and a full scan on every repaint, before the cache lookup
			// they were guarding had even been reached.
			int version;
			lock (_contentLock) { version = Volatile.Read(ref _contentVersion); }

			if (_dynamicVersion == version && !_dynamicFlag)
			{
				var hotKey = new ParseKey(version, renderWidth, fg, bg, md, wrap, zwj);
				for (int i = 0; i < ParseCacheCapacity; i++)
				{
					if (_cacheEntries[i] != null && _cacheKeys[i] == hotKey)
						return _cacheEntries[i]!;
				}
			}

			List<string> snapshot;
			lock (_contentLock) { snapshot = _content.ToList(); version = Volatile.Read(ref _contentVersion); }

			var key = new ParseKey(version, renderWidth, fg, bg, md, wrap, zwj);
			bool dynamic = HasDynamicContent(snapshot);
			_dynamicVersion = version;
			_dynamicFlag = dynamic;

			// An inline [spinner] is driven by elapsed time, not by a property change, so nothing else
			// would ever mark this control dirty — and the render loop skips a clean window. Register so
			// MarkupSpinnerClock.Tick can invalidate us each frame. Registration is idempotent and held
			// weakly, so repeating it per parse costs nothing and needs no teardown. [gradient] is also
			// "dynamic" for cache purposes but is not animated, so it is deliberately not registered.
			if (dynamic && HasInlineSpinner(snapshot))
				MarkupSpinnerClock.RegisterHost(this);

			if (!dynamic)
			{
				for (int i = 0; i < ParseCacheCapacity; i++)
				{
					if (_cacheEntries[i] != null && _cacheKeys[i] == key)
						return _cacheEntries[i]!;
				}
			}

			var rows = new List<List<Cell>>();
			var rowSource = new List<int>();
			var rowSoftWrap = new List<bool>();
			var rowLinks = new List<List<Parsing.LinkSpan>>();
			var lineRowCounts = new int[snapshot.Count];

			// Coalesce consecutive entries that form ONE open [markdown] region into a single parse
			// group, so a multi-line / streamed [markdown] block (built via AppendLine / AppendText /
			// streaming, where it spans several _content entries) parses as a unit and renders correctly.
			// The common case — an entry that does not leave a [markdown] open — forms a group of exactly
			// one entry, identical to per-entry parsing. [markdown]-only by design (see the streaming-
			// markdown-coalescing spec). All rows of a group are attributed to the group's FIRST entry,
			// matching how a multi-line [markdown] set via Text= is already attributed (one source index).
			var groups = new List<(string text, int sourceIndex)>();
			for (int g = 0; g < snapshot.Count;)
			{
				int start = g;

				// Fast path: an entry that closes whatever it opened forms a group of exactly one and
				// can be used as-is. Building a StringBuilder and calling ToString() for every entry
				// made even this common case allocate once per line, which is O(N) work on a rebuild
				// before a single character has been parsed.
				if (!Parsing.MarkupParser.HasUnclosedMarkdownRegion(snapshot[g]))
				{
					groups.Add((snapshot[g], start));
					g++;
					continue;
				}

				var sb = new System.Text.StringBuilder(snapshot[g]);
				while (Parsing.MarkupParser.HasUnclosedMarkdownRegion(sb.ToString()) && g + 1 < snapshot.Count)
				{
					g++;
					sb.Append('\n').Append(snapshot[g]);
				}
				groups.Add((sb.ToString(), start));
				g++;
			}

			// Non-wrap parses at an unconstrained width so word-wrap is disabled and the only breaks are
			// explicit newlines (parse-then-cut, so an open tag carries its style across them). Both
			// branches otherwise made the same call, so the width is the only difference and the parse
			// output depends on exactly these inputs — which is what makes them a sound cache key.
			int parseWidth = (wrap && renderWidth > 0) ? renderWidth : int.MaxValue;
			_groupGeneration++;

			foreach (var (text, sourceIndex) in groups)
			{
				int before = rows.Count;

				// A group carrying inline dynamic markup renders differently every frame, so it is
				// neither served from nor written to the cache — per group rather than per control, so
				// one spinner no longer forces the static lines beside it to re-parse.
				bool groupDynamic = IsDynamicLine(text);
				var groupKey = new GroupKey(text, parseWidth, fg, bg, md, zwj);

				ParsedGroup? parsedGroup = null;
				if (!groupDynamic && _groupCache.TryGetValue(groupKey, out var hit))
				{
					hit.Generation = _groupGeneration;
					parsedGroup = hit;
				}

				if (parsedGroup == null)
				{
					CountParse();
					var parsed = Parsing.MarkupParser.ParseLines(text, parseWidth, fg, bg, out var parsedLinks, out var parsedSoft, md);

					var groupRows = new List<List<Cell>>(parsed.Count);
					var groupLinks = new List<List<Parsing.LinkSpan>>(parsed.Count);
					var groupSoft = new List<bool>(parsed.Count);
					for (int w = 0; w < parsed.Count; w++)
					{
						groupRows.Add(parsed[w]);
						groupLinks.Add(w < parsedLinks.Count ? parsedLinks[w] : new List<Parsing.LinkSpan>());
						groupSoft.Add(w < parsedSoft.Count && parsedSoft[w]);
					}

					parsedGroup = new ParsedGroup { Rows = groupRows, Links = groupLinks, SoftWrap = groupSoft, Generation = _groupGeneration };
					if (!groupDynamic)
						_groupCache[groupKey] = parsedGroup;
				}

				// Rows are shared by reference with the cache entry, exactly as the LRU already shares
				// them between frames: parse output is treated as immutable, and paint composes onto the
				// buffer rather than into these lists.
				for (int w = 0; w < parsedGroup.Rows.Count; w++)
				{
					rows.Add(parsedGroup.Rows[w]);
					rowLinks.Add(parsedGroup.Links[w]);
					rowSource.Add(sourceIndex);
					rowSoftWrap.Add(parsedGroup.SoftWrap[w]);
				}

				// Rows are recorded against the group's FIRST entry. Absorbed entries (2nd+ in the group)
				// keep lineRowCounts == 0, so the RowPrefix prefix-sum stays consistent.
				lineRowCounts[sourceIndex] = rows.Count - before;
			}

			PruneGroupCache(groups.Count);

			var prefix = new int[snapshot.Count + 1];
			for (int i = 0; i < snapshot.Count; i++)
				prefix[i + 1] = prefix[i] + lineRowCounts[i];

			var result = new ParsedContent
			{
				Rows = rows,
				RowSourceLine = rowSource,
				RowIsSoftWrapContinuation = rowSoftWrap,
				RowLinks = rowLinks,
				LineRowCounts = lineRowCounts,
				RowPrefix = prefix,
			};

			if (dynamic)
			{
				for (int i = 0; i < ParseCacheCapacity; i++) { _cacheKeys[i] = null; _cacheEntries[i] = null; }
			}
			else
			{
				int slot = _cacheNextSlot;
				_cacheKeys[slot] = key;
				_cacheEntries[slot] = result;
				_cacheNextSlot = (slot + 1) % ParseCacheCapacity;
			}
			_cached = result;
			return result;
		}

		/// <summary>
		/// Bounds the per-group cache. Entries are kept for a few builds so the widths a single frame
		/// asks for all stay hot — the scroll-panel overflow probe measures at the viewport width and
		/// again at viewport-minus-scrollbar, and <see cref="NaturalContentWidth"/> probes a third at
		/// <c>int.MaxValue</c>. Evicting per build would make those widths evict each other and
		/// re-parse the buffer every frame, which is the defect this cache exists to remove.
		/// <para>
		/// Past the budget, anything the recent builds did not touch goes: that is what retires lines
		/// scrolled out of a bounded scrollback. If everything is recent the cache is cleared outright
		/// rather than allowed to grow without bound — a correctness-preserving reset, since a miss
		/// only costs a re-parse.
		/// </para>
		/// </summary>
		private void PruneGroupCache(int liveGroups)
		{
			int budget = Math.Max(512, liveGroups * 4);
			if (_groupCache.Count <= budget)
				return;

			int cutoff = _groupGeneration - 3;
			var stale = new List<GroupKey>();
			foreach (var entry in _groupCache)
			{
				if (entry.Value.Generation < cutoff)
					stale.Add(entry.Key);
			}
			foreach (var key in stale)
				_groupCache.Remove(key);

			if (_groupCache.Count > budget * 2)
				_groupCache.Clear();
		}

		/// <summary>
		/// Resolves the effective foreground/background the parser is keyed on. MUST be used by BOTH
		/// MeasureDOM and PaintDOM so they compute the same ParseKey and share one cache entry. The
		/// fallback chain is fully deterministic (explicit → role → container → theme → White) and does
		/// NOT depend on a caller-supplied default — measure has no painter-supplied default fg, so any
		/// such caller-supplied value would diverge from paint and evict the cache every frame.
		/// </summary>
		private (Color fg, Color bg) ResolveParseColors()
		{
			Color fg = _foregroundColor
				?? Helpers.ColorResolver.ColorRoleForeground(ColorRole, Container, Outline, mode: ColorRoleMode)
				?? Helpers.ColorResolver.ResolveForeground(null, Container, Color.White);
			// THE CONTAINER IS THE FALLBACK, not Transparent. A MarkupControl inside a container with a
			// background painted its TEXT cells transparent, so the container's colour showed only
			// where the control drew nothing — the glyphs sat in a hole the shape of the text. The
			// symptom is a markup row whose background stops and starts around its own words, and it
			// forced callers into workarounds: per-run `on <colour>` tags in the markup, [fillwidth]
			// markers, and wrapper grids, all to repaint what the container had already established.
			//
			// Transparent stays the last resort, for a control with no background and no container.
			Color bg = _backgroundColor ?? Container?.BackgroundColor ?? Color.Transparent;
			return (fg, bg);
		}

		/// <summary>
		/// The width the content is PARSED at — keyed by both MeasureDOM and PaintDOM so they hit one cache
		/// entry. MeasureDOM runs with the full available content width (it may be wider than the control's
		/// final fitted width); PaintDOM runs at the already-fitted bounds width. For a non-Stretch control
		/// whose content fits, both reduce to the natural content width (max StripLength) here, so the parse
		/// width — and thus the ParseKey — agree. Stretch fills, so it parses at the available width on both.
		/// StripLength is cheap and width-independent (no full parse, no counter bump), so this stays O(lines).
		/// </summary>
		private int ComputeParseWidth(int availableContentWidth)
		{
			if (HorizontalAlignment == HorizontalAlignment.Stretch)
				return availableContentWidth;

			// A [markdown] region restructures into a block whose RENDERED width is not its source
			// StripLength (fenced code blocks, rules, tables expand). Clamping to a wrong natural width
			// would parse the block too narrow and corrupt it, so don't fit-clamp such content — parse
			// at the full available width (measure & paint then key on their own width; a stable-width
			// repaint still hits its own cache, which is the path that matters for #42).
			//
			// The answer is cached against the content version: this runs from both MeasureDOM and
			// PaintDOM, and copying the line list and scanning every line for "[markdown]" on each call
			// left an O(N) cost on frames where nothing had changed.
			if (HasMarkdownRegion())
				return availableContentWidth;

			// Natural width from the REAL parse, not StripLength. StripLength strips any [..] as a tag, but
			// literal brackets (JSON, [INFO] logs, array[0]) render as text — StripLength under-measured
			// them, collapsing the fit-clamp to a tiny width and wrapping the label to ~1 token per line
			// (#63). A cheap scan can't be render-consistent (deciding tag-vs-literal needs ParseTag), so
			// use the parse. Keyed at int.MaxValue → one stable cache entry, independent of the frame's
			// available width, so a stable repaint still re-parses nothing.
			return Math.Min(NaturalContentWidth(), availableContentWidth);
		}

		/// <summary>
		/// The rendered natural (unwrapped) content width in display columns: the max row width of the
		/// content parsed at an unconstrained width. Render-consistent (literal brackets counted, real tags
		/// consumed, CJK = 2 cols, [markdown] expanded). Backs both <see cref="ComputeParseWidth"/> and
		/// <see cref="ContentWidth"/>. Uses the LRU parse cache, so repeated reads don't re-parse.
		/// </summary>
		private int NaturalContentWidth()
		{
			var (fg, bg) = ResolveParseColors();
			var parsed = EnsureParsed(int.MaxValue, fg, bg, ResolveMarkdownStyle(), _wrap);
			int max = 0;
			foreach (var row in parsed.Rows)
				if (row.Count > max) max = row.Count;
			return max;
		}

		/// <summary>Test-only: drive EnsureParsed with default colors/style to exercise the cache.</summary>
		internal ParsedContent EnsureParsedForTest(int width)
		{
			var (fg, bg) = ResolveParseColors();
			return EnsureParsed(width, fg, bg, ResolveMarkdownStyle(), _wrap);
		}
	}
}
