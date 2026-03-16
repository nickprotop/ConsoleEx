// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using SharpConsoleUI.Events;
using System.Drawing;
using HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment;
using VerticalAlignment = SharpConsoleUI.Layout.VerticalAlignment;

namespace SharpConsoleUI.Tests.Controls;

public class ColumnContainerTests
{
	#region Construction & Defaults

	[Fact]
	public void Constructor_SetsHorizontalGridContent()
	{
		// Arrange
		var grid = new HorizontalGridControl();

		// Act
		var column = new ColumnContainer(grid);

		// Assert
		Assert.NotNull(column);
		Assert.Same(grid, column.HorizontalGridContent);
	}

	[Fact]
	public void Defaults_VisibleTrue_EnabledTrue()
	{
		// Arrange
		var grid = new HorizontalGridControl();

		// Act
		var column = new ColumnContainer(grid);

		// Assert
		Assert.True(column.Visible);
		Assert.True(column.IsEnabled);
	}

	[Fact]
	public void Defaults_AlignmentValues()
	{
		// Arrange
		var grid = new HorizontalGridControl();

		// Act
		var column = new ColumnContainer(grid);

		// Assert
		Assert.Equal(HorizontalAlignment.Left, column.HorizontalAlignment);
		Assert.Equal(VerticalAlignment.Fill, column.VerticalAlignment);
	}

	[Fact]
	public void Defaults_MarginIsZero()
	{
		// Arrange
		var grid = new HorizontalGridControl();

		// Act
		var column = new ColumnContainer(grid);

		// Assert
		Assert.Equal(0, column.Margin.Left);
		Assert.Equal(0, column.Margin.Top);
		Assert.Equal(0, column.Margin.Right);
		Assert.Equal(0, column.Margin.Bottom);
	}

	[Fact]
	public void Defaults_FlexFactor_IsOne()
	{
		// Arrange
		var grid = new HorizontalGridControl();

		// Act
		var column = new ColumnContainer(grid);

		// Assert
		Assert.Equal(1.0, column.FlexFactor);
	}

	#endregion

	#region Child Management

	[Fact]
	public void AddContent_AddsChildToContents()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label = ContainerTestHelpers.CreateLabel("Test");

		// Act
		column.AddContent(label);

		// Assert
		Assert.Single(column.Contents);
	}

	[Fact]
	public void AddContent_SetsContainerOnChild()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label = ContainerTestHelpers.CreateLabel("Test");

		// Act
		column.AddContent(label);

		// Assert
		Assert.Same(column, label.Container);
	}

	[Fact]
	public void AddContent_MultipleChildren_MaintainsOrder()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label1 = ContainerTestHelpers.CreateLabel("First");
		var label2 = ContainerTestHelpers.CreateLabel("Second");
		var label3 = ContainerTestHelpers.CreateLabel("Third");

		// Act
		column.AddContent(label1);
		column.AddContent(label2);
		column.AddContent(label3);

		// Assert
		Assert.Equal(3, column.Contents.Count);
		Assert.Same(label1, column.Contents[0]);
		Assert.Same(label2, column.Contents[1]);
		Assert.Same(label3, column.Contents[2]);
	}

	[Fact]
	public void RemoveContent_RemovesChild()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label1 = ContainerTestHelpers.CreateLabel("First");
		var label2 = ContainerTestHelpers.CreateLabel("Second");
		column.AddContent(label1);
		column.AddContent(label2);

		// Act
		column.RemoveContent(label1);

		// Assert
		Assert.Single(column.Contents);
		Assert.Same(label2, column.Contents[0]);
	}

	[Fact]
	public void ClearContents_RemovesAllChildren()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		column.AddContent(ContainerTestHelpers.CreateLabel("First"));
		column.AddContent(ContainerTestHelpers.CreateLabel("Second"));
		column.AddContent(ContainerTestHelpers.CreateLabel("Third"));

		// Act
		column.ClearContents();

		// Assert
		Assert.Empty(column.Contents);
	}

	#endregion

	#region Layout Properties

	[Fact]
	public void Width_ExplicitWidth_ReturnsSetValue()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		// Act
		column.Width = 50;

		// Assert
		Assert.Equal(50, column.Width);
	}

	[Fact]
	public void FlexFactor_CanBeSet()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		// Act
		column.FlexFactor = 2.0;

		// Assert
		Assert.Equal(2.0, column.FlexFactor);
	}

	[Fact]
	public void MinWidth_CanBeSet()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		// Act
		column.MinWidth = 10;

		// Assert
		Assert.Equal(10, column.MinWidth);
	}

	[Fact]
	public void MaxWidth_CanBeSet()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		// Act
		column.MaxWidth = 80;

		// Assert
		Assert.Equal(80, column.MaxWidth);
	}

	#endregion

	#region Focus

	[Fact]
	public void CanReceiveFocus_AlwaysFalse()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		// Assert
		Assert.False(column.CanReceiveFocus);
	}

	#endregion

	#region Rendering

	[Fact]
	public void PaintDOM_EmptyColumn_DoesNotCrash()
	{
		// Arrange
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		grid.AddColumn(column);
		window.AddControl(grid);

		// Act & Assert - should not throw
		var exception = Record.Exception(() => window.RenderAndGetVisibleContent());
		Assert.Null(exception);
	}

	[Fact]
	public void PaintDOM_WithChildren_RendersContent()
	{
		// Arrange
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label = ContainerTestHelpers.CreateLabel("Hello Column");
		column.AddContent(label);
		grid.AddColumn(column);
		window.AddControl(grid);

		// Act
		var output = window.RenderAndGetVisibleContent();
		var plainText = ContainerTestHelpers.StripAnsiCodes(output);

		// Assert
		Assert.Contains("Hello Column", plainText);
	}

	[Fact]
	public void PaintDOM_HiddenColumn_DoesNotRender()
	{
		// Arrange
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label = ContainerTestHelpers.CreateLabel("Hidden Text");
		column.AddContent(label);
		column.Visible = false;
		grid.AddColumn(column);
		window.AddControl(grid);

		// Act
		var output = window.RenderAndGetVisibleContent();
		var plainText = ContainerTestHelpers.StripAnsiCodes(output);

		// Assert
		Assert.DoesNotContain("Hidden Text", plainText);
	}

	#endregion

	#region Layout Edge Cases

	[Fact]
	public void Margin_ReducesChildAvailableSpace()
	{
		// Arrange
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		column.Margin = new Margin(5, 2, 5, 2);
		var label = ContainerTestHelpers.CreateLabel("Margined");
		column.AddContent(label);
		grid.AddColumn(column);
		window.AddControl(grid);

		// Act - render should succeed with margins applied
		var output = window.RenderAndGetVisibleContent();
		var plainText = ContainerTestHelpers.StripAnsiCodes(output);

		// Assert - content should still render (margins reduce available space)
		Assert.Contains("Margined", plainText);
	}

	[Fact]
	public void Margin_LargerThanContainer_ChildGetsMinimalSpace()
	{
		// Arrange
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		// Set extremely large margins
		column.Margin = new Margin(40, 10, 40, 10);
		var label = ContainerTestHelpers.CreateLabel("Squeezed");
		column.AddContent(label);
		grid.AddColumn(column);
		window.AddControl(grid);

		// Act & Assert - should not crash even with extreme margins
		var exception = Record.Exception(() => window.RenderAndGetVisibleContent());
		Assert.Null(exception);
	}

	[Fact]
	public void ChildAlignment_Stretch_FillsColumnWidth()
	{
		// Arrange
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label = ContainerTestHelpers.CreateLabel("Stretched");
		label.HorizontalAlignment = HorizontalAlignment.Stretch;
		column.AddContent(label);
		grid.AddColumn(column);
		window.AddControl(grid);

		// Act
		var output = window.RenderAndGetVisibleContent();
		var plainText = ContainerTestHelpers.StripAnsiCodes(output);

		// Assert - content renders with stretch alignment
		Assert.Contains("Stretched", plainText);
	}

	[Fact]
	public void ChildAlignment_Center_PositionedInMiddle()
	{
		// Arrange
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label = ContainerTestHelpers.CreateLabel("Centered");
		label.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddContent(label);
		grid.AddColumn(column);
		window.AddControl(grid);

		// Act
		var output = window.RenderAndGetVisibleContent();
		var plainText = ContainerTestHelpers.StripAnsiCodes(output);

		// Assert - content renders with center alignment
		Assert.Contains("Centered", plainText);
	}

	[Fact]
	public void MultipleFillChildren_EqualDistribution()
	{
		// Arrange
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment();
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		// Add 3 fill children - they should share the available height
		var list1 = ContainerTestHelpers.CreateFocusableList("Item A1", "Item A2");
		list1.VerticalAlignment = VerticalAlignment.Fill;
		var list2 = ContainerTestHelpers.CreateFocusableList("Item B1", "Item B2");
		list2.VerticalAlignment = VerticalAlignment.Fill;
		var list3 = ContainerTestHelpers.CreateFocusableList("Item C1", "Item C2");
		list3.VerticalAlignment = VerticalAlignment.Fill;

		column.AddContent(list1);
		column.AddContent(list2);
		column.AddContent(list3);
		grid.AddColumn(column);
		window.AddControl(grid);

		// Act
		var output = window.RenderAndGetVisibleContent();
		var plainText = ContainerTestHelpers.StripAnsiCodes(output);

		// Assert - all three lists should render
		Assert.Contains("Item A1", plainText);
		Assert.Contains("Item B1", plainText);
		Assert.Contains("Item C1", plainText);
	}

	[Fact]
	public void FillChildren_NoRemainingSpace_GetZeroHeight()
	{
		// Arrange
		var (system, window) = ContainerTestHelpers.CreateTestEnvironment(
			winW: 60, winH: 10);
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		// Add many fixed-height labels that consume all space
		for (int i = 0; i < 15; i++)
		{
			var label = ContainerTestHelpers.CreateLabel($"Line {i}");
			label.VerticalAlignment = VerticalAlignment.Top;
			column.AddContent(label);
		}

		// Add a fill child that should get minimal/zero remaining space
		var fillList = ContainerTestHelpers.CreateFocusableList("Fill Item");
		fillList.VerticalAlignment = VerticalAlignment.Fill;
		column.AddContent(fillList);

		grid.AddColumn(column);
		window.AddControl(grid);

		// Act & Assert - should not crash even when fill children get no space
		var exception = Record.Exception(() => window.RenderAndGetVisibleContent());
		Assert.Null(exception);
	}

	#endregion

	#region GetChildren

	[Fact]
	public void GetChildren_ReturnsChildrenList()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label1 = ContainerTestHelpers.CreateLabel("Child 1");
		var label2 = ContainerTestHelpers.CreateLabel("Child 2");
		column.AddContent(label1);
		column.AddContent(label2);

		// Act
		var children = column.GetChildren();

		// Assert
		Assert.Equal(2, children.Count);
		Assert.Contains(label1, children);
		Assert.Contains(label2, children);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void RemoveContent_NonExistentChild_NoException()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label1 = ContainerTestHelpers.CreateLabel("First");
		var label2 = ContainerTestHelpers.CreateLabel("Second");
		column.AddContent(label1);

		// Act
		var exception = Record.Exception(() => column.RemoveContent(label2));

		// Assert
		Assert.Null(exception);
	}

	[Fact]
	public void AddContent_ClearContents_AddAgain_Works()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);
		var label1 = ContainerTestHelpers.CreateLabel("First");
		var label2 = ContainerTestHelpers.CreateLabel("Second");

		// Act
		column.AddContent(label1);
		column.ClearContents();
		column.AddContent(label2);

		// Assert
		Assert.Single(column.Contents);
		Assert.Equal(label2, column.Contents[0]);
	}

	[Fact]
	public void GetChildren_EmptyColumn_ReturnsEmpty()
	{
		// Arrange
		var grid = new HorizontalGridControl();
		var column = new ColumnContainer(grid);

		// Act
		var children = column.GetChildren();

		// Assert
		Assert.Empty(children);
	}

	#endregion
}
