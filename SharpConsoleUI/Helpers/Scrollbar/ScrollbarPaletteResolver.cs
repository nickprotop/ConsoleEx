// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Helpers.Scrollbar
{
	/// <summary>
	/// The inputs to <see cref="ScrollbarPaletteResolver.Resolve"/>: everything the shared colour
	/// cascade needs to pick a scrollbar's thumb and track colours.
	/// </summary>
	/// <param name="ThumbOverride">An explicit per-instance thumb colour, if the control was given one. Wins outright.</param>
	/// <param name="TrackOverride">An explicit per-instance track colour, if the control was given one. Wins outright.</param>
	/// <param name="Theme">The active theme, or <c>null</c> if none is available.</param>
	/// <param name="HasFocus">Whether the scrollable control currently has focus.</param>
	/// <param name="IsEnabled">Whether the scrollable control is enabled. A disabled bar dims to its unfocused colours.</param>
	/// <param name="Background">The cell background every scrollbar cell paints with.</param>
	/// <param name="UseTableThemeKeys">
	/// When <c>true</c>, reads <see cref="ITheme.TableScrollbarThumbColor"/>/<see cref="ITheme.TableScrollbarTrackColor"/>
	/// instead of the general <see cref="ITheme.ScrollbarThumbColor"/> family. Table themes its bars
	/// independently of every other scrollable control; those two keys have no unfocused variants.
	/// </param>
	public readonly record struct ScrollbarPaletteRequest(
		Color? ThumbOverride,
		Color? TrackOverride,
		ITheme? Theme,
		bool HasFocus,
		bool IsEnabled,
		Color Background,
		bool UseTableThemeKeys = false);

	/// <summary>
	/// Resolves a scrollbar's thumb and track colours the same way for every control, so that
	/// theme-driven scrollbar colours are no longer a per-control accident.
	/// </summary>
	/// <remarks>
	/// The cascade, in order: an explicit override wins outright; otherwise a focus-aware theme
	/// colour; otherwise a hardcoded fallback. <see cref="ScrollbarPaletteRequest.IsEnabled"/>
	/// dims a disabled bar to its unfocused colours — the library has no separate "disabled
	/// scrollbar" theme colour, so this is the same treatment every other control-level disabled
	/// state falls back to when no dedicated colour exists.
	/// </remarks>
	public static class ScrollbarPaletteResolver
	{
		/// <summary>Fallback thumb colour when focused and no theme is available.</summary>
		private static readonly Color FallbackThumbFocused = Color.Cyan1;

		/// <summary>Fallback thumb colour when unfocused (or disabled) and no theme is available.</summary>
		private static readonly Color FallbackThumbUnfocused = Color.Grey;

		/// <summary>Fallback track colour when focused and no theme is available.</summary>
		private static readonly Color FallbackTrackFocused = Color.Grey;

		/// <summary>Fallback track colour when unfocused (or disabled) and no theme is available.</summary>
		private static readonly Color FallbackTrackUnfocused = Color.Grey23;

		/// <summary>
		/// Resolves the thumb and track colours for one scrollbar paint.
		/// </summary>
		/// <param name="request">The override, theme, focus, enabled and background inputs to resolve from.</param>
		/// <returns>The resolved <see cref="ScrollbarPalette"/>.</returns>
		public static ScrollbarPalette Resolve(ScrollbarPaletteRequest request)
		{
			// A disabled bar is treated as unfocused: there is no dedicated "disabled scrollbar"
			// theme colour, so this is the closest existing tone.
			bool focused = request.HasFocus && request.IsEnabled;

			Color thumb = ResolveThumb(request, focused);
			Color track = ResolveTrack(request, focused);

			return new ScrollbarPalette(thumb, track, request.Background);
		}

		private static Color ResolveThumb(ScrollbarPaletteRequest request, bool focused)
		{
			if (request.ThumbOverride.HasValue)
				return request.ThumbOverride.Value;

			Color? themeColor = ResolveThemeThumb(request.Theme, focused, request.UseTableThemeKeys);
			if (themeColor.HasValue)
				return themeColor.Value;

			return focused ? FallbackThumbFocused : FallbackThumbUnfocused;
		}

		private static Color ResolveTrack(ScrollbarPaletteRequest request, bool focused)
		{
			if (request.TrackOverride.HasValue)
				return request.TrackOverride.Value;

			Color? themeColor = ResolveThemeTrack(request.Theme, focused, request.UseTableThemeKeys);
			if (themeColor.HasValue)
				return themeColor.Value;

			return focused ? FallbackTrackFocused : FallbackTrackUnfocused;
		}

		private static Color? ResolveThemeThumb(ITheme? theme, bool focused, bool useTableThemeKeys)
		{
			if (theme == null)
				return null;

			if (useTableThemeKeys)
				return theme.TableScrollbarThumbColor;

			return focused ? theme.ScrollbarThumbColor : theme.ScrollbarThumbUnfocusedColor;
		}

		private static Color? ResolveThemeTrack(ITheme? theme, bool focused, bool useTableThemeKeys)
		{
			if (theme == null)
				return null;

			if (useTableThemeKeys)
				return theme.TableScrollbarTrackColor;

			return focused ? theme.ScrollbarTrackColor : theme.ScrollbarTrackUnfocusedColor;
		}
	}
}
