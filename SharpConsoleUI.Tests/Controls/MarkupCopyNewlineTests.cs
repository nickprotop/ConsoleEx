// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

public class MarkupCopyNewlineTests
{
	private static MouseEventArgs M(int x, int y, params MouseFlags[] f)
	{ var p = new Point(x, y); return new MouseEventArgs(f.ToList(), p, p, p); }

	private static void Paint(MarkupControl c)
	{ var b = new CharacterBuffer(60, 25); var r = new LayoutRect(0, 0, 55, 20); c.PaintDOM(b, r, r, Color.White, Color.Black); }

	[Fact]
	public void RenderedCopy_OfMarkdownBlock_PreservesHardNewlines()
	{
		// changlv #59 (second half): copying a rendered [markdown] block dropped ALL newlines,
		// so "# Title\n\n- one\n- two" copied as "Title- one- two" on one line. The rendered rows
		// (heading, bullets) are hard-broken and must copy on separate lines.
		var c = new MarkupControl(new List<string> { "[markdown]# Title\n\n- one\n- two[/]" }) { EnableSelection = true };
		Paint(c);
		c.ProcessMouseEvent(M(0, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(M(40, 12, MouseFlags.Button1Dragged));
		string copied = c.GetSelectedText();
		Assert.Contains("\n", copied);                        // newlines preserved
		Assert.DoesNotContain("Titleone", copied.Replace(" ", "")); // heading not glued to first bullet
		Assert.Contains("Title", copied);
		Assert.Contains("one", copied);
		Assert.Contains("two", copied);
	}

	[Fact]
	public void RenderedCopy_OfWordWrappedParagraph_StaysOneLine()
	{
		// Must-not-regress: a single logical line that only SOFT-wraps must still copy as one line
		// (a soft-wrap continuation is not a hard newline).
		var c = new MarkupControl(new List<string> { "alpha beta gamma delta epsilon zeta eta theta" }) { EnableSelection = true };
		Paint(c);
		c.ProcessMouseEvent(M(0, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(M(40, 12, MouseFlags.Button1Dragged));
		string copied = c.GetSelectedText();
		Assert.DoesNotContain("\n", copied); // soft-wrap must not introduce a newline
	}

	[Fact]
	public void RenderedCopy_OfPlainMultilineEntries_PreservesNewlines()
	{
		// Regression guard for the already-working case: separate content entries copy newline-separated.
		var c = new MarkupControl(new List<string> { "aaa", "bbb", "ccc" }) { EnableSelection = true };
		Paint(c);
		c.ProcessMouseEvent(M(0, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(M(3, 4, MouseFlags.Button1Dragged));
		string copied = c.GetSelectedText();
		Assert.Equal("aaa\nbbb\nccc", copied);
	}

	[Fact]
	public void CopyToClipboard_Rendered_ExpandsMarkdown_WithNewlines()
	{
		// Copy-all in the default Rendered mode returns rendered text (no '#'/'-' syntax) WITH newlines.
		var c = new MarkupControl(new List<string> { "[markdown]# Title\n\n- one\n- two[/]" });
		Paint(c);
		string text = c.CopyToClipboardText(); // test seam returning what CopyToClipboard would set
		Assert.Contains("\n", text);
		Assert.DoesNotContain("#", text);      // markdown syntax expanded away
		Assert.Contains("Title", text);
		Assert.Contains("one", text);
	}

	[Fact]
	public void CopyToClipboard_Source_ReturnsRawMarkup()
	{
		var src = "[markdown]# Title\n\n- one\n- two[/]";
		var c = new MarkupControl(new List<string> { src }) { CopyMode = MarkupCopyMode.Source };
		Paint(c);
		string text = c.CopyToClipboardText();
		Assert.Equal(src, text);               // raw markup, newlines intact
	}
}
