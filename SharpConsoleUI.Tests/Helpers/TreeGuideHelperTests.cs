// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// The pieces <see cref="TreeGuideHelper"/> draws a tree's hierarchy from: guide glyphs per style,
/// the prefix of a node at any depth, and the expand indicator.
/// </summary>
public class TreeGuideHelperTests
{
	private static string Prefix(TreeGuide guide, string indent, params bool[] isLastAtLevel)
	{
		var builder = new StringBuilder();
		TreeGuideHelper.AppendPrefix(builder, isLastAtLevel, TreeGuideHelper.GetGuideChars(guide), indent);
		return builder.ToString();
	}

	[Theory]
	[InlineData(TreeGuide.Line, "└", "├", "│", "─")]
	[InlineData(TreeGuide.Ascii, "\\", "+", "|", "-")]
	[InlineData(TreeGuide.DoubleLine, "╚", "╠", "║", "═")]
	[InlineData(TreeGuide.BoldLine, "┗", "┣", "┃", "━")]
	public void EachGuideStyle_HasItsGlyphs(TreeGuide guide, string corner, string tee, string vertical, string horizontal)
	{
		Assert.Equal(new TreeGuideChars(corner, tee, vertical, horizontal), TreeGuideHelper.GetGuideChars(guide));
	}

	[Fact]
	public void ARoot_HasNoPrefix()
	{
		Assert.Equal("", Prefix(TreeGuide.Line, "  "));
	}

	[Fact]
	public void AChild_IsConnectedByATeeOrACorner()
	{
		Assert.Equal("├─ ", Prefix(TreeGuide.Line, "  ", false));
		Assert.Equal("└─ ", Prefix(TreeGuide.Line, "  ", true));
	}

	[Fact]
	public void EachAncestorLevel_IsALineOrABlank_ThenTheIndent()
	{
		Assert.Equal("│  │  └─ ", Prefix(TreeGuide.Line, "  ", false, false, true));
		Assert.Equal("   │  ├─ ", Prefix(TreeGuide.Line, "  ", true, false, false));
		Assert.Equal("|....+- ", Prefix(TreeGuide.Ascii, "....", false, false));
	}

	[Fact]
	public void ThePrefix_IsAppended_NotReplaced()
	{
		var builder = new StringBuilder("> ");

		TreeGuideHelper.AppendPrefix(builder, [true], TreeGuideHelper.GetGuideChars(TreeGuide.Line), "  ");

		Assert.Equal("> └─ ", builder.ToString());
	}

	[Fact]
	public void TheIndicator_SaysWhetherTheNodeIsExpanded_InFourCells()
	{
		Assert.Equal("[-] ", TreeGuideHelper.GetExpanderText(true));
		Assert.Equal("[+] ", TreeGuideHelper.GetExpanderText(false));
		Assert.Equal(4, TreeGuideHelper.ExpanderWidth);
	}
}
