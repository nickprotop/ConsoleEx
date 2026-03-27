// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides centralized color resolution logic for controls.
	/// Resolution chain: explicit value → theme slot → Color.Transparent.
	/// null and Color.Default are treated identically (both mean "no explicit value").
	/// </summary>
	public static class ColorResolver
	{
		/// <summary>
		/// Returns null if the value is null or Color.Default (IsDefault=true); otherwise returns the value.
		/// Both null and Color.Default mean "no explicit setting" in the resolution chain.
		/// </summary>
		internal static Color? Coalesce(Color? c) =>
			(c == null || c.Value.IsDefault) ? null : c;

		/// <summary>
		/// Resolves a generic background color: explicit value → Color.Transparent.
		/// Controls without a specific theme slot are transparent by default.
		/// The container parameter is accepted for API consistency but not used —
		/// generic controls have no theme slot to fall back to.
		/// </summary>
		public static Color ResolveBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Color.Transparent;

		/// <summary>
		/// Resolves multiline edit background: explicit → theme PromptInputBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveMultilineEditBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.PromptInputBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves focused multiline edit background: explicit → theme PromptInputFocusedBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveMultilineEditFocusedBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.PromptInputFocusedBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves a foreground color using the standard fallback chain:
		/// explicit value → container foreground → theme window foreground → default.
		/// </summary>
		public static Color ResolveForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.ForegroundColor
				?? container?.GetConsoleWindowSystem?.Theme?.WindowForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves menu bar background: explicit → theme MenuBarBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveMenuBarBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.MenuBarBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves menu bar foreground color.
		/// </summary>
		public static Color ResolveMenuBarForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.MenuBarForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves menu bar highlight background: explicit → theme MenuBarHighlightBackgroundColor → fallback.
		/// </summary>
		public static Color ResolveMenuBarHighlightBackground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.Blue;
			return Coalesce(explicitValue)
				?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.MenuBarHighlightBackgroundColor)
				?? defaultColor;
		}

		/// <summary>
		/// Resolves menu bar highlight foreground color.
		/// </summary>
		public static Color ResolveMenuBarHighlightForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.White;
			return Coalesce(explicitValue)
				?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.MenuBarHighlightForegroundColor)
				?? defaultColor;
		}

		/// <summary>
		/// Resolves dropdown background: explicit → theme MenuDropdownBackgroundColor → fallback.
		/// </summary>
		public static Color ResolveDropdownBackground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.White;
			return Coalesce(explicitValue)
				?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.MenuDropdownBackgroundColor)
				?? defaultColor;
		}

		/// <summary>
		/// Resolves dropdown foreground color.
		/// </summary>
		public static Color ResolveDropdownForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.Black;
			return Coalesce(explicitValue)
				?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.MenuDropdownForegroundColor)
				?? defaultColor;
		}

		/// <summary>
		/// Resolves dropdown highlight background: explicit → theme MenuDropdownHighlightBackgroundColor → fallback.
		/// </summary>
		public static Color ResolveDropdownHighlightBackground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.Blue;
			return Coalesce(explicitValue)
				?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.MenuDropdownHighlightBackgroundColor)
				?? defaultColor;
		}

		/// <summary>
		/// Resolves dropdown highlight foreground color.
		/// </summary>
		public static Color ResolveDropdownHighlightForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.White;
			return Coalesce(explicitValue)
				?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.MenuDropdownHighlightForegroundColor)
				?? defaultColor;
		}

		/// <summary>
		/// Resolves button background: explicit → theme ButtonBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveButtonBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.ButtonBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves button foreground color.
		/// </summary>
		public static Color ResolveButtonForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.ButtonForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves focused button background: explicit → theme ButtonFocusedBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveButtonFocusedBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves focused button foreground color.
		/// </summary>
		public static Color ResolveButtonFocusedForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.ButtonFocusedForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves disabled button background: explicit → theme ButtonDisabledBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveButtonDisabledBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves disabled button foreground color.
		/// </summary>
		public static Color ResolveButtonDisabledForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.ButtonDisabledForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves active tab header background: explicit → theme TabHeaderActiveBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveTabHeaderActiveBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.TabHeaderActiveBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves active tab header foreground color.
		/// </summary>
		public static Color ResolveTabHeaderActiveForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.White;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.TabHeaderActiveForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves inactive tab header background: explicit → theme TabHeaderBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveTabHeaderBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.TabHeaderBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves inactive tab header foreground color.
		/// </summary>
		public static Color ResolveTabHeaderForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.Silver;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.TabHeaderForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves disabled tab header background: explicit → theme TabHeaderDisabledBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveTabHeaderDisabledBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.TabHeaderDisabledBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves disabled tab header foreground color.
		/// </summary>
		public static Color ResolveTabHeaderDisabledForeground(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.Grey;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.TabHeaderDisabledForegroundColor
				?? container?.ForegroundColor
				?? defaultColor;
		}

		/// <summary>
		/// Resolves date picker background: explicit → theme DatePickerBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveDatePickerBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.DatePickerBackgroundColor)
			?? Color.Transparent;

		/// <summary>Resolves date picker foreground color.</summary>
		public static Color ResolveDatePickerForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.White;
			return explicitValue ?? container?.GetConsoleWindowSystem?.Theme?.DatePickerForegroundColor ?? container?.ForegroundColor ?? defaultColor;
		}

		/// <summary>
		/// Resolves date picker focused background: explicit → theme DatePickerFocusedBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveDatePickerFocusedBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.DatePickerFocusedBackgroundColor)
			?? Color.Transparent;

		/// <summary>Resolves date picker focused foreground color.</summary>
		public static Color ResolveDatePickerFocusedForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.White;
			return explicitValue ?? container?.GetConsoleWindowSystem?.Theme?.DatePickerFocusedForegroundColor ?? defaultColor;
		}

		/// <summary>
		/// Resolves date picker segment background: explicit → theme DatePickerSegmentBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveDatePickerSegmentBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.DatePickerSegmentBackgroundColor)
			?? Color.Transparent;

		/// <summary>Resolves date picker segment foreground color.</summary>
		public static Color ResolveDatePickerSegmentForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.White;
			return explicitValue ?? container?.GetConsoleWindowSystem?.Theme?.DatePickerSegmentForegroundColor ?? defaultColor;
		}

		/// <summary>
		/// Resolves time picker background: explicit → theme TimePickerBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveTimePickerBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.TimePickerBackgroundColor)
			?? Color.Transparent;

		/// <summary>Resolves time picker foreground color.</summary>
		public static Color ResolveTimePickerForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.White;
			return explicitValue ?? container?.GetConsoleWindowSystem?.Theme?.TimePickerForegroundColor ?? container?.ForegroundColor ?? defaultColor;
		}

		/// <summary>
		/// Resolves time picker focused background: explicit → theme TimePickerFocusedBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveTimePickerFocusedBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.TimePickerFocusedBackgroundColor)
			?? Color.Transparent;

		/// <summary>Resolves time picker focused foreground color.</summary>
		public static Color ResolveTimePickerFocusedForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.White;
			return explicitValue ?? container?.GetConsoleWindowSystem?.Theme?.TimePickerFocusedForegroundColor ?? defaultColor;
		}

		/// <summary>
		/// Resolves time picker segment background: explicit → theme TimePickerSegmentBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveTimePickerSegmentBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.TimePickerSegmentBackgroundColor)
			?? Color.Transparent;

		/// <summary>Resolves time picker segment foreground color.</summary>
		public static Color ResolveTimePickerSegmentForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.White;
			return explicitValue ?? container?.GetConsoleWindowSystem?.Theme?.TimePickerSegmentForegroundColor ?? defaultColor;
		}

		/// <summary>Resolves time picker disabled foreground color.</summary>
		public static Color ResolveTimePickerDisabledForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.Grey;
			return explicitValue ?? container?.GetConsoleWindowSystem?.Theme?.TimePickerDisabledForegroundColor ?? defaultColor;
		}

		/// <summary>
		/// Resolves status bar background: explicit → theme StatusBarBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveStatusBarBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.StatusBarBackgroundColor)
			?? Color.Transparent;

		/// <summary>Resolves status bar foreground color.</summary>
		public static Color ResolveStatusBarForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.White;
			return explicitValue ?? container?.GetConsoleWindowSystem?.Theme?.StatusBarForegroundColor ?? container?.ForegroundColor ?? defaultColor;
		}

		/// <summary>Resolves status bar shortcut foreground color.</summary>
		public static Color ResolveStatusBarShortcutForeground(Color? explicitValue, IContainer? container, Color defaultColor = default)
		{
			if (defaultColor == default) defaultColor = Color.Cyan1;
			return explicitValue ?? container?.GetConsoleWindowSystem?.Theme?.StatusBarShortcutForegroundColor ?? defaultColor;
		}

		/// <summary>Resolves tab content border color.</summary>
		public static Color ResolveTabContentBorder(
			Color? explicitValue,
			IContainer? container,
			Color defaultColor = default)
		{
			if (defaultColor == default)
				defaultColor = Color.Grey;

			return explicitValue
				?? container?.GetConsoleWindowSystem?.Theme?.TabContentBorderColor
				?? container?.GetConsoleWindowSystem?.Theme?.ActiveBorderForegroundColor
				?? defaultColor;
		}

		// --- New methods for Checkbox, List, Tree ---

		/// <summary>
		/// Resolves checkbox background: explicit → theme CheckboxBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveCheckboxBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.CheckboxBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves focused checkbox background: explicit → theme CheckboxFocusedBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveCheckboxFocusedBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.CheckboxFocusedBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves disabled checkbox background: explicit → theme CheckboxDisabledBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveCheckboxDisabledBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.CheckboxDisabledBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves list background: explicit → theme ListBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveListBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.ListBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves tree background: explicit → theme TreeBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveTreeBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.TreeBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves tree selection background (focused): explicit → theme TreeSelectionBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveTreeSelectionBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.TreeSelectionBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves tree unfocused selection background: explicit → theme TreeUnfocusedSelectionBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveTreeUnfocusedSelectionBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.TreeUnfocusedSelectionBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves line graph background: explicit → theme LineGraphBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveLineGraphBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.LineGraphBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves bar graph background: explicit → theme BarGraphBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveBarGraphBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.BarGraphBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves sparkline background: explicit → theme SparklineBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveSparklineBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.SparklineBackgroundColor)
			?? Color.Transparent;

		// Start menu color resolution

		/// <summary>
		/// Resolves Start menu header background: explicit → theme StartMenuHeaderBackgroundColor → MenuDropdownBackgroundColor → Color.Grey15.
		/// </summary>
		public static Color ResolveStartMenuHeaderBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.StartMenuHeaderBackgroundColor)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.MenuDropdownBackgroundColor)
			?? Color.Grey15;

		/// <summary>
		/// Resolves Start menu header foreground: explicit → theme StartMenuHeaderForegroundColor → MenuDropdownForegroundColor → Color.Grey93.
		/// </summary>
		public static Color ResolveStartMenuHeaderForeground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.StartMenuHeaderForegroundColor)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.MenuDropdownForegroundColor)
			?? Color.Grey93;

		/// <summary>
		/// Resolves Start menu section header background: explicit → theme StartMenuSectionHeaderBackgroundColor → Color.Transparent.
		/// </summary>
		public static Color ResolveStartMenuSectionHeaderBackground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.StartMenuSectionHeaderBackgroundColor)
			?? Color.Transparent;

		/// <summary>
		/// Resolves Start menu info strip foreground: explicit → theme StartMenuInfoStripForegroundColor → Color.DarkGray.
		/// </summary>
		public static Color ResolveStartMenuInfoStripForeground(Color? explicitValue, IContainer? container)
			=> Coalesce(explicitValue)
			?? Coalesce(container?.GetConsoleWindowSystem?.Theme?.StartMenuInfoStripForegroundColor)
			?? Color.Grey;
	}
}
