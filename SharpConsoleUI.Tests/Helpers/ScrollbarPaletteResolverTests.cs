// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers.Scrollbar;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers
{
	public class ScrollbarPaletteResolverTests
	{
		private static ScrollbarPaletteRequest Request(
			Color? thumbOverride = null,
			Color? trackOverride = null,
			ITheme? theme = null,
			bool hasFocus = true,
			bool isEnabled = true,
			bool useTableThemeKeys = false)
			=> new(thumbOverride, trackOverride, theme, hasFocus, isEnabled, Color.Black, useTableThemeKeys);

		[Fact]
		public void ExplicitOverride_WinsOverTheme()
		{
			ITheme theme = new ModernGrayTheme();
			var request = Request(thumbOverride: Color.Magenta1, trackOverride: Color.Yellow, theme: theme);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(Color.Magenta1, palette.Thumb);
			Assert.Equal(Color.Yellow, palette.Track);
		}

		[Fact]
		public void ExplicitOverride_WinsEvenWithNoTheme()
		{
			var request = Request(thumbOverride: Color.Magenta1, trackOverride: Color.Yellow, theme: null);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(Color.Magenta1, palette.Thumb);
			Assert.Equal(Color.Yellow, palette.Track);
		}

		[Fact]
		public void Focused_UsesThemeFocusedColors()
		{
			ITheme theme = new ModernGrayTheme();
			var request = Request(theme: theme, hasFocus: true);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(theme.ScrollbarThumbColor, palette.Thumb);
			Assert.Equal(theme.ScrollbarTrackColor, palette.Track);
		}

		[Fact]
		public void Unfocused_UsesThemeUnfocusedColors()
		{
			ITheme theme = new ModernGrayTheme();
			var request = Request(theme: theme, hasFocus: false);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(theme.ScrollbarThumbUnfocusedColor, palette.Thumb);
			Assert.Equal(theme.ScrollbarTrackUnfocusedColor, palette.Track);
		}

		[Fact]
		public void UseTableThemeKeys_ReadsTableScrollbarColors_Focused()
		{
			ITheme theme = new ModernGrayTheme();
			var request = Request(theme: theme, hasFocus: true, useTableThemeKeys: true);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(theme.TableScrollbarThumbColor, palette.Thumb);
			Assert.Equal(theme.TableScrollbarTrackColor, palette.Track);
		}

		[Fact]
		public void UseTableThemeKeys_ReadsTableScrollbarColors_Unfocused()
		{
			// TableScrollbarThumbColor/TrackColor have no unfocused variant in ITheme: the same
			// keys are read regardless of focus when UseTableThemeKeys is set.
			ITheme theme = new ModernGrayTheme();
			var request = Request(theme: theme, hasFocus: false, useTableThemeKeys: true);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(theme.TableScrollbarThumbColor, palette.Thumb);
			Assert.Equal(theme.TableScrollbarTrackColor, palette.Track);
		}

		[Fact]
		public void NoTheme_Focused_FallsBackToCyanAndGrey()
		{
			var request = Request(theme: null, hasFocus: true, isEnabled: true);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(Color.Cyan1, palette.Thumb);
			Assert.Equal(Color.Grey, palette.Track);
		}

		[Fact]
		public void NoTheme_Unfocused_FallsBackToGreyAndGrey23()
		{
			var request = Request(theme: null, hasFocus: false, isEnabled: true);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(Color.Grey, palette.Thumb);
			Assert.Equal(Color.Grey23, palette.Track);
		}

		[Fact]
		public void NoTheme_Disabled_UsesUnfocusedFallbackRegardlessOfFocusFlag()
		{
			var request = Request(theme: null, hasFocus: true, isEnabled: false);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(Color.Grey, palette.Thumb);
			Assert.Equal(Color.Grey23, palette.Track);
		}

		[Fact]
		public void Theme_Disabled_UsesUnfocusedThemeColorsRegardlessOfFocusFlag()
		{
			ITheme theme = new ModernGrayTheme();
			var request = Request(theme: theme, hasFocus: true, isEnabled: false);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(theme.ScrollbarThumbUnfocusedColor, palette.Thumb);
			Assert.Equal(theme.ScrollbarTrackUnfocusedColor, palette.Track);
		}

		[Fact]
		public void Background_IsPassedThroughUnchanged()
		{
			var request = new ScrollbarPaletteRequest(null, null, null, true, true, Color.DarkGreen);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(Color.DarkGreen, palette.Background);
		}

		[Fact]
		public void CustomTheme_ExplicitInterfaceOverrides_AreHonored()
		{
			// Mirrors ScrollbarThemeColorTests.CustomScrollbarTheme: a theme that overrides only the
			// general scrollbar colors via explicit interface members.
			ITheme theme = new CustomScrollbarTheme();
			var request = Request(theme: theme, hasFocus: true);

			var palette = ScrollbarPaletteResolver.Resolve(request);

			Assert.Equal(Color.Red, palette.Thumb);
			Assert.Equal(Color.Blue, palette.Track);
		}

		private sealed class CustomScrollbarTheme : ModernGrayTheme, ITheme
		{
			Color? ITheme.ScrollbarThumbColor => Color.Red;
			Color? ITheme.ScrollbarTrackColor => Color.Blue;
		}
	}
}
