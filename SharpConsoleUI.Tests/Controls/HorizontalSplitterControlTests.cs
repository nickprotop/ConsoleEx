// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class HorizontalSplitterControlTests
{
	#region Construction & Defaults

	[Fact]
	public void Constructor_Default_SetsExpectedDefaults()
	{
		var splitter = new HorizontalSplitterControl();

		Assert.Equal(HorizontalAlignment.Stretch, splitter.HorizontalAlignment);
		Assert.Equal(VerticalAlignment.Top, splitter.VerticalAlignment);
		Assert.True(splitter.IsEnabled);
		Assert.False(splitter.HasFocus);
		Assert.False(splitter.IsDragging);
		Assert.Null(splitter.AboveControl);
		Assert.Null(splitter.BelowControl);
	}

	[Fact]
	public void Constructor_WithNeighbors_SetsControls()
	{
		var above = new MarkupControl(new List<string> { "above" });
		var below = new MarkupControl(new List<string> { "below" });

		var splitter = new HorizontalSplitterControl(above, below);

		Assert.Same(above, splitter.AboveControl);
		Assert.Same(below, splitter.BelowControl);
	}

	[Fact]
	public void MinHeightAbove_DefaultIsThree()
	{
		var splitter = new HorizontalSplitterControl();

		Assert.Equal(ControlDefaults.HorizontalSplitterMinControlHeight, splitter.MinHeightAbove);
	}

	[Fact]
	public void MinHeightBelow_DefaultIsThree()
	{
		var splitter = new HorizontalSplitterControl();

		Assert.Equal(ControlDefaults.HorizontalSplitterMinControlHeight, splitter.MinHeightBelow);
	}

	[Fact]
	public void MinHeightAbove_CannotBeBelowMinimum()
	{
		var splitter = new HorizontalSplitterControl();

		splitter.MinHeightAbove = 1;

		Assert.Equal(ControlDefaults.HorizontalSplitterMinControlHeight, splitter.MinHeightAbove);
	}

	[Fact]
	public void MinHeightBelow_CannotBeBelowMinimum()
	{
		var splitter = new HorizontalSplitterControl();

		splitter.MinHeightBelow = 1;

		Assert.Equal(ControlDefaults.HorizontalSplitterMinControlHeight, splitter.MinHeightBelow);
	}

	#endregion

	#region SetControls

	[Fact]
	public void SetControls_SetsAboveAndBelow()
	{
		var splitter = new HorizontalSplitterControl();
		var above = new MarkupControl(new List<string> { "above" });
		var below = new MarkupControl(new List<string> { "below" });

		splitter.SetControls(above, below);

		Assert.Same(above, splitter.AboveControl);
		Assert.Same(below, splitter.BelowControl);
	}

	#endregion

	#region Neighbor Discovery

	[Fact]
	public void NeighborDiscovery_FindsAboveAndBelow_InWindow()
	{
		var ws = TestWindowSystemBuilder.CreateTestSystem();
		var panel1 = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl();
		var panel2 = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };

		var window = new WindowBuilder(ws)
			.WithTitle("Test")
			.WithSize(40, 20)
			.AddControl(panel1)
			.AddControl(splitter)
			.AddControl(panel2)
			.Build();

		// Verify container is set and is the Window
		Assert.NotNull(splitter.Container);
		Assert.IsType<Window>(splitter.Container);

		// Force neighbor resolution by rendering
		window.EnsureContentReady();

		Assert.Same(panel1, splitter.AboveControl);
		Assert.Same(panel2, splitter.BelowControl);
	}

	#endregion

	#region MoveSplitter

	[Fact]
	public void MoveSplitter_BothFill_SetsAboveHeight()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		// Simulate that both controls have rendered at a known size
		// by setting Height on above (making it "settable")
		above.Height = 10;
		below.Height = 10;

		splitter.MoveSplitter(3);

		Assert.Equal(13, above.Height);
		Assert.Equal(7, below.Height);
	}

	[Fact]
	public void MoveSplitter_ClampsToMinHeight()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 5;

		// Try to move down by 10 — below would go to -5, but clamps to MinHeightBelow (3)
		splitter.MoveSplitter(10);

		Assert.Equal(12, above.Height);
		Assert.Equal(ControlDefaults.HorizontalSplitterMinControlHeight, below.Height);
	}

	[Fact]
	public void MoveSplitter_NegativeDelta_ShrinkAbove()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;

		splitter.MoveSplitter(-4);

		Assert.Equal(6, above.Height);
		Assert.Equal(14, below.Height);
	}

	[Fact]
	public void MoveSplitter_ZeroDelta_NoChange()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;

		splitter.MoveSplitter(0);

		Assert.Equal(10, above.Height);
		Assert.Equal(10, below.Height);
	}

	[Fact]
	public void MoveSplitter_FiresSplitterMovedEvent()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;

		HorizontalSplitterMovedEventArgs? eventArgs = null;
		splitter.SplitterMoved += (s, e) => eventArgs = e;

		splitter.MoveSplitter(3);

		Assert.NotNull(eventArgs);
		Assert.Equal(3, eventArgs!.Delta);
		Assert.Equal(13, eventArgs.AboveControlHeight);
		Assert.Equal(7, eventArgs.BelowControlHeight);
	}

	#endregion

	#region Keyboard Input

	[Fact]
	public void ProcessKey_DownArrow_MovesDown()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;
		splitter.HasFocus = true;

		var key = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);
		bool handled = splitter.ProcessKey(key);

		Assert.True(handled);
		Assert.Equal(11, above.Height);
		Assert.Equal(9, below.Height);
	}

	[Fact]
	public void ProcessKey_UpArrow_MovesUp()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;
		splitter.HasFocus = true;

		var key = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false);
		bool handled = splitter.ProcessKey(key);

		Assert.True(handled);
		Assert.Equal(9, above.Height);
		Assert.Equal(11, below.Height);
	}

	[Fact]
	public void ProcessKey_ShiftDown_JumpsMultipleRows()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;
		splitter.HasFocus = true;

		var key = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, true, false, false);
		bool handled = splitter.ProcessKey(key);

		Assert.True(handled);
		Assert.Equal(10 + ControlDefaults.HorizontalSplitterKeyboardJumpSize, above.Height);
	}

	[Fact]
	public void ProcessKey_ShiftUp_JumpsMultipleRows()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 15;
		below.Height = 10;
		splitter.HasFocus = true;

		var key = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, true, false, false);
		bool handled = splitter.ProcessKey(key);

		Assert.True(handled);
		Assert.Equal(15 - ControlDefaults.HorizontalSplitterKeyboardJumpSize, above.Height);
	}

	[Fact]
	public void ProcessKey_NotFocused_ReturnsFalse()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;

		var key = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);
		bool handled = splitter.ProcessKey(key);

		Assert.False(handled);
	}

	[Fact]
	public void ProcessKey_UnrelatedKey_ReturnsFalse()
	{
		var splitter = new HorizontalSplitterControl();
		splitter.HasFocus = true;

		var key = new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false);
		bool handled = splitter.ProcessKey(key);

		Assert.False(handled);
	}

	#endregion

	#region Focus

	[Fact]
	public void Focus_GainedFocus_FiresGotFocusEvent()
	{
		var splitter = new HorizontalSplitterControl();

		bool gotFocusFired = false;
		splitter.GotFocus += (s, e) => gotFocusFired = true;

		splitter.HasFocus = true;

		Assert.True(gotFocusFired);
	}

	[Fact]
	public void Focus_LostFocus_FiresLostFocusEvent()
	{
		var splitter = new HorizontalSplitterControl();
		splitter.HasFocus = true;

		bool lostFocusFired = false;
		splitter.LostFocus += (s, e) => lostFocusFired = true;

		splitter.HasFocus = false;

		Assert.True(lostFocusFired);
	}

	[Fact]
	public void Focus_LostFocus_ClearsDragging()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;
		splitter.HasFocus = true;

		// Start dragging via keyboard
		var key = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);
		splitter.ProcessKey(key);

		Assert.True(splitter.IsDragging);

		// Lose focus
		splitter.HasFocus = false;

		Assert.False(splitter.IsDragging);
	}

	#endregion

	#region Builder

	[Fact]
	public void Builder_CreatesWithDefaults()
	{
		HorizontalSplitterControl splitter = Builders.Controls.HorizontalSplitter().Build();

		Assert.NotNull(splitter);
		Assert.True(splitter.IsEnabled);
	}

	[Fact]
	public void Builder_WithMinHeights_SetsValues()
	{
		var splitter = Builders.Controls.HorizontalSplitter()
			.WithMinHeights(5, 7)
			.Build();

		Assert.Equal(5, splitter.MinHeightAbove);
		Assert.Equal(7, splitter.MinHeightBelow);
	}

	[Fact]
	public void Builder_WithName_SetsName()
	{
		var splitter = Builders.Controls.HorizontalSplitter()
			.WithName("mySplitter")
			.Build();

		Assert.Equal("mySplitter", splitter.Name);
	}

	[Fact]
	public void Builder_WithControls_SetsNeighbors()
	{
		var above = new MarkupControl(new List<string> { "above" });
		var below = new MarkupControl(new List<string> { "below" });

		var splitter = Builders.Controls.HorizontalSplitter()
			.WithControls(above, below)
			.Build();

		Assert.Same(above, splitter.AboveControl);
		Assert.Same(below, splitter.BelowControl);
	}

	[Fact]
	public void Builder_OnSplitterMoved_HandlerRegistered()
	{
		bool eventFired = false;
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };

		var splitter = Builders.Controls.HorizontalSplitter()
			.WithControls(above, below)
			.OnSplitterMoved((s, e) => eventFired = true)
			.Build();

		above.Height = 10;
		below.Height = 10;

		splitter.MoveSplitter(2);

		Assert.True(eventFired);
	}

	[Fact]
	public void Builder_ImplicitConversion_Works()
	{
		HorizontalSplitterControl splitter = Builders.Controls.HorizontalSplitter();

		Assert.NotNull(splitter);
	}

	#endregion

	#region IWindowControl.Height

	[Fact]
	public void IWindowControl_Height_DefaultIsNull()
	{
		var control = new MarkupControl(new List<string> { "test" });

		Assert.Null(control.Height);
	}

	[Fact]
	public void IWindowControl_Height_CanBeSet_OnBaseControl()
	{
		var control = new MarkupControl(new List<string> { "test" });

		control.Height = 10;

		Assert.Equal(10, control.Height);
	}

	[Fact]
	public void IWindowControl_Height_SetNegative_ClampsToZero()
	{
		var control = new MarkupControl(new List<string> { "test" });

		control.Height = -5;

		Assert.Equal(0, control.Height);
	}

	#endregion

	#region Measurement

	[Fact]
	public void MeasureDOM_ReturnsOneRowHeight()
	{
		var splitter = new HorizontalSplitterControl();

		var constraints = new LayoutConstraints(0, 80, 0, 100);
		var size = splitter.MeasureDOM(constraints);

		Assert.Equal(80, size.Width);
		Assert.Equal(1, size.Height);
	}

	[Fact]
	public void MeasureDOM_IncludesMargins()
	{
		var splitter = new HorizontalSplitterControl();
		splitter.Margin = new Margin(0, 1, 0, 1);

		var constraints = new LayoutConstraints(0, 80, 0, 100);
		var size = splitter.MeasureDOM(constraints);

		Assert.Equal(3, size.Height); // 1 + top margin + bottom margin
	}

	#endregion

	#region Layout Integration - ExplicitHeight in VerticalStackLayout

	[Fact]
	public void VerticalStackLayout_FillChildWithExplicitHeight_TreatedAsFixed()
	{
		var layout = new VerticalStackLayout();
		var root = new LayoutNode(null, layout);

		// Child 1: Fill with explicit height (simulates a control resized by splitter)
		var child1 = new LayoutNode(new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill });
		child1.VerticalAlignment = VerticalAlignment.Fill;
		child1.ExplicitHeight = 10;
		root.AddChild(child1);

		// Child 2: Fill without explicit height (should flex)
		var child2 = new LayoutNode(new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill });
		child2.VerticalAlignment = VerticalAlignment.Fill;
		root.AddChild(child2);

		var constraints = new LayoutConstraints(0, 80, 0, 30);
		layout.MeasureChildren(root, constraints);
		layout.ArrangeChildren(root, new LayoutRect(0, 0, 80, 30));

		// Child 1 should get exactly 10 (its explicit height)
		Assert.Equal(10, child1.Bounds.Height);
		// Child 2 should get the remaining 20
		Assert.Equal(20, child2.Bounds.Height);
	}

	[Fact]
	public void VerticalStackLayout_FillChildWithoutExplicitHeight_SharesSpace()
	{
		var layout = new VerticalStackLayout();
		var root = new LayoutNode(null, layout);

		var child1 = new LayoutNode(new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill });
		child1.VerticalAlignment = VerticalAlignment.Fill;
		root.AddChild(child1);

		var child2 = new LayoutNode(new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill });
		child2.VerticalAlignment = VerticalAlignment.Fill;
		root.AddChild(child2);

		var constraints = new LayoutConstraints(0, 80, 0, 30);
		layout.MeasureChildren(root, constraints);
		layout.ArrangeChildren(root, new LayoutRect(0, 0, 80, 30));

		// Both should share equally
		Assert.Equal(15, child1.Bounds.Height);
		Assert.Equal(15, child2.Bounds.Height);
	}

	#endregion

	#region Layout Integration - ExplicitHeight in WindowContentLayout

	[Fact]
	public void WindowContentLayout_FillChildWithExplicitHeight_TreatedAsFixed()
	{
		var layout = new WindowContentLayout();
		var root = new LayoutNode(null, layout);

		// Child 1: Fill with explicit height
		var panel1 = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var child1 = new LayoutNode(panel1);
		child1.VerticalAlignment = VerticalAlignment.Fill;
		child1.ExplicitHeight = 10;
		root.AddChild(child1);

		// Child 2: splitter (1 row, Top alignment)
		var splitter = new HorizontalSplitterControl();
		var child2 = new LayoutNode(splitter);
		child2.VerticalAlignment = VerticalAlignment.Top;
		root.AddChild(child2);

		// Child 3: Fill without explicit height
		var panel2 = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var child3 = new LayoutNode(panel2);
		child3.VerticalAlignment = VerticalAlignment.Fill;
		root.AddChild(child3);

		var constraints = new LayoutConstraints(0, 80, 0, 30);
		layout.MeasureChildren(root, constraints);
		layout.ArrangeChildren(root, new LayoutRect(0, 0, 80, 30));

		// Child 1 should get exactly 10 (its explicit height)
		Assert.Equal(10, child1.Bounds.Height);
		// Child 2 (splitter) should get 1
		Assert.Equal(1, child2.Bounds.Height);
		// Child 3 should get the remaining 19
		Assert.Equal(19, child3.Bounds.Height);
	}

	[Fact]
	public void WindowContentLayout_FillChildWithoutExplicitHeight_SharesSpace()
	{
		var layout = new WindowContentLayout();
		var root = new LayoutNode(null, layout);

		var panel1 = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var child1 = new LayoutNode(panel1);
		child1.VerticalAlignment = VerticalAlignment.Fill;
		root.AddChild(child1);

		var panel2 = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var child2 = new LayoutNode(panel2);
		child2.VerticalAlignment = VerticalAlignment.Fill;
		root.AddChild(child2);

		var constraints = new LayoutConstraints(0, 80, 0, 30);
		layout.MeasureChildren(root, constraints);
		layout.ArrangeChildren(root, new LayoutRect(0, 0, 80, 30));

		// Both should share equally
		Assert.Equal(15, child1.Bounds.Height);
		Assert.Equal(15, child2.Bounds.Height);
	}

	[Fact]
	public void WindowContentLayout_SplitterResizesGrid_CorrectHeights()
	{
		// Simulate: [Grid(Fill,Height=15), Splitter(Top,1row), Panel(Fill)]
		// This is the outer splitter scenario from the demo
		var layout = new WindowContentLayout();
		var root = new LayoutNode(null, layout);

		var grid = new HorizontalGridControl() { VerticalAlignment = VerticalAlignment.Fill };
		grid.Height = 15;
		var gridNode = new LayoutNode(grid);
		gridNode.VerticalAlignment = VerticalAlignment.Fill;
		gridNode.ExplicitHeight = 15;
		root.AddChild(gridNode);

		var splitter = new HorizontalSplitterControl();
		var splitterNode = new LayoutNode(splitter);
		splitterNode.VerticalAlignment = VerticalAlignment.Top;
		root.AddChild(splitterNode);

		var panel = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var panelNode = new LayoutNode(panel);
		panelNode.VerticalAlignment = VerticalAlignment.Fill;
		root.AddChild(panelNode);

		var constraints = new LayoutConstraints(0, 80, 0, 30);
		layout.MeasureChildren(root, constraints);
		layout.ArrangeChildren(root, new LayoutRect(0, 0, 80, 30));

		Assert.Equal(15, gridNode.Bounds.Height);
		Assert.Equal(1, splitterNode.Bounds.Height);
		Assert.Equal(14, panelNode.Bounds.Height);

		// Now simulate splitter moving up by 5 (grid shrinks, panel grows)
		gridNode.ExplicitHeight = 10;
		layout.MeasureChildren(root, constraints);
		layout.ArrangeChildren(root, new LayoutRect(0, 0, 80, 30));

		Assert.Equal(10, gridNode.Bounds.Height);
		Assert.Equal(1, splitterNode.Bounds.Height);
		Assert.Equal(19, panelNode.Bounds.Height);
	}

	#endregion

	#region Full Window Integration

	[Fact]
	public void FullWindow_OuterSplitter_ResizesGridAndBottomPanel()
	{
		var ws = TestWindowSystemBuilder.CreateTestSystem(80, 30);

		var grid = Builders.Controls.HorizontalGrid()
			.WithAlignment(HorizontalAlignment.Stretch)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.Column(col => col
				.Add(Builders.Controls.Markup().AddLine("Column 1").Build()))
			.Column(col => col
				.Add(Builders.Controls.Markup().AddLine("Column 2").Build()))
			.Build();

		var splitter = new HorizontalSplitterControl();
		var bottomPanel = new PanelControl("Bottom") { VerticalAlignment = VerticalAlignment.Fill };

		var window = new WindowBuilder(ws)
			.WithTitle("Test")
			.WithSize(80, 30)
			.AddControl(grid)
			.AddControl(splitter)
			.AddControl(bottomPanel)
			.Build();

		// Force render to populate ActualHeight
		window.EnsureContentReady();

		int gridHeightBefore = grid.ActualHeight;
		int bottomHeightBefore = bottomPanel.ActualHeight;

		Assert.True(gridHeightBefore > 0, "Grid should have rendered");
		Assert.True(bottomHeightBefore > 0, "Bottom panel should have rendered");

		// Set explicit height on grid (simulating what MoveSplitter does)
		grid.Height = gridHeightBefore - 5;

		// Force re-render
		window.EnsureContentReady();

		Assert.Equal(gridHeightBefore - 5, grid.ActualHeight);
		Assert.Equal(bottomHeightBefore + 5, bottomPanel.ActualHeight);
	}

	#endregion

	#region Visibility Edge Cases

	[Fact]
	public void ResolveNeighbors_SkipsInvisibleAbove()
	{
		var ws = TestWindowSystemBuilder.CreateTestSystem();
		var panel1 = new PanelControl("top1") { VerticalAlignment = VerticalAlignment.Fill };
		var panel2 = new PanelControl("top2") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl();
		var panel3 = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };

		// panel2 is directly above splitter but invisible
		panel2.Visible = false;

		var window = new WindowBuilder(ws)
			.WithTitle("Test")
			.WithSize(40, 20)
			.AddControl(panel1)
			.AddControl(panel2)
			.AddControl(splitter)
			.AddControl(panel3)
			.Build();

		window.EnsureContentReady();

		// Should skip panel2 (invisible) and find panel1
		Assert.Same(panel1, splitter.AboveControl);
		Assert.Same(panel3, splitter.BelowControl);
	}

	[Fact]
	public void ResolveNeighbors_SkipsInvisibleBelow()
	{
		var ws = TestWindowSystemBuilder.CreateTestSystem();
		var panel1 = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl();
		var panel2 = new PanelControl("mid") { VerticalAlignment = VerticalAlignment.Fill };
		var panel3 = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };

		// panel2 is directly below splitter but invisible
		panel2.Visible = false;

		var window = new WindowBuilder(ws)
			.WithTitle("Test")
			.WithSize(40, 20)
			.AddControl(panel1)
			.AddControl(splitter)
			.AddControl(panel2)
			.AddControl(panel3)
			.Build();

		window.EnsureContentReady();

		Assert.Same(panel1, splitter.AboveControl);
		Assert.Same(panel3, splitter.BelowControl);
	}

	[Fact]
	public void MoveSplitter_InvisibleAbove_OnlyResizesBelow()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;
		above.Visible = false;

		splitter.MoveSplitter(3);

		// Above is invisible so not settable; only below is adjusted
		Assert.Equal(10, above.Height); // unchanged
		Assert.Equal(7, below.Height);
	}

	[Fact]
	public void MoveSplitter_InvisibleBelow_OnlyResizesAbove()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;
		below.Visible = false;

		splitter.MoveSplitter(3);

		Assert.Equal(13, above.Height);
		Assert.Equal(10, below.Height); // unchanged
	}

	[Fact]
	public void MoveSplitter_BothInvisible_NoOp()
	{
		var above = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var below = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl(above, below);

		above.Height = 10;
		below.Height = 10;
		above.Visible = false;
		below.Visible = false;

		splitter.MoveSplitter(3);

		Assert.Equal(10, above.Height);
		Assert.Equal(10, below.Height);
	}

	[Fact]
	public void AutoHide_BothNeighborsInvisible_SplitterHides()
	{
		var ws = TestWindowSystemBuilder.CreateTestSystem();
		var panel1 = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl();
		var panel2 = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };

		var window = new WindowBuilder(ws)
			.WithTitle("Test")
			.WithSize(40, 20)
			.AddControl(panel1)
			.AddControl(splitter)
			.AddControl(panel2)
			.Build();

		window.EnsureContentReady();
		Assert.True(splitter.Visible);

		// Hide both neighbors
		panel1.Visible = false;
		panel2.Visible = false;

		// Force re-resolve by accessing neighbors
		// _neighborsResolved was cleared, so accessing triggers resolve
		_ = splitter.AboveControl;

		Assert.False(splitter.Visible);
	}

	[Fact]
	public void AutoHide_OneNeighborInvisible_SplitterHides()
	{
		var ws = TestWindowSystemBuilder.CreateTestSystem();
		var panel1 = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl();
		var panel2 = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };

		var window = new WindowBuilder(ws)
			.WithTitle("Test")
			.WithSize(40, 20)
			.AddControl(panel1)
			.AddControl(splitter)
			.AddControl(panel2)
			.Build();

		window.EnsureContentReady();
		Assert.True(splitter.Visible);

		// Hide only one neighbor — splitter needs both sides to function
		panel2.Visible = false;

		// Force re-resolve
		_ = splitter.AboveControl;

		Assert.False(splitter.Visible);
	}

	[Fact]
	public void AutoHide_NeighborBecomesVisible_SplitterReappears()
	{
		var ws = TestWindowSystemBuilder.CreateTestSystem();
		var panel1 = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl();
		var panel2 = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };

		var window = new WindowBuilder(ws)
			.WithTitle("Test")
			.WithSize(40, 20)
			.AddControl(panel1)
			.AddControl(splitter)
			.AddControl(panel2)
			.Build();

		window.EnsureContentReady();

		// Hide a neighbor — splitter auto-hides
		panel2.Visible = false;
		_ = splitter.AboveControl;
		Assert.False(splitter.Visible);

		// Show the neighbor again — splitter should reappear
		panel2.Visible = true;
		_ = splitter.AboveControl; // triggers re-resolve since _neighborsResolved was cleared
		Assert.True(splitter.Visible);
	}

	[Fact]
	public void ManualHide_NotOverriddenByAutoShow()
	{
		var ws = TestWindowSystemBuilder.CreateTestSystem();
		var panel1 = new PanelControl("top") { VerticalAlignment = VerticalAlignment.Fill };
		var splitter = new HorizontalSplitterControl();
		var panel2 = new PanelControl("bottom") { VerticalAlignment = VerticalAlignment.Fill };

		var window = new WindowBuilder(ws)
			.WithTitle("Test")
			.WithSize(40, 20)
			.AddControl(panel1)
			.AddControl(splitter)
			.AddControl(panel2)
			.Build();

		window.EnsureContentReady();

		// User explicitly hides the splitter
		splitter.Visible = false;

		// Both neighbors are visible, but splitter should stay hidden
		// because user explicitly set it
		Assert.False(splitter.Visible);
	}

	#endregion
}
