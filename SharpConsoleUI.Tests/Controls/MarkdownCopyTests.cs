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
}
