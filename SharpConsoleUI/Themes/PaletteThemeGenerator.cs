// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
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
			// DERIVED FROM THE PRIMARY, not a fixed cyan. Success, warning and danger keep their
			// literals because a fixed hue IS their meaning — red reads as danger whatever the
			// palette, and tinting it toward the accent weakens the signal it exists to carry.
			// Info is not that: it marks a thing as a DIFFERENT KIND rather than as a hazard, and a
			// fixed #0dcaf0 lands as an unthemed intrusion — cyan inside a crimson theme, cyan on a
			// light background — on every seed theme, none of which supplies a Palette.Info.
			//
			// TINTED RATHER THAN SHADED, so it separates from secondary and tertiary, which shade
			// the same seed in steps. Info sits on the other side of the accent from them.
			Color info = palette.Info ?? primary.Tint(0.30);

			// "Raised" surfaces lighten in dark mode / darken in light mode; "Recessed" the inverse.
			Color Raise(Color c, double amt) => mode == ThemeMode.Dark ? c.Tint(amt) : c.Shade(amt);
			Color Recess(Color c, double amt) => mode == ThemeMode.Dark ? c.Shade(amt) : c.Tint(amt);

			// Common derived neutrals.
			Color raised = Raise(background, 0.12);             // bars, modal, dropdown, table header, tab header
			Color recessed = Recess(background, 0.25);          // desktop / sunken surfaces
			Color dimText = foreground.Mix(background, 0.35);   // inactive / disabled / dim labels
			Color faintText = foreground.Mix(background, 0.55); // very dim labels / separators
			Color border = foreground.Mix(background, 0.5);     // generic borders / separators

			// Grooves an accent is painted INTO: scrollbar and slider tracks, the progress bar's
			// unfilled remainder, a checkbox's well. Named rather than repeated inline because the
			// accents that sit on them have to be contrast-checked against THESE, not against the
			// window background, and a copy of `Raise(background, 0.1)` at each use site is exactly
			// how that check came to be done against the wrong surface.
			Color trackSurface = Raise(background, 0.1);
			Color unfocusedTrackSurface = Raise(background, 0.06);
			// Only the DISABLED checkbox fill now; the resting state has none (see below).
			Color checkboxSurface = Raise(background, 0.2);

			// The same accent, but kept readable against the surfaces it is actually painted on when
			// those are not the window background. An accent is only ever as visible as its own
			// backdrop allows, and these backdrops are raised above the window.
			Color accentOnTrack = PaletteColors.EnsureContrastRatio(primary, trackSurface);
			Color accentOnRaised = PaletteColors.EnsureContrastRatio(primary, raised);

			// Accent-derived selection surfaces.
			Color selectionBg = primary.Mix(background, 0.6);
			Color selectionFg = PaletteColors.ReadableOn(selectionBg);
			Color primaryFocused = primary.Tint(0.2);

			// Disabled states are damped with ALPHA rather than shading. Because the renderer is a real
			// compositor, a semi-transparent disabled color blends with the surface behind the control,
			// giving a natural "greyed-out" look that tracks whatever is underneath (idea from
			// discussion #44). Dim(c) reduces a color's opacity to DisabledStateAlpha.
			Color Dim(Color c) => c.WithAlpha(ControlDefaults.DisabledStateAlpha);

			// Disabled TEXT, which has to recede and still be read: a user has to see what a control
			// says to know what is unavailable to them. A flat alpha cannot promise that, because
			// what lands on screen is the blend and how legible it is depends entirely on the
			// surface. On Daylight every disabled label composited to 2.87:1, and a tab header - dim
			// text damped a second time - reached 1.90:1. So: blend exactly as the compositor will,
			// check THAT, and walk the alpha back up until the painted result clears the floor.
			Color DimText(Color c, Color surface)
			{
				for (int alpha = ControlDefaults.DisabledStateAlpha; alpha <= 255; alpha += 10)
				{
					Color painted = surface.Mix(c, alpha / 255.0);
					if (PaletteColors.ContrastRatio(painted, surface) >= PaletteColors.GraphicContrast)
						return c.WithAlpha((byte)alpha);
				}

				// Fully opaque still fails: the colour itself is too close to the surface, so move
				// the colour rather than its opacity.
				return PaletteColors.EnsureContrastRatio(c, surface);
			}

			var t = new MutableTheme { Mode = mode };

			// ===== ROLE SEED family (surface the resolved accents/status colors) =====
			t.PrimaryColor = primary;
			t.SecondaryColor = secondary;
			t.TertiaryColor = tertiary;
			t.InfoColor = info;
			t.SuccessColor = success;
			t.WarningColor = warning;
			t.DangerColor = danger;

			// ===== NEUTRAL family (from background / foreground) =====
			t.WindowBackgroundColor = background;
			t.WindowForegroundColor = foreground;
			t.InactiveBorderForegroundColor = border;
			t.InactiveTitleForegroundColor = dimText;

			t.TopBarBackgroundColor = raised;
			t.TopBarForegroundColor = foreground;
			t.BottomBarBackgroundColor = raised;
			t.BottomBarForegroundColor = foreground;

			// The desktop pattern is faint ON PURPOSE, but faintText is mixed against the WINDOW
			// background while it is drawn on the RECESSED desktop, so the intended fade became a
			// real-contrast failure on every seed theme (3.06:1 to 4.32:1). Keep it the faintest
			// thing on screen, but hold the graphical floor against the surface it is drawn on.
			t.DesktopBackgroundColor = recessed;
			t.DesktopForegroundColor = PaletteColors.EnsureContrastRatio(faintText, recessed);

			t.ModalBackgroundColor = raised;
			// A modal's border frames the MODAL surface, which is raised above the window.
			t.ModalBorderForegroundColor = accentOnRaised;
			t.ModalTitleForegroundColor = foreground;

			t.PromptInputBackgroundColor = Raise(background, 0.2);
			t.PromptInputForegroundColor = foreground;

			t.DropdownBackgroundColor = raised;
			t.DropdownForegroundColor = foreground;
			t.DropdownDisabledForegroundColor = DimText(foreground, raised);
			t.DropdownDisabledBackgroundColor = Dim(raised);

			t.ListForegroundColor = foreground;
			t.ListDisabledForegroundColor = DimText(foreground, background);
			t.ListDisabledBackgroundColor = Dim(background);

			t.CheckboxForegroundColor = foreground;
			t.CheckboxDisabledForegroundColor = DimText(foreground, background);

			t.DatePickerDisabledBackgroundColor = Dim(raised);

			t.HtmlForegroundColor = foreground;

			t.MenuDropdownBackgroundColor = raised;
			t.MenuDropdownForegroundColor = foreground;

			// Tab header is chrome (like bars/dropdown/table header): use the RAISED surface, not the
			// recessed one. On a light theme "recessed" tints lighter than the window, which washes the
			// inactive tabs out; "raised" keeps the header strip a visible, consistent chrome surface.
			t.TabHeaderBackgroundColor = raised;
			// An inactive tab's label is dimmed, but it is still a label a user has to read, and it
			// sits on the RAISED header rather than on the window dimText was mixed against.
			t.TabHeaderForegroundColor =
				PaletteColors.EnsureContrastRatio(dimText, raised, PaletteColors.TextContrast);
			t.TabHeaderActiveBackgroundColor = background;
			t.TabHeaderActiveForegroundColor = foreground;
			t.TabHeaderDisabledBackgroundColor = Dim(raised);
			t.TabHeaderDisabledForegroundColor = DimText(dimText, raised);

			t.TableBackgroundColor = background;
			t.TableForegroundColor = foreground;
			t.TableHeaderBackgroundColor = raised;
			t.TableHeaderForegroundColor = foreground;
			t.TableScrollbarTrackColor = trackSurface;

			t.ScrollbarTrackColor = trackSurface;
			t.ScrollbarTrackUnfocusedColor = unfocusedTrackSurface;

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
			t.DatePickerDisabledForegroundColor = DimText(foreground, raised);
			t.DatePickerCalendarHeaderColor = dimText;

			t.TimePickerBackgroundColor = raised;
			t.TimePickerForegroundColor = foreground;
			t.TimePickerSegmentBackgroundColor = Raise(background, 0.2);
			t.TimePickerSegmentForegroundColor = foreground;
			t.TimePickerDisabledForegroundColor = DimText(foreground, raised);

			t.StatusBarBackgroundColor = raised;
			t.StatusBarForegroundColor = foreground;

			t.SliderTrackColor = trackSurface;

			// NOT a fill. A checkbox and a radio are surface-text controls: a marker glyph and a
			// label drawn onto whatever is behind them, exactly like a label. Assigning a raised
			// surface here put every checkbox and radio in a palette theme inside a visible grey
			// slab that no other text control has, which reads as a selection highlight stuck on.
			// ITheme documents this member as "Null means transparent" and ModernGray leaves it
			// null; the generator was the only piece that disagreed. The FOCUSED and DISABLED
			// states below keep their fills, because those are momentary feedback a user reads as
			// "this control, right now" rather than as the control's resting appearance.
			t.CheckboxBackgroundColor = null;
			t.CheckboxDisabledBackgroundColor = Dim(checkboxSurface);

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
			// The active window's title is text, so it takes the text floor; the border beside it is a
			// graphical edge and keeps the lower one. EnsureContrast only guarantees a raw-channel gap,
			// which left the title at 3.75:1 on Slate and 4.01:1 on Daylight.
			t.ActiveTitleForegroundColor =
				PaletteColors.EnsureContrastRatio(accentOnBackground, background, PaletteColors.TextContrast);

			// Button labels are measured against their own fill. ContrastOn/ReadableOn choose
			// near-white or near-black from a raw-luminance midpoint, which is near a coin flip on
			// the mid-tone fills a themed button actually uses, and picked the losing side often
			// enough to ship unreadable labels (Crimson 3.30:1, Slate 4.18:1).
			t.ButtonBackgroundColor = primary;
			t.ButtonForegroundColor = PaletteColors.ReadableOnMeasured(primary);
			t.ButtonFocusedBackgroundColor = primaryFocused;
			t.ButtonFocusedForegroundColor = PaletteColors.ReadableOnMeasured(primaryFocused);
			t.ButtonSelectedBackgroundColor = secondary;
			t.ButtonSelectedForegroundColor = PaletteColors.ReadableOnMeasured(secondary);
			// The disabled bg is alpha-translucent, so the window background shows through it. The
			// disabled text must therefore read against the WINDOW background (not against the opaque
			// primary it would sit on when enabled) — so damp the window foreground, which is already
			// contrast-guaranteed against the surface.
			// The disabled fill is itself alpha-damped, so what the label actually lands on is
			// primary blended over the window, not primary. Measuring against the stored colour
			// rather than the composited one is how this pair stayed broken.
			t.ButtonDisabledBackgroundColor = Dim(primary);
			Color disabledButtonSurface = background.Mix(primary, ControlDefaults.DisabledStateAlpha / 255.0);
			t.ButtonDisabledForegroundColor = DimText(foreground, disabledButtonSurface);

			t.ListUnfocusedHighlightBackgroundColor = selectionBg.Mix(background, 0.35);
			t.ListUnfocusedHighlightForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.35));
			t.CollapsibleHeaderFocusedBackgroundColor = selectionBg;
			t.CollapsibleHeaderFocusedForegroundColor = selectionFg;

			t.PromptInputFocusedBackgroundColor = selectionBg.Mix(background, 0.4);
			t.PromptInputFocusedForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.4));
			t.TextEditFocusedNotEditing = primaryFocused;

			t.DropdownHighlightBackgroundColor = selectionBg;
			t.DropdownHighlightForegroundColor = selectionFg;
			// The focused row's label is TEXT on the blended focus surface below it, not on the
			// window: accentOnBackground is guarded against the wrong thing here and left Crimson
			// at 3.74:1 and Daylight at 2.92:1. Every neighbouring highlight pair already derives
			// its foreground from its own surface; this one did not.
			t.DropdownFocusedBackgroundColor = selectionBg.Mix(background, 0.4);
			t.DropdownFocusedForegroundColor = PaletteColors.EnsureContrastRatio(
				accentOnBackground, t.DropdownFocusedBackgroundColor!.Value, PaletteColors.TextContrast);

			t.ListFocusedForegroundColor = accentOnBackground;
			t.ListSelectedForegroundColor = selectionFg;
			t.ListSelectedBackgroundColor = selectionBg;

			t.CheckboxFocusedForegroundColor = accentOnBackground;
			// The checkmark is drawn on the WINDOW surface, because a checkbox has no resting fill
			// (see CheckboxBackgroundColor above). accentOnBackground is already guarded against
			// exactly that surface.
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
			// Scrollbar thumbs sit on a RAISED track, not on the window background, so they are
			// checked against the track. Guarding them against the background instead let a thumb
			// clear the window and still vanish into the groove it is actually drawn in.
			t.TableScrollbarThumbColor = accentOnTrack;

			t.ScrollbarThumbColor = accentOnTrack;
			// The unfocused thumb is deliberately damped, but damping it by a fixed .Shade(0.4) drove
			// it UNDER its own track on every seed theme (1.60:1 to 2.97:1). Damp, then restore the
			// graphical-object floor against the track it sits on, so "quieter" never becomes "gone".
			t.ScrollbarThumbUnfocusedColor = PaletteColors.EnsureContrastRatio(
				accentOnTrack.Shade(0.4), unfocusedTrackSurface);

			// Nullable accents.
			t.ListHoverBackgroundColor = selectionBg.Mix(background, 0.6);
			t.ListHoverForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.6));

			t.DatePickerFocusedBackgroundColor = selectionBg.Mix(background, 0.4);
			t.DatePickerFocusedForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.4));
			t.DatePickerCalendarTodayColor = info;
			t.DatePickerCalendarSelectedColor = primary;

			t.TimePickerFocusedBackgroundColor = selectionBg.Mix(background, 0.4);
			t.TimePickerFocusedForegroundColor = PaletteColors.ReadableOn(selectionBg.Mix(background, 0.4));

			// Shortcut keys are TEXT on the raised status bar, so they take the text floor, not the
			// graphical one, and are measured against the bar rather than the window behind it.
			t.StatusBarShortcutForegroundColor =
				PaletteColors.EnsureContrastRatio(primary, raised, PaletteColors.TextContrast);

			// Filled portion, thumb and focused thumb all sit in the slider groove.
			t.SliderFilledTrackColor = accentOnTrack;
			t.SliderThumbColor = accentOnTrack;
			t.SliderFocusedThumbColor = PaletteColors.EnsureContrastRatio(primaryFocused, trackSurface);

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

			// A progress bar is read as the boundary between filled and unfilled, so the fill is
			// checked against the remainder it has to be told apart from.
			t.ProgressBarFilledColor = accentOnTrack;
			t.ProgressBarUnfilledColor = trackSurface;
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
