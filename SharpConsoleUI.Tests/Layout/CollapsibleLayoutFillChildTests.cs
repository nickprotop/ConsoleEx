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
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// Regression tests for issue #83: CollapsibleLayout arranged every body child at its measured
/// height cut to the space left, with no share of the leftover height for VerticalAlignment.Fill
/// children — unlike VerticalStackLayout. Inside a grid (which measures cells a second time with an
/// unbounded height) a Fill ScrollablePanel fell back to its 10-row unbounded default and a Fill
/// GridControl with a star row collapsed to 0.
/// </summary>
public class CollapsibleLayoutFillChildTests
{
	private const int WindowHeight = 20;
	private const int HeadingRows = 1;
	private const int ExpectedFillHeight = WindowHeight - HeadingRows;

	private static IWindowControl MakeFillChild(string kind)
	{
		var lines = SharpConsoleUI.Builders.Controls.Markup()
			.AddLines(Enumerable.Range(1, 50).Select(i => $"row {i}").ToArray())
			.Build();

		var scroller = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.AddControl(lines)
			.Build();

		if (kind == "scrollablepanel")
			return scroller;

		// A Fill GridControl with a star row: the case that collapsed to 0 rows entirely.
		var grid = new GridControl
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill
		};
		grid.ColumnDefinitions.Add(GridLength.Star());
		grid.RowDefinitions.Add(GridLength.Star());
		grid.Place(scroller, 0, 0);
		return grid;
	}

	private static PanelControl MakePanel(IWindowControl fill) =>
		SharpConsoleUI.Builders.Controls.Panel()
			.NoBorder()
			.WithPadding(0)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("heading").Build())
			.AddControl(fill)
			.Build();

	private static int MeasureFillHeight(Func<IWindowControl, IWindowControl> host, string kind)
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(80, 25),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 40,
			Height = WindowHeight,
			BorderStyle = BorderStyle.Frameless
		};

		var fill = MakeFillChild(kind);
		window.AddControl(host(fill));
		system.AddWindow(window);

		// Render twice: the grid's second measure pass (unbounded height) is what used to leave the
		// Fill child at its natural height, so the bug only settles on the second frame.
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		return fill.ActualHeight;
	}

	[Theory]
	[InlineData("scrollablepanel")]
	[InlineData("grid")]
	public void FillChild_InPanel_DirectlyInWindow_FillsBody(string kind)
	{
		// Case A from the issue: already correct, kept as the control case.
		Assert.Equal(ExpectedFillHeight, MeasureFillHeight(fill => MakePanel(fill), kind));
	}

	[Theory]
	[InlineData("scrollablepanel")]
	[InlineData("grid")]
	public void FillChild_InPanel_InHorizontalGridColumn_FillsBody(string kind)
	{
		// Case B from the issue: the reported bug.
		int height = MeasureFillHeight(
			fill => SharpConsoleUI.Builders.Controls.HorizontalGrid()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.Column(column => column.Add(MakePanel(fill)))
				.Build(),
			kind);

		Assert.Equal(ExpectedFillHeight, height);
	}

	[Theory]
	[InlineData("scrollablepanel")]
	[InlineData("grid")]
	public void FillChild_InColumn_WithoutPanel_FillsBody(string kind)
	{
		// Case C from the issue: correct before and after, proves the column itself was never at fault.
		int height = MeasureFillHeight(
			fill => SharpConsoleUI.Builders.Controls.HorizontalGrid()
				.WithVerticalAlignment(VerticalAlignment.Fill)
				.Column(column => column
					.Add(SharpConsoleUI.Builders.Controls.Markup("heading").Build())
					.Add(fill))
				.Build(),
			kind);

		Assert.Equal(ExpectedFillHeight, height);
	}

	[Fact]
	public void NonFillChildren_KeepTheirMeasuredHeight()
	{
		// The fix must not change how non-Fill children are arranged: a short body child stays short
		// rather than being stretched over the panel body.
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(80, 25),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 40,
			Height = WindowHeight,
			BorderStyle = BorderStyle.Frameless
		};

		var shortChild = SharpConsoleUI.Builders.Controls.Markup("just one line").Build();
		var panel = SharpConsoleUI.Builders.Controls.Panel()
			.NoBorder()
			.WithPadding(0)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("heading").Build())
			.AddControl(shortChild)
			.Build();

		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		Assert.Equal(1, shortChild.ActualHeight);
	}

	[Fact]
	public void TwoFillChildren_ShareTheLeftoverHeight()
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(80, 25),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 40,
			Height = WindowHeight,
			BorderStyle = BorderStyle.Frameless
		};

		var first = (ScrollablePanelControl)MakeFillChild("scrollablepanel");
		var second = (ScrollablePanelControl)MakeFillChild("scrollablepanel");
		var panel = SharpConsoleUI.Builders.Controls.Panel()
			.NoBorder()
			.WithPadding(0)
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.AddControl(SharpConsoleUI.Builders.Controls.Markup("heading").Build())
			.AddControl(first)
			.AddControl(second)
			.Build();

		window.AddControl(panel);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();
		window.RenderAndGetVisibleContent();

		Assert.Equal(ExpectedFillHeight, first.ActualHeight + second.ActualHeight);
		Assert.InRange(first.ActualHeight, second.ActualHeight - 1, second.ActualHeight + 1);
	}
}
