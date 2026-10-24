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
/// The experiment's central claim, tested the only way that settles it: render the SAME window
/// through <see cref="WindowContentLayout"/> and through <see cref="GridRootLayout"/>, and compare
/// the painted character buffers cell by cell.
/// </summary>
/// <remarks>
/// <para>
/// Geometry parity (<see cref="GridRootLayoutParityTests"/>) compares where controls are arranged.
/// That is necessary and not sufficient: it cannot see a clip rectangle that leaks a colour across
/// a band boundary, a paint order that draws the body over a sticky header, or a scrollbar drawn
/// from the wrong metrics. Those are differences in the buffer, not in the bounds.
/// </para>
/// <para>
/// These tests are serialised with <see cref="GoldenBufferCollection"/>: they swap a static
/// factory, so running them beside another renderer test would let one test's root layout leak
/// into another's window.
/// </para>
/// </remarks>
[Collection("RootLayoutSwap")]
public class GridRootLayoutGoldenBufferTests
{
	private readonly ITestOutputHelper _out;

	public GridRootLayoutGoldenBufferTests(ITestOutputHelper output) => _out = output;

	/// <summary>
	/// Builds and renders a window under the given root layout, then captures its buffer. The
	/// factory is always restored, including when the render throws, so a failure here cannot
	/// leave every later test running on the wrong root.
	/// </summary>
	private static GoldenBuffer Capture(
		Func<IWindowRootLayout> root, string label, Action<Window> populate, int w, int h,
		Action<Window>? afterFirstRender = null)
	{
		var previous = WindowRenderer.RootLayoutFactory;
		try
		{
			WindowRenderer.RootLayoutFactory = root;

			var system = TestWindowSystemBuilder.CreateTestSystem(Math.Max(w + 20, 100), Math.Max(h + 20, 60));
			var window = new Window(system) { Title = "Golden", Width = w, Height = h, Left = 0, Top = 0 };
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

	private void AssertSameBuffer(
		Action<Window> populate, int w, int h, string label, Action<Window>? afterFirstRender = null)
	{
		var old = Capture(() => new WindowContentLayout(), $"{label}/old", populate, w, h, afterFirstRender);
		var @new = Capture(() => new GridRootLayout(), $"{label}/new", populate, w, h, afterFirstRender);

		var report = GoldenBuffer.Compare(old, @new);
		if (report.Length > 0)
		{
			_out.WriteLine($"{label} at {w}x{h}:");
			_out.WriteLine(report);
			_out.WriteLine("--- old ---");
			_out.WriteLine(old.ToText());
			_out.WriteLine("--- new ---");
			_out.WriteLine(@new.ToText());
		}

		Assert.True(report.Length == 0, $"{label} at {w}x{h} differs between root layouts");
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
	public void TheStickySandwich_PaintsIdentically(int w, int h)
		=> AssertSameBuffer(Sandwich, w, h, "sticky sandwich");

	[Theory]
	[InlineData(3)]
	[InlineData(17)]
	[InlineData(1000)]   // past the end: both must clamp to the same place
	public void AScrolledBody_PaintsIdentically(int by)
		=> AssertSameBuffer(Sandwich, 60, 20, $"scrolled by {by}", w => w.ScrollBy(by));

	[Fact]
	public void MultipleControlsPerBand_PaintIdentically()
		=> AssertSameBuffer(w =>
		{
			w.AddControl(Builder.Markup("[bold]TOP ONE[/]").StickyTop().Build());
			w.AddControl(Builder.Markup("top two\nwith a second line").StickyTop().Build());
			for (int i = 0; i < 25; i++)
				w.AddControl(Builder.Markup($"row {i}").Build());
			w.AddControl(Builder.Markup("[dim]BOTTOM ONE[/]").StickyBottom().Build());
			w.AddControl(Builder.Markup("[dim]BOTTOM TWO[/]").StickyBottom().Build());
		}, 50, 16, "multi-control bands");

	[Fact]
	public void AFillScrollablePanel_PaintsIdentically()
		// The control that drove five of the six step 3 defects: self-bounding, so it flexes to the
		// band and scrolls its own content. Its border is a precise record of the band it was given.
		=> AssertSameBuffer(w =>
		{
			w.AddControl(Builder.Markup("[bold]HEADER[/]").StickyTop().Build());

			var panel = new ScrollablePanelControl { BorderStyle = BorderStyle.Rounded };
			for (int i = 0; i < 30; i++)
				panel.AddControl(Builder.Markup($"panel line {i}").Build());
			w.AddControl(panel);

			w.AddControl(Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build());
		}, 50, 18, "fill scrollable panel");

	[Fact]
	public void AnEmptyFillPanel_PaintsIdentically()
		// The 60x2-instead-of-60x20 collapse. The border box IS the assertion: if the body shrinks
		// to its content, the bottom border moves up and the buffers differ on every row between.
		=> AssertSameBuffer(w =>
		{
			var panel = new ScrollablePanelControl
			{
				BorderStyle = BorderStyle.Rounded,
				VerticalAlignment = VerticalAlignment.Fill,
			};
			w.AddControl(panel);
		}, 60, 20, "empty fill panel");

	[Fact]
	public void AShortBody_PaintsIdentically()
		=> AssertSameBuffer(w =>
		{
			w.AddControl(Builder.Markup("[bold]HEADER[/]").StickyTop().Build());
			w.AddControl(Builder.Markup("one short line").Build());
			w.AddControl(Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build());
		}, 50, 20, "short body");

	[Fact]
	public void StickyOnly_PaintsIdentically()
		=> AssertSameBuffer(w =>
		{
			w.AddControl(Builder.Markup("[bold]TOP[/]").StickyTop().Build());
			w.AddControl(Builder.Markup("[dim]BOTTOM[/]").StickyBottom().Build());
		}, 40, 12, "sticky only");

	[Fact]
	public void AnInvisibleStickyChild_PaintsIdentically()
		// A hidden child must reserve no band. If it did, every row below would shift by its height.
		=> AssertSameBuffer(w =>
		{
			var hidden = Builder.Markup("HIDDEN HEADER").StickyTop().Build();
			hidden.Visible = false;
			w.AddControl(hidden);
			for (int i = 0; i < 20; i++)
				w.AddControl(Builder.Markup($"line {i}").Build());
			w.AddControl(Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build());
		}, 40, 12, "invisible sticky child");

	[Fact]
	public void AlignedChildren_PaintIdentically()
		// Left/Center/Right in the body: the horizontal placement rule the two layouts must share.
		// ScrollLayout ignored HorizontalAlignment entirely before step 2 caught it.
		=> AssertSameBuffer(w =>
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
}

/// <summary>
/// Serialises every test that swaps <see cref="WindowRenderer.RootLayoutFactory"/>.
/// </summary>
[CollectionDefinition("RootLayoutSwap", DisableParallelization = true)]
public class RootLayoutSwapCollection
{
}
