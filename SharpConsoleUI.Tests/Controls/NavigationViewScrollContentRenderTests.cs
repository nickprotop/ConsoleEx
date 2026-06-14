// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Rendering-stability net for a NavigationView whose content panel (an SPC) has its overflowing
/// content repeatedly rebuilt and re-rendered.
///
/// <see cref="SharpConsoleUI.Tests.FocusManagement.NavViewFocusReclaimTests"/> pins the FOCUS angle
/// of this scenario; this pins the RENDER angle. The hang bug was a runaway render loop triggered by
/// rebuild → invalidate → relayout on an overflowing SPC body inside the NavigationView. A hang
/// would have stalled the run silently; here the test simply COMPLETING (no exception, no timeout)
/// is the guard, backed by the CI <c>--blame-hang</c> timeout.
/// </summary>
public class NavigationViewScrollContentRenderTests
{
	/// <summary>
	/// Builds a window with a NavigationView whose single item's content panel hosts the supplied
	/// content. A render pass is driven so the panel viewport is laid out. Mirrors
	/// <c>NavViewFocusReclaimTests.BuildNavWithFocusedTable()</c>.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, NavigationView nav, NavigationItem item)
		BuildNav(Action<ScrollablePanelControl> populate)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(100, 30);
		var window = new Window(system)
		{
			Title = "Test",
			Left = 0,
			Top = 0,
			Width = 100,
			Height = 30
		};

		var nav = new NavigationView();
		var item = nav.AddItem("Item 1");
		nav.SetItemContent(item, populate);

		window.AddControl(nav);
		system.AddWindow(window);

		// Select the item so the content factory populates the panel.
		nav.SelectedIndex = 0;

		system.Render.UpdateDisplay();
		system.Render.UpdateDisplay();

		return (system, window, nav, item);
	}

	/// <summary>
	/// Fills a panel with content taller than its viewport (a table with many rows), so the SPC
	/// body actually overflows and must scroll — the condition under which the runaway occurred.
	/// </summary>
	private static void PopulateOverflowingContent(ScrollablePanelControl panel, int rows, string tag)
	{
		var table = new TableControl();
		table.AddColumn("Col");
		for (int i = 0; i < rows; i++)
			table.AddRow($"{tag} row {i}");
		panel.AddControl(table);
	}

	[Fact]
	public void NavView_SpcBody_RepeatedContentRebuild_RendersWithoutHangOrThrow()
	{
		var (system, window, nav, item) = BuildNav(panel => PopulateOverflowingContent(panel, 60, "init"));

		var ex = Record.Exception(() =>
		{
			for (int tick = 0; tick < 5; tick++)
			{
				// Rebuild the content with fresh overflowing content (ClearContents + add).
				nav.SetItemContent(item, panel => PopulateOverflowingContent(panel, 60, $"tick{tick}"));

				// Re-render twice each iteration — the rebuild → invalidate → relayout path that
				// triggered the runaway. If this hangs, the CI --blame-hang timeout fails the run;
				// locally the test simply never returns. Completing is the guard.
				system.Render.UpdateDisplay();
				system.Render.UpdateDisplay();
			}
		});

		Assert.Null(ex);
	}

	[Fact]
	public void NavView_SpcBody_Renders_InnerContentVisible()
	{
		const string marker = "UNIQUE_NAV_BODY_MARKER";

		var (system, window, nav, item) = BuildNav(panel =>
		{
			panel.AddControl(ContainerTestHelpers.CreateLabel(marker));
			for (int i = 0; i < 40; i++)
				panel.AddControl(ContainerTestHelpers.CreateLabel($"filler {i}"));
		});

		var rendered = ContainerTestHelpers.StripAnsiCodes(window.RenderAndGetVisibleContent());

		Assert.Contains(marker, rendered);
	}
}
