// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using System.Text;
using Wcwidth;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides display width information for Unicode characters.
	/// Delegates to the Wcwidth library for accurate width calculation
	/// including zero-width characters (combining marks, variation selectors, ZWJ).
	/// Spacing Combining Marks (Unicode category Mc) are corrected to width 1,
	/// as they occupy visual space in terminals despite Wcwidth marking them zero-width.
	/// Adapts to terminal capabilities: uses Unicode 15.0 width tables unless the
	/// terminal is detected to support Unicode 16.0 widths (probed at startup).
	/// </summary>
	public static class UnicodeWidth
	{
		/// <summary>
		/// Returns the Wcwidth Unicode version to use based on terminal capabilities.
		/// Unicode 16.0 widened 86 codepoints (trigrams U+2630-2637, hexagrams U+4DC0-4DFF, etc.)
		/// from 1 to 2 columns. Most terminals haven't adopted this yet, so we default to 15.0.
		/// </summary>
		private static Unicode EffectiveUnicodeVersion
			=> TerminalCapabilities.SupportsUnicode16Widths
				? Unicode.Version_16_0_0
				: Unicode.Version_15_0_0;

		/// <summary>
		/// Returns the display width of a character in terminal columns (0, 1, or 2).
		/// Spacing Combining Marks (Mc) are corrected to width 1.
		/// </summary>
		public static int GetCharWidth(char c)
		{
			int w = UnicodeCalculator.GetWidth(c, EffectiveUnicodeVersion);
			if (w <= 0)
			{
				// Spacing Combining Marks (Mc) occupy visual space in terminals
				if (w == 0 && Rune.GetUnicodeCategory(new Rune(c)) == UnicodeCategory.SpacingCombiningMark)
					return 1;
				return 0;
			}
			return w;
		}

		/// <summary>
		/// Returns the display width of a Rune in terminal columns (0, 1, or 2).
		/// Spacing Combining Marks (Mc) are corrected to width 1.
		/// </summary>
		public static int GetRuneWidth(Rune r)
		{
			int w = UnicodeCalculator.GetWidth(r.Value, EffectiveUnicodeVersion);
			if (w <= 0)
			{
				// Spacing Combining Marks (Mc) occupy visual space in terminals
				if (w == 0 && Rune.GetUnicodeCategory(r) == UnicodeCategory.SpacingCombiningMark)
					return 1;
				return 0;
			}
			return w;
		}

		/// <summary>Returns true if the character occupies 2 terminal columns.</summary>
		public static bool IsWide(char c) => GetCharWidth(c) == 2;

		/// <summary>Returns true if the Rune occupies 2 terminal columns.</summary>
		public static bool IsWideRune(Rune r) => GetRuneWidth(r) == 2;

		/// <summary>Returns true if the Rune occupies 0 terminal columns (combining mark, etc.).</summary>
		public static bool IsZeroWidth(Rune r) => GetRuneWidth(r) == 0;

		/// <summary>
		/// Returns true if the rune is Variation Selector 16 (U+FE0F),
		/// which widens certain emoji from 1 to 2 terminal columns.
		/// </summary>
		public static bool IsVS16(Rune r) => r.Value == 0xFE0F;

		/// <summary>The Zero-Width Joiner (U+200D). A rune that immediately follows a ZWJ continues the
		/// current grapheme cluster and renders as part of the same glyph on ligating terminals.</summary>
		private const int Zwj = 0x200D;

		/// <summary>
		/// True if a rune continues the current grapheme cluster — i.e. the immediately preceding rune was a
		/// ZWJ and the terminal ligates ZWJ sequences. A continuation rune contributes 0 display columns.
		/// Gated on <see cref="TerminalCapabilities.SupportsZwjLigation"/>.
		/// </summary>
		private static bool IsClusterContinuation(bool prevWasZwj)
			=> prevWasZwj && TerminalCapabilities.SupportsZwjLigation;

		/// <summary>
		/// True if the rune starting at <paramref name="idx"/> belongs to the ZWJ cluster currently being
		/// consumed — either the just-consumed rune was a ZWJ (so this rune is a 0-width continuation), or
		/// this rune is itself a ZWJ that joins the preceding glyph to the next. Used by <see cref="TakeColumns"/>
		/// to avoid stopping a slice in the middle of a cluster once the column budget is reached.
		/// Gated on <see cref="TerminalCapabilities.SupportsZwjLigation"/>.
		/// </summary>
		private static bool NextRuneContinuesCluster(string s, int idx, bool prevWasZwj)
		{
			if (!TerminalCapabilities.SupportsZwjLigation) return false;
			if (prevWasZwj) return true; // upcoming rune is a 0-width cluster continuation
			if (idx >= s.Length) return false;
			return Rune.TryGetRuneAt(s, idx, out var next) && next.Value == Zwj;
		}

		/// <summary>
		/// Returns true if VS16 (U+FE0F) after this rune widens it from 1 to 2 columns.
		/// Uses the Wcwidth library's Vs16Table for accurate lookup.
		/// Returns false if the terminal does not support VS16 widening
		/// (detected at startup via TerminalCapabilities.Probe).
		/// </summary>
		public static bool IsVs16Widened(Rune baseRune)
			=> TerminalCapabilities.SupportsVS16Widening
				&& Vs16Table.GetTable(Unicode.Version_9_0_0).Find(baseRune.Value) == 1;

		/// <summary>
		/// Returns the total display width of a string, summing per-Rune widths.
		/// Zero-width characters contribute 0 to the total.
		/// VS16 (U+FE0F) after a widenable emoji adds 1 column (widening from 1 to 2).
		/// </summary>
		public static int GetStringWidth(string s)
		{
			if (string.IsNullOrEmpty(s))
				return 0;

			int w = 0;
			Rune? lastMeasured = null;
			bool prevWasZwj = false;
			foreach (var rune in s.EnumerateRunes())
			{
				if (IsVS16(rune) && lastMeasured != null && IsVs16Widened(lastMeasured.Value))
				{
					w += 1; // widen from 1→2
					lastMeasured = null;
					prevWasZwj = false;
					continue;
				}
				if (IsClusterContinuation(prevWasZwj))
				{
					// Part of the preceding ZWJ glyph — contributes no columns.
					prevWasZwj = (rune.Value == Zwj);
					continue;
				}
				int rw = GetRuneWidth(rune);
				if (rw > 0) lastMeasured = rune;
				w += rw;
				prevWasZwj = (rune.Value == Zwj);
			}
			return w;
		}

		/// <summary>
		/// Returns the character index (UTF-16 offset) at or before the given display column.
		/// A column that lands inside a wide char resolves to that char's start. Clamps to s.Length.
		/// Mirrors <see cref="GetStringWidth"/> so the two never disagree.
		/// </summary>
		public static int ColumnToCharOffset(string s, int column)
		{
			if (string.IsNullOrEmpty(s) || column <= 0) return 0;
			int col = 0;
			int charIndex = 0;
			Rune? lastMeasured = null;
			bool prevWasZwj = false;
			foreach (var rune in s.EnumerateRunes())
			{
				int rw;
				if (IsVS16(rune) && lastMeasured != null && IsVs16Widened(lastMeasured.Value))
				{
					rw = 1; lastMeasured = null;
				}
				else if (IsClusterContinuation(prevWasZwj))
				{
					rw = 0;
				}
				else
				{
					rw = GetRuneWidth(rune);
					if (rw > 0) lastMeasured = rune;
				}
				prevWasZwj = (rune.Value == Zwj);
				if (col + rw > column) return charIndex; // adding this rune would pass the target column
				col += rw;
				charIndex += rune.Utf16SequenceLength;
			}
			return charIndex; // column at/after end → end of string
		}

		/// <summary>
		/// Returns the display column at the given character index (UTF-16 offset).
		/// Equivalent to GetStringWidth(s.Substring(0, charOffset)) but without allocating.
		/// </summary>
		public static int CharOffsetToColumn(string s, int charOffset)
		{
			if (string.IsNullOrEmpty(s) || charOffset <= 0) return 0;
			if (charOffset >= s.Length) return GetStringWidth(s);
			int col = 0;
			int idx = 0;
			Rune? lastMeasured = null;
			bool prevWasZwj = false;
			foreach (var rune in s.EnumerateRunes())
			{
				// Stop before a rune that starts at/after the target, or that straddles it
				// (a charOffset landing mid-surrogate is not a rune boundary — exclude the partial rune).
				if (idx + rune.Utf16SequenceLength > charOffset) break;
				if (IsVS16(rune) && lastMeasured != null && IsVs16Widened(lastMeasured.Value))
				{
					col += 1; lastMeasured = null;
				}
				else if (IsClusterContinuation(prevWasZwj))
				{
					// 0 columns
				}
				else
				{
					int rw = GetRuneWidth(rune);
					if (rw > 0) lastMeasured = rune;
					col += rw;
				}
				prevWasZwj = (rune.Value == Zwj);
				idx += rune.Utf16SequenceLength;
			}
			return col;
		}

		/// <summary>
		/// Width-aware slice primitive for wrapping: starting at character index <paramref name="startChar"/>,
		/// consumes whole runes while the total stays within <paramref name="maxColumns"/>. Returns the END
		/// character index and the display columns actually consumed. Guarantees forward progress: a single
		/// rune wider than maxColumns is still taken whole (so wrap loops can never stall).
		/// </summary>
		public static (int endChar, int width) TakeColumns(string s, int startChar, int maxColumns)
		{
			if (string.IsNullOrEmpty(s) || startChar >= s.Length) return (s?.Length ?? 0, 0);
			int col = 0;
			int idx = startChar;
			Rune? lastMeasured = null;
			bool prevWasZwj = false;
			bool tookAny = false;
			while (idx < s.Length)
			{
				int rw;
				int advance;
				if (Rune.TryGetRuneAt(s, idx, out var rune))
				{
					if (IsVS16(rune) && lastMeasured != null && IsVs16Widened(lastMeasured.Value))
					{
						rw = 1; lastMeasured = null;
					}
					else if (IsClusterContinuation(prevWasZwj))
					{
						rw = 0;
					}
					else
					{
						rw = GetRuneWidth(rune);
						if (rw > 0) lastMeasured = rune;
					}
					prevWasZwj = (rune.Value == Zwj);
					advance = rune.Utf16SequenceLength;
				}
				else
				{
					// Lone/unpaired UTF-16 surrogate (e.g. transient state while typing an
					// emoji): treat it as a width-1 replacement glyph and advance one code
					// unit so the loop always makes forward progress and never throws.
					rw = 1; advance = 1; lastMeasured = null; prevWasZwj = false;
				}
				if (tookAny && col + rw > maxColumns) break; // would overflow - stop (but always take >= 1 rune)
				col += rw;
				idx += advance;
				tookAny = true;
				// Reached the column budget, but if the next rune begins/continues a ZWJ cluster
				// (a ZWJ itself, or a 0-width continuation rune after one) keep consuming — those runes
				// are part of the glyph just taken and must not spill into the next slice.
				if (col >= maxColumns && !NextRuneContinuesCluster(s, idx, prevWasZwj)) break;
			}
			return (idx, col);
		}
	}
}
