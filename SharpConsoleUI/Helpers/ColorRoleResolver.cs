// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Derives a coordinated <see cref="ColorRoleSet"/> set from a single <see cref="ColorRole"/>,
	/// using per-theme seed colours (<c>theme.PrimaryColor</c> and friends) with a fall back to
	/// built-in defaults, plus the two non-null theme anchors. Pure and reflection-free — the single
	/// derivation brain shared by controls and the palette theme generator.
	/// </summary>
	public static class ColorRoleResolver
	{
		/// <summary>
		/// Resolves the coordinated colour set for <paramref name="role"/> using the active theme of
		/// the supplied <paramref name="container"/> (falling back to built-in defaults when the
		/// container or its window system is null).
		/// </summary>
		/// <param name="role">The semantic role to resolve.</param>
		/// <param name="container">The container whose window-system theme supplies the anchors, or null.</param>
		/// <param name="outline">When true, the role colour is used as text/border on the surface instead of as a fill.</param>
		/// <param name="state">The interaction state (Normal, Focused, Disabled).</param>
		/// <param name="mode">When non-null, overrides the theme's <see cref="ThemeMode"/> for the dark/light seed-colour decision. When null, the theme's own mode is used.</param>
		/// <returns>A coordinated, contrast-checked <see cref="ColorRoleSet"/>.</returns>
		public static ColorRoleSet Resolve(ColorRole role, IContainer? container, bool outline = false, ColorRoleState state = ColorRoleState.Normal, ThemeMode? mode = null)
			=> Resolve(role, container?.GetConsoleWindowSystem?.Theme ?? new ModernGrayTheme(), outline, state, mode);

		/// <summary>
		/// Resolves the coordinated colour set for <paramref name="role"/> against the supplied
		/// <paramref name="theme"/>'s two non-null anchors.
		/// </summary>
		/// <param name="role">The semantic role to resolve.</param>
		/// <param name="theme">The theme supplying the surface anchors and mode.</param>
		/// <param name="outline">When true, the role colour is used as text/border on the surface instead of as a fill.</param>
		/// <param name="state">The interaction state (Normal, Focused, Disabled).</param>
		/// <param name="mode">When non-null, overrides <paramref name="theme"/>'s <see cref="ThemeMode"/> for the dark/light seed-colour decision. When null, the theme's own mode is used.</param>
		/// <returns>A coordinated, contrast-checked <see cref="ColorRoleSet"/>.</returns>
		public static ColorRoleSet Resolve(ColorRole role, ITheme theme, bool outline = false, ColorRoleState state = ColorRoleState.Normal, ThemeMode? mode = null)
		{
			Color surfaceBg = theme.WindowBackgroundColor;
			Color surfaceFg = theme.WindowForegroundColor;

			if (role == ColorRole.Default)
				return new ColorRoleSet(surfaceFg, surfaceBg, surfaceFg, surfaceBg);

			Color baseColor = ResolveBaseColor(role, theme, mode);
			if (state == ColorRoleState.Focused)
				baseColor = baseColor.Tint(0.2);

			// The role colour painted directly on the surface (outline text/border, and the solid
			// variant's text/border) must be contrast-checked against it — a role whose seed sits close
			// to the window background (e.g. a near-surface tertiary, or amber/warning on a light theme)
			// would otherwise vanish.
			Color roleTextOnSurface = PaletteColors.EnsureContrast(baseColor, surfaceBg);

			ColorRoleSet result = outline
				? new ColorRoleSet(roleTextOnSurface, surfaceBg, roleTextOnSurface, roleTextOnSurface)
				: new ColorRoleSet(
					roleTextOnSurface,
					baseColor,
					PaletteColors.ContrastOn(baseColor),
					roleTextOnSurface);

			if (state == ColorRoleState.Disabled)
				result = new ColorRoleSet(
					result.Text.WithAlpha(ControlDefaults.DisabledStateAlpha),
					result.Background.WithAlpha(ControlDefaults.DisabledStateAlpha),
					result.TextOnBackground.WithAlpha(ControlDefaults.DisabledStateAlpha),
					result.Border.WithAlpha(ControlDefaults.DisabledStateAlpha));

			return result;
		}

		/// <summary>One step in a colour's derivation: what happened, and the result.</summary>
		/// <param name="Source">What this step was, in the words of the thing that did it.</param>
		/// <param name="Color">The colour after the step.</param>
		public readonly record struct ColorStep(string Source, Color Color);

		/// <summary>
		/// How a role arrives at its colour, step by step.
		/// </summary>
		/// <param name="role">The role to explain.</param>
		/// <param name="theme">The theme supplying the anchors.</param>
		/// <param name="outline">Whether the role paints as text rather than fill.</param>
		/// <param name="state">The interaction state.</param>
		/// <param name="mode">An override for the theme's dark/light decision.</param>
		/// <returns>The derivation, first step first, ending at the resolved colour.</returns>
		/// <remarks>
		/// A resolved role is COMPUTED, not looked up: an anchor seeds it,
		/// contrast against the surface may move it, focus tints it and disabled
		/// fades it. When the colour is wrong, the question is which of those
		/// steps moved it - and the colour alone cannot say.
		/// <para>
		/// Mirrors <see cref="Resolve(ColorRole, ITheme, bool, ColorRoleState, ThemeMode?)"/>
		/// step for step. A test asserts the last step equals what Resolve
		/// returns, so the two cannot quietly drift apart.
		/// </para>
		/// </remarks>
		public static IReadOnlyList<ColorStep> Explain(ColorRole role, ITheme theme,
			bool outline = false, ColorRoleState state = ColorRoleState.Normal,
			ThemeMode? mode = null)
		{
			ArgumentNullException.ThrowIfNull(theme);

			var steps = new List<ColorStep>();

			Color surfaceBg = theme.WindowBackgroundColor;
			Color surfaceFg = theme.WindowForegroundColor;

			if (role == ColorRole.Default)
			{
				// Default is not a colour of its own - it IS the window's surface.
				steps.Add(new ColorStep("theme surface (ColorRole.Default)", surfaceBg));
				return steps;
			}

			Color baseColor = ResolveBaseColor(role, theme, mode);
			steps.Add(new ColorStep($"theme {AnchorName(role)}", baseColor));

			if (state == ColorRoleState.Focused)
			{
				baseColor = baseColor.Tint(0.2);
				steps.Add(new ColorStep("tinted for focused state", baseColor));
			}

			Color roleTextOnSurface = PaletteColors.EnsureContrast(baseColor, surfaceBg);
			if (roleTextOnSurface != baseColor)
			{
				// Said plainly, because this step moves the colour painted ON the
				// surface - the outline, the border, the text of a solid variant -
				// and leaves a solid fill alone. Without saying which, a reader
				// sees a colour change that the next line appears to undo.
				steps.Add(new ColorStep(
					outline
						? "moved for contrast against the surface"
						: "moved for contrast against the surface (text and border only)",
					roleTextOnSurface));
			}

			ColorRoleSet result = outline
				? new ColorRoleSet(roleTextOnSurface, surfaceBg, roleTextOnSurface, roleTextOnSurface)
				: new ColorRoleSet(
					roleTextOnSurface,
					baseColor,
					PaletteColors.ContrastOn(baseColor),
					roleTextOnSurface);

			steps.Add(new ColorStep(
				outline ? "painted as outline (surface stays the background)" : "painted as fill",
				result.Background));

			if (state == ColorRoleState.Disabled)
			{
				result = new ColorRoleSet(
					result.Text.WithAlpha(ControlDefaults.DisabledStateAlpha),
					result.Background.WithAlpha(ControlDefaults.DisabledStateAlpha),
					result.TextOnBackground.WithAlpha(ControlDefaults.DisabledStateAlpha),
					result.Border.WithAlpha(ControlDefaults.DisabledStateAlpha));

				steps.Add(new ColorStep("faded for disabled state", result.Background));
			}

			return steps;
		}

		/// <summary>The theme property a role seeds from, for a cascade reader.</summary>
		private static string AnchorName(ColorRole role) => role switch
		{
			ColorRole.Primary => "PrimaryColor",
			ColorRole.Secondary => "SecondaryColor",
			ColorRole.Tertiary => "TertiaryColor",
			ColorRole.Info => "InfoColor",
			ColorRole.Success => "SuccessColor",
			ColorRole.Warning => "WarningColor",
			ColorRole.Danger => "DangerColor",
			_ => role.ToString(),
		};

		private static Color ResolveBaseColor(ColorRole role, ITheme theme, ThemeMode? mode = null)
		{
			bool dark = (mode ?? theme.Mode) == ThemeMode.Dark;
			Color primary = theme.PrimaryColor ?? (dark ? new Color(0, 255, 255) : new Color(13, 110, 253));
			Color secondary = theme.SecondaryColor ?? primary.Shade(0.25);
			Color tertiary = theme.TertiaryColor ?? secondary.Shade(0.25);
			return role switch
			{
				ColorRole.Primary => primary,
				ColorRole.Secondary => secondary,
				ColorRole.Tertiary => tertiary,
				ColorRole.Info => theme.InfoColor ?? new Color(13, 202, 240),
				ColorRole.Success => theme.SuccessColor ?? new Color(40, 167, 69),
				ColorRole.Warning => theme.WarningColor ?? new Color(255, 193, 7),
				ColorRole.Danger => theme.DangerColor ?? new Color(220, 53, 69),
				_ => primary,
			};
		}
	}
}
