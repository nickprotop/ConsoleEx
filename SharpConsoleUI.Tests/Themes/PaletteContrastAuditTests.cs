// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using SharpConsoleUI;
using SharpConsoleUI.Core;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

/// <summary>
/// Safety net: every palette-generated catalog theme must have readable text — each foreground has a
/// sufficient luminance gap against the EFFECTIVE surface it composites onto (its own background if
/// opaque, else the window background). Guards against the "invisible control text" class of bug.
/// </summary>
public class PaletteContrastAuditTests
{
	// (foregroundMember, backgroundMember). Background null-string => uses window background.
	private static readonly (string fg, string? bg)[] Pairs =
	{
		("WindowForegroundColor", "WindowBackgroundColor"),
		("ButtonForegroundColor", "ButtonBackgroundColor"),
		("ButtonFocusedForegroundColor", "ButtonFocusedBackgroundColor"),
		("ButtonDisabledForegroundColor", "ButtonDisabledBackgroundColor"),
		("ButtonSelectedForegroundColor", "ButtonSelectedBackgroundColor"),
		("DropdownForegroundColor", "DropdownBackgroundColor"),
		("DropdownFocusedForegroundColor", "DropdownFocusedBackgroundColor"),
		("DropdownDisabledForegroundColor", "DropdownDisabledBackgroundColor"),
		("DropdownHighlightForegroundColor", "DropdownHighlightBackgroundColor"),
		("ListForegroundColor", null),
		("ListSelectedForegroundColor", "ListSelectedBackgroundColor"),
		("ListDisabledForegroundColor", "ListDisabledBackgroundColor"),
		("ListUnfocusedHighlightForegroundColor", "ListUnfocusedHighlightBackgroundColor"),
		("CheckboxForegroundColor", null),
		("TableForegroundColor", "TableBackgroundColor"),
		("TableHeaderForegroundColor", "TableHeaderBackgroundColor"),
		("TableSelectionForegroundColor", "TableSelectionBackgroundColor"),
		("TabHeaderForegroundColor", "TabHeaderBackgroundColor"),
		("TabHeaderActiveForegroundColor", "TabHeaderActiveBackgroundColor"),
		("PromptInputForegroundColor", "PromptInputBackgroundColor"),
		("PromptInputFocusedForegroundColor", "PromptInputFocusedBackgroundColor"),
		("MenuDropdownForegroundColor", "MenuDropdownBackgroundColor"),
		("MenuDropdownHighlightForegroundColor", "MenuDropdownHighlightBackgroundColor"),
	};

	private static Color Get(ITheme t, string member)
	{
		var p = typeof(ITheme).GetProperty(member)!;
		var v = p.GetValue(t);
		if (v is Color c) return c;
		// A Color? property with no value boxes to null.
		if (v is null) return Color.Transparent;
		throw new InvalidOperationException(member);
	}

	[Theory]
	[InlineData("Ocean")]
	[InlineData("Amber")]
	[InlineData("Forest")]
	[InlineData("Crimson")]
	[InlineData("Slate")]
	[InlineData("Daylight")]
	public void CatalogTheme_AllForegroundsReadable(string themeName)
	{
		var reg = new ThemeRegistryStateService();
		var theme = reg.GetTheme(themeName)!;
		var windowBg = theme.WindowBackgroundColor;
		var bad = new List<string>();

		foreach (var (fgName, bgName) in Pairs)
		{
			var fg = Get(theme, fgName);
			Color? bg = bgName == null ? null : Get(theme, bgName);
			var surface = PaletteColors.EffectiveSurface(bg, windowBg);
			var gap = Math.Abs(fg.Luminance() - surface.Luminance());
			if (gap < 60)
				bad.Add($"{fgName} on {(bgName ?? "WindowBackground")} (effective surface {surface}): gap {gap:0} < 60");
		}

		Assert.True(bad.Count == 0, $"{themeName} has unreadable text:\n" + string.Join("\n", bad));
	}
}
