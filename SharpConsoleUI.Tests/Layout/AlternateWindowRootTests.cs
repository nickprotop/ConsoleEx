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
/// Answers the question the <see cref="IWindowRootLayout"/> seam raises: can a window's root be
/// something other than <see cref="DockRootLayout"/>, or is the interface only wide enough for the
/// one implementation it was extracted from?
/// </summary>
/// <remarks>
/// A seam that only its original occupant fits is not a seam. The test is a root built on
/// <see cref="VerticalStackLayout"/> - an existing container that knows nothing about sticky
/// bands, scrolling, or grids, written long before this experiment and not modified for it.
/// </remarks>
[Collection("RootLayoutSwap")]
public class AlternateWindowRootTests
{
	private readonly ITestOutputHelper _out;

	public AlternateWindowRootTests(ITestOutputHelper output) => _out = output;

	/// <summary>
	/// A window root that simply stacks its children and never scrolls, delegating all layout to
	/// the stock <see cref="VerticalStackLayout"/>.
	/// </summary>
	/// <remarks>
	/// Deliberately minimal, to measure the real cost of the contract. Sticky is ignored - a root
	/// is entitled to have no bands - and the scroll members report an empty range and do nothing,
	/// which is the honest answer for a layout with no scrolling region.
	/// </remarks>
	private sealed class StackRootLayout : IWindowRootLayout
	{
		private readonly VerticalStackLayout _stack = new();

		public LayoutSize MeasureChildren(LayoutNode node, LayoutConstraints constraints)
			=> _stack.MeasureChildren(node, constraints);

		public void ArrangeChildren(LayoutNode node, LayoutRect finalRect)
			=> _stack.ArrangeChildren(node, finalRect);

		/// <summary>No bands, so every child is clipped to the window's own content area.</summary>
		public LayoutRect GetPaintClipRect(LayoutNode child, LayoutRect parentClipRect)
			=> parentClipRect;

		public int ScrollOffset { get => 0; set { /* nothing scrolls */ } }

		public int ScrollableContentHeight => 0;

		public int MaxScrollOffset => 0;

		public void ScrollBy(int delta) { }

		public void ScrollToTop() { }

		public void ScrollToBottom() { }

		public void PageUp() { }

		public void PageDown() { }
	}

	private static T WithRoot<T>(Func<IWindowRootLayout> root, Func<ConsoleWindowSystem, T> body)
	{
		var previous = WindowRenderer.RootLayoutFactory;
		try
		{
			WindowRenderer.RootLayoutFactory = root;
			return body(TestWindowSystemBuilder.CreateTestSystem(100, 40));
		}
		finally
		{
			WindowRenderer.RootLayoutFactory = previous;
		}
	}

	private static Window Render(Window window)
	{
		window.RenderAndGetVisibleContent(
			new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });
		return window;
	}

	[Fact]
	public void AStackRoot_RendersAWindow()
	{
		var text = WithRoot(() => new StackRootLayout(), system =>
		{
			var window = new Window(system) { Title = "Stack", Width = 40, Height = 10, Left = 0, Top = 0 };
			window.AddControl(Builder.Markup("first").Build());
			window.AddControl(Builder.Markup("second").Build());
			window.AddControl(Builder.Markup("third").Build());
			system.AddWindow(window);

			return GoldenBuffer.Capture(Render(window), "stack root").ToText();
		});

		_out.WriteLine(text);

		// Stacked in order, each on its own row, with no band reordering.
		var lines = text.Split('\n');
		Assert.Contains("first", lines[0]);
		Assert.Contains("second", lines[1]);
		Assert.Contains("third", lines[2]);
	}

	[Fact]
	public void AStackRoot_IgnoresSticky_BecauseItHasNoBands()
	{
		// The point of the seam: a root defines its OWN model. DockRootLayout gives
		// StickyPosition a meaning; this one does not, and is entitled not to. A sticky-bottom
		// child stays where author order put it rather than jumping to the last row.
		var text = WithRoot(() => new StackRootLayout(), system =>
		{
			var window = new Window(system) { Title = "Stack", Width = 40, Height = 10, Left = 0, Top = 0 };
			window.AddControl(Builder.Markup("FOOTER").StickyBottom().Build());
			window.AddControl(Builder.Markup("body").Build());
			system.AddWindow(window);

			return GoldenBuffer.Capture(Render(window), "stack sticky").ToText();
		});

		_out.WriteLine(text);

		var lines = text.Split('\n');
		Assert.Contains("FOOTER", lines[0]);
		Assert.Contains("body", lines[1]);
	}

	[Fact]
	public void TheSameWindow_RendersDifferentlyUnderEachRoot()
	{
		// The seam is real only if the choice is observable. Identical controls, two roots: the
		// grid root banishes the sticky footer to the last row, the stack root leaves it first.
		Func<ConsoleWindowSystem, string> build = system =>
		{
			var window = new Window(system) { Title = "Both", Width = 40, Height = 10, Left = 0, Top = 0 };
			window.AddControl(Builder.Markup("FOOTER").StickyBottom().Build());
			window.AddControl(Builder.Markup("body").Build());
			system.AddWindow(window);
			return GoldenBuffer.Capture(Render(window), "either").ToText();
		};

		var docked = WithRoot(() => new DockRootLayout(), build);
		var stack = WithRoot(() => new StackRootLayout(), build);

		_out.WriteLine("--- dock root ---");
		_out.WriteLine(docked);
		_out.WriteLine("--- stack root ---");
		_out.WriteLine(stack);

		Assert.NotEqual(docked, stack);

		// And specifically: the dock root honours sticky, the stack root does not. The captured
		// buffer is the window's CONTENT area, so a 10-row window with a border paints 8 rows and
		// the last one is index 7.
		var dockedLines = docked.Split('\n');
		var stackLines = stack.Split('\n');
		Assert.Contains("FOOTER", dockedLines[^2]);     // last content row
		Assert.Contains("FOOTER", stackLines[0]);     // author order, first row
	}

	[Fact]
	public void AStackRoot_ReportsNoScrollRange_AndTheWindowAcceptsThat()
	{
		// A root with no scrolling region reports an empty range; the window's scroll API must
		// degrade to a no-op rather than throw or misreport.
		WithRoot(() => new StackRootLayout(), system =>
		{
			var window = new Window(system) { Title = "Stack", Width = 40, Height = 6, Left = 0, Top = 0 };
			for (int i = 0; i < 40; i++)
				window.AddControl(Builder.Markup($"line {i}").Build());
			system.AddWindow(window);
			Render(window);

			Assert.Equal(0, window.Renderer!.MaxScrollOffset);

			window.ScrollBy(10);
			Render(window);

			Assert.Equal(0, window.Renderer!.ScrollOffset);
			return true;
		});
	}

	[Fact]
	public void TwoWindows_CanUseDifferentRoots_AtTheSameTime()
	{
		// The strongest form of the question: not "can the root be replaced globally" but "can one
		// window have a different parent from another", which is how WinUI works and how this was
		// originally framed. Both windows are alive in one system, rendered in the same pass.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);

		var defaultWindow = new Window(system) { Title = "Default", Width = 40, Height = 10, Left = 0, Top = 0 };
		defaultWindow.AddControl(Builder.Markup("FOOTER").StickyBottom().Build());
		defaultWindow.AddControl(Builder.Markup("body").Build());
		system.AddWindow(defaultWindow);

		var stackWindow = new Window(system) { Title = "Stack", Width = 40, Height = 10, Left = 50, Top = 0 };
		stackWindow.Root = () => new StackRootLayout();
		stackWindow.AddControl(Builder.Markup("FOOTER").StickyBottom().Build());
		stackWindow.AddControl(Builder.Markup("body").Build());
		system.AddWindow(stackWindow);

		var grid = GoldenBuffer.Capture(Render(defaultWindow), "default-rooted window").ToText();
		var stack = GoldenBuffer.Capture(Render(stackWindow), "stack window").ToText();

		_out.WriteLine("--- default-rooted window ---");
		_out.WriteLine(grid);
		_out.WriteLine("--- stack-rooted window ---");
		_out.WriteLine(stack);

		// Same controls, same size, same system, same render pass - different layout models.
		Assert.Contains("FOOTER", grid.Split('\n')[^2]);    // pinned to the last row
		Assert.Contains("FOOTER", stack.Split('\n')[0]);    // left in author order
		Assert.NotEqual(grid, stack);
	}

	[Fact]
	public void SettingRootToNull_FallsBackToTheDefault()
	{
		// The other half of the surface: assigning a root switches the model, and clearing it
		// returns the window to the application default WITHOUT recreating the window. Both
		// directions are live - the setter rebuilds, so assignment alone is enough.
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 40);

		var window = new Window(system) { Title = "Switch", Width = 40, Height = 10, Left = 0, Top = 0 };
		window.AddControl(Builder.Markup("FOOTER").StickyBottom().Build());
		window.AddControl(Builder.Markup("body").Build());
		system.AddWindow(window);

		// Default root: sticky honoured.
		var asDefault = GoldenBuffer.Capture(Render(window), "default").ToText();
		Assert.Contains("FOOTER", asDefault.Split('\n')[^2]);

		// Swap to a root with no bands: the footer falls back into author order.
		window.Root = () => new StackRootLayout();
		var asStack = GoldenBuffer.Capture(Render(window), "stack").ToText();
		Assert.Contains("FOOTER", asStack.Split('\n')[0]);

		// Clear it: back to the default, and byte-identical to how it started.
		window.Root = null;
		var restored = GoldenBuffer.Capture(Render(window), "restored").ToText();

		_out.WriteLine(restored);
		Assert.Equal(asDefault, restored);
	}

	[Fact]
	public void SwappingBack_LeavesNoResidue()
	{
		// The factory is static, so a leak would silently change every later window in the
		// process. Render under the alternate root, restore, and confirm sticky works again.
		WithRoot(() => new StackRootLayout(), system =>
		{
			var w = new Window(system) { Title = "Stack", Width = 40, Height = 8, Left = 0, Top = 0 };
			w.AddControl(Builder.Markup("x").StickyBottom().Build());
			system.AddWindow(w);
			Render(w);
			return true;
		});

		var system2 = TestWindowSystemBuilder.CreateTestSystem(100, 40);
		var after = new Window(system2) { Title = "After", Width = 40, Height = 8, Left = 0, Top = 0 };
		after.AddControl(Builder.Markup("FOOTER").StickyBottom().Build());
		system2.AddWindow(after);
		Render(after);

		var text = GoldenBuffer.Capture(after, "after restore").ToText();
		_out.WriteLine(text);

		// Content area again: an 8-row bordered window paints 6 rows, so the footer is the last
		// non-empty one. Asserting "not the first row" is the claim that matters - sticky is live.
		var restored = text.Split('\n');
		Assert.Contains("FOOTER", restored[^2]);
		Assert.DoesNotContain("FOOTER", restored[0]);
	}
}
