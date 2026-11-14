// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering;

/// <summary>
/// A hidden panel must not leave the status-bar dirty signal stuck on.
/// </summary>
/// <remarks>
/// <para>
/// <c>IsStatusBarDirty()</c> is one of the terms in the main loop's <c>shouldRender</c>
/// decision, and <c>PanelStateService.IsDirty</c> asks the panels for their flag without
/// asking whether they are visible. The flag itself is cleared in exactly one place: the
/// end of <c>Panel.Render</c>. <c>RenderCoordinator.RenderPanel</c> returned on a hidden
/// panel BEFORE reaching that clear, so a panel marked dirty while hidden stayed dirty for
/// the life of the process.
/// </para>
/// <para>
/// The cost is not a wrong pixel - a hidden panel has nothing to draw - it is the render
/// loop never settling. With the term permanently true the loop renders at the frame cap
/// forever, laying out and compositing a frame that writes zero bytes to the terminal, and
/// an idle application burns CPU for as long as it is open. Hiding a panel is enough to
/// trigger it: the <c>Visible</c> setter marks it dirty on the way out.
/// </para>
/// </remarks>
public class HiddenPanelDirtyFlagTests
{
	/// <summary>Builds a system whose panels exist and are hidden, as a panel-less app has.</summary>
	private static ConsoleWindowSystem HiddenPanelSystem()
		=> TestWindowSystemBuilder.CreateTestSystem(o => o with
		{
			ShowTopPanel = false,
			ShowBottomPanel = false,
		});

	[Fact]
	public void HiddenPanels_DoNotReportTheStatusBarDirty()
	{
		var system = HiddenPanelSystem();

		// Panels were constructed dirty and hidden; one render must settle the signal.
		system.Render.UpdateDisplay();

		Assert.False(system.Render.IsStatusBarDirty());
	}

	[Fact]
	public void MarkingAHiddenPanelDirty_DoesNotStickTheSignalOn()
	{
		var system = HiddenPanelSystem();
		system.Render.UpdateDisplay();

		// Exactly what a clock element or a status-text update does, while hidden.
		system.Render.InvalidateStatusCache();
		system.Render.UpdateDisplay();

		Assert.False(system.Render.IsStatusBarDirty());
	}

	[Fact]
	public void HidingAVisiblePanel_DoesNotStickTheSignalOn()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(o => o with
		{
			ShowTopPanel = true,
			ShowBottomPanel = true,
		});
		system.Render.UpdateDisplay();
		Assert.False(system.Render.IsStatusBarDirty());

		// The Visible setter marks the panel dirty on the way out, and a hidden panel
		// never renders, so this is the move that used to wedge the loop.
		system.PanelStateService.ShowTopPanel = false;
		system.PanelStateService.ShowBottomPanel = false;
		system.Render.UpdateDisplay();

		Assert.False(system.Render.IsStatusBarDirty());
	}

	[Fact]
	public void RepeatedRenders_WithHiddenPanels_StaySettled()
	{
		var system = HiddenPanelSystem();

		// The loop renders many frames; the signal must not come back on by itself.
		for (int i = 0; i < 5; i++)
		{
			system.Render.UpdateDisplay();
			Assert.False(system.Render.IsStatusBarDirty());
		}
	}

	[Fact]
	public void AVisiblePanel_StillReportsDirtyUntilItRenders()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(o => o with
		{
			ShowTopPanel = true,
			ShowBottomPanel = true,
		});
		system.Render.UpdateDisplay();

		// The regression guard: the fix must not silence a panel that genuinely needs painting.
		system.Render.InvalidateStatusCache();
		Assert.True(system.Render.IsStatusBarDirty());

		system.Render.UpdateDisplay();
		Assert.False(system.Render.IsStatusBarDirty());
	}
}
