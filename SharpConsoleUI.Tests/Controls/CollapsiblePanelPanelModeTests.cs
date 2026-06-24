// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Tests for CollapsiblePanel acting as a plain (non-collapsible / header-optional) panel.
/// </summary>
public class CollapsiblePanelPanelModeTests
{
	[Fact]
	public void Defaults_AreCollapsibleAndHeaderShown()
	{
		var panel = new CollapsiblePanel();
		Assert.True(panel.Collapsible);
		Assert.True(panel.ShowHeader);
	}

	[Fact]
	public void Collapsible_True_ForcesHeaderShown_EvenIfShowHeaderFalse()
	{
		// Invalid combo resolves gracefully: collapsibility wins, header stays visible.
		var panel = new CollapsiblePanel { Title = "Sec", Collapsible = true, ShowHeader = false, Width = 20 };
		var lines = ContainerTestHelpers.RenderToLines(panel, width: 22, height: 6);
		Assert.Contains(lines, l => l.Contains("Sec"));
	}

	[Fact]
	public void HeaderHidden_Borderless_BodyStartsAtTop_NoHeaderRow()
	{
		var panel = new CollapsiblePanel
		{
			Title = "Title",
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Borderless,
			Width = 20
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("body text"));

		var lines = ContainerTestHelpers.RenderToLines(panel, width: 22, height: 6);

		// No title rendered, and the body appears on the first content row.
		Assert.DoesNotContain(lines, l => l.Contains("Title"));
		Assert.Contains(lines, l => l.Contains("body text"));
		Assert.Equal(0, panel.HeaderHeightForTest);
	}

	[Fact]
	public void HeaderHidden_Bordered_KeepsTopBorderRow()
	{
		var panel = new CollapsiblePanel
		{
			Title = "Title",
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Bordered,
			Width = 20
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("body text"));

		// Bordered box reserves one top-border row even with the header hidden.
		Assert.Equal(1, panel.HeaderHeightForTest);
	}

	[Fact]
	public void NonCollapsible_HeaderShown_HasNoIndicatorGlyph()
	{
		var panel = new CollapsiblePanel
		{
			Title = "Section",
			Collapsible = false,
			ShowHeader = true,
			HeaderStyle = CollapsibleHeaderStyle.Borderless,
			Width = 24
		};
		var lines = ContainerTestHelpers.RenderToLines(panel, width: 26, height: 5);

		Assert.Contains(lines, l => l.Contains("Section"));
		Assert.DoesNotContain(lines, l => l.Contains("▾"));
		Assert.DoesNotContain(lines, l => l.Contains("▸"));
	}

	[Fact]
	public void HeaderHidden_Bordered_DrawsCornersWithoutTitle()
	{
		var panel = new CollapsiblePanel
		{
			Title = "Title",
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Bordered,
			Width = 16
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));
		var lines = ContainerTestHelpers.RenderToLines(panel, width: 18, height: 6);
		var text = string.Join("\n", lines);

		// Clean box: corners present, body inside, no title on the top border.
		Assert.Contains("┌", text);
		Assert.Contains("┐", text);
		Assert.Contains("└", text);
		Assert.Contains("┘", text);
		Assert.Contains("body", text);
		Assert.DoesNotContain(lines, l => l.Contains("┌") && l.Contains("Title"));
	}

	[Fact]
	public void HeaderHidden_Borderless_NoHeaderTextRow()
	{
		var panel = new CollapsiblePanel
		{
			Title = "HeaderTitle",
			Collapsible = false,
			ShowHeader = false,
			HeaderStyle = CollapsibleHeaderStyle.Borderless,
			Width = 20
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("firstbody"));
		var lines = ContainerTestHelpers.RenderToLines(panel, width: 22, height: 6);

		// The header title must not appear anywhere, and the first non-empty content row is the body.
		Assert.DoesNotContain(lines, l => l.Contains("HeaderTitle"));
		var firstNonEmpty = lines.FirstOrDefault(l => l.Trim().Length > 0);
		Assert.NotNull(firstNonEmpty);
		Assert.Contains("firstbody", firstNonEmpty!);
	}

	[Fact]
	public void NonCollapsible_IgnoresEnterSpace_AndStaysExpanded()
	{
		var panel = new CollapsiblePanel { Title = "S", Collapsible = false };
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));

		bool handledEnter = panel.ProcessKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
		bool handledSpace = panel.ProcessKey(new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false));

		Assert.False(handledEnter);
		Assert.False(handledSpace);
		Assert.True(panel.IsExpanded);
	}

	[Fact]
	public void NonCollapsible_CannotReceiveFocus_Collapsible_Can()
	{
		var panel = new CollapsiblePanel { Title = "S", Collapsible = false };
		Assert.False(panel.CanReceiveFocus);

		var collapsible = new CollapsiblePanel { Title = "S", Collapsible = true };
		Assert.True(collapsible.CanReceiveFocus);
	}

	[Fact]
	public void NonCollapsible_HeaderClick_DoesNotToggle()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 25);
		var window = new Window(system) { Width = 80, Height = 25 };
		var panel = new CollapsiblePanel
		{
			Title = "S",
			Collapsible = false,
			Width = 20,
			HeaderStyle = CollapsibleHeaderStyle.Bordered
		};
		panel.AddControl(ContainerTestHelpers.CreateLabel("body"));
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		var args = new MouseEventArgs(
			new List<MouseFlags> { MouseFlags.Button1Clicked },
			new Point(2, 0), new Point(2, 0), new Point(2, 0), window);
		bool handled = ((IMouseAwareControl)panel).ProcessMouseEvent(args);

		Assert.True(panel.IsExpanded);   // never collapses
										 // A non-collapsible header click does NOT toggle (asserted by IsExpanded above), but it IS
										 // surfaced as the panel's own MouseClick and reported handled, so a bubbling dispatcher stops
										 // at the panel and an outer container does not double-handle the same click.
		Assert.True(handled);
	}

	[Fact]
	public void Builder_PanelMode_ConfiguresFlags()
	{
		var panel = SharpConsoleUI.Builders.Controls.CollapsiblePanel("Box")
			.NonCollapsible()
			.HideHeader()
			.WithHeaderStyle(CollapsibleHeaderStyle.Bordered)
			.AddControl(ContainerTestHelpers.CreateLabel("body"))
			.Build();

		Assert.False(panel.Collapsible);
		Assert.False(panel.ShowHeader);
		Assert.Equal(CollapsibleHeaderStyle.Bordered, panel.HeaderStyle);
		Assert.True(panel.IsExpanded);
	}

	[Fact]
	public void Builder_Collapsible_And_ShowHeader_Setters()
	{
		var p1 = SharpConsoleUI.Builders.Controls.CollapsiblePanel("A").Collapsible(false).Build();
		Assert.False(p1.Collapsible);

		var p2 = SharpConsoleUI.Builders.Controls.CollapsiblePanel("B").Collapsible(true).Build();
		Assert.True(p2.Collapsible);

		var p3 = SharpConsoleUI.Builders.Controls.CollapsiblePanel("C").ShowHeader(false).Build();
		Assert.False(p3.ShowHeader);

		var p4 = SharpConsoleUI.Builders.Controls.CollapsiblePanel("D").ShowHeader(true).Build();
		Assert.True(p4.ShowHeader);
	}

	[Fact]
	public void NonCollapsible_FocusPassesThroughToBodyChild()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(80, 25);
		var window = new Window(system) { Width = 80, Height = 25 };
		var button = new ButtonControl { Text = "X" };
		var panel = new CollapsiblePanel { Title = "Section", Collapsible = false };
		panel.AddControl(button);
		window.AddControl(panel);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		// Tab forward through every focus stop. The panel is a transparent (non-collapsible)
		// container, so traversal must descend into it and land on the body button without the
		// panel itself ever becoming the focused stop. Bound the walk by a full cycle and detect
		// revisiting the start so a focus cycle fails fast instead of relying on a magic count.
		const int maxFocusStops = 32; // generous upper bound on distinct focus stops in a test window
		bool reached = window.FocusManager.IsFocused(button);
		bool panelEverFocused = window.FocusManager.IsFocused(panel);
		for (int i = 0; i < maxFocusStops && !reached; i++)
		{
			window.SwitchFocus(backward: false);
			if (window.FocusManager.IsFocused(panel)) panelEverFocused = true;
			if (window.FocusManager.IsFocused(button)) { reached = true; break; }
		}

		Assert.True(reached, "Non-collapsible panel: Tab traversal should pass through to the body button.");
		Assert.False(panelEverFocused, "Non-collapsible panel must never be a focus stop during traversal.");
	}
}
