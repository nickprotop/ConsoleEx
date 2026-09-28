// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Follow-ups reported on issue #85 after the reporter migrated a side-by-side diff viewer onto
/// <see cref="ScrollbarControl"/>. Each one is a place the standalone bar behaved differently from
/// the embedded scrollbars it was extracted from — the opposite of what the consolidation was for.
/// </summary>
[Collection("WheelStepDefault")]
public class ScrollbarControlFollowUpTests
{
	/// <summary>
	/// A bar that has actually been arranged. This matters: <c>ProcessMouseEvent</c> bails out when
	/// the track length is 0, so an unrendered bar would make the "notch was not handled"
	/// assertions below pass for the wrong reason.
	/// </summary>
	private static ScrollbarControl Bar(int maximum = 100, int viewport = 10)
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(80, 25),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 12,
			Height = 12,
			BorderStyle = BorderStyle.Frameless
		};

		var bar = new ScrollbarControl
		{
			Maximum = maximum,
			ViewportLength = viewport,
			Value = 0,
			VerticalAlignment = VerticalAlignment.Fill,
		};

		window.AddControl(bar);
		system.AddWindow(window);
		window.RenderAndGetVisibleContent();

		Assert.True(bar.ActualHeight > 0, "test premise: the bar must be arranged before driving the wheel");
		return bar;
	}

	#region 1 — the wheel step is separable from the arrow step

	/// <summary>
	/// Setting the app-wide wheel default must not change how far the bar's ARROW buttons step.
	/// The reporter set <c>DefaultScrollWheelLines = 3</c> for their app and found the arrows
	/// moving 3 lines too, because one value drove both.
	/// </summary>
	[Fact]
	public void MouseWheelScrollSpeed_IsSeparateFromSmallChange()
	{
		var bar = Bar();

		bar.SmallChange = 1;
		bar.MouseWheelScrollSpeed = 3;

		Assert.Equal(1, bar.SmallChange);
		Assert.Equal(3, bar.MouseWheelScrollSpeed);
	}

	[Fact]
	public void MouseWheelScrollSpeed_DefaultsToTheLibraryWheelStep()
	{
		Assert.Equal(ControlDefaults.DefaultScrollWheelLines, Bar().MouseWheelScrollSpeed);
	}

	[Theory]
	[InlineData(0, 1)]
	[InlineData(-5, 1)]
	[InlineData(4, 4)]
	public void MouseWheelScrollSpeed_ClampsToAtLeastOne(int set, int expected)
	{
		var bar = Bar();
		bar.MouseWheelScrollSpeed = set;
		Assert.Equal(expected, bar.MouseWheelScrollSpeed);
	}

	[Fact]
	public void Builder_SetsTheWheelSpeed()
	{
		var bar = SharpConsoleUI.Builders.Controls.Scrollbar()
			.WithMaximum(100)
			.WithViewportLength(10)
			.WithMouseWheelScrollSpeed(4)
			.Build();

		Assert.Equal(4, bar.MouseWheelScrollSpeed);
	}

	#endregion

	#region 2 — an unusable wheel notch bubbles instead of being swallowed

	/// <summary>
	/// At the top of its range an upward notch changes nothing, so it must reach the containers
	/// around the bar — which is what <see cref="ScrollablePanelControl"/> already does. Swallowing
	/// it strands a bar inside a scrollable page.
	/// </summary>
	[Fact]
	public void WheelUp_AtTheTop_IsNotHandled()
	{
		var bar = Bar();
		bar.Value = 0;

		var args = new Events.MouseEventArgs(
			new List<MouseFlags> { MouseFlags.WheeledUp },
			new System.Drawing.Point(0, 3), new System.Drawing.Point(0, 3),
			new System.Drawing.Point(0, 3), null);

		bool handled = bar.ProcessMouseEvent(args);

		Assert.False(handled);
		Assert.False(args.Handled);
		Assert.Equal(0, bar.Value);
	}

	[Fact]
	public void WheelDown_AtTheBottom_IsNotHandled()
	{
		var bar = Bar();
		bar.Value = bar.Maximum - bar.ViewportLength;
		int atEnd = bar.Value;

		var args = new Events.MouseEventArgs(
			new List<MouseFlags> { MouseFlags.WheeledDown },
			new System.Drawing.Point(0, 3), new System.Drawing.Point(0, 3),
			new System.Drawing.Point(0, 3), null);

		bool handled = bar.ProcessMouseEvent(args);

		Assert.False(handled);
		Assert.False(args.Handled);
		Assert.Equal(atEnd, bar.Value);
	}

	[Fact]
	public void WheelInTheMiddle_IsStillHandled()
	{
		var bar = Bar();
		bar.Value = 40;

		var args = new Events.MouseEventArgs(
			new List<MouseFlags> { MouseFlags.WheeledDown },
			new System.Drawing.Point(0, 3), new System.Drawing.Point(0, 3),
			new System.Drawing.Point(0, 3), null);

		bool handled = bar.ProcessMouseEvent(args);

		Assert.True(handled);
		Assert.True(args.Handled);
		Assert.True(bar.Value > 40);
	}

	#endregion

	#region 3 — the bar can hide itself when everything fits

	[Fact]
	public void AutoHide_DefaultsToOff_SoExistingCodeIsUnchanged()
	{
		Assert.False(Bar().AutoHideWhenContentFits);
	}

	/// <summary>
	/// With auto-hide on and the content fitting, the bar paints nothing — matching the embedded
	/// scrollbars, which appear only when there is something to scroll.
	/// </summary>
	[Fact]
	public void AutoHide_PaintsNothingWhenContentFits()
	{
		Assert.Equal("", RenderBarColumn(maximum: 5, viewport: 10, autoHide: true).Trim());
	}

	[Fact]
	public void AutoHide_StillPaintsWhenContentOverflows()
	{
		Assert.NotEqual("", RenderBarColumn(maximum: 100, viewport: 10, autoHide: true).Trim());
	}

	[Fact]
	public void WithoutAutoHide_ContentThatFitsStillPaints()
	{
		Assert.NotEqual("", RenderBarColumn(maximum: 5, viewport: 10, autoHide: false).Trim());
	}

	private static string RenderBarColumn(int maximum, int viewport, bool autoHide)
	{
		var system = new ConsoleWindowSystem(
			new HeadlessConsoleDriver(80, 25),
			new ConsoleWindowSystemOptions(ShowTopPanel: false, ShowBottomPanel: false));
		var window = new Window(system)
		{
			Left = 0,
			Top = 0,
			Width = 12,
			Height = 10,
			BorderStyle = BorderStyle.Frameless
		};

		var bar = new ScrollbarControl
		{
			Maximum = maximum,
			ViewportLength = viewport,
			Value = 0,
			AutoHideWhenContentFits = autoHide,
		};

		window.AddControl(bar);
		system.AddWindow(window);

		var rows = window.RenderAndGetVisibleContent()
			.Select(r => Regex.Replace(r, @"\x1b\[[0-9;?]*[ -/]*[@-~]", ""))
			.ToList();

		// Everything the bar could have drawn, with blanks removed.
		return string.Concat(rows.Select(r => r.Replace(" ", ""))).Trim();
	}

	#endregion
}
