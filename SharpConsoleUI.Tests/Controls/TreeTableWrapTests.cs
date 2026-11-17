// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Xunit;
using Color = SharpConsoleUI.Color;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// A TreeTableControl whose rows can be several lines tall.
/// </summary>
/// <remarks>
/// TreeTableControl derives from TableControl and overrides no rendering, so it
/// inherits wrapping whole. What it has to add is the guides: they are drawn as a
/// prefix in front of the tree column's value, on the row's line, and a tall row
/// left the lines below it with no guide at all, so the hierarchy stopped reading
/// halfway down a row.
/// </remarks>
public class TreeTableWrapTests
{
	private const string LongTitle =
		"The quick brown fox jumps over the lazy dog and keeps running onward";

	/// <summary>A root with two children, the first child's title long enough to wrap.</summary>
	private static TreeTableControl Build(bool wrap, bool expanded = true)
	{
		var tree = new TreeTableControl
		{
			BorderStyle = BorderStyle.None,
			ShowHeader = false,
			Height = 14,
		};
		tree.AddColumn(new TableColumn("Title", TextJustification.Left, 24) { Wrap = wrap });
		tree.AddColumn(new TableColumn("Size", TextJustification.Right, 6));

		var root = tree.AddRootRow("root", "0");
		root.AddChild(LongTitle, "10");
		root.AddChild("second child", "20");

		if (expanded) tree.ExpandAll();
		return tree;
	}

	/// <summary>Clicks well right of the guides and reports the row the tree selected.</summary>
	private static int ClickRow(TreeTableControl tree, int y)
	{
		var point = new System.Drawing.Point(20, y);
		tree.ProcessMouseEvent(new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Clicked }, point, point, point));
		return tree.SelectedRowIndex;
	}

	private static List<string> Paint(TreeTableControl tree, int w = 44, int h = 14)
	{
		var buffer = new CharacterBuffer(w, h);
		var bounds = new LayoutRect(0, 0, w, h);
		tree.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		return Enumerable.Range(0, h)
			.Select(y => string.Concat(Enumerable.Range(0, w)
				.Select(x => buffer.GetCell(x, y).Character.ToString())).TrimEnd())
			.ToList();
	}

	[Fact]
	public void TallTreeRow_GuidesContinueOnExtraLines()
	{
		var lines = Paint(Build(wrap: true));

		// Line 0 is the root, line 1 starts the long child. Its continuation lines must
		// still carry a guide character, not start at the left edge.
		int childLine = lines.FindIndex(l => l.Contains("The quick"));
		Assert.True(childLine > 0, "the wrapping child row was not painted");

		string continuation = lines[childLine + 1];
		Assert.False(string.IsNullOrWhiteSpace(continuation),
			"the line after a tall tree row is blank: its guides stopped");

		// A guide character leads the line, so the wrapped text does not start at column 0.
		Assert.Contains("│", continuation);
		Assert.True(continuation.IndexOf("jumps", StringComparison.Ordinal) > 1,
			$"the continuation line '{continuation}' put its text at the left edge");
	}

	[Fact]
	public void TallTreeRow_ContinuationHasNoElbow()
	{
		var lines = Paint(Build(wrap: true));
		int childLine = lines.FindIndex(l => l.Contains("The quick"));

		// The row's own line carries the connector; its continuations must not repeat it,
		// or the tree reads as several siblings where there is one row.
		Assert.Contains("├", lines[childLine]);          // the row's tee
		Assert.DoesNotContain("├", lines[childLine + 1]); // not on the continuation
		Assert.DoesNotContain("└", lines[childLine + 1]);
	}

	[Fact]
	public void TallTreeRow_ContinuationKeepsTheValueColumnAligned()
	{
		var lines = Paint(Build(wrap: true));
		int childLine = lines.FindIndex(l => l.Contains("The quick"));

		// The wrapped text must start at the same column on every line of the row, which
		// only holds if the continuation prefix is the same display width as the guide.
		int firstTextX = lines[childLine].IndexOf("The quick", StringComparison.Ordinal);
		int contTextX = lines[childLine + 1].IndexOf("jumps", StringComparison.Ordinal);

		Assert.Equal(firstTextX, contTextX);
	}

	[Fact]
	public void ExpandingUnderATallRow_PutsChildrenInTheRightPlace()
	{
		var tree = Build(wrap: true, expanded: false);
		Paint(tree);

		tree.ExpandAll();
		var lines = Paint(tree);

		// Both children appear, and the second comes after the first's wrapped lines.
		int first = lines.FindIndex(l => l.Contains("The quick"));
		int second = lines.FindIndex(l => l.Contains("second child"));

		Assert.True(first > 0, "the first child did not appear after expanding");
		Assert.True(second > first + 1,
			$"the second child landed on line {second}, inside the first child's {first} wrapped rows");
	}

	[Fact]
	public void CollapsingATallRow_RemovesItsLines()
	{
		var tree = Build(wrap: true);
		var expanded = Paint(tree);
		Assert.Contains(expanded, l => l.Contains("The quick"));

		tree.CollapseAll();
		var collapsed = Paint(tree);

		Assert.DoesNotContain(collapsed, l => l.Contains("The quick"));
	}

	[Fact]
	public void TallTreeRow_ClickOnAContinuationLine_SelectsThatRow()
	{
		var tree = Build(wrap: true);
		var lines = Paint(tree);
		int childLine = lines.FindIndex(l => l.Contains("The quick"));

		// Inherited from TableControl's band-based hit test; worth pinning here because a
		// tree's prefix shifts the value right and the row still owns the whole line.
		int onOwnLine = ClickRow(tree, childLine);
		int onContinuation = ClickRow(tree, childLine + 1);

		Assert.Equal(onOwnLine, onContinuation);
	}

	[Fact]
	public void WrapOff_TreeRendersAsBefore()
	{
		var lines = Paint(Build(wrap: false));

		// Root, then both children, one line each, in order.
		int root = lines.FindIndex(l => l.Contains("root"));
		int second = lines.FindIndex(l => l.Contains("second child"));

		Assert.Equal(0, root);
		Assert.Equal(2, second);
	}

	[Fact]
	public void AnExistingGuideOverride_StillWorks()
	{
		// A subclass that overrides only GetGuideMarkup must keep working, and its guide
		// must be what the continuation is derived from.
		var tree = new CustomGuideTree
		{
			BorderStyle = BorderStyle.None,
			ShowHeader = false,
			Height = 14,
		};
		tree.AddColumn(new TableColumn("Title", TextJustification.Left, 24) { Wrap = true });
		var root = tree.AddRootRow("root");
		root.AddChild(LongTitle);
		tree.ExpandAll();

		var buffer = new CharacterBuffer(44, 14);
		var bounds = new LayoutRect(0, 0, 44, 14);
		tree.PaintDOM(buffer, bounds, bounds, Color.White, Color.Black);

		var lines = Enumerable.Range(0, 14)
			.Select(y => string.Concat(Enumerable.Range(0, 44)
				.Select(x => buffer.GetCell(x, y).Character.ToString())).TrimEnd())
			.ToList();

		int childLine = lines.FindIndex(l => l.Contains("The quick"));
		Assert.True(childLine > 0, "the override's child row did not paint");
		Assert.Contains(">>", lines[childLine]);
	}

	private sealed class CustomGuideTree : TreeTableControl
	{
		protected override string GetGuideMarkup(in TreeTableRowContext context)
			=> context.Depth == 0 ? string.Empty : ">>";
	}
}
