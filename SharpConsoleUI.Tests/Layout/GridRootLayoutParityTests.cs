// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Xunit;
using Xunit.Abstractions;
using Builder = SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Layout;

/// <summary>
/// Step 2 of docs/experiment/design.md: does <see cref="GridRootLayout"/> arrange a window's
/// children exactly where <see cref="WindowContentLayout"/> does?
/// </summary>
/// <remarks>
/// Both layouts are driven over the same control set, at the same constraints, and the resulting
/// child rectangles are compared. This runs below the window — no renderer, no buffer — because
/// arranged bounds are where a mapping error first appears; the golden-buffer comparison comes
/// after, to catch what geometry parity cannot (clip rects, paint order).
///
/// Scrolling is out of scope here: <see cref="GridRootLayout"/> does not implement it yet, so
/// every case is at offset 0. That limit is deliberate and is recorded in the spec.
/// </remarks>
public class GridRootLayoutParityTests
{
	private readonly ITestOutputHelper _out;

	public GridRootLayoutParityTests(ITestOutputHelper output) => _out = output;

	private static LayoutNode BuildTree(ILayoutContainer layout, IEnumerable<IWindowControl> controls)
	{
		var root = new LayoutNode(null, layout);
		foreach (var c in controls)
		{
			var child = LayoutNodeFactory.CreateSubtree(c);
			child.IsVisible = c.Visible;
			root.AddChild(child);
		}
		return root;
	}

	private static List<LayoutRect> ArrangeWith(
		ILayoutContainer layout, Func<List<IWindowControl>> makeControls, int w, int h)
	{
		// Fresh controls per run: a control carries arranged state, so sharing instances between
		// the two layouts would let the first run's results leak into the second's.
		var root = BuildTree(layout, makeControls());
		root.Measure(new LayoutConstraints(0, w, 0, h));
		root.Arrange(new LayoutRect(0, 0, w, h));

		// Compare the LEAF controls, not the root's direct children. GridRootLayout inserts a
		// structural body node between the root and its non-sticky children, so the trees have
		// different shapes by design; what must match is where the controls that actually paint
		// end up. Flattening to real controls compares exactly that and is shape-agnostic.
		var bounds = new List<LayoutRect>();
		Flatten(root, bounds);
		return bounds;
	}

	private static void Flatten(LayoutNode node, List<LayoutRect> into)
	{
		foreach (var child in node.Children)
		{
			// The body marker is structure, never painted; its children are the real content.
			if (child.Control != null && child.Control.GetType().Name != "BodyMarker")
				into.Add(child.AbsoluteBounds);
			Flatten(child, into);
		}
	}

	private void AssertParity(Func<List<IWindowControl>> makeControls, int w, int h, string label)
	{
		var oldBounds = ArrangeWith(new WindowContentLayout(), makeControls, w, h);
		var newBounds = ArrangeWith(new GridRootLayout(), makeControls, w, h);

		Assert.Equal(oldBounds.Count, newBounds.Count);

		var drift = new List<string>();
		for (int i = 0; i < oldBounds.Count; i++)
		{
			if (!oldBounds[i].Equals(newBounds[i]))
				drift.Add($"  child[{i}]  old {Fmt(oldBounds[i])}   new {Fmt(newBounds[i])}");
		}

		if (drift.Count > 0)
		{
			_out.WriteLine($"{label} ({w}x{h}) — {drift.Count} of {oldBounds.Count} children differ:");
			foreach (var d in drift)
				_out.WriteLine(d);
		}

		Assert.True(drift.Count == 0, $"{label} at {w}x{h}: {drift.Count} child bounds differ");
	}

	private static string Fmt(LayoutRect r) => $"({r.X},{r.Y} {r.Width}x{r.Height})";

	private static List<IWindowControl> Sandwich(int bodyLines = 40) => new()
	{
		Builder.Markup("[bold]HEADER[/]").StickyTop().Build(),
		Builder.Markup(string.Join('\n', Enumerable.Range(0, bodyLines).Select(i => $"body {i}"))).Build(),
		Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build(),
	};

	[Theory]
	[InlineData(60, 20)]
	[InlineData(20, 6)]
	[InlineData(200, 50)]
	[InlineData(12, 4)]
	public void StickySandwich_ArrangesIdentically(int w, int h)
		=> AssertParity(() => Sandwich(), w, h, "sticky sandwich");

	[Fact]
	public void BodyOnly_NoStickyChildren_ArrangesIdentically()
		=> AssertParity(() => new List<IWindowControl>
		{
			Builder.Markup("alpha").Build(),
			Builder.Markup("beta").Build(),
			Builder.Markup("gamma").Build(),
		}, 40, 12, "body only");

	[Fact]
	public void StickyOnly_NoBody_ArrangesIdentically()
		=> AssertParity(() => new List<IWindowControl>
		{
			Builder.Markup("top").StickyTop().Build(),
			Builder.Markup("bottom").StickyBottom().Build(),
		}, 40, 12, "sticky only");

	[Fact]
	public void MultipleControlsPerBand_KeepTheirOrderAndOwnHeights()
		// One Auto row per sticky child, not one shared row for the band: a shared row would size
		// every control in it to the tallest.
		=> AssertParity(() => new List<IWindowControl>
		{
			Builder.Markup("top one").StickyTop().Build(),
			Builder.Markup("top two\nwith a second line").StickyTop().Build(),
			Builder.Markup("body").Build(),
			Builder.Markup("bottom one").StickyBottom().Build(),
			Builder.Markup("bottom two").StickyBottom().Build(),
		}, 50, 16, "multi-control bands");

	[Fact]
	public void AnInvisibleChild_IsExcludedByBoth()
	{
		// A hidden sticky child must not reserve a row. If GridRootLayout built a track for it
		// the whole body would shift down by its height.
		var hidden = Builder.Markup("hidden header").StickyTop().Build();
		hidden.Visible = false;

		var oldBounds = ArrangeWith(new WindowContentLayout(),
			() => new List<IWindowControl> { hidden, Builder.Markup("body").Build() }, 40, 10);

		var hidden2 = Builder.Markup("hidden header").StickyTop().Build();
		hidden2.Visible = false;
		var newBounds = ArrangeWith(new GridRootLayout(),
			() => new List<IWindowControl> { hidden2, Builder.Markup("body").Build() }, 40, 10);

		Assert.Equal(oldBounds.Count, newBounds.Count);
	}

	[Theory]
	[InlineData(40, 12)]
	[InlineData(12, 12)]
	[InlineData(80, 30)]
	public void AFillChild_SharesTheViewportIdentically(int w, int h)
	{
		// The case nine passing parity tests missed, because every one of them used content-sized
		// Markup controls. A Fill child is measured against a SHARE of the viewport, not against
		// int.MaxValue; getting that wrong does not shift a row, it returns int.MaxValue and hangs
		// the paint walk. See the step 3 finding in docs/experiment/design.md.
		AssertParity(() => new List<IWindowControl>
		{
			Builder.Markup("[bold]HEADER[/]").StickyTop().Build(),
			new ScrollbarControl
			{
				Maximum = 100,
				ViewportLength = 10,
				Value = 0,
				VerticalAlignment = VerticalAlignment.Fill,
			},
			Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build(),
		}, w, h, "fill child");
	}

	[Fact]
	public void TwoFillChildren_SplitTheViewportIdentically()
		// Two flex children expose the integer-division remainder: a viewport of odd height cannot
		// be halved evenly, and the old layout spreads the leftover row onto the first child. An
		// arrange that divides without redistributing leaves a one-row gap at the bottom.
		=> AssertParity(() => new List<IWindowControl>
		{
			new ScrollbarControl { Maximum = 100, ViewportLength = 10, VerticalAlignment = VerticalAlignment.Fill },
			new ScrollbarControl { Maximum = 100, ViewportLength = 10, VerticalAlignment = VerticalAlignment.Fill },
		}, 30, 15, "two fill children");

	[Fact]
	public void AScrollablePanel_IsTreatedAsSelfBoundingByBoth()
	{
		// A ScrollablePanelControl is an IScrollableContainer, so it flexes even without an explicit
		// Fill: it scrolls its own content inside the space it is given. Letting it grow instead
		// would hand the same content to two scrollers.
		AssertParity(() =>
		{
			var panel = new ScrollablePanelControl();
			for (int i = 0; i < 30; i++)
				panel.AddControl(Builder.Markup($"panel line {i}").Build());

			return new List<IWindowControl>
			{
				Builder.Markup("[bold]HEADER[/]").StickyTop().Build(),
				panel,
			};
		}, 40, 14, "nested scrollable panel");
	}

	[Fact]
	public void AFillChildBesideContentSizedChildren_GetsOnlyWhatIsLeft()
		// Mixed body: the fixed children consume their content height first, and the flex child
		// receives the remainder. Measuring the flex child before the fixed ones would give it the
		// whole viewport and push the rest out.
		=> AssertParity(() => new List<IWindowControl>
		{
			Builder.Markup("three\nline\nblock").Build(),
			new ScrollbarControl { Maximum = 100, ViewportLength = 10, VerticalAlignment = VerticalAlignment.Fill },
			Builder.Markup("tail").Build(),
		}, 36, 16, "fill beside fixed");

	[Fact]
	public void AnEmptyFillContainer_StillFillsTheViewport()
		// The body's own measured height must not become the viewport's size. An empty panel's
		// content is two rows (its borders); treating that as the Star row's content floor shrinks
		// a 20-row window to 2. Missed by every other case here, because they all have content
		// TALLER than the viewport, where content-floor and allocation happen to agree.
		=> AssertParity(() => new List<IWindowControl>
		{
			new ScrollablePanelControl
			{
				BorderStyle = BorderStyle.Rounded,
				VerticalAlignment = VerticalAlignment.Fill,
			},
		}, 60, 20, "empty fill container");

	[Fact]
	public void AShortBody_DoesNotShrinkTheViewport()
		// Same shape without the container: one short child between sticky bands. The body region
		// is the space between the bands regardless of how little content sits in it.
		=> AssertParity(() => new List<IWindowControl>
		{
			Builder.Markup("[bold]HEADER[/]").StickyTop().Build(),
			Builder.Markup("one short line").Build(),
			Builder.Markup("[dim]FOOTER[/]").StickyBottom().Build(),
		}, 50, 20, "short body");

	[Fact]
	public void NoChildren_IsHandledByBoth()
		=> AssertParity(() => new List<IWindowControl>(), 40, 10, "empty");
}
