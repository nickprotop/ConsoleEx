// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------
using SharpConsoleUI.Controls;
using SharpConsoleUI.Parsing;
using Xunit;

namespace SharpConsoleUI.Tests.Parsing;

[Collection("InlineSpinner")]
public class InlineSpinnerTruncateTests
{
	[Fact]
	public void TruncateCountsSpinnerReservedWidth()
	{
		// "abc " = 4 cols, then [spinner dots] = 3 cols => 7 visible total.
		// With maxLength 5, the spinner must NOT fit (4 + 3 = 7 > 5), so the
		// truncated result, when its visible width is measured, must be <= 5.
		string input = "abc [spinner dots]";
		string truncated = MarkupParser.Truncate(input, 5);
		Assert.True(MarkupParser.StripLength(truncated) <= 5,
			$"Truncated visible width {MarkupParser.StripLength(truncated)} exceeded maxLength 5: '{truncated}'");
	}

	[Fact]
	public void TruncateKeepsSpinnerWhenItFits()
	{
		// "ab " = 3 cols + [spinner dots] 3 = 6 <= 6 -> spinner kept, width stays 6.
		string input = "ab [spinner dots]";
		string truncated = MarkupParser.Truncate(input, 6);
		Assert.Equal(6, MarkupParser.StripLength(truncated));
	}

	[Fact]
	public void TruncateDoesNotLeaveSpinnerOnTagStack()
	{
		// A spinner tag must not produce a spurious trailing [/] (it is self-contained).
		string truncated = MarkupParser.Truncate("[spinner]", 5);
		// Re-parsing must not throw and must yield a glyph; the key assertion is that
		// the output does not contain an unmatched closing tag artifact.
		Assert.DoesNotContain("[/]", truncated);
	}
}
