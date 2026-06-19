// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI;
using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

/// <summary>
/// Safety net: every <see cref="ColorRole"/> resolved against every catalog theme must produce
/// readable text — both solid and outline variants in the Normal state must keep a sufficient
/// luminance gap against the surface they composite onto. Guards against the "invisible role
/// control" class of bug across the whole theme catalog.
/// </summary>
public class ColorRoleContrastAuditTests
{
	private static readonly ColorRole[] Roles =
	{
		ColorRole.Primary, ColorRole.Secondary, ColorRole.Tertiary,
		ColorRole.Info, ColorRole.Success, ColorRole.Warning, ColorRole.Danger
	};

	public static IEnumerable<object[]> Cases()
	{
		var reg = new ThemeRegistryStateService();
		foreach (var name in reg.GetAvailableThemeNames())
			foreach (var role in Roles)
				yield return new object[] { name, role };
	}

	[Theory]
	[MemberData(nameof(Cases))]
	public void Role_Solid_Normal_IsReadable(string themeName, ColorRole role)
	{
		var reg = new ThemeRegistryStateService();
		var theme = reg.GetTheme(themeName)!;
		var rc = ColorRoleResolver.Resolve(role, theme, outline: false, state: ColorRoleState.Normal);
		var surfaceBg = theme.WindowBackgroundColor;

		// Border + the role-as-text colour must stand out against the window surface.
		Assert.True(Math.Abs(rc.Border.Luminance() - surfaceBg.Luminance()) >= 60,
			$"{themeName}/{role}: Border not readable on surface");
		Assert.True(Math.Abs(rc.Text.Luminance() - surfaceBg.Luminance()) >= 60,
			$"{themeName}/{role}: Text not readable on surface");
		// The fill's text must be readable on the fill.
		Assert.True(Math.Abs(rc.TextOnBackground.Luminance() - rc.Background.Luminance()) >= 60,
			$"{themeName}/{role}: TextOnBackground not readable on fill");
	}

	[Theory]
	[MemberData(nameof(Cases))]
	public void Role_Outline_Normal_IsReadable(string themeName, ColorRole role)
	{
		var reg = new ThemeRegistryStateService();
		var theme = reg.GetTheme(themeName)!;
		var rc = ColorRoleResolver.Resolve(role, theme, outline: true, state: ColorRoleState.Normal);
		var surfaceBg = theme.WindowBackgroundColor;
		// Outline: text + border are the role colour on the surface — must be readable on it.
		Assert.True(Math.Abs(rc.Text.Luminance() - surfaceBg.Luminance()) >= 60,
			$"{themeName}/{role} outline: Text not readable on surface");
		Assert.True(Math.Abs(rc.Border.Luminance() - surfaceBg.Luminance()) >= 60,
			$"{themeName}/{role} outline: Border not readable on surface");
	}
}
