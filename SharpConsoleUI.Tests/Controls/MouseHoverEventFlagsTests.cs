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
/// Regression tests for issue #82: the dispatcher synthesized MouseEnter / MouseLeave by ADDING the
/// flag to the triggering event's flags, so a wheel notch that changed the hovered control arrived
/// twice — once on the synthesized enter event, once as the real notch — scrolling 2 lines (3 in a
/// grid cell, which forwards its own enter event) for a single notch.
/// </summary>
public class MouseHoverEventFlagsTests
{
	/// <summary>
	/// Drives the REAL input path: a running window system, mouse injected through the headless
	/// driver, so events go through WindowEventDispatcher rather than a direct control call.
	/// </summary>
	private static (ScrollablePanelControl panel, ConsoleWindowSystem system, HeadlessConsoleDriver driver) StartSystem(bool inGrid)
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
			.AddControl(SharpConsoleUI.Builders.Controls.Markup()
				.AddLines(Enumerable.Range(1, 50).Select(i => $"row {i}").ToArray())
				.Build())
			.Build();

		IWindowControl content = panel;
		if (inGrid)
		{
			var grid = new GridControl
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Fill
			};
			grid.ColumnDefinitions.Add(GridLength.Star());
			grid.RowDefinitions.Add(GridLength.Star());
			grid.Place(panel, 0, 0);
			content = grid;
		}

		window.AddControl(content);
		new Thread(() => system.Run()) { IsBackground = true }.Start();
		system.EnqueueOnUIThread(() => system.AddWindow(window));

		Assert.True(
			SpinWait.SpinUntil(() => panel.ActualHeight > 0
				&& system.WindowStateService.ActiveWindow == window
				&& system.GetWindowAtPoint(new Point(0, 0)) == window, 5000),
			"the window did not become active and hit-testable in time");

		return (panel, system, driver);
	}

	[Fact]
	public void WheelNotch_EnteringControl_ScrollsOnce()
	{
		var (panel, system, driver) = StartSystem(inGrid: false);
		try
		{
			// The very first event over the panel is a wheel notch: the pointer never moved in, so the
			// dispatcher synthesizes MouseEnter from this same event.
			driver.SimulateMouseEvent([MouseFlags.WheeledDown], new Point(5, 5));
			SpinWait.SpinUntil(() => panel.VerticalScrollOffset > 0, 2000);
			Assert.Equal(1, panel.VerticalScrollOffset);

			driver.SimulateMouseEvent([MouseFlags.WheeledDown], new Point(5, 5));
			SpinWait.SpinUntil(() => panel.VerticalScrollOffset > 1, 2000);
			Assert.Equal(2, panel.VerticalScrollOffset);
		}
		finally
		{
			system.Shutdown();
		}
	}

	[Fact]
	public void WheelNotch_EnteringControlInGridCell_ScrollsOnce()
	{
		var (panel, system, driver) = StartSystem(inGrid: true);
		try
		{
			// The grid forwards its own enter event to the child under the pointer, which used to add a
			// third scroll on top of the two above.
			driver.SimulateMouseEvent([MouseFlags.WheeledDown], new Point(5, 5));
			SpinWait.SpinUntil(() => panel.VerticalScrollOffset > 0, 2000);
			Assert.Equal(1, panel.VerticalScrollOffset);
		}
		finally
		{
			system.Shutdown();
		}
	}

	[Fact]
	public void WheelNotch_AfterPlainMove_ScrollsOnce()
	{
		var (panel, system, driver) = StartSystem(inGrid: false);
		try
		{
			// Control case from the issue: a plain move enters the control first, so the enter event
			// carried no wheel flags and this path was always correct.
			driver.SimulateMouseEvent([MouseFlags.ReportMousePosition], new Point(5, 5));
			driver.SimulateMouseEvent([MouseFlags.WheeledDown], new Point(5, 5));
			SpinWait.SpinUntil(() => panel.VerticalScrollOffset > 0, 2000);

			Assert.Equal(1, panel.VerticalScrollOffset);
		}
		finally
		{
			system.Shutdown();
		}
	}
}
