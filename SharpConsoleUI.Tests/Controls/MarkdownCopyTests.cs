// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

[Collection("EnvSerial")]
public class MarkdownCopyTests
{
	private static MouseEventArgs Mouse(int x, int y, params MouseFlags[] flags)
	{
		var p = new Point(x, y);
		return new MouseEventArgs(flags.ToList(), p, p, p);
	}

	private static void Paint(MarkupControl control)
	{
		var buffer = new CharacterBuffer(45, 15);
		var bounds = new LayoutRect(0, 0, 40, 10);
		control.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
	}

	[Fact]
	public void Markdown_SelectionCopy_StaysPlainText()
	{
		var c = new MarkupControl(new List<string> { "[markdown]# Hello[/]" }) { EnableSelection = true };
		Paint(c);
		// drag-select across the first row
		c.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(Mouse(5, 0, MouseFlags.Button1Dragged));

		string copied = c.GetSelectedText();
		Assert.DoesNotContain("#", copied);
		Assert.DoesNotContain("[bold", copied);
		Assert.DoesNotContain("[/]", copied);
		Assert.Contains("Hello", copied);
	}

	[Fact]
	public void CopyMode_Source_ReturnsRawMarkdownSource_WithNewlines()
	{
		// Opt-in: copying [markdown] content returns the SOURCE the user set (issue #59 — changlv), including
		// its embedded newlines, instead of the rendered glyphs.
		string src = "[markdown]# Title\n\n- item 1\n- item 2[/]";
		var c = new MarkupControl(new List<string> { src }) { EnableSelection = true, CopyMode = MarkupCopyMode.Source };
		Paint(c);
		c.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(Mouse(20, 6, MouseFlags.Button1Dragged)); // drag across the rendered block

		string copied = c.GetSelectedText();
		Assert.Equal(src, copied);                 // the original markdown source, verbatim
		Assert.Contains("\n", copied);             // newlines preserved
		Assert.Contains("# Title", copied);        // raw markdown, not the rendered heading
	}

	[Fact]
	public void CopyMode_DefaultsToRendered()
	{
		var c = new MarkupControl(new List<string> { "[markdown]# Hi[/]" });
		Assert.Equal(MarkupCopyMode.Rendered, c.CopyMode);
	}

	[Fact]
	public void CopyMode_Source_MultipleEntries_JoinedByNewline()
	{
		// A selection spanning two source entries copies both entries' source, newline-separated.
		var c = new MarkupControl(new List<string> { "[yellow]first[/]", "[green]second[/]" })
		{ EnableSelection = true, CopyMode = MarkupCopyMode.Source };
		Paint(c);
		c.ProcessMouseEvent(Mouse(0, 0, MouseFlags.Button1Pressed));
		c.ProcessMouseEvent(Mouse(6, 1, MouseFlags.Button1Dragged));

		string copied = c.GetSelectedText();
		Assert.Equal("[yellow]first[/]\n[green]second[/]", copied);
	}
}
