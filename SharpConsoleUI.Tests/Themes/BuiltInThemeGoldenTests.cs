// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Reflection;
using SharpConsoleUI;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

/// <summary>
/// Backbone backward-compat test: the built-in ModernGray theme must stay byte-for-byte identical.
/// Each member's value captured BEFORE the theme-hierarchy refactor is the golden reference.
/// When new ITheme members are added later, their pinned ModernGray value is appended here.
/// </summary>
public class BuiltInThemeGoldenTests
{
	private static readonly Dictionary<string, string> Golden = new()
	{
		{ "ModernGray.ActiveBorderForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.ActiveTitleForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.BarGraphBackgroundColor", "" },
		{ "ModernGray.BottomBarBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.BottomBarForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.ButtonBackgroundColor", "Color(48, 48, 48)" },
		{ "ModernGray.ButtonDisabledBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.ButtonDisabledForegroundColor", "Color(128, 128, 128)" },
		{ "ModernGray.ButtonFocusedBackgroundColor", "Color(68, 68, 68)" },
		{ "ModernGray.ButtonFocusedForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.ButtonForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.ButtonSelectedBackgroundColor", "Color(58, 58, 58)" },
		{ "ModernGray.ButtonSelectedForegroundColor", "Color(255, 255, 255)" },
		{ "ModernGray.CheckboxBackgroundColor", "" },
		{ "ModernGray.CheckboxDisabledBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.CheckboxFocusedBackgroundColor", "Color(48, 48, 48)" },
		{ "ModernGray.CollapsibleHeaderFocusedBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.CollapsibleHeaderFocusedForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.DatePickerBackgroundColor", "" },
		{ "ModernGray.DatePickerCalendarHeaderColor", "Color(0, 255, 255)" },
		{ "ModernGray.DatePickerCalendarSelectedColor", "Color(88, 88, 88)" },
		{ "ModernGray.DatePickerCalendarTodayColor", "Color(0, 255, 255)" },
		{ "ModernGray.DatePickerDisabledForegroundColor", "Color(128, 128, 128)" },
		{ "ModernGray.DatePickerFocusedBackgroundColor", "Color(68, 68, 68)" },
		{ "ModernGray.DatePickerFocusedForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.DatePickerForegroundColor", "" },
		{ "ModernGray.DatePickerSegmentBackgroundColor", "Color(88, 88, 88)" },
		{ "ModernGray.DatePickerSegmentForegroundColor", "Color(255, 255, 255)" },
		{ "ModernGray.Description", "Professional dark theme with grayscale foundation and cyan accents, inspired by modern developer tools" },
		{ "ModernGray.DesktopBackgroundChar", " " },
		{ "ModernGray.DesktopBackgroundColor", "Color(28, 28, 28)" },
		{ "ModernGray.DesktopBackgroundGradient", "" },
		{ "ModernGray.DesktopForegroundColor", "Color(128, 128, 128)" },
		{ "ModernGray.DropdownBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.DropdownForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.DropdownHighlightBackgroundColor", "Color(88, 88, 88)" },
		{ "ModernGray.DropdownHighlightForegroundColor", "Color(255, 255, 255)" },
		{ "ModernGray.InactiveBorderForegroundColor", "Color(128, 128, 128)" },
		{ "ModernGray.InactiveTitleForegroundColor", "Color(178, 178, 178)" },
		{ "ModernGray.LineGraphBackgroundColor", "" },
		{ "ModernGray.ListBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.ListHoverBackgroundColor", "Color(68, 68, 68)" },
		{ "ModernGray.ListHoverForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.ListUnfocusedHighlightBackgroundColor", "Color(88, 88, 88)" },
		{ "ModernGray.ListUnfocusedHighlightForegroundColor", "Color(255, 255, 255)" },
		{ "ModernGray.MenuBarBackgroundColor", "" },
		{ "ModernGray.MenuBarForegroundColor", "" },
		{ "ModernGray.MenuBarHighlightBackgroundColor", "Color(68, 68, 68)" },
		{ "ModernGray.MenuBarHighlightForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.MenuDropdownBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.MenuDropdownForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.MenuDropdownHighlightBackgroundColor", "Color(88, 88, 88)" },
		{ "ModernGray.MenuDropdownHighlightForegroundColor", "Color(255, 255, 255)" },
		{ "ModernGray.ModalBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.ModalBorderForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.ModalFlashColor", "Color(88, 88, 88)" },
		{ "ModernGray.ModalTitleForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.Mode", "Dark" },
		{ "ModernGray.Name", "ModernGray" },
		{ "ModernGray.NotificationDangerWindowBackgroundColor", "Color(128, 0, 0)" },
		{ "ModernGray.NotificationInfoWindowBackgroundColor", "Color(95, 135, 175)" },
		{ "ModernGray.NotificationSuccessWindowBackgroundColor", "Color(0, 128, 0)" },
		{ "ModernGray.NotificationWarningWindowBackgroundColor", "Color(215, 135, 0)" },
		{ "ModernGray.NotificationWindowBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.ProgressBarFilledColor", "Color(0, 255, 255)" },
		{ "ModernGray.ProgressBarPercentageColor", "Color(238, 238, 238)" },
		{ "ModernGray.ProgressBarUnfilledColor", "Color(58, 58, 58)" },
		{ "ModernGray.PromptInputBackgroundColor", "Color(48, 48, 48)" },
		{ "ModernGray.PromptInputFocusedBackgroundColor", "Color(68, 68, 68)" },
		{ "ModernGray.PromptInputFocusedForegroundColor", "Color(255, 255, 255)" },
		{ "ModernGray.PromptInputForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.ScrollbarThumbColor", "Color(0, 255, 255)" },
		{ "ModernGray.ScrollbarThumbUnfocusedColor", "Color(128, 128, 128)" },
		{ "ModernGray.ScrollbarTrackColor", "Color(58, 58, 58)" },
		{ "ModernGray.ScrollbarTrackUnfocusedColor", "Color(58, 58, 58)" },
		{ "ModernGray.SeparatorForegroundColor", "Color(58, 58, 58)" },
		{ "ModernGray.ShowModalShadow", "True" },
		{ "ModernGray.SliderFilledTrackColor", "Color(0, 255, 255)" },
		{ "ModernGray.SliderFocusedThumbColor", "Color(255, 255, 0)" },
		{ "ModernGray.SliderThumbColor", "Color(238, 238, 238)" },
		{ "ModernGray.SliderTrackColor", "Color(58, 58, 58)" },
		{ "ModernGray.SparklineBackgroundColor", "" },
		{ "ModernGray.StartMenuHeaderBackgroundColor", "" },
		{ "ModernGray.StartMenuHeaderForegroundColor", "" },
		{ "ModernGray.StartMenuInfoStripForegroundColor", "Color(128, 128, 128)" },
		{ "ModernGray.StartMenuSectionHeaderBackgroundColor", "Color(48, 48, 48)" },
		{ "ModernGray.StatusBarBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.StatusBarForegroundColor", "Color(178, 178, 178)" },
		{ "ModernGray.StatusBarShortcutForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.TabContentBackgroundColor", "" },
		{ "ModernGray.TabContentBorderColor", "Color(0, 255, 255)" },
		{ "ModernGray.TabHeaderActiveBackgroundColor", "Color(68, 68, 68)" },
		{ "ModernGray.TabHeaderActiveFocusedBackgroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.TabHeaderActiveFocusedForegroundColor", "Color(0, 0, 0)" },
		{ "ModernGray.TabHeaderActiveForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.TabHeaderBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.TabHeaderDisabledBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.TabHeaderDisabledForegroundColor", "Color(88, 88, 88)" },
		{ "ModernGray.TabHeaderFocusedBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.TabHeaderFocusedForegroundColor", "Color(218, 218, 218)" },
		{ "ModernGray.TabHeaderForegroundColor", "Color(178, 178, 178)" },
		{ "ModernGray.TableBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.TableBorderColor", "Color(0, 255, 255)" },
		{ "ModernGray.TableForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.TableHeaderBackgroundColor", "Color(48, 48, 48)" },
		{ "ModernGray.TableHeaderForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.TableHoverBackgroundColor", "Color(68, 68, 68)" },
		{ "ModernGray.TableHoverForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.TableScrollbarThumbColor", "Color(0, 255, 255)" },
		{ "ModernGray.TableScrollbarTrackColor", "Color(58, 58, 58)" },
		{ "ModernGray.TableSelectionBackgroundColor", "Color(88, 88, 88)" },
		{ "ModernGray.TableSelectionForegroundColor", "Color(255, 255, 255)" },
		{ "ModernGray.TableUnfocusedSelectionBackgroundColor", "Color(58, 58, 58)" },
		{ "ModernGray.TableUnfocusedSelectionForegroundColor", "Color(178, 178, 178)" },
		{ "ModernGray.TextEditFocusedNotEditing", "Color(58, 58, 58)" },
		{ "ModernGray.TimePickerBackgroundColor", "" },
		{ "ModernGray.TimePickerDisabledForegroundColor", "Color(128, 128, 128)" },
		{ "ModernGray.TimePickerFocusedBackgroundColor", "Color(68, 68, 68)" },
		{ "ModernGray.TimePickerFocusedForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.TimePickerForegroundColor", "" },
		{ "ModernGray.TimePickerSegmentBackgroundColor", "Color(88, 88, 88)" },
		{ "ModernGray.TimePickerSegmentForegroundColor", "Color(255, 255, 255)" },
		{ "ModernGray.ToolbarBackgroundColor", "" },
		{ "ModernGray.ToolbarForegroundColor", "" },
		{ "ModernGray.TopBarBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.TopBarForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.TreeBackgroundColor", "" },
		{ "ModernGray.TreeSelectionBackgroundColor", "Color(58, 58, 58)" },
		{ "ModernGray.TreeUnfocusedSelectionBackgroundColor", "Color(48, 48, 48)" },
		{ "ModernGray.UseDoubleLineBorderForModal", "True" },
		{ "ModernGray.WindowBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.WindowForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.DropdownFocusedForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.DropdownFocusedBackgroundColor", "Color(68, 68, 68)" },
		{ "ModernGray.DropdownDisabledForegroundColor", "Color(128, 128, 128)" },
		{ "ModernGray.DropdownDisabledBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.ListForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.ListFocusedForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.ListSelectedForegroundColor", "Color(255, 255, 255)" },
		{ "ModernGray.ListSelectedBackgroundColor", "Color(58, 58, 58)" },
		{ "ModernGray.ListDisabledForegroundColor", "Color(128, 128, 128)" },
		{ "ModernGray.ListDisabledBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.CheckboxForegroundColor", "Color(238, 238, 238)" },
		{ "ModernGray.CheckboxFocusedForegroundColor", "Color(0, 255, 255)" },
		{ "ModernGray.CheckboxDisabledForegroundColor", "Color(128, 128, 128)" },
		{ "ModernGray.CheckboxCheckmarkColor", "Color(0, 255, 255)" },
		{ "ModernGray.DatePickerDisabledBackgroundColor", "Color(38, 38, 38)" },
		{ "ModernGray.HtmlForegroundColor", "Color(238, 238, 238)" },
	};

	[Theory]
	[InlineData("ModernGray")]
	public void BuiltInTheme_MembersUnchanged(string themeName)
	{
		ITheme theme = new ModernGrayTheme();
		var mismatches = new List<string>();
		foreach (var p in typeof(ITheme).GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			var actual = $"{p.GetValue(theme)}";
			var key = $"{themeName}.{p.Name}";
			if (!Golden.TryGetValue(key, out var expected))
				mismatches.Add($"{key}: NEW member not in golden set (value {actual})");
			else if (actual != expected)
				mismatches.Add($"{key}: expected {expected}, got {actual}");
		}
		Assert.True(mismatches.Count == 0, "built-in theme drifted from golden values:\n" + string.Join("\n", mismatches));
	}
}
