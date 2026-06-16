// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;

namespace SharpConsoleUI.Themes
{
	/// <summary>
	/// Generates a complete <see cref="MutableTheme"/> from a small <see cref="Palette"/> of seed
	/// colors, deriving every theme color via tint/shade/mix. Hand-written, reflection-free (AOT-safe).
	/// The assignment list below documents where each theme color comes from.
	/// </summary>
	internal static class PaletteThemeGenerator
	{
		public static MutableTheme Generate(Palette palette)
		{
			if (palette == null) throw new System.ArgumentNullException(nameof(palette));

			ThemeMode mode = palette.Mode
				?? (palette.Background is { } bgGiven ? (bgGiven.IsDark() ? ThemeMode.Dark : ThemeMode.Light) : ThemeMode.Dark);

			Color background = palette.Background ?? (mode == ThemeMode.Dark ? new Color(38, 38, 38) : new Color(245, 245, 245));
			// Honor a user-supplied foreground, but guarantee it stays readable against the background
			// (a low-contrast or missing foreground falls back to a readable contrast color).
			Color foreground = PaletteColors.EnsureContrast(palette.Foreground ?? PaletteColors.ContrastOn(background), background);
			Color primary = palette.Primary ?? (mode == ThemeMode.Dark ? new Color(0, 255, 255) : new Color(13, 110, 253));
			Color secondary = palette.Secondary ?? primary.Shade(0.25);
			Color tertiary = palette.Tertiary ?? secondary.Shade(0.25);
			Color success = palette.Success ?? new Color(40, 167, 69);
			Color warning = palette.Warning ?? new Color(255, 193, 7);
			Color danger = palette.Danger ?? new Color(220, 53, 69);
			Color info = palette.Info ?? new Color(13, 202, 240);

			// "Raised" surfaces lighten in dark mode / darken in light mode; "Recessed" the inverse.
			Color Raise(Color c, double amt) => mode == ThemeMode.Dark ? c.Tint(amt) : c.Shade(amt);
			Color Recess(Color c, double amt) => mode == ThemeMode.Dark ? c.Shade(amt) : c.Tint(amt);

			// Common derived neutrals.
			Color raised = Raise(background, 0.12);             // bars, modal, dropdown, table header, tab header
			Color recessed = Recess(background, 0.25);          // desktop / sunken surfaces
			Color dimText = foreground.Mix(background, 0.35);   // inactive / disabled / dim labels
			Color faintText = foreground.Mix(background, 0.55); // very dim labels / separators
			Color border = foreground.Mix(background, 0.5);     // generic borders / separators

			// Accent-derived selection surfaces.
			Color selectionBg = primary.Mix(background, 0.6);
			Color selectionFg = PaletteColors.ReadableOn(selectionBg);
			Color primaryDisabled = primary.Shade(0.5);
			Color primaryFocused = primary.Tint(0.2);

			var t = new MutableTheme { Mode = mode };

			// ===== NEUTRAL family (from background / foreground) =====
			t.WindowBackgroundColor = background;
			t.WindowForegroundColor = foreground;
			t.InactiveBorderForegroundColor = border;
			t.InactiveTitleForegroundColor = dimText;

			t.TopBarBackgroundColor = raised;
			t.TopBarForegroundColor = foreground;
			t.BottomBarBackgroundColor = raised;
			t.BottomBarForegroundColor = foreground;

			t.DesktopBackgroundColor = recessed;
			t.DesktopForegroundColor = faintText;

			t.ModalBackgroundColor = raised;
			t.ModalBorderForegroundColor = primary;
			t.ModalTitleForegroundColor = foreground;

			t.PromptInputBackgroundColor = Raise(background, 0.2);
			t.PromptInputForegroundColor = foreground;

			t.DropdownBackgroundColor = raised;
			t.DropdownForegroundColor = foreground;
			t.DropdownDisabledForegroundColor = faintText;
			t.DropdownDisabledBackgroundColor = recessed;

			t.ListForegroundColor = foreground;
			t.ListDisabledForegroundColor = faintText;
			t.ListDisabledBackgroundColor = recessed;

			t.CheckboxForegroundColor = foreground;
			t.CheckboxDisabledForegroundColor = faintText;

			t.DatePickerDisabledBackgroundColor = recessed;

			t.HtmlForegroundColor = foreground;

			t.MenuDropdownBackgroundColor = raised;
			t.MenuDropdownForegroundColor = foreground;

			t.TabHeaderBackgroundColor = recessed;
			t.TabHeaderForegroundColor = dimText;
			t.TabHeaderActiveBackgroundColor = background;
			t.TabHeaderActiveForegroundColor = foreground;
			t.TabHeaderDisabledBackgroundColor = recessed;
			t.TabHeaderDisabledForegroundColor = faintText;

			t.TableBackgroundColor = background;
			t.TableForegroundColor = foreground;
			t.TableHeaderBackgroundColor = raised;
			t.TableHeaderForegroundColor = foreground;
			t.TableScrollbarTrackColor = Raise(background, 0.1);

			t.ScrollbarTrackColor = Raise(background, 0.1);
			t.ScrollbarTrackUnfocusedColor = Raise(background, 0.06);

			// Nullable neutrals.
			t.ListBackgroundColor = background;
			t.ToolbarBackgroundColor = raised;
			t.ToolbarForegroundColor = foreground;
			t.SeparatorForegroundColor = border;
			t.MenuBarBackgroundColor = raised;
			t.MenuBarForegroundColor = foreground;
			t.TableBorderColor = border;
			t.TabContentBorderColor = border;
			t.TabContentBackgroundColor = background;

			t.DatePickerBackgroundColor = raised;
			t.DatePickerForegroundColor = foreground;
			t.DatePickerSegmentBackgroundColor = Raise(background, 0.2);
			t.DatePickerSegmentForegroundColor = foreground;
			t.DatePickerDisabledForegroundColor = faintText;
			t.DatePickerCalendarHeaderColor = dimText;

			t.TimePickerBackgroundColor = raised;
			t.TimePickerForegroundColor = foreground;
			t.TimePickerSegmentBackgroundColor = Raise(background, 0.2);
			t.TimePickerSegmentForegroundColor = foreground;
			t.TimePickerDisabledForegroundColor = faintText;

			t.StatusBarBackgroundColor = raised;
			t.StatusBarForegroundColor = foreground;

			t.SliderTrackColor = Raise(background, 0.1);

			t.CheckboxBackgroundColor = Raise(background, 0.2);
			t.CheckboxDisabledBackgroundColor = recessed;

			t.TreeBackgroundColor = background;

			t.LineGraphBackgroundColor = background;
			t.BarGraphBackgroundColor = background;
			t.SparklineBackgroundColor = background;

			t.StartMenuHeaderForegroundColor = foreground;
			t.StartMenuSectionHeaderBackgroundColor = raised;
			t.StartMenuInfoStripForegroundColor = dimText;

			// ===== ACCENT family (from primary / secondary / tertiary) =====
			// The active border/title accent sits on the window background; keep it visible even when
			// the primary color is close to the background luminance.
			Color accentOnBackground = PaletteColors.EnsureContrast(primary, background);
			t.ActiveBorderForegroundColor = accentOnBackground;
			t.ActiveTitleForegroundColor = accentOnBackground;

			t.ButtonBackgroundColor = primary;
			t.ButtonForegroundColor = PaletteColors.ContrastOn(primary);
			t.ButtonFocusedBackgroundColor = primaryFocused;
			t.ButtonFocusedForegroundColor = PaletteColors.ContrastOn(primaryFocused);
			t.ButtonSelectedBackgroundColor = secondary;
			t.ButtonSelectedForegroundColor = PaletteColors.ReadableOn(secondary);
			t.ButtonDisabledBackgroundColor = primaryDisabled;
			t.ButtonDisabledForegroundColor = PaletteColors.ReadableOn(primaryDisabled);

			t.ListUnfocusedHighlightBackgroundColor = selectionBg.Mix(background, 0.35);
			t.ListUnfocusedHighlightForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.35));
			t.CollapsibleHeaderFocusedBackgroundColor = selectionBg;
			t.CollapsibleHeaderFocusedForegroundColor = selectionFg;

			t.PromptInputFocusedBackgroundColor = selectionBg.Mix(background, 0.4);
			t.PromptInputFocusedForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.4));
			t.TextEditFocusedNotEditing = primaryFocused;

			t.DropdownHighlightBackgroundColor = selectionBg;
			t.DropdownHighlightForegroundColor = selectionFg;
			t.DropdownFocusedForegroundColor = accentOnBackground;
			t.DropdownFocusedBackgroundColor = selectionBg.Mix(background, 0.4);

			t.ListFocusedForegroundColor = accentOnBackground;
			t.ListSelectedForegroundColor = selectionFg;
			t.ListSelectedBackgroundColor = selectionBg;

			t.CheckboxFocusedForegroundColor = accentOnBackground;
			t.CheckboxCheckmarkColor = accentOnBackground;

			t.MenuBarHighlightBackgroundColor = selectionBg;
			t.MenuBarHighlightForegroundColor = selectionFg;
			t.MenuDropdownHighlightBackgroundColor = selectionBg;
			t.MenuDropdownHighlightForegroundColor = selectionFg;

			t.TabHeaderFocusedBackgroundColor = selectionBg.Mix(background, 0.4);
			t.TabHeaderFocusedForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.4));
			t.TabHeaderActiveFocusedBackgroundColor = primary;
			t.TabHeaderActiveFocusedForegroundColor = PaletteColors.ContrastOn(primary);

			t.TableSelectionBackgroundColor = selectionBg;
			t.TableSelectionForegroundColor = selectionFg;
			t.TableUnfocusedSelectionBackgroundColor = selectionBg.Mix(background, 0.35);
			t.TableUnfocusedSelectionForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.35));
			t.TableHoverBackgroundColor = selectionBg.Mix(background, 0.6);
			t.TableHoverForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.6));
			// Scrollbar thumbs sit on a track derived straight from the window background; keep the
			// accent visible when primary is close to the background luminance.
			t.TableScrollbarThumbColor = accentOnBackground;

			t.ScrollbarThumbColor = accentOnBackground;
			t.ScrollbarThumbUnfocusedColor = accentOnBackground.Shade(0.4);

			// Nullable accents.
			t.ListHoverBackgroundColor = selectionBg.Mix(background, 0.6);
			t.ListHoverForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.6));

			t.DatePickerFocusedBackgroundColor = selectionBg.Mix(background, 0.4);
			t.DatePickerFocusedForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.4));
			t.DatePickerCalendarTodayColor = info;
			t.DatePickerCalendarSelectedColor = primary;

			t.TimePickerFocusedBackgroundColor = selectionBg.Mix(background, 0.4);
			t.TimePickerFocusedForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.4));

			t.StatusBarShortcutForegroundColor = primary;

			t.SliderFilledTrackColor = primary;
			t.SliderThumbColor = primary;
			t.SliderFocusedThumbColor = primaryFocused;

			t.CheckboxFocusedBackgroundColor = selectionBg.Mix(background, 0.4);

			t.TreeSelectionBackgroundColor = selectionBg;
			t.TreeUnfocusedSelectionBackgroundColor = selectionBg.Mix(background, 0.35);

			t.StartMenuHeaderBackgroundColor = primary;

			// ===== STATUS family (from success / warning / danger / info) =====
			t.ModalFlashColor = warning;
			t.NotificationWindowBackgroundColor = raised;
			t.NotificationInfoWindowBackgroundColor = info;
			t.NotificationSuccessWindowBackgroundColor = success;
			t.NotificationWarningWindowBackgroundColor = warning;
			t.NotificationDangerWindowBackgroundColor = danger;

			t.ProgressBarFilledColor = primary;
			t.ProgressBarUnfilledColor = Raise(background, 0.1);
			t.ProgressBarPercentageColor = foreground;

			// ===== NON-COLOR members =====
			t.DesktopBackgroundChar = ' ';
			t.ShowModalShadow = true;
			t.UseDoubleLineBorderForModal = false;
			// DesktopBackgroundGradient stays null (MutableTheme default).

			t.NameValue = "Custom (Palette)";
			t.DescriptionValue = "Theme generated from a color palette.";
			return t;
		}
	}
}
