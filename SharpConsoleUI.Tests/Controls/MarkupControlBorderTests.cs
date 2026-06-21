// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class MarkupControlBorderTests
{
	[Fact]
	public void BorderProperties_DefaultToNoBorder()
	{
		var m = new MarkupControl(new System.Collections.Generic.List<string> { "hello" });
		Assert.Equal(BorderStyle.None, m.Border);
		Assert.Null(m.BorderColor);
		Assert.Null(m.Header);
		Assert.False(m.UseSafeBorder);
		Assert.Equal(new Padding(0, 0, 0, 0), m.Padding);
	}

	[Fact]
	public void SettingBorder_IsReflectedByGetter()
	{
		var m = new MarkupControl(new System.Collections.Generic.List<string> { "hello" })
		{
			Border = BorderStyle.Rounded,
			BorderColor = Color.Red,
			Header = "Title",
			HeaderAlignment = TextJustification.Center,
			UseSafeBorder = true,
			Padding = new Padding(1, 0, 1, 0)
		};
		Assert.Equal(BorderStyle.Rounded, m.Border);
		Assert.Equal(Color.Red, m.BorderColor);
		Assert.Equal("Title", m.Header);
		Assert.Equal(TextJustification.Center, m.HeaderAlignment);
		Assert.True(m.UseSafeBorder);
		Assert.Equal(new Padding(1, 0, 1, 0), m.Padding);
	}

	private static System.Collections.Generic.List<string> RenderLines(IWindowControl c, int width, int height)
		=> ContainerTestHelpers.RenderToLines(c, width, height);

	[Fact]
	public void BorderedMarkup_DrawsBorderAndContent()
	{
		var m = new MarkupControl(new System.Collections.Generic.List<string> { "Hi" })
		{
			Border = BorderStyle.Single,
			Header = "T",
			Width = 10 // wide enough frame for the header to embed in the top border
		};
		var text = ContainerTestHelpers.StripAnsiCodes(RenderLines(m, 14, 5));
		Assert.Contains("T", text);   // header rendered in the top border
		Assert.Contains("Hi", text);  // content rendered inside
		Assert.Contains("│", text); // '│' vertical border edge (Single style)
	}

	[Fact]
	public void NoBorder_RendersPlainContent_Unchanged()
	{
		var m = new MarkupControl(new System.Collections.Generic.List<string> { "Plain" });
		var text = ContainerTestHelpers.StripAnsiCodes(RenderLines(m, 10, 4));
		Assert.Contains("Plain", text);
		Assert.DoesNotContain("│", text); // no border glyphs when Border == None
	}

	[Fact]
	public void BorderedMarkup_LinkHitTest_AccountsForBorderInset()
	{
		// Regression guard for the cache/hit-test inset. A link inside a bordered markup must be
		// hit-tested at the INSET coordinate. If the inset is missing from the layout caches, the
		// click lands on the wrong cell and the link does NOT fire.
		var m = new MarkupControl(new System.Collections.Generic.List<string> { "[link=go]X[/]" })
		{
			Border = BorderStyle.Single
		};
		string? clickedLink = null;
		m.LinkClicked += (_, e) => clickedLink = e.Url; // LinkClicked is EventHandler<LinkClickedEventArgs>; .Url exists
		RenderLines(m, 10, 4); // arrange — populates the layout cache
		// Content origin with Single border + no padding = control-relative (1,1). The 'X' glyph sits there.
		var click = ContainerTestHelpers.CreateClick(1, 1);
		m.ProcessMouseEvent(click);
		Assert.Equal("go", clickedLink);
	}

	[Fact]
	public void Builder_SetsBorderProperties()
	{
		var m = SharpConsoleUI.Builders.Controls.Markup("hi")
			.WithBorder(BorderStyle.Rounded)
			.WithBorderColor(Color.Lime)
			.WithHeader("H")
			.WithHeaderAlignment(TextJustification.Right)
			.UseSafeBorder(true)
			.WithPadding(1, 0, 1, 0)
			.Build();
		Assert.Equal(BorderStyle.Rounded, m.Border);
		Assert.Equal(Color.Lime, m.BorderColor);
		Assert.Equal("H", m.Header);
		Assert.Equal(TextJustification.Right, m.HeaderAlignment);
		Assert.True(m.UseSafeBorder);
		Assert.Equal(new Padding(1, 0, 1, 0), m.Padding);
	}

	[Fact]
	public void Builder_RoundedAndNoBorder_Shortcuts()
	{
		Assert.Equal(BorderStyle.Rounded, SharpConsoleUI.Builders.Controls.Markup("x").Rounded().Build().Border);
		Assert.Equal(BorderStyle.None, SharpConsoleUI.Builders.Controls.Markup("x").WithBorder(BorderStyle.Single).NoBorder().Build().Border);
	}
}
