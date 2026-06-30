// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Decides line-break opportunities between two <see cref="LineBreakClass"/> values per the
	/// terminal-relevant subset of UAX #14. Pure; consulted at each candidate break point by the wrap loop.
	/// </summary>
	internal static class LineBreaker
	{
		/// <summary>
		/// True if a line break is allowed BETWEEN a run of class <paramref name="prev"/> and the following
		/// run of class <paramref name="next"/>. Unlisted pairs default to NO break (keeps tokens whole; the
		/// caller's hard-break-at-width fallback guarantees forward progress for unbreakable runs).
		/// </summary>
		public static bool MayBreakBetween(LineBreakClass prev, LineBreakClass next)
		{
			// XX resolves as AL (keep unknown runs whole).
			if (prev == LineBreakClass.XX) prev = LineBreakClass.AL;
			if (next == LineBreakClass.XX) next = LineBreakClass.AL;

			// LB7/LB9/LB11: never break before combining marks, word joiners, or glue; or after glue/WJ.
			if (next == LineBreakClass.CM || next == LineBreakClass.WJ || next == LineBreakClass.GL)
				return false;
			if (prev == LineBreakClass.WJ || prev == LineBreakClass.GL)
				return false;

			// LB8: break after a zero-width space.
			if (prev == LineBreakClass.ZW) return true;

			// CJK (ID) breaks freely on either side.
			if (prev == LineBreakClass.ID || next == LineBreakClass.ID) return true;

			// LB18: break after a space.
			if (prev == LineBreakClass.SP) return true;
			// LB7: never break before a space (the space ends the line; trimmed by the caller).
			if (next == LineBreakClass.SP) return false;

			// LB14: no break after open punctuation.
			if (prev == LineBreakClass.OP) return false;
			// LB13: no break before close/infix/symbol punctuation.
			// Exception: after IS (comma/semicolon acting as list separator) a break before QU or AL is
			// a valid opportunity — e.g. `,"next_key"` or `; value` in structured text.
			if (prev == LineBreakClass.IS && (next == LineBreakClass.QU || next == LineBreakClass.AL))
				return true;
			if (next == LineBreakClass.CL || next == LineBreakClass.QU
				|| next == LineBreakClass.IS || next == LineBreakClass.SY)
				return false;

			// LB21: break after a hyphen or break-after class.
			if (prev == LineBreakClass.HY || prev == LineBreakClass.BA) return true;

			// LB23/LB25: numbers bind to letters, infix separators, and currency affixes.
			if (prev == LineBreakClass.AL && next == LineBreakClass.NU) return false;
			if (prev == LineBreakClass.NU && next == LineBreakClass.AL) return false;
			if (prev == LineBreakClass.NU && next == LineBreakClass.NU) return false;
			if (prev == LineBreakClass.NU && next == LineBreakClass.IS) return false;
			if (prev == LineBreakClass.IS && next == LineBreakClass.NU) return false;
			if (prev == LineBreakClass.PR && next == LineBreakClass.NU) return false;
			if (prev == LineBreakClass.NU && next == LineBreakClass.PO) return false;

			// LB28: no break between letters.
			if (prev == LineBreakClass.AL && next == LineBreakClass.AL) return false;

			// Default: keep tokens whole.
			return false;
		}
	}
}
