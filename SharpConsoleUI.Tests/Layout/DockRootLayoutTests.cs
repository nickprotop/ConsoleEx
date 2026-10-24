// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Windows;
using Xunit;
using Xunit.Abstractions;
using Builder = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// Tests whether one root can serve both models: is <see cref="StickyPosition"/> just docking on
/// the vertical axis, or are the banded root and a dock root genuinely different algorithms?
/// </summary>
/// <remarks>
/// If <see cref="DockRootLayout"/> reproduces <see cref="GridRootLayout"/> byte for byte on sticky
/// content, then shipping both would be shipping one algorithm twice, once restricted to two of
/// its four edges.
/// </remarks>
[Collection("RootLayoutSwap")]
public class DockRootLayoutTests
{
	private readonly ITestOutputHelper _out;

	public DockRootLayoutTests(ITestOutputHelper output) => _out = output;

	private static GoldenBuffer Capture(
		Func<IWindowRootLayout> root, string label, Action<Window> populate, int w, int h,
		Action<Window>? afterFirstRender = null)
	{
		var previous = WindowRenderer.RootLayoutFactory;
		try
		{
			WindowRenderer.RootLayoutFactory = root;

			var system = TestWindowSystemBuilder.CreateTestSystem(Math.Max(w + 20, 100), Math.Max(h + 20, 60));
			var window = new Window(system) { Title = "Dock", Width = w, Height = h, Left = 0, Top = 0 };
			populate(window);
			system.AddWindow(window);

			Render(window);
			if (afterFirstRender != null)
			{
				afterFirstRender(window);
				Render(window);
			}

			return GoldenBuffer.Capture(window, label);
		}
		finally
		{
			WindowRenderer.RootLayoutFactory = previous;
		}
	}

	private static void Render(Window window)
		=> window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });

	private void AssertSameAsBandedRoot(
		Action<Window> populate, int w, int h, string label, Action<Window>? afterFirstRender = null)
	{
		var banded = Capture(() => new GridRootLayout(), $"{label}/banded", populate, w, h, afterFirstRender);
		var docked = Capture(() => new DockRootLayout(), $"{label}/docked", populate, w, h, afterFirstRender);

		var report = GoldenBuffer.Compare(banded, docked);
		if (report.Length > 0)
		{
			_out.WriteLine($"{label} at {w}x{h}:");
			_out.WriteLine(report);
			_out.WriteLine("--- banded ---");
			_out.WriteLine(banded.ToText());
			_out.WriteLine("--- docked ---");
			_out.WriteLine(docked.ToText());
		}

		Assert.True(report.Length == 0, $"{label} at {w}x{h}: dock root differs from banded root");
	}

	private static void Sandwich(Window w)
	{
		w.AddControl(Builder.Markup("[bold]HEADER[/]").StickyTop().Build());
		for (int i = 0; i < 40; i++)
			w.AddControl(Builder.Markup($"body line {i}").Build());
		w.AddControl(Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build());
	}

	[Theory]
	[InlineData(60, 20)]
	[InlineData(20, 6)]
	[InlineData(200, 50)]
	[InlineData(12, 4)]
	public void TheStickySandwich_IsTheSameUnderBothRoots(int w, int h)
		=> AssertSameAsBandedRoot(Sandwich, w, h, "sticky sandwich");

	[Theory]
	[InlineData(3)]
	[InlineData(17)]
	[InlineData(1000)]
	public void AScrolledBody_IsTheSameUnderBothRoots(int by)
		=> AssertSameAsBandedRoot(Sandwich, 60, 20, $"scrolled {by}", w => w.ScrollBy(by));

	[Fact]
	public void MultipleControlsPerBand_AreTheSameUnderBothRoots()
		=> AssertSameAsBandedRoot(w =>
		{
			w.AddControl(Builder.Markup("[bold]TOP ONE[/]").StickyTop().Build());
			w.AddControl(Builder.Markup("top two\nwith a second line").StickyTop().Build());
			for (int i = 0; i < 25; i++)
				w.AddControl(Builder.Markup($"row {i}").Build());
			w.AddControl(Builder.Markup("[dim]BOTTOM ONE[/]").StickyBottom().Build());
			w.AddControl(Builder.Markup("[dim]BOTTOM TWO[/]").StickyBottom().Build());
		}, 50, 16, "multi-control bands");

	[Fact]
	public void AFillScrollablePanel_IsTheSameUnderBothRoots()
		=> AssertSameAsBandedRoot(w =>
		{
			w.AddControl(Builder.Markup("[bold]HEADER[/]").StickyTop().Build());

			var panel = new ScrollablePanelControl { BorderStyle = BorderStyle.Rounded };
			for (int i = 0; i < 30; i++)
				panel.AddControl(Builder.Markup($"panel line {i}").Build());
			w.AddControl(panel);

			w.AddControl(Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build());
		}, 50, 18, "fill scrollable panel");

	[Fact]
	public void AnEmptyFillPanel_IsTheSameUnderBothRoots()
		=> AssertSameAsBandedRoot(w =>
		{
			w.AddControl(new ScrollablePanelControl
			{
				BorderStyle = BorderStyle.Rounded,
				VerticalAlignment = VerticalAlignment.Fill,
			});
		}, 60, 20, "empty fill panel");

	[Fact]
	public void AShortBody_IsTheSameUnderBothRoots()
		=> AssertSameAsBandedRoot(w =>
		{
			w.AddControl(Builder.Markup("[bold]HEADER[/]").StickyTop().Build());
			w.AddControl(Builder.Markup("one short line").Build());
			w.AddControl(Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build());
		}, 50, 20, "short body");

	[Fact]
	public void StickyOnly_IsTheSameUnderBothRoots()
		=> AssertSameAsBandedRoot(w =>
		{
			w.AddControl(Builder.Markup("[bold]TOP[/]").StickyTop().Build());
			w.AddControl(Builder.Markup("[dim]BOTTOM[/]").StickyBottom().Build());
		}, 40, 12, "sticky only");

	[Fact]
	public void AnInvisibleStickyChild_IsTheSameUnderBothRoots()
		=> AssertSameAsBandedRoot(w =>
		{
			var hidden = Builder.Markup("HIDDEN").StickyTop().Build();
			hidden.Visible = false;
			w.AddControl(hidden);
			for (int i = 0; i < 20; i++)
				w.AddControl(Builder.Markup($"line {i}").Build());
			w.AddControl(Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build());
		}, 40, 12, "invisible sticky child");

	[Fact]
	public void AlignedChildren_AreTheSameUnderBothRoots()
		=> AssertSameAsBandedRoot(w =>
		{
			w.AddControl(Builder.Markup("[bold]HEADER[/]").StickyTop().Build());

			var left = Builder.Markup("left").Build();
			left.HorizontalAlignment = HorizontalAlignment.Left;
			var centre = Builder.Markup("centre").Build();
			centre.HorizontalAlignment = HorizontalAlignment.Center;
			var right = Builder.Markup("right").Build();
			right.HorizontalAlignment = HorizontalAlignment.Right;

			w.AddControl(left);
			w.AddControl(centre);
			w.AddControl(right);
		}, 40, 12, "aligned children");

	[Fact]
	public void LeftAndRightDocks_AreWhatTheBandedRootCouldNotDo()
	{
		// The payoff. Identical byte-for-byte on two edges is only worth having if the third and
		// fourth edges come free, so this drives all four plus a scrolling body.
		{
			var text = Capture(() => new DockRootLayout(), "four edges", w =>
			{
				var header = Builder.Markup("HEADER").Build();
				header.Dock = Dock.Top;
				var sidebar = Builder.Markup("SIDE").Build();
				sidebar.Dock = Dock.Left;
				var inspector = Builder.Markup("INSP").Build();
				inspector.Dock = Dock.Right;
				var status = Builder.Markup("STATUS").Build();
				status.Dock = Dock.Bottom;

				// Declaration order IS the semantics: header and status are declared before the
				// side docks, so they span the full width and the sides occupy what is left
				// between them. Declaring the sides first would instead give them full height and
				// leave the header spanning only the middle. That is the dock contract, not a
				// quirk - it is how a title bar above a sidebar differs from a sidebar beside a
				// title bar.
				w.AddControl(header);
				w.AddControl(status);
				w.AddControl(sidebar);
				w.AddControl(inspector);
				for (int i = 0; i < 20; i++)
					w.AddControl(Builder.Markup($"body {i}").Build());
			}, 44, 12).ToText();

			_out.WriteLine(text);

			var lines = text.Split('\n');

			// Header spans the top, status the bottom - both declared before the sides.
			Assert.StartsWith("HEADER", lines[0]);
			Assert.StartsWith("STATUS", lines[^2]);

			// Sidebar left, inspector right, body between - on the first row of the middle band.
			// A single-line control docked to a side OCCUPIES the full height but only PAINTS its
			// one line, so the text appears on row 1 and the rows below it are the body indented
			// past the reserved strip.
			var band = lines[1];
			Assert.StartsWith("SIDE", band);
			Assert.Contains("INSP", band);
			Assert.True(band.IndexOf("INSP") > band.IndexOf("SIDE"),
				"the right dock must sit to the right of the left dock");
			Assert.Contains("body", band);

			// And the strip stays reserved further down: body text is pushed past the sidebar.
			Assert.StartsWith("    body", lines[2]);
		}
	}

	[Fact]
	public void NoChildren_IsTheSameUnderBothRoots()
		=> AssertSameAsBandedRoot(_ => { }, 40, 10, "empty window");
}
