// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Tests.Infrastructure;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Flows
{
	public class FlowHostBorderTintTests
	{
		[Fact]
		public void ResolveBorderColors_DangerSeverity_ActiveIsDangerRoleBorder()
		{
			ITheme theme = new ModernGrayTheme();
			var chrome = new FlowChrome("X", null, null, null,
				severity: NotificationSeverityEnum.Danger);

			var (active, inactive) = FlowContentHelpers.ResolveBorderColors(chrome, theme);

			var expectedActive = ColorRoleResolver.Resolve(ColorRole.Danger, theme).Border;
			Assert.Equal(expectedActive, active);
			Assert.Equal(expectedActive.Shade(ControlDefaults.FlowInactiveBorderShade), inactive);
		}

		[Fact]
		public void ResolveBorderColors_NoneSeverity_UsesPrimaryRole()
		{
			ITheme theme = new ModernGrayTheme();
			var chrome = new FlowChrome("X", null, null, null); // severity defaults to None

			var (active, _) = FlowContentHelpers.ResolveBorderColors(chrome, theme);

			Assert.Equal(ColorRoleResolver.Resolve(ColorRole.Primary, theme).Border, active);
		}

		[Fact]
		public void ResolveBorderColors_ProgressGlyph_ForcesPrimary()
		{
			ITheme theme = new ModernGrayTheme();
			// Internal ctor sets UseProgressGlyph; build a Danger chrome WITH the progress glyph and
			// assert the border still resolves to Primary (frame follows the top band, which forces Primary).
			var chrome = new FlowChrome("X", null, null, null, null, null, NotificationSeverityEnum.Danger, useProgressGlyph: true);

			var (active, _) = FlowContentHelpers.ResolveBorderColors(chrome, theme);

			Assert.Equal(ColorRoleResolver.Resolve(ColorRole.Primary, theme).Border, active);
		}

		// -----------------------------------------------------------------------
		// Real-thing: ModalWindowHost — Danger step tints the modal window border
		// -----------------------------------------------------------------------

		[Fact]
		public async Task ModalWindowHost_DangerStep_TintsWindowBorder()
		{
			// Real-thing: present a Danger step via the real ModalWindowHost, render it, and assert
			// the modal window's border colors match the Danger role resolved against the live theme.
			// This catches any regression where the builder call is missing or uses the wrong colors.
			var system = TestWindowSystemBuilder.CreateTestSystem(width: 50, height: 14);
			var host = new ModalWindowHost(system, parent: null);
			var content = new TallTextStepContent(lineCount: 1);
			var chrome = new FlowChrome(
				title: "Danger",
				widthHint: 40,
				heightHint: 10,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) },
				severity: NotificationSeverityEnum.Danger);

			var presentTask = host.PresentAsync(content, chrome, CancellationToken.None);
			system.Render.UpdateDisplay();

			// The modal is the only window open; grab it to inspect its border color properties.
			var window = system.Windows.Values.Single();

			// Expected: the colors the host resolved when it built the window.
			var (expActive, expInactive) = FlowContentHelpers.ResolveBorderColors(chrome, system.Theme);

			Assert.Equal(expActive, window.ActiveBorderForegroundColor);
			Assert.Equal(expInactive, window.InactiveBorderForegroundColor);

			// Re-render to confirm colors survive a second paint cycle.
			system.Render.UpdateDisplay();
			Assert.Equal(expActive, window.ActiveBorderForegroundColor);
			Assert.Equal(expInactive, window.InactiveBorderForegroundColor);

			// Resolve the flow so the test does not hang.
			bool clicked = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-OK");
			Assert.True(clicked, "Expected to find and click 'flow-host-btn-OK'");

			var outcome = await presentTask.WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Equal(FlowVerdict.Next, outcome.Verdict);
		}

		// -----------------------------------------------------------------------
		// Real-thing: SwapContentHost — border updates per step as severity changes
		// -----------------------------------------------------------------------

		[Fact]
		public async Task SwapContentHost_BorderChangesPerStep_SurvivesReRender()
		{
			// Real-thing: reused window host — present step 1 as Info (→ Primary border), assert the border,
			// then present step 2 as Danger on the SAME window and assert the border has changed and
			// survives a re-render. This catches any regression in the per-step EnqueueOnUIThread wiring.
			var system = TestWindowSystemBuilder.CreateTestSystem(width: 50, height: 14);
			using var host = new SwapContentHost(system, parent: null);

			// ---- Step 1: Info → Primary border ----
			var step1 = new TallTextStepContent(lineCount: 1);
			var chrome1 = new FlowChrome(
				title: "One",
				stepIndicator: null,
				widthHint: 40,
				heightHint: 10,
				buttons: new[] { new FlowButton("Next", FlowVerdict.Next) },
				severity: NotificationSeverityEnum.Info);

			var t1 = host.PresentAsync(step1, chrome1, CancellationToken.None);

			// Drain the EnqueueOnUIThread border-set action then paint.
			for (int i = 0; i < 3; i++)
			{
				system.DrainPendingUIActionsForTest();
				system.Render.UpdateDisplay();
			}

			var window = system.Windows.Values.Single();
			var expPrimary = ColorRoleResolver.Resolve(ColorRole.Primary, system.Theme).Border;
			Assert.Equal(expPrimary, window.ActiveBorderForegroundColor);

			// Resolve step 1 via button click.
			bool clicked1 = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Next");
			Assert.True(clicked1, "Expected to find and click 'flow-host-btn-Next'");
			await t1.WaitAsync(TimeSpan.FromSeconds(5));

			// ---- Step 2: Danger → Danger border on the SAME reused window ----
			var step2 = new TallTextStepContent(lineCount: 1);
			var chrome2 = new FlowChrome(
				title: "Two",
				stepIndicator: null,
				widthHint: 40,
				heightHint: 10,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) },
				severity: NotificationSeverityEnum.Danger);

			var t2 = host.PresentAsync(step2, chrome2, CancellationToken.None);

			// Drain the border-set action then paint.
			for (int i = 0; i < 3; i++)
			{
				system.DrainPendingUIActionsForTest();
				system.Render.UpdateDisplay();
			}

			var expDanger = ColorRoleResolver.Resolve(ColorRole.Danger, system.Theme).Border;
			Assert.Equal(expDanger, window.ActiveBorderForegroundColor);

			// Survives a re-render.
			system.Render.UpdateDisplay();
			Assert.Equal(expDanger, window.ActiveBorderForegroundColor);

			// Resolve step 2.
			bool clicked2 = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-OK");
			Assert.True(clicked2, "Expected to find and click 'flow-host-btn-OK'");
			await t2.WaitAsync(TimeSpan.FromSeconds(5));
		}
	}
}
