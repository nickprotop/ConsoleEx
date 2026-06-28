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
	}
}
