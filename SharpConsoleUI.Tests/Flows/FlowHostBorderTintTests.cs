// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Core;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Helpers;
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
	}
}
