// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for TreeControl self-invalidation: when a consumer mutates a TreeNode
/// directly (AddChild/RemoveChild/ClearChildren, or Text/IsExpanded/colour/Tag setters) on a
/// node that is owned by a TreeControl, the owning control must automatically rebuild its
/// flattened-nodes cache AND invalidate its window — WITHOUT the consumer calling
/// tree.Invalidate() (or any TreeControl method) manually.
///
/// Each test follows the "real thing" path: a real ConsoleWindowSystem + Window hosting the
/// TreeControl, rendered once to drain PendingWork to None, then a pure node-level mutation,
/// then an assertion on window.PendingWork and (where relevant) the flattened node count read
/// back from the control's own arranged metrics (GetLogicalContentSize().Height, which equals
/// the flattened count when margins are zero).
/// </summary>
public class TreeControlSelfInvalidationTests
{
	#region Helpers

	private static (ConsoleWindowSystem system, Window window, TreeControl tree) NewTreeWindow(int w = 40, int h = 20)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(w + 4, h + 4);
		var window = new Window(system)
		{
			Title = "Tree",
			Left = 0,
			Top = 0,
			Width = w,
			Height = h
		};
		var tree = new TreeControl();
		window.AddControl(tree);
		system.AddWindow(window);
		return (system, window, tree);
	}

	/// <summary>
	/// Renders the window once with a non-empty visible region so the frame is treated as
	/// on-screen and the pending work is consumed back to None.
	/// </summary>
	private static void Render(Window window)
	{
		var region = new List<Rectangle> { new Rectangle(0, 0, Math.Max(1, window.Width), Math.Max(1, window.Height)) };
		window.RenderAndGetVisibleContent(region);
	}

	/// <summary>Flattened (visible) node count, read from the control's own arranged size (margins are 0).</summary>
	private static int FlattenedCount(TreeControl tree) => tree.GetLogicalContentSize().Height;

	#endregion

	[Fact]
	public void AddChild_OnOwnedNode_InvalidatesRelayout_AndGrowsFlattenedSet_WithoutManualInvalidate()
	{
		var (_, window, tree) = NewTreeWindow();
		var root = tree.AddRootNode("root");          // owned, expanded by default
		root.AddChild("a");
		root.AddChild("b");

		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
		int before = FlattenedCount(tree);

		// Pure consumer-level mutation — no tree.Invalidate() / no TreeControl method call.
		root.AddChild("c");

		Assert.NotEqual(FrameWork.None, window.PendingWork);
		Assert.Equal(FrameWork.Relayout, window.PendingWork);

		// Flattened set grew (root + a + b + c == before + 1).
		Render(window);
		Assert.Equal(before + 1, FlattenedCount(tree));
	}

	[Fact]
	public void AddChild_OnDeeplyNestedOwnedNode_PropagatesOwnerAndInvalidates()
	{
		var (_, window, tree) = NewTreeWindow();
		var root = tree.AddRootNode("root");
		var child = root.AddChild("child");
		var grandchild = child.AddChild("grandchild");

		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
		int before = FlattenedCount(tree);

		// Mutating a grandchild (which inherited Owner via propagation) must invalidate.
		grandchild.AddChild("leaf");

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
		Render(window);
		Assert.Equal(before + 1, FlattenedCount(tree));
	}

	[Fact]
	public void TextChange_OnOwnedNode_Invalidates()
	{
		var (_, window, tree) = NewTreeWindow();
		var root = tree.AddRootNode("root");
		root.AddChild("a");

		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		root.Text = "renamed";

		// Appearance-only change: at least a repaint must be pending.
		Assert.NotEqual(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void ColorAndTagChange_OnOwnedNode_Invalidates()
	{
		var (_, window, tree) = NewTreeWindow();
		var node = tree.AddRootNode("root");

		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
		node.TextColor = Color.Red;
		Assert.NotEqual(FrameWork.None, window.PendingWork);

		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
		node.Tag = "payload";
		Assert.NotEqual(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void IsExpandedToggle_OnOwnedNode_ReflattensAndInvalidatesRelayout()
	{
		var (_, window, tree) = NewTreeWindow();
		var root = tree.AddRootNode("root");
		root.AddChild("a");
		root.AddChild("b");
		// Collapse so children are hidden in the flattened set.
		root.IsExpanded = false;

		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
		int collapsedCount = FlattenedCount(tree);   // just the root

		// Expanding must re-flatten (visible count changes) and request a relayout.
		root.IsExpanded = true;

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
		Render(window);
		int expandedCount = FlattenedCount(tree);
		Assert.True(expandedCount > collapsedCount, $"expected expanded count > {collapsedCount}, got {expandedCount}");
		Assert.Equal(collapsedCount + 2, expandedCount);
	}

	[Fact]
	public void RemoveChild_OnOwnedNode_InvalidatesAndShrinksFlattenedSet()
	{
		var (_, window, tree) = NewTreeWindow();
		var root = tree.AddRootNode("root");
		var a = root.AddChild("a");
		root.AddChild("b");

		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
		int before = FlattenedCount(tree);

		root.RemoveChild(a);

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
		Render(window);
		Assert.Equal(before - 1, FlattenedCount(tree));
	}

	[Fact]
	public void ClearChildren_OnOwnedNode_InvalidatesAndShrinksFlattenedSet()
	{
		var (_, window, tree) = NewTreeWindow();
		var root = tree.AddRootNode("root");
		root.AddChild("a");
		root.AddChild("b");
		root.AddChild("c");

		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);
		int before = FlattenedCount(tree);
		Assert.True(before >= 4); // root + 3 children

		root.ClearChildren();

		Assert.Equal(FrameWork.Relayout, window.PendingWork);
		Render(window);
		Assert.Equal(1, FlattenedCount(tree)); // only the root remains
	}

	[Fact]
	public void RemovedSubtree_NoLongerNotifies_AfterDetach()
	{
		var (_, window, tree) = NewTreeWindow();
		var root = tree.AddRootNode("root");
		var a = root.AddChild("a");

		Render(window);
		root.RemoveChild(a);    // 'a' is now detached (Owner cleared)
		Render(window);
		Assert.Equal(FrameWork.None, window.PendingWork);

		// Mutating the detached node must NOT invalidate the (former) owner's window.
		a.AddChild("orphan");
		a.Text = "still detached";

		Assert.Equal(FrameWork.None, window.PendingWork);
	}
}
