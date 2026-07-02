// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

// #65: collapsing a CollapsiblePanel whose nested markdown had CJK content froze the UI — a column
// squeezed to width 1 met a 2-column CJK glyph and WrapCellLine spun forever. Real nesting from
// changlv's RunDemo13c, driven through the real render + collapse path, must complete (not hang).
// Runs synchronously; if the loop regresses the render never returns and --blame-hang flags it.
[Collection("EnvSerial")]
public class CollapsiblePanelCjkCollapseTests
{
	private static void RenderThenCollapse(string heading)
	{
		// Neutralize stdin so ConsoleWindowSystem's ctor piped-input capture returns immediately in the
		// test host (an open, non-EOF stdin would otherwise block ReadToEnd — unrelated to this bug).
		var savedIn = Console.In;
		Console.SetIn(TextReader.Null);
		try
		{
			var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
			var outer = new ScrollablePanelControl { BorderStyle = BorderStyle.Rounded, VerticalAlignment = VerticalAlignment.Fill, AutoScroll = true };
			var main = new CollapsiblePanel { Title = "Main Panel 1", MaxContentHeight = 8 };
			var sub = new ScrollablePanelControl { BorderStyle = BorderStyle.None, VerticalAlignment = VerticalAlignment.Fill, AutoScroll = true };
			var md = heading + "\n\n- item 1\n- item 2\n\n---\n\n| col1 | col2 |\n|---|---|\n| one line | one line |\n"
				+ "| this is long text this is long text this is long text | this is long text this is long text |\n";
			var label = new MarkupControl(new List<string> { $"[markdown]{md}[/]" }) { EnableSelection = true };
			sub.AddControl(label); main.AddControl(sub); outer.AddControl(main);
			window.AddControl(outer);
			for (int i = 0; i < 10; i++) window.RenderAndGetVisibleContent();
			main.Toggle(); // the collapse (title click)
			for (int i = 0; i < 10; i++) window.RenderAndGetVisibleContent();
		}
		finally { Console.SetIn(savedIn); }
	}

	[Fact]
	public void CjkContent_CollapseToggle_CompletesWithoutHang()
	{
		RenderThenCollapse("# 测试信息");
		Assert.True(true); // reaching here = no infinite loop (#65)
	}

	[Fact]
	public void EnglishContent_CollapseToggle_Completes()
	{
		RenderThenCollapse("# Test Header");
		Assert.True(true);
	}
}
