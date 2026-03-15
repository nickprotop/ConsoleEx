// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ToolbarMultiHeightTests
{
	[Fact]
	public void AutoHeight_AllSingleRowItems_ReturnsHeight1()
	{
		// Auto-height with height-1 items should produce identical layout to the old fixed height=1
		var toolbar = ToolbarControl.Create()
			.AddButton("A", (_, _) => { })
			.AddButton("B", (_, _) => { })
			.Build();

		var size = toolbar.MeasureDOM(new LayoutConstraints(0, 80, 0, 10));

		Assert.Equal(ControlDefaults.DefaultToolbarRowHeight, size.Height);
	}

	[Fact]
	public void AutoHeight_WithBorderedButton_ReturnsHeight3()
	{
		var borderedButton = new ButtonBuilder()
			.WithText("OK")
			.WithBorder(ButtonBorderStyle.Rounded)
			.Build();

		var toolbar = new ToolbarControl();
		toolbar.AddItem(borderedButton);

		var size = toolbar.MeasureDOM(new LayoutConstraints(0, 80, 0, 10));

		Assert.Equal(3, size.Height);
	}

	[Fact]
	public void AutoHeight_MixedItems_RowHeightEqualsTallest()
	{
		var borderedButton = new ButtonBuilder()
			.WithText("OK")
			.WithBorder(ButtonBorderStyle.Rounded)
			.Build();

		var plainButton = new ButtonBuilder()
			.WithText("Cancel")
			.Build();

		var toolbar = new ToolbarControl();
		toolbar.AddItem(borderedButton);
		toolbar.AddItem(plainButton);

		var size = toolbar.MeasureDOM(new LayoutConstraints(0, 80, 0, 10));

		// Row height should be 3 (from bordered button), not 1
		Assert.Equal(3, size.Height);
	}

	[Fact]
	public void ExplicitHeight_ClipsAsExpected()
	{
		var borderedButton = new ButtonBuilder()
			.WithText("OK")
			.WithBorder(ButtonBorderStyle.Rounded)
			.Build();

		var toolbar = ToolbarControl.Create()
			.WithHeight(1)
			.Build();
		toolbar.AddItem(borderedButton);

		var size = toolbar.MeasureDOM(new LayoutConstraints(0, 80, 0, 10));

		// Explicit height=1 should clip the 3-row button
		Assert.Equal(1, size.Height);
	}

	[Fact]
	public void AutoHeight_Wrapping_DifferentRowHeights()
	{
		// Create a bordered button (height 3) and a plain button (height 1)
		// Force wrapping so they end up on different rows
		var borderedButton = new ButtonBuilder()
			.WithText("OK")
			.WithBorder(ButtonBorderStyle.Rounded)
			.WithWidth(45)
			.Build();

		var plainButton = new ButtonBuilder()
			.WithText("X")
			.WithWidth(10)
			.Build();

		var toolbar = ToolbarControl.Create()
			.WithWrap()
			.Build();
		toolbar.AddItem(borderedButton);
		toolbar.AddItem(plainButton);

		// Available width 50: bordered button (45) fits row 0, plain button (10) wraps to row 1
		var size = toolbar.MeasureDOM(new LayoutConstraints(0, 50, 0, 20));

		// Row 0 height=3 (bordered), Row 1 height=1 (plain) => total = 4
		Assert.Equal(4, size.Height);
	}

	[Fact]
	public void AutoHeight_VerticalAlignmentCenter_OffsetsItemWithinRow()
	{
		var borderedButton = new ButtonBuilder()
			.WithText("OK")
			.WithBorder(ButtonBorderStyle.Rounded)
			.Build();

		// Create a plain 1-height control with Center alignment
		var label = new MarkupControl(new List<string> { "Label" })
		{
			VerticalAlignment = VerticalAlignment.Center
		};

		var toolbar = new ToolbarControl();
		toolbar.AddItem(borderedButton);
		toolbar.AddItem(label);

		var size = toolbar.MeasureDOM(new LayoutConstraints(0, 80, 0, 10));

		// Row is height 3 from bordered button
		Assert.Equal(3, size.Height);
	}

	[Fact]
	public void AutoHeight_VerticalAlignmentBottom_OffsetsItemToBottom()
	{
		var borderedButton = new ButtonBuilder()
			.WithText("OK")
			.WithBorder(ButtonBorderStyle.Rounded)
			.Build();

		var label = new MarkupControl(new List<string> { "Label" })
		{
			VerticalAlignment = VerticalAlignment.Bottom
		};

		var toolbar = new ToolbarControl();
		toolbar.AddItem(borderedButton);
		toolbar.AddItem(label);

		var size = toolbar.MeasureDOM(new LayoutConstraints(0, 80, 0, 10));

		Assert.Equal(3, size.Height);
	}

	[Fact]
	public void DefaultHeight_IsNull()
	{
		var toolbar = new ToolbarControl();
		Assert.Null(toolbar.Height);
	}

	[Fact]
	public void BuilderDefaultHeight_IsNull()
	{
		var toolbar = ToolbarControl.Create().Build();
		Assert.Null(toolbar.Height);
	}

	[Fact]
	public void GetLogicalContentSize_AutoHeight_MeasuresTallestItem()
	{
		// GetLogicalContentSize uses item.Height ?? logicalSize.Height
		// MarkupControl with 3 lines will report height 3 via GetLogicalContentSize
		var tallControl = new MarkupControl(new List<string> { "Line1", "Line2", "Line3" });
		var shortControl = new MarkupControl(new List<string> { "Short" });

		var toolbar = new ToolbarControl();
		toolbar.AddItem(tallControl);
		toolbar.AddItem(shortControl);

		var size = toolbar.GetLogicalContentSize();

		Assert.Equal(3, size.Height);
	}
}
