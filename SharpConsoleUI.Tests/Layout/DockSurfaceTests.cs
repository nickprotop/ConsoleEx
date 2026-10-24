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
/// The dock surfaces as an authoring API: add, remove, insert, clear, query - and the guarantee
/// that they and <c>Window.Controls</c> can never disagree, because a surface is a view over the
/// child list rather than a second collection.
/// </summary>
[Collection("RootLayout")]
public class DockSurfaceTests
{
	private readonly ITestOutputHelper _out;

	public DockSurfaceTests(ITestOutputHelper output) => _out = output;

	private static Window NewWindow(ConsoleWindowSystem system, int w = 44, int h = 12)
	{
		var window = new Window(system) { Title = "Docks", Width = w, Height = h, Left = 0, Top = 0 };
		system.AddWindow(window);
		return window;
	}

	private static ConsoleWindowSystem NewSystem() => TestWindowSystemBuilder.CreateTestSystem(100, 40);

	#region StickyPosition is sugar over Dock

	[Fact]
	public void SettingSticky_SetsDock()
	{
		var c = Builder.Markup("x").Build();

		c.StickyPosition = StickyPosition.Top;
		Assert.Equal(Dock.Top, c.Dock);

		c.StickyPosition = StickyPosition.Bottom;
		Assert.Equal(Dock.Bottom, c.Dock);

		c.StickyPosition = StickyPosition.None;
		Assert.Equal(Dock.None, c.Dock);
	}

	[Fact]
	public void SettingDock_IsVisibleThroughSticky()
	{
		var c = Builder.Markup("x").Build();

		c.Dock = Dock.Top;
		Assert.Equal(StickyPosition.Top, c.StickyPosition);

		c.Dock = Dock.Bottom;
		Assert.Equal(StickyPosition.Bottom, c.StickyPosition);
	}

	[Fact]
	public void TheHorizontalEdges_ReadBackAsNoneThroughSticky()
	{
		// Honest rather than lossy: sticky only ever named the vertical edges, so a left-docked
		// control is not "sticky top" or "sticky bottom" and must not claim to be.
		var c = Builder.Markup("x").Build();

		c.Dock = Dock.Left;
		Assert.Equal(StickyPosition.None, c.StickyPosition);
		Assert.Equal(Dock.Left, c.Dock);   // and the real value is untouched
	}

	[Fact]
	public void TheBuilderSugar_StillWorks()
	{
		Assert.Equal(Dock.Top, Builder.Markup("x").StickyTop().Build().Dock);
		Assert.Equal(Dock.Bottom, Builder.Markup("x").StickyBottom().Build().Dock);
	}

	#endregion

	#region Surfaces are views, not collections

	[Fact]
	public void AddingThroughASurface_PutsTheControlInTheWindow()
	{
		var window = NewWindow(NewSystem());
		var header = Builder.Markup("HEADER").Build();

		window.DockTop.Add(header);

		Assert.Contains(header, window.GetControls());
		Assert.Equal(Dock.Top, header.Dock);
		Assert.Equal(1, window.DockTop.Count);
	}

	[Fact]
	public void AddingDirectlyWithADock_ShowsUpInTheSurface()
	{
		// The other direction: the surface is not a registry you must go through.
		var window = NewWindow(NewSystem());
		var footer = Builder.Markup("FOOTER").Build();
		footer.Dock = Dock.Bottom;

		window.AddControl(footer);

		Assert.Equal(1, window.DockBottom.Count);
		Assert.Same(footer, window.DockBottom[0]);
	}

	[Fact]
	public void ChangingDockOnAChild_MovesItBetweenSurfaces()
	{
		// The case a second collection would get wrong: nobody told the surfaces anything.
		var window = NewWindow(NewSystem());
		var c = Builder.Markup("x").Build();
		window.DockTop.Add(c);

		Assert.Equal(1, window.DockTop.Count);
		Assert.Equal(0, window.DockBottom.Count);

		c.Dock = Dock.Bottom;

		Assert.Equal(0, window.DockTop.Count);
		Assert.Equal(1, window.DockBottom.Count);
	}

	[Fact]
	public void UndockedControls_AreTheBody()
	{
		var window = NewWindow(NewSystem());
		window.DockTop.Add(Builder.Markup("HEADER").Build());
		window.AddControl(Builder.Markup("one").Build());
		window.AddControl(Builder.Markup("two").Build());

		Assert.Equal(2, window.DockBody.Count);
		Assert.Equal(1, window.DockTop.Count);
		Assert.Equal(3, window.GetControls().Count);
	}

	#endregion

	#region Mutation

	[Fact]
	public void Remove_TakesTheControlOutOfTheWindow()
	{
		var window = NewWindow(NewSystem());
		var header = Builder.Markup("HEADER").Build();
		window.DockTop.Add(header);

		Assert.True(window.DockTop.Remove(header));

		Assert.Empty(window.GetControls());
		Assert.Equal(0, window.DockTop.Count);
	}

	[Fact]
	public void Remove_RefusesAControlDockedElsewhere()
	{
		// Removing through the wrong edge must not silently work, or a caller tidying up one
		// region could delete another's content.
		var window = NewWindow(NewSystem());
		var footer = Builder.Markup("FOOTER").Build();
		window.DockBottom.Add(footer);

		Assert.False(window.DockTop.Remove(footer));
		Assert.Contains(footer, window.GetControls());
	}

	[Fact]
	public void Clear_EmptiesOnlyItsOwnEdge()
	{
		var window = NewWindow(NewSystem());
		window.DockTop.Add(Builder.Markup("H1").Build());
		window.DockTop.Add(Builder.Markup("H2").Build());
		var footer = Builder.Markup("FOOTER").Build();
		window.DockBottom.Add(footer);
		var body = Builder.Markup("body").Build();
		window.AddControl(body);

		window.DockTop.Clear();

		Assert.Equal(0, window.DockTop.Count);
		Assert.Equal(1, window.DockBottom.Count);
		Assert.Equal(1, window.DockBody.Count);
		Assert.Equal(2, window.GetControls().Count);
	}

	[Fact]
	public void Insert_PlacesTheControlWithinItsEdge()
	{
		var window = NewWindow(NewSystem());
		var first = Builder.Markup("FIRST").Build();
		var third = Builder.Markup("THIRD").Build();
		window.DockTop.Add(first);
		window.DockTop.Add(third);

		var second = Builder.Markup("SECOND").Build();
		window.DockTop.Insert(1, second);

		Assert.Equal(3, window.DockTop.Count);
		Assert.Same(first, window.DockTop[0]);
		Assert.Same(second, window.DockTop[1]);
		Assert.Same(third, window.DockTop[2]);
	}

	[Fact]
	public void Insert_KeepsTheRelativeOrderOfOtherEdges()
	{
		// Order ACROSS edges is semantic - a top dock declared before a left dock spans the full
		// width - so inserting into one edge must not reshuffle the others.
		var window = NewWindow(NewSystem());
		var header = Builder.Markup("HEADER").WithName("header").Build();
		var side = Builder.Markup("SIDE").WithName("side").Build();
		var tail = Builder.Markup("TAIL").WithName("tail").Build();

		window.DockTop.Add(header);
		window.DockLeft.Add(side);
		window.DockTop.Add(tail);

		var mid = Builder.Markup("MID").WithName("mid").Build();
		window.DockTop.Insert(1, mid);

		Assert.Equal(new[] { "header", "mid", "tail" },
			window.DockTop.Select(c => c.Name).ToArray());
		Assert.Equal(1, window.DockLeft.Count);
		Assert.Same(side, window.DockLeft[0]);
	}

	[Fact]
	public void Insert_RejectsAnIndexPastTheEdge()
	{
		var window = NewWindow(NewSystem());
		window.DockTop.Add(Builder.Markup("ONE").Build());

		Assert.Throws<ArgumentOutOfRangeException>(
			() => window.DockTop.Insert(5, Builder.Markup("X").Build()));
	}

	[Fact]
	public void Contains_IsEdgeSpecific()
	{
		var window = NewWindow(NewSystem());
		var footer = Builder.Markup("FOOTER").Build();
		window.DockBottom.Add(footer);

		Assert.True(window.DockBottom.Contains(footer));
		Assert.False(window.DockTop.Contains(footer));
	}

	[Fact]
	public void ASurface_Enumerates()
	{
		var window = NewWindow(NewSystem());
		window.DockTop.Add(Builder.Markup("A").Build());
		window.DockTop.Add(Builder.Markup("B").Build());

		Assert.Equal(2, window.DockTop.Count());
	}

	#endregion

	#region The surfaces actually render

	[Fact]
	public void AFourEdgeShell_BuiltEntirelyThroughSurfaces()
	{
		var previous = WindowRenderer.RootLayoutFactory;
		try
		{
			WindowRenderer.RootLayoutFactory = () => new DockRootLayout();

			var system = NewSystem();
			var window = NewWindow(system);

			window.DockTop.Add(Builder.Markup("HEADER").Build());
			window.DockBottom.Add(Builder.Markup("STATUS").Build());
			window.DockLeft.Add(Builder.Markup("SIDE").Build());
			window.DockRight.Add(Builder.Markup("INSP").Build());
			for (int i = 0; i < 20; i++)
				window.DockBody.Add(Builder.Markup($"body {i}").Build());

			window.RenderAndGetVisibleContent(
				new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) });

			var text = GoldenBuffer.Capture(window, "four edges").ToText();
			_out.WriteLine(text);

			var lines = text.Split('\n');
			Assert.StartsWith("HEADER", lines[0]);
			Assert.StartsWith("STATUS", lines[^2]);
			Assert.StartsWith("SIDE", lines[1]);
			Assert.Contains("INSP", lines[1]);
			Assert.Contains("body", lines[1]);
		}
		finally
		{
			WindowRenderer.RootLayoutFactory = previous;
		}
	}

	#endregion
}
