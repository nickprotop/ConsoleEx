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
	internal void Break(LineBreakClass a, LineBreakClass b) => Assert.True(B(a, b));
}
