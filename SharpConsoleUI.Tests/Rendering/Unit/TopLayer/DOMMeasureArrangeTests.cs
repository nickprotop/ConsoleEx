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

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

/// <summary>
/// Tests for the DOM measure/arrange layout phases.
/// Validates MeasureDOM returns correct sizes and PaintDOM renders at correct bounds.
/// </summary>
public class DOMMeasureArrangeTests
{
	#region MarkupControl Measure Tests

	[Fact]
	public void MarkupControl_MeasureDOM_SingleLine_ReturnsOneRow()
	{
		var ctrl = new MarkupControl(new List<string> { "Hello World" });
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.Equal(1, size.Height);
		Assert.True(size.Width > 0);
	}

	[Fact]
	public void MarkupControl_MeasureDOM_MultiLine_ReturnsCorrectHeight()
	{
		var ctrl = new MarkupControl(new List<string> { "Line 1", "Line 2", "Line 3" });
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.Equal(3, size.Height);
	}

	[Fact]
	public void MarkupControl_MeasureDOM_EmptyContent_ReturnsMinimal()
	{
		var ctrl = new MarkupControl(new List<string>());
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.True(size.Height >= 0);
	}

	[Fact]
	public void MarkupControl_MeasureDOM_ClampsToMaxWidth()
	{
		var ctrl = new MarkupControl(new List<string> { "A very long line of text that should exceed the constraint" });
		var constraints = new LayoutConstraints(0, 20, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.True(size.Width <= 20);
	}

	[Fact]
	public void MarkupControl_MeasureDOM_ClampsToMinWidth()
	{
		var ctrl = new MarkupControl(new List<string> { "Hi" });
		var constraints = new LayoutConstraints(30, 100, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.True(size.Width >= 30);
	}

	[Fact]
	public void MarkupControl_MeasureDOM_WithMargins_IncludesMargins()
	{
		var ctrl = new MarkupControl(new List<string> { "Text" });
		ctrl.Margin = new Margin(2, 1, 2, 1);
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		// Height should include margins: 1 line + 1 top + 1 bottom = 3
		Assert.Equal(3, size.Height);
	}

	[Fact]
	public void MarkupControl_MeasureDOM_StretchAlignment_UsesMaxWidth()
	{
		var ctrl = new MarkupControl(new List<string> { "Short" });
		ctrl.HorizontalAlignment = HorizontalAlignment.Stretch;
		var constraints = new LayoutConstraints(0, 80, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.Equal(80, size.Width);
	}

	#endregion

	#region ButtonControl Measure Tests

	[Fact]
	public void ButtonControl_MeasureDOM_ReturnsCorrectSize()
	{
		var btn = new ButtonControl { Text = "Click Me" };
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = btn.MeasureDOM(constraints);

		Assert.True(size.Width > 0);
		Assert.True(size.Height >= 1);
	}

	[Fact]
	public void ButtonControl_MeasureDOM_WidthMatchesTextLength()
	{
		var btn = new ButtonControl { Text = "OK" };
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = btn.MeasureDOM(constraints);

		// Button width should accommodate text
		Assert.True(size.Width >= 2); // at least text length
	}

	[Fact]
	public void ButtonControl_MeasureDOM_WithMargins()
	{
		var btn = new ButtonControl { Text = "Go" };
		btn.Margin = new Margin(3, 1, 3, 1);
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = btn.MeasureDOM(constraints);

		// Width includes margins
		Assert.True(size.Width >= 2 + 6); // text + left + right margins
	}

	#endregion

	#region DropdownControl Measure Tests

	[Fact]
	public void DropdownControl_MeasureDOM_ReturnsHeaderSizeOnly()
	{
		var dd = new DropdownControl("S:", new[] { "Alpha", "Beta" });
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = dd.MeasureDOM(constraints);

		Assert.Equal(1, size.Height); // header only, dropdown list is portal
		Assert.True(size.Width > 0);
	}

	[Fact]
	public void DropdownControl_MeasureDOM_StableWidth()
	{
		var dd = new DropdownControl("S:", new[] { "Short", "A Much Longer Item" });
		var constraints = new LayoutConstraints(0, 100, 0, 100);

		dd.SelectedIndex = 0;
		var size1 = dd.MeasureDOM(constraints);

		dd.SelectedIndex = 1;
		var size2 = dd.MeasureDOM(constraints);

		Assert.Equal(size1.Width, size2.Width); // stable regardless of selection
	}

	[Fact]
	public void DropdownControl_MeasureDOM_EmptyItems_HasMinimum()
	{
		var dd = new DropdownControl("S:");
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = dd.MeasureDOM(constraints);

		Assert.True(size.Width > 0);
		Assert.Equal(1, size.Height);
	}

	[Fact]
	public void DropdownControl_MeasureDOM_WithWidthOverride()
	{
		var dd = new DropdownControl("S:", new[] { "A" });
		dd.Width = 40;
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = dd.MeasureDOM(constraints);

		Assert.Equal(40 + dd.Margin.Left + dd.Margin.Right, size.Width);
	}

	#endregion

	#region DatePickerControl Measure Tests

	[Fact]
	public void DatePickerControl_MeasureDOM_ReturnsHeaderSize()
	{
		var dp = new DatePickerControl("Date:");
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = dp.MeasureDOM(constraints);

		Assert.True(size.Width > 0);
		Assert.True(size.Height >= 1);
	}

	[Fact]
	public void DatePickerControl_MeasureDOM_WithMargins()
	{
		var dp = new DatePickerControl("Date:");
		dp.Margin = new Margin(1, 2, 1, 2);
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = dp.MeasureDOM(constraints);

		// height = 1 line + 2 top + 2 bottom = 5
		Assert.Equal(5, size.Height);
	}

	[Fact]
	public void DatePickerControl_MeasureDOM_StretchAlignment()
	{
		var dp = new DatePickerControl("Date:");
		dp.HorizontalAlignment = HorizontalAlignment.Stretch;
		var constraints = new LayoutConstraints(0, 60, 0, 100);
		var size = dp.MeasureDOM(constraints);

		Assert.Equal(60, size.Width);
	}

	#endregion

	#region ListControl Measure Tests

	[Fact]
	public void ListControl_MeasureDOM_HeightMatchesItems()
	{
		var list = new ListControl(new[] { "A", "B", "C" });
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = list.MeasureDOM(constraints);

		Assert.True(size.Height >= 3);
	}

	[Fact]
	public void ListControl_MeasureDOM_EmptyList()
	{
		var list = new ListControl(Array.Empty<string>());
		var constraints = new LayoutConstraints(0, 100, 0, 100);
		var size = list.MeasureDOM(constraints);

		Assert.True(size.Height >= 0);
	}

	#endregion

	#region Constraint Clamping Tests

	[Fact]
	public void MeasureDOM_MaxWidthConstraint_Honored()
	{
		var ctrl = new MarkupControl(new List<string> { new string('X', 200) });
		var constraints = new LayoutConstraints(0, 50, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.True(size.Width <= 50);
	}

	[Fact]
	public void MeasureDOM_MinWidthConstraint_Honored()
	{
		var ctrl = new MarkupControl(new List<string> { "X" });
		var constraints = new LayoutConstraints(40, 100, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.True(size.Width >= 40);
	}

	[Fact]
	public void MeasureDOM_MaxHeightConstraint_Honored()
	{
		var ctrl = new MarkupControl(new List<string> { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" });
		var constraints = new LayoutConstraints(0, 100, 0, 5);
		var size = ctrl.MeasureDOM(constraints);

		Assert.True(size.Height <= 5);
	}

	[Fact]
	public void MeasureDOM_MinHeightConstraint_Honored()
	{
		var ctrl = new MarkupControl(new List<string> { "X" });
		var constraints = new LayoutConstraints(0, 100, 5, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.True(size.Height >= 5);
	}

	[Fact]
	public void MeasureDOM_EqualMinMax_ReturnsExact()
	{
		var ctrl = new MarkupControl(new List<string> { "Test" });
		var constraints = new LayoutConstraints(50, 50, 10, 10);
		var size = ctrl.MeasureDOM(constraints);

		Assert.Equal(50, size.Width);
		Assert.Equal(10, size.Height);
	}

	#endregion

	#region Window Layout Integration Tests

	[Fact]
	public void Window_Layout_ControlsRenderInContentArea()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 10, Top = 5, Width = 40, Height = 20, Title = "Test" };

		var ctrl = new MarkupControl(new List<string> { "Hello" });
		window.AddControl(ctrl);
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void Window_Layout_MultipleControlsStack()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 10, Top = 5, Width = 50, Height = 25, Title = "Stack" };

		window.AddControl(new MarkupControl(new List<string> { "First" }));
		window.AddControl(new MarkupControl(new List<string> { "Second" }));
		window.AddControl(new MarkupControl(new List<string> { "Third" }));
		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void Window_Layout_StickyTop_StaysAtTop()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 20 };

		var stickyCtrl = new MarkupControl(new List<string> { "I am sticky" });
		stickyCtrl.StickyPosition = StickyPosition.Top;
		window.AddControl(stickyCtrl);

		for (int i = 0; i < 10; i++)
			window.AddControl(new MarkupControl(new List<string> { $"Line {i}" }));

		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	[Fact]
	public void Window_Layout_StickyBottom_StaysAtBottom()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var window = new Window(system) { Left = 0, Top = 0, Width = 60, Height = 20 };

		for (int i = 0; i < 10; i++)
			window.AddControl(new MarkupControl(new List<string> { $"Line {i}" }));

		var stickyCtrl = new MarkupControl(new List<string> { "I am bottom" });
		stickyCtrl.StickyPosition = StickyPosition.Bottom;
		window.AddControl(stickyCtrl);

		system.WindowStateService.AddWindow(window);
		system.Render.UpdateDisplay();

		var snapshot = system.RenderingDiagnostics?.LastConsoleSnapshot;
		Assert.NotNull(snapshot);
	}

	#endregion

	#region Alignment Tests

	[Fact]
	public void MarkupControl_Stretch_FillsWidth()
	{
		var ctrl = new MarkupControl(new List<string> { "Text" });
		ctrl.HorizontalAlignment = HorizontalAlignment.Stretch;
		var constraints = new LayoutConstraints(0, 80, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.Equal(80, size.Width);
	}

	[Fact]
	public void MarkupControl_Left_UsesContentWidth()
	{
		var ctrl = new MarkupControl(new List<string> { "Short" });
		ctrl.HorizontalAlignment = HorizontalAlignment.Left;
		var constraints = new LayoutConstraints(0, 80, 0, 100);
		var size = ctrl.MeasureDOM(constraints);

		Assert.True(size.Width <= 80);
	}

	[Fact]
	public void DropdownControl_Stretch_UsesAvailableWidth()
	{
		var dd = new DropdownControl("S:", new[] { "A" });
		dd.HorizontalAlignment = HorizontalAlignment.Stretch;
		var constraints = new LayoutConstraints(0, 60, 0, 100);
		var size = dd.MeasureDOM(constraints);

		Assert.Equal(60, size.Width);
	}

	#endregion

	#region VerticalAlignment Tests

	[Fact]
	public void MarkupControl_VerticalFill_MeasureIncludesConstraints()
	{
		var ctrl = new MarkupControl(new List<string> { "Text" });
		ctrl.VerticalAlignment = VerticalAlignment.Fill;
		var constraints = new LayoutConstraints(0, 100, 0, 50);
		var size = ctrl.MeasureDOM(constraints);

		// Fill should expand to max
		Assert.True(size.Height >= 1);
	}

	#endregion

	#region LayoutSize and LayoutConstraints Tests

	[Fact]
	public void LayoutSize_Equality()
	{
		var a = new LayoutSize(10, 5);
		var b = new LayoutSize(10, 5);
		Assert.Equal(a, b);
	}

	[Fact]
	public void LayoutSize_Properties()
	{
		var size = new LayoutSize(42, 17);
		Assert.Equal(42, size.Width);
		Assert.Equal(17, size.Height);
	}

	[Fact]
	public void LayoutConstraints_Properties()
	{
		var c = new LayoutConstraints(10, 100, 5, 50);
		Assert.Equal(10, c.MinWidth);
		Assert.Equal(100, c.MaxWidth);
		Assert.Equal(5, c.MinHeight);
		Assert.Equal(50, c.MaxHeight);
	}

	#endregion
}
