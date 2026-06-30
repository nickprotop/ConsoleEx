// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class LineBreakerRuleTests
{
	private static bool B(LineBreakClass a, LineBreakClass b) => LineBreaker.MayBreakBetween(a, b);

	// NO-BREAK pairs (the core fixes).
	[Theory]
	[InlineData(LineBreakClass.AL, LineBreakClass.AL)] // don't split words
	[InlineData(LineBreakClass.NU, LineBreakClass.NU)] // don't split numbers
	[InlineData(LineBreakClass.AL, LineBreakClass.NU)] // length:4204 (AL NU)
	[InlineData(LineBreakClass.NU, LineBreakClass.AL)]
	[InlineData(LineBreakClass.NU, LineBreakClass.IS)] // 3.14 / 08:00 (NU . / NU :)
	[InlineData(LineBreakClass.IS, LineBreakClass.NU)]
	[InlineData(LineBreakClass.PR, LineBreakClass.NU)] // $5
	[InlineData(LineBreakClass.NU, LineBreakClass.PO)] // 5%
	[InlineData(LineBreakClass.AL, LineBreakClass.CM)] // combining attaches
	[InlineData(LineBreakClass.AL, LineBreakClass.WJ)] // word joiner
	[InlineData(LineBreakClass.GL, LineBreakClass.AL)] // glue (NBSP) no break after
	[InlineData(LineBreakClass.AL, LineBreakClass.GL)] // glue no break before
	[InlineData(LineBreakClass.OP, LineBreakClass.AL)] // no break after open bracket
	[InlineData(LineBreakClass.AL, LineBreakClass.CL)] // no break before close bracket
	[InlineData(LineBreakClass.AL, LineBreakClass.IS)] // no break before infix punct
	[InlineData(LineBreakClass.XX, LineBreakClass.XX)] // unknown runs stay whole (resolved as AL)
													   // Gap-fill: every remaining no-break branch in MayBreakBetween.
	[InlineData(LineBreakClass.WJ, LineBreakClass.AL)] // no break after word joiner
	[InlineData(LineBreakClass.AL, LineBreakClass.QU)] // no break before a quote (LB13)
	[InlineData(LineBreakClass.AL, LineBreakClass.SY)] // no break before '/' symbol (LB13)
	[InlineData(LineBreakClass.NU, LineBreakClass.SY)] // 1/2 — number then slash stays
	[InlineData(LineBreakClass.OP, LineBreakClass.NU)] // no break after open bracket before number
	[InlineData(LineBreakClass.OP, LineBreakClass.OP)] // no break after open bracket before open bracket
	[InlineData(LineBreakClass.AL, LineBreakClass.PO)] // no break before postfix percent on a word
	[InlineData(LineBreakClass.PR, LineBreakClass.AL)] // prefix currency before a letter — keep together
	[InlineData(LineBreakClass.CL, LineBreakClass.CL)] // )] runs — closers don't start a new line by default
	[InlineData(LineBreakClass.CM, LineBreakClass.AL)] // combining then letter — default no-break
	internal void NoBreak(LineBreakClass a, LineBreakClass b) => Assert.False(B(a, b));

	// BREAK-ALLOWED pairs.
	[Theory]
	[InlineData(LineBreakClass.AL, LineBreakClass.ID)] // before CJK
	[InlineData(LineBreakClass.ID, LineBreakClass.AL)] // after CJK
	[InlineData(LineBreakClass.ID, LineBreakClass.ID)] // between CJK
	[InlineData(LineBreakClass.SP, LineBreakClass.AL)] // after a space
	[InlineData(LineBreakClass.BA, LineBreakClass.AL)] // after soft hyphen
	[InlineData(LineBreakClass.ZW, LineBreakClass.AL)] // after zero-width space
	[InlineData(LineBreakClass.AL, LineBreakClass.ID)] // word then CJK
													   // Gap-fill: every remaining break-allowed branch.
	[InlineData(LineBreakClass.HY, LineBreakClass.AL)] // break after a hyphen (state-of-the-art)
	[InlineData(LineBreakClass.ID, LineBreakClass.NU)] // CJK then number breaks
	[InlineData(LineBreakClass.NU, LineBreakClass.ID)] // number then CJK breaks
	[InlineData(LineBreakClass.SP, LineBreakClass.NU)] // space before a number breaks
	[InlineData(LineBreakClass.ZW, LineBreakClass.ID)] // zero-width space then CJK breaks
	[InlineData(LineBreakClass.IS, LineBreakClass.QU)] // ," — structured text breaks at comma+quote (deviation)
	[InlineData(LineBreakClass.IS, LineBreakClass.AL)] // ;value — break after separator before a letter (deviation)
	internal void Break(LineBreakClass a, LineBreakClass b) => Assert.True(B(a, b));

	// Completeness guard: MayBreakBetween must accept EVERY LineBreakClass on both sides without throwing,
	// and the decision must be deterministic. This ensures no class is silently unhandled if the enum grows.
	[Fact]
	public void EveryClassPair_IsDeterministicAndDoesNotThrow()
	{
		var classes = (LineBreakClass[])System.Enum.GetValues(typeof(LineBreakClass));
		foreach (var a in classes)
			foreach (var b in classes)
			{
				bool first = LineBreaker.MayBreakBetween(a, b);
				bool second = LineBreaker.MayBreakBetween(a, b);
				Assert.Equal(first, second); // deterministic
			}
	}
}
