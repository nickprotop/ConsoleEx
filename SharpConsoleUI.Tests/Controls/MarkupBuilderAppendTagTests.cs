// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class MarkupBuilderAppendTagTests
{
	[Fact]
	public void Append_MultilineStyledTag_KeepsRegionAtomic()
	{
		// A [yellow] region spanning \r\n must NOT be torn across content lines by Append, otherwise only
		// the first line renders yellow (issue #59 — changlv's .Append("[yellow]...\r\n...[/]")).
		var m = MarkupControl.Create()
			.Append("[yellow]line one\r\nline two\r\nline three[/]")
			.Build();

		var content = m.GetContentLinesForTest();
		// The whole [yellow]…[/] region stays in ONE content entry (atomic), so the render path applies the
		// style across all three lines.
		Assert.Single(content);
		Assert.Equal("[yellow]line one\r\nline two\r\nline three[/]", content[0]);
	}

	[Fact]
	public void Append_PlainMultiline_SplitsIntoLines()
	{
		// Plain text with no open tag region splits per line as before (StringBuilder-like).
		var m = MarkupControl.Create()
			.Append("a\nb\nc")
			.Build();
		Assert.Equal(new[] { "a", "b", "c" }, m.GetContentLinesForTest().ToArray());
	}

	[Fact]
	public void Append_FirstSegmentJoinsCurrentLine_WhenNoOpenTag()
	{
		// StringBuilder-style join semantics preserved for plain text.
		var m = MarkupControl.Create()
			.AddLine("x")
			.Append(" tail")
			.Build();
		Assert.Equal("x tail", m.Text);
	}
}
