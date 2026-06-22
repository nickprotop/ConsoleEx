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
	public partial class MarkupControl
	{
		// Process-wide count of full line-parses performed by MarkupParser via this control's
		// paint/measure paths. Test-only: lets a test assert the cache eliminated re-parsing.
		private static long _parseCount = 0;

		/// <summary>Test-only: total markup line-parses performed across all MarkupControls.</summary>
		internal static long GetParseCountForTest() => Interlocked.Read(ref _parseCount);

		/// <summary>Increment the parse counter once per logical line parsed.</summary>
		private static void CountParse() => Interlocked.Increment(ref _parseCount);

		// Bumped on every content mutation; part of the parse-cache key so any content change is a miss.
		// Interlocked/Volatile so a background-thread content mutation (CLAUDE.md rule 13) cannot tear or
		// be reordered against the UI-thread cache-key read in EnsureParsed.
		private int _contentVersion = 0;

		/// <summary>Test-only: current content version (bumped on each content mutation).</summary>
		internal int GetContentVersionForTest() => Volatile.Read(ref _contentVersion);

		/// <summary>Invalidate the parse cache by advancing the content version. Call from every content mutator.</summary>
		private void BumpContentVersion() => Interlocked.Increment(ref _contentVersion);

		/// <summary>The inputs a parse depends on. Equality of two keys ⇒ identical parse output ⇒ cache hit.</summary>
		private readonly record struct ParseKey(
			int ContentVersion,
			int RenderWidth,
			Color Fg,
			Color Bg,
			MarkdownStyle? MdStyle,
			bool Wrap);

		/// <summary>
		/// The cached result of parsing the whole content: one cell-row per display row, the source
		/// (logical-line) index of each display row, the link spans per display row, and per-logical-line
		/// row counts with their total (a prefix-sum gives O(log N) display-row → logical-line mapping).
		/// </summary>
		internal sealed class ParsedContent
		{
			public required List<List<Cell>> Rows { get; init; }
			public required List<int> RowSourceLine { get; init; }
			public required List<List<Parsing.LinkSpan>> RowLinks { get; init; }
			public required int[] LineRowCounts { get; init; }
			public required int[] RowPrefix { get; init; } // RowPrefix[i] = sum(LineRowCounts[0..i)); length = LineRowCounts.Length + 1
			public int TotalRows => RowPrefix.Length > 0 ? RowPrefix[^1] : 0;
		}

		private ParseKey? _cacheKey;
		private ParsedContent? _cached;

		/// <summary>True if a logical line carries inline dynamic markup that must re-parse every frame.</summary>
		private static bool IsDynamicLine(string line)
			=> line.Contains("[spinner", StringComparison.Ordinal)
			|| line.Contains("[gradient", StringComparison.Ordinal);

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
			List<string> snapshot;
			int version;
			lock (_contentLock) { snapshot = _content.ToList(); version = Volatile.Read(ref _contentVersion); }

			var key = new ParseKey(version, renderWidth, fg, bg, md, wrap);
			bool dynamic = HasDynamicContent(snapshot);
			if (!dynamic && _cached != null && _cacheKey == key)
				return _cached;

			var rows = new List<List<Cell>>();
			var rowSource = new List<int>();
			var rowLinks = new List<List<Parsing.LinkSpan>>();
			var lineRowCounts = new int[snapshot.Count];

			for (int sourceIndex = 0; sourceIndex < snapshot.Count; sourceIndex++)
			{
				string line = snapshot[sourceIndex];
				int before = rows.Count;

				if (wrap && renderWidth > 0)
				{
					CountParse();
					var wrapped = Parsing.MarkupParser.ParseLines(line, renderWidth, fg, bg, out var wrappedLinks, md);
					for (int w = 0; w < wrapped.Count; w++)
					{
						rows.Add(wrapped[w]);
						rowLinks.Add(w < wrappedLinks.Count ? wrappedLinks[w] : new List<Parsing.LinkSpan>());
						rowSource.Add(sourceIndex);
					}
				}
				else
				{
					foreach (var subLine in line.Split(["\r\n", "\r", "\n"], StringSplitOptions.None))
					{
						CountParse();
						var cells = Parsing.MarkupParser.Parse(subLine, fg, bg, out var lineLinks, md);
						rows.Add(cells);
						rowLinks.Add(lineLinks);
						rowSource.Add(sourceIndex);
					}
				}

				lineRowCounts[sourceIndex] = rows.Count - before;
			}

			var prefix = new int[snapshot.Count + 1];
			for (int i = 0; i < snapshot.Count; i++)
				prefix[i + 1] = prefix[i] + lineRowCounts[i];

			var result = new ParsedContent
			{
				Rows = rows,
				RowSourceLine = rowSource,
				RowLinks = rowLinks,
				LineRowCounts = lineRowCounts,
				RowPrefix = prefix,
			};

			if (dynamic)
			{
				_cached = null;
				_cacheKey = null;
			}
			else
			{
				_cached = result;
				_cacheKey = key;
			}
			return result;
		}

		/// <summary>
		/// Resolves the effective foreground/background the parser is keyed on. MUST be used by BOTH
		/// MeasureDOM and PaintDOM so they compute the same ParseKey and share one cache entry. The
		/// fallback foreground passed in is paint's local default (container/theme derived); measure
		/// passes the same role-resolved value so the keys match.
		/// </summary>
		private (Color fg, Color bg) ResolveParseColors(Color fallbackFg)
		{
			Color fg = _foregroundColor
				?? Helpers.ColorResolver.ColorRoleForeground(ColorRole, Container, Outline, mode: ColorRoleMode)
				?? fallbackFg;
			Color bg = _backgroundColor ?? Color.Transparent;
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

			List<string> snapshot;
			lock (_contentLock) { snapshot = _content.ToList(); }

			int natural = 0;
			for (int i = 0; i < snapshot.Count; i++)
			{
				// A [markdown] region restructures into a block whose RENDERED width is not its source
				// StripLength (fenced code blocks, rules, tables expand). Clamping to a wrong natural width
				// would parse the block too narrow and corrupt it, so don't fit-clamp such content — parse
				// at the full available width (measure & paint then key on their own width; a stable-width
				// repaint still hits its own cache, which is the path that matters for #42).
				if (snapshot[i].Contains("[markdown]", StringComparison.Ordinal))
					return availableContentWidth;
				natural = Math.Max(natural, Parsing.MarkupParser.StripLength(snapshot[i]));
			}

			return Math.Min(natural, availableContentWidth);
		}

		/// <summary>Test-only: drive EnsureParsed with default colors/style to exercise the cache.</summary>
		internal ParsedContent EnsureParsedForTest(int width)
			=> EnsureParsed(width, Color.White, Color.Transparent, ResolveMarkdownStyle(), _wrap);
	}
}
