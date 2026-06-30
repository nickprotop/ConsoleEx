// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class LineBreakClassifierTests
{
	private static LineBreakClass C(int cp) => LineBreakClassifier.Classify(new Rune(cp));

	[Theory]
	[InlineData('A', LineBreakClass.AL)]
	[InlineData('z', LineBreakClass.AL)]
	[InlineData('_', LineBreakClass.AL)]   // underscore binds like a letter (identifier runs)
	[InlineData('5', LineBreakClass.NU)]
	[InlineData(' ', LineBreakClass.SP)]
	[InlineData('.', LineBreakClass.IS)]
	[InlineData(',', LineBreakClass.IS)]
	[InlineData(':', LineBreakClass.IS)]
	[InlineData(';', LineBreakClass.IS)]
	[InlineData('/', LineBreakClass.SY)]
	[InlineData('(', LineBreakClass.OP)]
	[InlineData('[', LineBreakClass.OP)]
	[InlineData('{', LineBreakClass.OP)]
	[InlineData(')', LineBreakClass.CL)]
	[InlineData(']', LineBreakClass.CL)]
	[InlineData('}', LineBreakClass.CL)]
	[InlineData('"', LineBreakClass.QU)]
	[InlineData('\'', LineBreakClass.QU)]
	[InlineData('-', LineBreakClass.HY)]
	[InlineData('$', LineBreakClass.PR)]
	[InlineData('%', LineBreakClass.PO)]
	internal void ClassifyAscii(int cp, LineBreakClass expected) => Assert.Equal(expected, C(cp));

	[Theory]
	[InlineData(0x4E2D, LineBreakClass.ID)]  // 中 ideograph
	[InlineData(0x3042, LineBreakClass.ID)]  // あ hiragana (wide)
	[InlineData(0x00A0, LineBreakClass.GL)]  // NBSP
	[InlineData(0x200B, LineBreakClass.ZW)]  // ZWSP
	[InlineData(0x2060, LineBreakClass.WJ)]  // word joiner
	[InlineData(0x00AD, LineBreakClass.BA)]  // soft hyphen
	[InlineData(0x0301, LineBreakClass.CM)]  // combining acute
	internal void ClassifyUnicode(int cp, LineBreakClass expected) => Assert.Equal(expected, C(cp));

	[Fact]
	public void TablePrecedesCategory_NbspIsGlueNotSpace()
		=> Assert.Equal(LineBreakClass.GL, C(0x00A0)); // SpaceSeparator category, but GL by table

	[Fact]
	public void UnknownLetterlikeFoldsToAL()
		=> Assert.Equal(LineBreakClass.AL, C(0x00E9)); // é — letter → AL
}
