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

public class ScrollbarControlTests
{
	private static ScrollbarControl Bar() =>
		new() { Maximum = 100, ViewportLength = 10, Value = 0 };

	[Fact]
	public void Value_ClampsToTheScrollRange()
	{
		var bar = Bar();

		bar.Value = 999;
		Assert.Equal(90, bar.Value);

		bar.Value = -5;
		Assert.Equal(0, bar.Value);
	}

	[Fact]
	public void Value_SetByCode_RaisesValueChangedOnly()
	{
		var bar = Bar();
		int valueChanged = 0, userChanged = 0;
		bar.ValueChanged += (_, _) => valueChanged++;
		bar.UserValueChanged += (_, _) => userChanged++;

		bar.Value = 20;

		Assert.Equal(1, valueChanged);
		Assert.Equal(0, userChanged);
	}

	[Fact]
	public void Value_SetToTheSameValue_RaisesNothing()
	{
		var bar = Bar();
		bar.Value = 20;

		int raised = 0;
		bar.ValueChanged += (_, _) => raised++;
		bar.Value = 20;

		Assert.Equal(0, raised);
	}

	[Fact]
	public void LargeChange_DefaultsToTheViewportLength()
	{
		Assert.Equal(10, Bar().LargeChange);
	}

	[Fact]
	public void SmallChange_DefaultsToTheLibraryWheelStep()
	{
		Assert.Equal(SharpConsoleUI.Configuration.ControlDefaults.DefaultScrollWheelLines, Bar().SmallChange);
	}

	[Fact]
	public void DoesNotTakeFocus()
	{
		Assert.False(Bar().CanReceiveFocus);
	}

	[Fact]
	public void ShrinkingTheRange_ReclampsTheValue()
	{
		var bar = Bar();
		bar.Value = 90;

		bar.Maximum = 20;

		Assert.Equal(10, bar.Value);
	}
}

/// <summary>
/// Drives <see cref="ScrollbarControl"/> through the real input path (a running window system, mouse
/// injected through the headless driver) rather than calling its methods directly, so the assertions
/// exercise the same path a real application would (CLAUDE.md "real thing" test rule).
/// </summary>
public class ScrollbarControlDrivenInputTests
{
	private const int TrackHeight = 10;

	private static (ScrollbarControl bar, Window window, ConsoleWindowSystem system, HeadlessConsoleDriver driver) StartSystem()
	{
		var driver = new HeadlessConsoleDriver(20, TrackHeight + 2);
		var system = new ConsoleWindowSystem(
			driver,
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 20,
			Height = TrackHeight,
			BorderStyle = BorderStyle.Frameless
		};

		var bar = SharpConsoleUI.Builders.Controls.Scrollbar()
			.Vertical()
			.WithMaximum(100)
			.WithViewportLength(10)
			.Build();

		window.AddControl(bar);
		new Thread(() => system.Run()) { IsBackground = true }.Start();
		system.EnqueueOnUIThread(() => system.AddWindow(window));

		Assert.True(
			SpinWait.SpinUntil(() => bar.ActualHeight > 0 && system.WindowStateService.ActiveWindow == window, 5000),
			"the window did not become active and hit-testable in time");

		return (bar, window, system, driver);
	}

	[Fact]
	public void WheelNotch_RaisesBothEvents()
	{
		var (bar, _, system, driver) = StartSystem();
		try
		{
			int valueChanged = 0, userChanged = 0;
			bar.ValueChanged += (_, _) => valueChanged++;
			bar.UserValueChanged += (_, _) => userChanged++;

			driver.SimulateMouseEvent([MouseFlags.WheeledDown], new Point(0, 0));
			SpinWait.SpinUntil(() => bar.Value > 0, 2000);

			Assert.True(bar.Value > 0);
			Assert.True(valueChanged >= 1);
			Assert.True(userChanged >= 1);
		}
		finally
		{
			system.Shutdown();
		}
	}

	[Fact]
	public void ArrowClick_RaisesBothEvents()
	{
		var (bar, _, system, driver) = StartSystem();
		try
		{
			int valueChanged = 0, userChanged = 0;
			bar.ValueChanged += (_, _) => valueChanged++;
			bar.UserValueChanged += (_, _) => userChanged++;

			// Down arrow: last cell of the track.
			driver.SimulateMouseEvent([MouseFlags.Button1Clicked], new Point(0, bar.ActualHeight - 1));
			SpinWait.SpinUntil(() => bar.Value > 0, 2000);

			Assert.True(bar.Value > 0);
			Assert.True(valueChanged >= 1);
			Assert.True(userChanged >= 1);
		}
		finally
		{
			system.Shutdown();
		}
	}

	[Fact]
	public void TrackClick_RaisesBothEvents()
	{
		var (bar, _, system, driver) = StartSystem();
		try
		{
			int valueChanged = 0, userChanged = 0;
			bar.ValueChanged += (_, _) => valueChanged++;
			bar.UserValueChanged += (_, _) => userChanged++;

			// The thumb starts at the top; click near the bottom of the track (above the down arrow)
			// to page down.
			driver.SimulateMouseEvent([MouseFlags.Button1Clicked], new Point(0, bar.ActualHeight - 2));
			SpinWait.SpinUntil(() => bar.Value > 0, 2000);

			Assert.True(bar.Value > 0);
			Assert.True(valueChanged >= 1);
			Assert.True(userChanged >= 1);
		}
		finally
		{
			system.Shutdown();
		}
	}

	[Fact]
	public void ThumbDrag_RaisesBothEvents()
	{
		var (bar, _, system, driver) = StartSystem();
		try
		{
			int valueChanged = 0, userChanged = 0;
			bar.ValueChanged += (_, _) => valueChanged++;
			bar.UserValueChanged += (_, _) => userChanged++;

			// Thumb sits at the top of the track, right after the up arrow (row 1).
			driver.SimulateMouseEvent([MouseFlags.Button1Pressed], new Point(0, 1));
			driver.SimulateMouseEvent([MouseFlags.Button1Pressed, MouseFlags.Button1Dragged], new Point(0, bar.ActualHeight - 2));
			driver.SimulateMouseEvent([MouseFlags.Button1Released], new Point(0, bar.ActualHeight - 2));

			SpinWait.SpinUntil(() => bar.Value > 0, 2000);

			Assert.True(bar.Value > 0);
			Assert.True(valueChanged >= 1);
			Assert.True(userChanged >= 1);
		}
		finally
		{
			system.Shutdown();
		}
	}

	/// <summary>
	/// A code-set Value raises ValueChanged but never UserValueChanged — the guarantee that lets a
	/// composite write the bar's value back from its views without an echo loop.
	/// </summary>
	/// <remarks>
	/// Deliberately does NOT start a window system. Setting Value is synchronous, so a running loop
	/// adds nothing but a dependency on UI-thread scheduling: the original version enqueued the set
	/// and waited on SpinWait, which starved under a loaded parallel suite and failed about one run
	/// in three. The driven-input tests below still use the real system, because THEY need it.
	/// </remarks>
	[Fact]
	public void CodeSetValue_RaisesOnlyValueChanged()
	{
		var bar = new ScrollbarControl { Maximum = 100, ViewportLength = 10, Value = 0 };

		int valueChanged = 0, userChanged = 0;
		bar.ValueChanged += (_, _) => valueChanged++;
		bar.UserValueChanged += (_, _) => userChanged++;

		bar.Value = 30;

		Assert.Equal(30, bar.Value);
		Assert.Equal(1, valueChanged);
		Assert.Equal(0, userChanged);
	}

	/// <summary>
	/// The composite case that justifies the whole control (issue #85): one bar driving two panes,
	/// wired through <see cref="ScrollbarControl.UserValueChanged"/> (to push the bar's value to both
	/// panes) and the panes' <c>Scrolled</c> event (to write back to the bar's <c>Value</c>), with no
	/// echo and no guard flag. Both panes must stay in step and no infinite loop may occur.
	/// </summary>
	[Fact]
	public void CompositeTwoPanesOneBar_StayInStepWithoutEcho()
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

		const int lineCount = 50;
		const int viewportRows = 10;

		var paneA = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithHeight(viewportRows)
			.WithScrollbar(false)
			.Build();
		for (int i = 1; i <= lineCount; i++)
			paneA.AddControl(SharpConsoleUI.Builders.Controls.Markup($"A{i}").Build());

		var paneB = SharpConsoleUI.Builders.Controls.ScrollablePanel()
			.WithHeight(viewportRows)
			.WithScrollbar(false)
			.Build();
		for (int i = 1; i <= lineCount; i++)
			paneB.AddControl(SharpConsoleUI.Builders.Controls.Markup($"B{i}").Build());

		var bar = SharpConsoleUI.Builders.Controls.Scrollbar()
			.Vertical()
			.WithMaximum(lineCount)
			.WithViewportLength(viewportRows)
			.WithHeight(viewportRows)
			.Build();

		int echoGuardTripCount = 0;

		// UserValueChanged pushes the bar's value to both panes.
		bar.UserValueChanged += (_, value) =>
		{
			system.EnqueueOnUIThread(() =>
			{
				paneA.ScrollVerticalBy(value - paneA.VerticalScrollOffset);
				paneB.ScrollVerticalBy(value - paneB.VerticalScrollOffset);
			});
		};

		// Each pane's own Scrolled event writes back to the bar — a code-set Value, so it raises only
		// ValueChanged, never re-entering UserValueChanged. No guard flag is used, proving the design.
		paneA.Scrolled += (_, e) =>
		{
			if (bar.Value != e.VerticalOffset)
			{
				bar.Value = e.VerticalOffset;
				echoGuardTripCount++;
			}
		};

		var grid = new GridControl
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Fill
		};
		grid.ColumnDefinitions.Add(GridLength.Star());
		grid.ColumnDefinitions.Add(GridLength.Star());
		grid.ColumnDefinitions.Add(GridLength.Cells(1));
		grid.RowDefinitions.Add(GridLength.Star());
		grid.Place(paneA, 0, 0);
		grid.Place(paneB, 0, 1);
		grid.Place(bar, 0, 2);

		window.AddControl(grid);
		new Thread(() => system.Run()) { IsBackground = true }.Start();
		system.EnqueueOnUIThread(() => system.AddWindow(window));

		try
		{
			Assert.True(
				SpinWait.SpinUntil(() => bar.ActualHeight > 0 && system.WindowStateService.ActiveWindow == window, 5000),
				"the window did not become active in time");

			// Drive the bar via its own UserValueChanged path (simulate a user setting the value
			// directly, as its arrow/track/drag handlers do).
			system.EnqueueOnUIThread(() =>
			{
				bar.GetType()
					.GetMethod("SetValueFromUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
					.Invoke(bar, new object[] { 15 });
			});

			SpinWait.SpinUntil(() => paneA.VerticalScrollOffset == 15 && paneB.VerticalScrollOffset == 15, 2000);

			Assert.Equal(15, paneA.VerticalScrollOffset);
			Assert.Equal(15, paneB.VerticalScrollOffset);
			Assert.Equal(15, bar.Value);

			// No echo: the write-back from paneA.Scrolled only fires when the value actually differs,
			// and since bar.Value already equals 15 by the time the pane's Scrolled fires, no
			// unbounded loop occurs — the trip count settles rather than growing unboundedly.
			int tripsAfterSettling = echoGuardTripCount;
			Thread.Sleep(100);
			Assert.Equal(tripsAfterSettling, echoGuardTripCount);
		}
		finally
		{
			system.Shutdown();
		}
	}
}
