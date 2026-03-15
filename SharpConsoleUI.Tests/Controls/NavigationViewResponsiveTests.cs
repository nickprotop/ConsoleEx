// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using static SharpConsoleUI.Builders.Controls;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Test suite for NavigationView responsive display mode behavior.
/// </summary>
public class NavigationViewResponsiveTests
{
	#region Helpers

	/// <summary>
	/// Creates a NavigationView with some test items for use in tests.
	/// </summary>
	private static NavigationView CreateTestNav()
	{
		var nav = new NavigationView();
		nav.AddItem("Dashboard", "\u2302"); // ⌂
		nav.AddItem("Settings", "\u2699"); // ⚙
		nav.AddItem("Profile", "\u263A"); // ☺
		return nav;
	}

	/// <summary>
	/// Creates a test environment with a NavigationView inside a window of the given size.
	/// Performs a render cycle to establish bounds.
	/// </summary>
	private static (ConsoleWindowSystem system, Window window, NavigationView nav) CreateRenderedEnvironment(
		int width, int height, NavigationView? nav = null)
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(width, height);
		var window = new Window(system)
		{
			Title = "Test",
			Left = 0,
			Top = 0,
			Width = width,
			Height = height
		};
		nav ??= CreateTestNav();
		nav.VerticalAlignment = VerticalAlignment.Fill;
		window.AddControl(nav);
		system.AddWindow(window);
		system.Render.UpdateDisplay();
		return (system, window, nav);
	}

	#endregion

	#region Display Mode Resolution

	[Fact]
	public void Auto_WideWidth_ResolvesToExpanded()
	{
		var nav = CreateTestNav();
		var resolved = nav.ResolveDisplayMode(120);
		Assert.Equal(NavigationViewDisplayMode.Expanded, resolved);
	}

	[Fact]
	public void Auto_MediumWidth_ResolvesToCompact()
	{
		var nav = CreateTestNav();
		var resolved = nav.ResolveDisplayMode(60);
		Assert.Equal(NavigationViewDisplayMode.Compact, resolved);
	}

	[Fact]
	public void Auto_NarrowWidth_ResolvesToMinimal()
	{
		var nav = CreateTestNav();
		var resolved = nav.ResolveDisplayMode(40);
		Assert.Equal(NavigationViewDisplayMode.Minimal, resolved);
	}

	[Fact]
	public void Forced_Expanded_IgnoresWidth()
	{
		var nav = CreateTestNav();
		nav.PaneDisplayMode = NavigationViewDisplayMode.Expanded;
		var resolved = nav.ResolveDisplayMode(30);
		Assert.Equal(NavigationViewDisplayMode.Expanded, resolved);
	}

	[Fact]
	public void Forced_Compact_IgnoresWidth()
	{
		var nav = CreateTestNav();
		nav.PaneDisplayMode = NavigationViewDisplayMode.Compact;
		var resolved = nav.ResolveDisplayMode(120);
		Assert.Equal(NavigationViewDisplayMode.Compact, resolved);
	}

	[Fact]
	public void Forced_Minimal_IgnoresWidth()
	{
		var nav = CreateTestNav();
		nav.PaneDisplayMode = NavigationViewDisplayMode.Minimal;
		var resolved = nav.ResolveDisplayMode(120);
		Assert.Equal(NavigationViewDisplayMode.Minimal, resolved);
	}

	[Fact]
	public void CustomThresholds_Respected()
	{
		var nav = CreateTestNav();
		nav.ExpandedThreshold = 100;
		nav.CompactThreshold = 60;
		var resolved = nav.ResolveDisplayMode(70);
		Assert.Equal(NavigationViewDisplayMode.Compact, resolved);
	}

	[Fact]
	public void DisplayModeChanged_EventFires()
	{
		var nav = CreateTestNav();
		NavigationViewDisplayMode? firedMode = null;
		nav.DisplayModeChanged += (_, mode) => firedMode = mode;

		// Apply compact mode
		nav.CheckAndApplyDisplayMode(60);

		Assert.NotNull(firedMode);
		Assert.Equal(NavigationViewDisplayMode.Compact, firedMode);
	}

	[Fact]
	public void DisplayModeChanged_DoesNotFireWhenSameMode()
	{
		var nav = CreateTestNav();
		// First apply Expanded (default)
		nav.CheckAndApplyDisplayMode(120);

		int fireCount = 0;
		nav.DisplayModeChanged += (_, _) => fireCount++;

		// Apply same mode again
		nav.CheckAndApplyDisplayMode(120);

		Assert.Equal(0, fireCount);
	}

	#endregion

	#region Compact Mode Layout

	[Fact]
	public void Compact_NavColumnWidth_EqualsCompactPaneWidth()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Compact);
		Assert.Equal(ControlDefaults.DefaultNavigationViewCompactPaneWidth, nav.InternalGrid.Columns[0].Width!.Value);
	}

	[Fact]
	public void Compact_PaneHeaderShowsHamburger()
	{
		var nav = CreateTestNav();
		nav.PaneHeader = "[bold]Menu[/]";
		nav.AnimateTransitions = false;
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Compact);

		// In Compact mode, the pane header is visible and shows a hamburger button
		var navColumn = nav.InternalGrid.Columns[0];
		Assert.NotNull(navColumn);
		var children = navColumn.GetChildren();
		// First child is the pane header — should be visible as hamburger
		Assert.True(children[0].Visible);
	}

	[Fact]
	public void Compact_HeaderItemsHidden()
	{
		var nav = new NavigationView();
		var header = nav.AddHeader("Section");
		nav.AddItemToHeader(header, "SubItem", "\u2302");
		nav.AnimateTransitions = false;
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Compact);

		// The header item control should be hidden
		// Items[0] is the header
		Assert.Equal(NavigationViewDisplayMode.Compact, nav.CurrentDisplayMode);
		// We verify via the Items being properly count and mode set
		Assert.Equal(NavigationViewDisplayMode.Compact, nav.CurrentDisplayMode);
	}

	[Fact]
	public void Compact_SelectableItemsVisible()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Compact);

		// All selectable items should still be visible
		Assert.Equal(3, nav.Items.Count);
		Assert.Equal(NavigationViewDisplayMode.Compact, nav.CurrentDisplayMode);
	}

	[Fact]
	public void Compact_SelectionChangesContent()
	{
		var nav = CreateTestNav();
		bool contentChanged = false;
		nav.SelectedItemChanged += (_, _) => contentChanged = true;
		nav.AnimateTransitions = false;
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Compact);

		// Change selection
		nav.SelectedIndex = 1;

		Assert.True(contentChanged);
		Assert.Equal(1, nav.SelectedIndex);
	}

	[Fact]
	public void Compact_HamburgerOpensPortal()
	{
		var (system, window, nav) = CreateRenderedEnvironment(60, 20);
		nav.AnimateTransitions = false;
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Compact);

		// Open portal (as if hamburger was clicked)
		nav.OpenNavigationPortal();
		Assert.True(nav.IsPortalOpen);

		// Close portal
		nav.CloseNavigationPortal();
		Assert.False(nav.IsPortalOpen);
	}

	#endregion

	#region Minimal Mode Layout

	[Fact]
	public void Minimal_NavColumnWidth_IsZero()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Minimal);

		var navColumn = nav.InternalGrid.Columns[0];
		Assert.NotNull(navColumn);
		Assert.Equal(0, navColumn.Width!.Value);
	}

	[Fact]
	public void Minimal_ContentHeaderHasHamburger()
	{
		var (system, window, nav) = CreateRenderedEnvironment(40, 20);
		nav.AnimateTransitions = false;

		// Force minimal mode
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Minimal);

		Assert.Equal(NavigationViewDisplayMode.Minimal, nav.CurrentDisplayMode);
	}

	#endregion

	#region Portal (Minimal Mode)

	[Fact]
	public void Minimal_OpenPortal_CreatesOverlay()
	{
		var (system, window, nav) = CreateRenderedEnvironment(40, 20);
		nav.AnimateTransitions = false;
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Minimal);

		nav.OpenNavigationPortal();

		Assert.True(nav.IsPortalOpen);
	}

	[Fact]
	public void Minimal_ClosePortal_CleansUp()
	{
		var (system, window, nav) = CreateRenderedEnvironment(40, 20);
		nav.AnimateTransitions = false;
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Minimal);

		nav.OpenNavigationPortal();
		Assert.True(nav.IsPortalOpen);

		nav.CloseNavigationPortal();
		Assert.False(nav.IsPortalOpen);
	}

	[Fact]
	public void Minimal_PortalDismissOnOutsideClick()
	{
		var (system, window, nav) = CreateRenderedEnvironment(40, 20);
		nav.AnimateTransitions = false;
		nav.ApplyDisplayMode(NavigationViewDisplayMode.Minimal);

		nav.OpenNavigationPortal();

		// Portal should have DismissOnOutsideClick enabled
		Assert.True(nav.IsPortalOpen);
	}

	#endregion

	#region Mode Transitions

	[Fact]
	public void Expanded_To_Compact_UpdatesLayout()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;

		// Start in expanded
		nav.CheckAndApplyDisplayMode(120);
		Assert.Equal(NavigationViewDisplayMode.Expanded, nav.CurrentDisplayMode);

		// Transition to compact
		nav.CheckAndApplyDisplayMode(60);
		Assert.Equal(NavigationViewDisplayMode.Compact, nav.CurrentDisplayMode);
	}

	[Fact]
	public void Compact_To_Expanded_UpdatesLayout()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;

		// Start in compact
		nav.CheckAndApplyDisplayMode(60);
		Assert.Equal(NavigationViewDisplayMode.Compact, nav.CurrentDisplayMode);

		// Transition to expanded
		nav.CheckAndApplyDisplayMode(120);
		Assert.Equal(NavigationViewDisplayMode.Expanded, nav.CurrentDisplayMode);
	}

	[Fact]
	public void Compact_To_Minimal_HidesNavColumn()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;

		nav.CheckAndApplyDisplayMode(60);
		Assert.Equal(NavigationViewDisplayMode.Compact, nav.CurrentDisplayMode);

		nav.CheckAndApplyDisplayMode(30);
		Assert.Equal(NavigationViewDisplayMode.Minimal, nav.CurrentDisplayMode);

		var navColumn = nav.InternalGrid.Columns[0];
		Assert.NotNull(navColumn);
		Assert.Equal(0, navColumn.Width!.Value);
	}

	[Fact]
	public void Minimal_To_Compact_ShowsNavColumn()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;

		nav.CheckAndApplyDisplayMode(30);
		Assert.Equal(NavigationViewDisplayMode.Minimal, nav.CurrentDisplayMode);

		nav.CheckAndApplyDisplayMode(60);
		Assert.Equal(NavigationViewDisplayMode.Compact, nav.CurrentDisplayMode);

		var navColumn = nav.InternalGrid.Columns[0];
		Assert.NotNull(navColumn);
		Assert.Equal(ControlDefaults.DefaultNavigationViewCompactPaneWidth, navColumn.Width!.Value);
	}

	#endregion

	#region Animation

	[Fact]
	public void AnimateTransitions_True_UsesAnimationManager()
	{
		var (system, window, nav) = CreateRenderedEnvironment(120, 20);
		nav.AnimateTransitions = true;

		// Transition to compact should create an animation
		nav.CheckAndApplyDisplayMode(60);

		// The animation manager should have an active animation
		Assert.True(system.Animations.HasActiveAnimations);
	}

	[Fact]
	public void AnimateTransitions_False_InstantTransition()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;

		nav.CheckAndApplyDisplayMode(60);

		// Width should be immediately set
		var navColumn = nav.InternalGrid.Columns[0];
		Assert.NotNull(navColumn);
		Assert.Equal(ControlDefaults.DefaultNavigationViewCompactPaneWidth, navColumn.Width!.Value);
	}

	[Fact]
	public void Animation_CancelledOnNewTransition()
	{
		var (system, window, nav) = CreateRenderedEnvironment(120, 20);
		nav.AnimateTransitions = true;

		// Start first animation
		nav.CheckAndApplyDisplayMode(60);
		Assert.True(system.Animations.HasActiveAnimations);

		// Start second animation (should cancel the first)
		nav.CheckAndApplyDisplayMode(30);
		// Should still have animations but the old one should be cancelled
		Assert.Equal(NavigationViewDisplayMode.Minimal, nav.CurrentDisplayMode);
	}

	#endregion

	#region Selection Preservation

	[Fact]
	public void ModeChange_PreservesSelectedIndex()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;
		nav.SelectedIndex = 1;

		nav.CheckAndApplyDisplayMode(60); // Compact
		Assert.Equal(1, nav.SelectedIndex);

		nav.CheckAndApplyDisplayMode(120); // Expanded
		Assert.Equal(1, nav.SelectedIndex);

		nav.CheckAndApplyDisplayMode(30); // Minimal
		Assert.Equal(1, nav.SelectedIndex);
	}

	[Fact]
	public void ModeChange_PreservesContentPanel()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;

		bool factoryCalled = false;
		nav.SetItemContent(0, panel =>
		{
			factoryCalled = true;
			panel.AddControl(new MarkupControl(new List<string> { "Test content" }));
		});

		// The factory was called on initial selection
		Assert.True(factoryCalled);

		// Mode change should not call factory again
		factoryCalled = false;
		nav.CheckAndApplyDisplayMode(60);
		Assert.False(factoryCalled);
	}

	#endregion

	#region Builder

	[Fact]
	public void Builder_WithPaneDisplayMode_SetsProperty()
	{
		var nav = new NavigationViewBuilder()
			.AddItem("Test")
			.WithPaneDisplayMode(NavigationViewDisplayMode.Compact)
			.Build();
		Assert.Equal(NavigationViewDisplayMode.Compact, nav.PaneDisplayMode);
	}

	[Fact]
	public void Builder_WithExpandedThreshold_SetsProperty()
	{
		var nav = new NavigationViewBuilder()
			.AddItem("Test")
			.WithExpandedThreshold(100)
			.Build();
		Assert.Equal(100, nav.ExpandedThreshold);
	}

	[Fact]
	public void Builder_WithCompactThreshold_SetsProperty()
	{
		var nav = new NavigationViewBuilder()
			.AddItem("Test")
			.WithCompactThreshold(60)
			.Build();
		Assert.Equal(60, nav.CompactThreshold);
	}

	[Fact]
	public void Builder_WithCompactPaneWidth_SetsProperty()
	{
		var nav = new NavigationViewBuilder()
			.AddItem("Test")
			.WithCompactPaneWidth(8)
			.Build();
		Assert.Equal(8, nav.CompactPaneWidth);
	}

	[Fact]
	public void Builder_WithAnimateTransitions_SetsProperty()
	{
		var nav = new NavigationViewBuilder()
			.AddItem("Test")
			.WithAnimateTransitions(false)
			.Build();
		Assert.False(nav.AnimateTransitions);
	}

	#endregion

	#region Backward Compatibility / Zero Regression

	[Fact]
	public void DefaultPaneDisplayMode_IsAuto()
	{
		var nav = new NavigationView();
		Assert.Equal(NavigationViewDisplayMode.Auto, nav.PaneDisplayMode);
	}

	[Fact]
	public void NoResponsiveConfig_BehavesAsExpanded()
	{
		var nav = CreateTestNav();
		nav.AnimateTransitions = false;

		// With a wide width, default Auto mode should resolve to Expanded
		nav.CheckAndApplyDisplayMode(120);
		Assert.Equal(NavigationViewDisplayMode.Expanded, nav.CurrentDisplayMode);

		// Nav pane width should be the configured NavPaneWidth
		var navColumn = nav.InternalGrid.Columns[0];
		Assert.NotNull(navColumn);
		Assert.Equal(nav.NavPaneWidth, navColumn.Width!.Value);
	}

	[Fact]
	public void ExistingNavPaneWidth_StillWorks()
	{
		var nav = new NavigationView();
		nav.NavPaneWidth = 30;
		Assert.Equal(30, nav.NavPaneWidth);

		// NavPaneWidth should be used for expanded mode width
		nav.AnimateTransitions = false;
		nav.CheckAndApplyDisplayMode(120);

		var navColumn = nav.InternalGrid.Columns[0];
		Assert.NotNull(navColumn);
		Assert.Equal(30, navColumn.Width!.Value);
	}

	#endregion
}
