// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Verifies NavigationItem is a live model: mutating a display property on an ADDED item updates the
/// view (re-bakes the row / content header / child visibility) and invalidates. Includes one real-render
/// smoke test proving the new value reaches the viewport — the regression class that hid the original bug
/// (baked content looked correct in isolation while the screen stayed stale).
/// </summary>
public class NavigationItemSelfInvalidationTests
{
	private static (ConsoleWindowSystem system, Window window, NavigationView nav) Build()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var window = new Window(system) { Title = "Nav", Left = 0, Top = 0, Width = 60, Height = 20 };
		var nav = new NavigationView { VerticalAlignment = VerticalAlignment.Fill, HorizontalAlignment = HorizontalAlignment.Stretch };
		window.AddControl(nav);
		system.AddWindow(window);
		return (system, window, nav);
	}

	private static List<string> Render(Window window)
		=> window.RenderAndGetVisibleContent(new List<Rectangle> { new(0, 0, window.Width, window.Height) });

	// --- Detached item: no owner, setter is a harmless no-op (must not throw) ---

	[Fact]
	public void DetachedItem_MutatingProperty_DoesNotThrow()
	{
		var item = new NavigationItem("Original");
		var ex = Record.Exception(() => { item.Text = "Changed"; item.Subtitle = "s"; item.IsExpanded = false; });
		Assert.Null(ex);
		Assert.Equal("Changed", item.Text);
	}

	// --- Text/Icon/IsEnabled/HeaderColor → row re-bake + invalidation ---

	[Fact]
	public void Text_OnAddedItem_InvalidatesView()
	{
		var (_, window, nav) = Build();
		var item = nav.AddItem("First");
		Render(window); // drain initial
		Assert.Equal(FrameWork.None, window.PendingWork);

		item.Text = "Renamed";

		Assert.NotEqual(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void Icon_OnAddedItem_InvalidatesView()
	{
		var (_, window, nav) = Build();
		var item = nav.AddItem("First");
		Render(window);
		item.Icon = "★";
		Assert.NotEqual(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void IsEnabled_OnAddedItem_InvalidatesView()
	{
		var (_, window, nav) = Build();
		var item = nav.AddItem("First");
		Render(window);
		item.IsEnabled = false;
		Assert.NotEqual(FrameWork.None, window.PendingWork);
	}

	[Fact]
	public void HeaderColor_OnAddedHeader_InvalidatesView()
	{
		var (_, window, nav) = Build();
		var header = nav.AddHeader("Group");
		Render(window);
		header.HeaderColor = Color.Red;
		Assert.NotEqual(FrameWork.None, window.PendingWork);
	}

	// --- Subtitle → content header (only when selected) ---

	[Fact]
	public void Subtitle_OnSelectedItem_AppearsInRenderedOutput()
	{
		var (_, window, nav) = Build();
		var item = nav.AddItem("First"); // auto-selected (first selectable item)
		Render(window);

		item.Subtitle = "uniqueSub42";
		var rendered = Render(window);

		Assert.Contains(rendered, l => l.Contains("uniqueSub42"));
	}

	// --- IsExpanded → child row visibility ---

	[Fact]
	public void IsExpanded_False_HidesChildRows()
	{
		var (_, window, nav) = Build();
		var header = nav.AddHeader("Group");
		var child = nav.AddItemToHeader(header, "Child");
		Render(window);

		header.IsExpanded = false;
		var collapsed = Render(window);
		Assert.DoesNotContain(collapsed, l => l.Contains("Child"));

		header.IsExpanded = true;
		var expanded = Render(window);
		Assert.Contains(expanded, l => l.Contains("Child"));
	}

	// --- THE regression guard: a mutated value actually reaches the viewport ---

	[Fact]
	public void Text_OnSelectedItem_NewValueRendered_OldValueGone()
	{
		var (_, window, nav) = Build();
		var item = nav.AddItem("OrigTitle"); // auto-selected
		var before = Render(window);
		Assert.Contains(before, l => l.Contains("OrigTitle"));

		item.Text = "NewTitle";
		var after = Render(window);

		Assert.Contains(after, l => l.Contains("NewTitle"));
		Assert.DoesNotContain(after, l => l.Contains("OrigTitle"));
	}
}
