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
using System.Linq;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class MarkupContentWidthTests
{
	private static MarkupControl Bare(string text) => new MarkupControl(new List<string> { text });

	[Fact]
	public void LiteralBrackets_MeasuredAtRenderedWidth_NotStripped()
	{
		Assert.Equal("[INFO] server started".Length, Bare("[INFO] server started").ContentWidth);
		Assert.Equal("array[0] + array[1]".Length, Bare("array[0] + array[1]").ContentWidth);
		Assert.Equal("see item [3] in list".Length, Bare("see item [3] in list").ContentWidth);
	}

	[Fact]
	public void ChanglvJsonLine_MeasuresFullWidth_Not23()
	{
		string json = " * result：{\"entries\":[{\"name\":\"基本 Git 指令.md\",\"type\":\"File\","
			+ "\"length\":4204,\"last_write_time\":\"2026-05-27T03:41:30.3094599+08:00\",\"is_text\":true}]}";
		int w = Bare(json).ContentWidth ?? 0;
		Assert.True(w > 100, $"expected full width (>100), got {w}");
	}

	[Fact]
	public void RealMarkupTags_AreConsumed_NotCounted()
	{
		Assert.Equal(2, Bare("[red]hi[/]").ContentWidth);
	}

	[Fact]
	public void Cjk_CountsTwoColumnsPerChar()
	{
		Assert.Equal(4, Bare("中文").ContentWidth);
	}

	[Fact]
	public void MarkdownBlock_MeasuresRenderedText()
	{
		Assert.Equal(5, Bare("[markdown]# Title[/]").ContentWidth);
	}

	[Fact]
	public void BracketFreeText_Unchanged()
	{
		Assert.Equal("plain ascii text".Length, Bare("plain ascii text").ContentWidth);
	}

	[Fact]
	public void MaximizedWindow_JsonLabel_WrapsNearFullWidth_NotTiny()
	{
		// Real usage (changlv RunDemo14): a bare selectable label with literal-bracket JSON in a maximized
		// window must wrap near the window width, not collapse to ~22 columns. stdin is neutralized so the
		// ConsoleWindowSystem ctor's piped-input capture doesn't block on a non-EOF test-host stdin.
		var savedIn = Console.In;
		Console.SetIn(TextReader.Null);
		try
		{
			var (_, window) = ContainerTestHelpers.CreateTestEnvironment(sysW: 120, sysH: 40, winW: 120, winH: 40);
			var label = Builders.Controls.Markup().WithSelectionEnabled().WithCopyEnabled().Build();
			label.Text = " * result：{\"entries\":[{\"name\":\"基本 Git 指令.md\",\"type\":\"File\","
				+ "\"length\":4204,\"last_write_time\":\"2026-05-27T03:41:30.3094599+08:00\",\"is_text\":true}]}";
			window.AddControl(label);
			var lines = window.RenderAndGetVisibleContent();

			// The datetime token (33 cols) cannot fit on a ~22-col collapsed line; its presence intact on a
			// single returned line proves the wider wrap (pre-fix it was split across lines).
			Assert.Contains(lines, l => l.Contains("2026-05-27T03:41:30.3094599+08:00"));
		}
		finally { Console.SetIn(savedIn); }
	}

}
