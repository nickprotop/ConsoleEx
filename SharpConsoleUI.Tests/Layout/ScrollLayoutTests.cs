// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// Task 2 of the ScrollLayout refactor: proves <see cref="ScrollLayout"/> in isolation BEFORE
/// SPC is flipped to tree participation. SPC still self-paints; here we drive ScrollLayout
/// directly by building a LayoutNode for a real <see cref="ScrollablePanelControl"/> and
/// AddChild-ing the child subtrees, then running Measure/Arrange.
///
/// THE CRUX: arranging at scroll offset N must shift every child's AbsoluteBounds.Y UP by N,
/// proving the scroll offset flows into AbsoluteBounds via the standard LayoutNode.Arrange,
/// so the standard hit-test / cursor translation work with no SPC-specific overrides.
/// </summary>
public class ScrollLayoutTests
{
	// Each MarkupControl with a single line measures to height 1. Borderless, no padding,
	// so the content viewport == the panel's inner box and the math is clean.
	private const int ChildHeight = 1;

	/// <summary>
	/// Builds a borderless SPC with <paramref name="childCount"/> single-line labels and the
	/// given explicit height/width, then wraps it in a ScrollLayout node with one child node
	/// per label (built via the real LayoutNodeFactory, exactly as the future flip will do).
	/// </summary>
	private static (ScrollablePanelControl panel, LayoutNode node, List<LayoutNode> childNodes)
		BuildScrollNode(int childCount, int panelHeight, int panelWidth = 40, int? rootX = null, int? rootY = null)
	{
		var panel = new ScrollablePanelControl
		{
			Height = panelHeight,
			Width = panelWidth,
			BorderStyle = BorderStyle.None,
			ShowScrollbar = true,
			VerticalScrollMode = ScrollMode.Scroll,
		};

		for (int i = 0; i < childCount; i++)
			panel.AddControl(new MarkupControl(new List<string> { $"Line {i}" }));

		var node = new LayoutNode(panel, new ScrollLayout());
		var childNodes = new List<LayoutNode>();
		foreach (var child in panel.Children)
		{
			var cn = LayoutNodeFactory.CreateSubtree(child);
			cn.IsVisible = child.Visible;
			node.AddChild(cn);
			childNodes.Add(cn);
		}

		return (panel, node, childNodes);
	}

	/// <summary>
	/// Runs a full Measure + Arrange pass with the panel arranged at (x,y) of the given size,
	/// mirroring how a parent layout node would arrange this node.
	/// </summary>
	private static void MeasureAndArrange(LayoutNode node, int width, int height, int x = 0, int y = 0)
	{
		node.Measure(new LayoutConstraints(0, width, 0, height));
		node.Arrange(new LayoutRect(x, y, width, height));
	}

	[Fact]
	public void MeasureChildren_ReturnsViewportSize_NotFullContentExtent()
	{
		// 20 children of height 1 = content extent 20; viewport height = 8.
		var (panel, node, _) = BuildScrollNode(childCount: 20, panelHeight: 8, panelWidth: 40);

		var desired = node.Measure(new LayoutConstraints(0, 40, 0, 8));

		// DesiredSize is the VIEWPORT, not the 20-row content extent.
		Assert.Equal(8, desired.Height);
		Assert.True(panel.TotalContentHeightInternal >= 20,
			$"content extent should be >= 20, was {panel.TotalContentHeightInternal}");
		Assert.True(desired.Height < panel.TotalContentHeightInternal,
			"viewport height must be smaller than the full content extent");
	}

	[Fact]
	public void Arrange_AtOffsetZero_StacksChildrenContiguously()
	{
		var (_, node, children) = BuildScrollNode(childCount: 20, panelHeight: 8, panelWidth: 40);

		MeasureAndArrange(node, width: 40, height: 8);

		// child[1].Y == child[0].Y + child[0].height
		Assert.Equal(children[0].AbsoluteBounds.Y + children[0].AbsoluteBounds.Height,
			children[1].AbsoluteBounds.Y);
		Assert.Equal(ChildHeight, children[0].AbsoluteBounds.Height);
	}

	/// <summary>
	/// THE GO/NO-GO ASSERTION. Setting the panel's vertical scroll offset to 5 and re-arranging
	/// must shift EVERY child's AbsoluteBounds.Y up by exactly 5 — proving the scroll offset flows
	/// into AbsoluteBounds through the standard LayoutNode.Arrange with no SPC-specific override.
	/// </summary>
	[Fact]
	public void Arrange_AtOffsetFive_ShiftsEveryChildAbsoluteBoundsUpByFive()
	{
		var (panel, node, children) = BuildScrollNode(childCount: 20, panelHeight: 8, panelWidth: 40);

		// Arrange at offset 0 and snapshot every child's absolute Y.
		MeasureAndArrange(node, width: 40, height: 8, x: 3, y: 2);
		var yAtZero = children.Select(c => c.AbsoluteBounds.Y).ToList();

		// Scroll down by 5 and re-arrange (no other change).
		panel.ScrollVerticalBy(5);
		Assert.Equal(5, panel.VerticalScrollOffsetInternal);

		MeasureAndArrange(node, width: 40, height: 8, x: 3, y: 2);
		var yAtFive = children.Select(c => c.AbsoluteBounds.Y).ToList();

		for (int i = 0; i < children.Count; i++)
		{
			Assert.Equal(yAtZero[i] - 5, yAtFive[i]);
		}
	}

	[Fact]
	public void GetPaintClipRect_HeightWithinViewport_ExcludesScrollbarChrome()
	{
		// Force a horizontal scrollbar (reserves a row) AND a vertical scrollbar (reserves columns)
		// so we can prove the clip rect excludes both chrome regions.
		var (panel, node, children) = BuildScrollNode(childCount: 20, panelHeight: 8, panelWidth: 40);
		panel.HorizontalScrollMode = ScrollMode.Scroll;
		// A wide child forces horizontal overflow -> horizontal scrollbar row reserved.
		panel.AddControl(new MarkupControl(new List<string> { new string('X', 200) }));

		var clipParent = new LayoutRect(0, 0, 40, 8);
		MeasureAndArrange(node, width: 40, height: 8);

		var layout = (IRegionClippingLayout)node.Layout!;
		var clip = layout.GetPaintClipRect(children[0], node.AbsoluteBounds);

		int viewportH = panel.ContentViewportHeight;
		Assert.True(clip.Height <= viewportH,
			$"clip height {clip.Height} must be <= content viewport height {viewportH}");
		// With a vertical scrollbar active, the clip width must exclude the scrollbar columns.
		Assert.True(panel.VerticalScrollbarActive, "expected a vertical scrollbar for 21 rows in an 8-row viewport");
		Assert.True(clip.Width <= panel.ContentViewportWidth,
			$"clip width {clip.Width} must be <= content viewport width {panel.ContentViewportWidth}");
		Assert.True(clip.Width < node.AbsoluteBounds.Width,
			"clip width must be narrower than the full panel width (scrollbar columns excluded)");
	}

	[Fact]
	public void FillChild_GetsSameSlotHeight_AsSpcComputeFillMetrics()
	{
		// One fixed child (height 1) + one Fill child, in an 8-row viewport.
		var panel = new ScrollablePanelControl
		{
			Height = 8,
			Width = 40,
			BorderStyle = BorderStyle.None,
			VerticalScrollMode = ScrollMode.Scroll,
		};
		var fixedChild = new MarkupControl(new List<string> { "fixed" });
		var fillChild = new MarkupControl(new List<string> { "fill" }) { VerticalAlignment = VerticalAlignment.Fill };
		panel.AddControl(fixedChild);
		panel.AddControl(fillChild);

		var node = new LayoutNode(panel, new ScrollLayout());
		var fixedNode = LayoutNodeFactory.CreateSubtree(fixedChild);
		var fillNode = LayoutNodeFactory.CreateSubtree(fillChild);
		node.AddChild(fixedNode);
		node.AddChild(fillNode);

		MeasureAndArrange(node, width: 40, height: 8);

		// Compute the expected Fill slot height directly from SPC's shared helpers.
		var snapshot = panel.Children;
		int contentWidth = panel.ContentViewportWidth;
		var (_, _, perFillHeight) = panel.ComputeFillMetrics(snapshot, contentWidth);
		int expectedFillHeight = panel.ComputeChildHeight(fillChild, contentWidth, perFillHeight);

		Assert.Equal(expectedFillHeight, fillNode.AbsoluteBounds.Height);
		// Sanity: the Fill child should occupy more than its 1-row content (it fills the slot).
		Assert.True(fillNode.AbsoluteBounds.Height > ChildHeight,
			$"fill child should fill its slot (>1), was {fillNode.AbsoluteBounds.Height}");
	}
}
