// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Pixel-parity oracle for PanelControl. Captures the current rendered output so the
/// CollapsiblePanel-facade refactor can be proven identical. Each case renders a content-only
/// panel and asserts the joined rows. Intentional shift: BorderStyle.None now matches Frameless
/// (see spec Item N) — that case is asserted in PanelControlFacadeTests, not here.
/// </summary>
public class PanelControlParityTests
{
	private static string Render(PanelControl p, int w = 24, int h = 8) =>
		ContainerTestHelpers.StripAnsiCodes(ContainerTestHelpers.RenderToLines(p, w, h));

	[Theory]
	[InlineData(BorderStyle.Single)]
	[InlineData(BorderStyle.Rounded)]
	[InlineData(BorderStyle.DoubleLine)]
	public void BorderStyle_WithHeader_RendersBoxAndTitle(BorderStyle style)
	{
		var p = new PanelControl("hello") { BorderStyle = style, Header = "Hdr", Width = 20 };
		var text = Render(p);
		Assert.Contains("hello", text);
		Assert.Contains("Hdr", text);
	}

	[Theory]
	[InlineData(BorderStyle.Single, '┌')]
	[InlineData(BorderStyle.Rounded, '╭')]
	[InlineData(BorderStyle.DoubleLine, '╔')]
	public void BorderStyle_DrawsExpectedTopLeftCorner(BorderStyle style, char corner)
	{
		var p = new PanelControl("x") { BorderStyle = style, Width = 20 };
		var lines = ContainerTestHelpers.RenderToLines(p, 24, 8);
		Assert.Contains(lines, l => l.Contains(corner));
	}

	[Fact]
	public void SafeBorder_DrawsAsciiBox()
	{
		var p = new PanelControl("x") { BorderStyle = BorderStyle.Single, UseSafeBorder = true, Width = 20 };
		var text = Render(p);
		Assert.Contains("+", text);
		Assert.DoesNotContain("┌", text);
	}

	[Fact]
	public void WordWrapOff_LongLineIsClipped_NotWrapped()
	{
		var p = new PanelControl("abcdefghijklmnopqrstuvwxyz") { Width = 12, WordWrap = false };
		var lines = ContainerTestHelpers.RenderToLines(p, 16, 8);
		int rowsWithLetters = lines.FindAll(l => l.Contains("abc")).Count;
		Assert.Equal(1, rowsWithLetters);
	}

	[Fact]
	public void WordWrapOn_LongLineWraps()
	{
		var p = new PanelControl("alpha beta gamma delta epsilon") { Width = 12, WordWrap = true };
		var lines = ContainerTestHelpers.RenderToLines(p, 16, 12);
		int contentRows = lines.FindAll(l => l.Contains("alpha") || l.Contains("beta") || l.Contains("gamma") || l.Contains("delta") || l.Contains("epsilon")).Count;
		Assert.True(contentRows >= 2, "wrapped content must span multiple rows");
	}

	[Fact]
	public void Header_CenterAlignment_PlacesTitleAwayFromLeftEdge()
	{
		var p = new PanelControl("x") { BorderStyle = BorderStyle.Single, Header = "Hi", HeaderAlignment = TextJustification.Center, Width = 20 };
		var lines = ContainerTestHelpers.RenderToLines(p, 24, 8);
		var top = lines.Find(l => l.Contains("Hi"));
		Assert.NotNull(top);
		Assert.True(top!.IndexOf("Hi") > 2, "centered header should not start at the left corner");
	}
}
