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
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// What <see cref="TreeControl"/> draws in front of each node — guide lines, indentation and the
/// expand indicator — character for character, and where a click toggles a node. Pinned before the
/// drawing was shared with other controls, so the sharing cannot change a single glyph.
/// </summary>
public class TreeControlGoldenRenderTests
{
	#region Helpers

	/// <summary>
	/// Two roots; the first has a leaf, an expanded parent with one leaf, and a collapsed parent.
	/// </summary>
	private static TreeControl SampleTree(TreeGuide guide = TreeGuide.Line, string? indent = null)
	{
		var tree = new TreeControl { Guide = guide };
		if (indent != null)
			tree.Indent = indent;

		var a = tree.AddRootNode("A");
		a.AddChild("a1");
		var a2 = a.AddChild("a2");
		a2.AddChild("a2x");
		var a3 = a.AddChild("a3");
		a3.AddChild("hidden");
		a3.IsExpanded = false;
		tree.AddRootNode("B");
		return tree;
	}

	private static List<string> Lines(TreeControl tree, int width = 30, int height = 8)
	{
		var buffer = new CharacterBuffer(width, height);
		var bounds = new LayoutRect(0, 0, width, height);
		tree.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);
		return Enumerable.Range(0, height)
			.Select(y => string.Concat(Enumerable.Range(0, width).Select(x => buffer.GetCell(x, y).Character.ToString())).TrimEnd())
			.Where(line => line.Length > 0)
			.ToList();
	}

	#endregion

	#region Guides and indentation

	[Fact]
	public void LineGuides()
	{
		Assert.Equal(
			["[-] A", "├─ a1", "├─ [-] a2", "│  └─ a2x", "└─ [+] a3", "B"],
			Lines(SampleTree(TreeGuide.Line)));
	}

	[Fact]
	public void AsciiGuides()
	{
		Assert.Equal(
			["[-] A", "+- a1", "+- [-] a2", "|  \\- a2x", "\\- [+] a3", "B"],
			Lines(SampleTree(TreeGuide.Ascii)));
	}

	[Fact]
	public void DoubleLineGuides()
	{
		Assert.Equal(
			["[-] A", "╠═ a1", "╠═ [-] a2", "║  ╚═ a2x", "╚═ [+] a3", "B"],
			Lines(SampleTree(TreeGuide.DoubleLine)));
	}

	[Fact]
	public void BoldLineGuides()
	{
		Assert.Equal(
			["[-] A", "┣━ a1", "┣━ [-] a2", "┃  ┗━ a2x", "┗━ [+] a3", "B"],
			Lines(SampleTree(TreeGuide.BoldLine)));
	}

	[Fact]
	public void AWiderIndent_WidensEveryAncestorLevel()
	{
		Assert.Equal(
			["[-] A", "├─ a1", "├─ [-] a2", "│    └─ a2x", "└─ [+] a3", "B"],
			Lines(SampleTree(indent: "    ")));
	}

	[Fact]
	public void TheContentWidth_CountsTheWidestNode()
	{
		var tree = SampleTree();
		Lines(tree);

		Assert.Equal("│  └─ a2x".Length, tree.ContentWidth);
	}

	#endregion

	#region The indicator is what a click toggles

	private static void Click(TreeControl tree, int x, int y)
	{
		var point = new Point(x, y);
		tree.ProcessMouseEvent(new MouseEventArgs(new List<MouseFlags> { MouseFlags.Button1Clicked }, point, point, point));
	}

	[Theory]
	[InlineData(3)]
	[InlineData(6)]
	public void ClickingTheIndicator_TogglesTheNode(int x)
	{
		var tree = SampleTree();
		Lines(tree);
		var a2 = tree.RootNodes[0].Children[1];

		Click(tree, x, 2);

		Assert.False(a2.IsExpanded);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(7)]
	public void ClickingBesideTheIndicator_LeavesTheNodeAsItWas(int x)
	{
		var tree = SampleTree();
		Lines(tree);
		var a2 = tree.RootNodes[0].Children[1];

		Click(tree, x, 2);

		Assert.True(a2.IsExpanded);
	}

	#endregion
}
