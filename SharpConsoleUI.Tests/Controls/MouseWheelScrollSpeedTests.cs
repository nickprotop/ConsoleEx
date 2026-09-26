// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Issue #84: the scrolling controls scrolled by the fixed ControlDefaults.DefaultScrollWheelLines
/// with no way to change it, while a scrolling window moved 3 lines.
/// </summary>
[Collection("WheelStepDefault")]
public class MouseWheelScrollSpeedTests
{
	[Fact]
	public void ScrollablePanel_DefaultsToLibraryDefault()
	{
		var panel = new ScrollablePanelControl();
		Assert.Equal(ControlDefaults.DefaultScrollWheelLines, panel.MouseWheelScrollSpeed);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-5, 1)]
	[InlineData(3, 3)]
	public void ScrollablePanel_ClampsToAtLeastOne(int set, int expected)
	{
		var panel = new ScrollablePanelControl { MouseWheelScrollSpeed = set };
		Assert.Equal(expected, panel.MouseWheelScrollSpeed);
	}

	[Fact]
	public void ScrollablePanel_RaisesPropertyChanged()
	{
		var panel = new ScrollablePanelControl();
		string? changed = null;
		panel.PropertyChanged += (_, e) => changed = e.PropertyName;

		panel.MouseWheelScrollSpeed = 4;

		Assert.Equal(nameof(ScrollablePanelControl.MouseWheelScrollSpeed), changed);
	}

	[Fact]
	public void ScrollablePanel_Builder_SetsTheSpeed()
	{
		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithMouseWheelScrollSpeed(4)
			.Build();

		Assert.Equal(4, panel.MouseWheelScrollSpeed);
	}

	/// <summary>
	/// The real usage path: a wheel notch delivered through the dispatcher moves exactly
	/// MouseWheelScrollSpeed lines, not the library default.
	/// </summary>
	[Fact]
	public void ScrollablePanel_WheelNotch_MovesConfiguredLines()
	{
		var driver = new HeadlessConsoleDriver(40, 12);
		var system = new ConsoleWindowSystem(
			driver,
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 40,
			Height = 12,
			BorderStyle = BorderStyle.Frameless
		};

		var panel = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithVerticalAlignment(VerticalAlignment.Fill)
			.WithMouseWheelScrollSpeed(4)
			.AddControl(SharpConsoleUI.Builders.Controls.Markup()
				.AddLines(Enumerable.Range(1, 50).Select(i => $"row {i}").ToArray())
				.Build())
			.Build();

		window.AddControl(panel);
		new Thread(() => system.Run()) { IsBackground = true }.Start();
		system.EnqueueOnUIThread(() => system.AddWindow(window));

		Assert.True(
			SpinWait.SpinUntil(() => panel.ActualHeight > 0
				&& system.WindowStateService.ActiveWindow == window
				&& system.GetWindowAtPoint(new Point(0, 0)) == window, 5000),
			"the window did not become active and hit-testable in time");

		try
		{
			driver.SimulateMouseEvent([MouseFlags.ReportMousePosition], new Point(5, 5));
			driver.SimulateMouseEvent([MouseFlags.WheeledDown], new Point(5, 5));
			SpinWait.SpinUntil(() => panel.VerticalScrollOffset >= 4, 2000);

			Assert.Equal(4, panel.VerticalScrollOffset);
		}
		finally
		{
			system.Shutdown();
		}
	}

	[Fact]
	public void Tree_DefaultsToLibraryDefault()
	{
		var tree = new TreeControl();
		Assert.Equal(ControlDefaults.DefaultScrollWheelLines, tree.MouseWheelScrollSpeed);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-5, 1)]
	[InlineData(3, 3)]
	public void Tree_ClampsToAtLeastOne(int set, int expected)
	{
		var tree = new TreeControl { MouseWheelScrollSpeed = set };
		Assert.Equal(expected, tree.MouseWheelScrollSpeed);
	}

	[Fact]
	public void Tree_RaisesPropertyChanged()
	{
		var tree = new TreeControl();
		string? changed = null;
		tree.PropertyChanged += (_, e) => changed = e.PropertyName;

		tree.MouseWheelScrollSpeed = 4;

		Assert.Equal(nameof(TreeControl.MouseWheelScrollSpeed), changed);
	}

	[Fact]
	public void Table_DefaultsToLibraryDefault()
	{
		var table = new TableControl();
		Assert.Equal(ControlDefaults.DefaultScrollWheelLines, table.MouseWheelScrollSpeed);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-5, 1)]
	[InlineData(3, 3)]
	public void Table_ClampsToAtLeastOne(int set, int expected)
	{
		var table = new TableControl { MouseWheelScrollSpeed = set };
		Assert.Equal(expected, table.MouseWheelScrollSpeed);
	}

	[Fact]
	public void Table_RaisesPropertyChanged()
	{
		var table = new TableControl();
		string? changed = null;
		table.PropertyChanged += (_, e) => changed = e.PropertyName;

		table.MouseWheelScrollSpeed = 4;

		Assert.Equal(nameof(TableControl.MouseWheelScrollSpeed), changed);
	}

	[Fact]
	public void MultilineEdit_DefaultsToLibraryDefault()
	{
		var editor = new MultilineEditControl();
		Assert.Equal(ControlDefaults.DefaultScrollWheelLines, editor.MouseWheelScrollSpeed);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-5, 1)]
	[InlineData(3, 3)]
	public void MultilineEdit_ClampsToAtLeastOne(int set, int expected)
	{
		var editor = new MultilineEditControl { MouseWheelScrollSpeed = set };
		Assert.Equal(expected, editor.MouseWheelScrollSpeed);
	}

	[Fact]
	public void MultilineEdit_RaisesPropertyChanged()
	{
		var editor = new MultilineEditControl();
		string? changed = null;
		editor.PropertyChanged += (_, e) => changed = e.PropertyName;

		editor.MouseWheelScrollSpeed = 4;

		Assert.Equal(nameof(MultilineEditControl.MouseWheelScrollSpeed), changed);
	}

	[Fact]
	public void Html_Builder_SetsTheSpeed()
	{
		var html = new HtmlBuilder()
			.WithMouseWheelScrollSpeed(4)
			.Build();

		Assert.Equal(4, html.MouseWheelScrollSpeed);
	}
}
