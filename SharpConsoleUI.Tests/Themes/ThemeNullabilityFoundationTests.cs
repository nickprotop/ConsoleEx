// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

public class ThemeNullabilityFoundationTests
{
	// (a) Fall-through: a MutableTheme leaves NON-anchor members null by default ("unset = follow chain"
	//     is expressible), while the two anchors stay non-null (they terminate the chain).
	[Fact]
	public void MutableTheme_UnsetColorMembers_AreNull_ExceptAnchors()
	{
		var t = new MutableTheme();
		Assert.Null(t.ButtonForegroundColor);       // representative plain foreground (converted)
		Assert.Null(t.DesktopBackgroundColor);        // representative background (converted)
		Assert.Null(t.ScrollbarThumbColor);           // computed default delegating to a null member
		Assert.Null(t.ScrollbarTrackColor);           // computed default => null

		Assert.NotNull(t.WindowForegroundColor);      // anchor
		Assert.NotNull(t.WindowBackgroundColor);      // anchor
	}

	// (b) Generator invariant (load-bearing): FromPalette MUST pin scrollbar track + thumb, since the
	//     interface default is now null — a future generator edit dropping these would silently break.
	[Fact]
	public void PaletteGenerator_PinsScrollbarTrackAndThumb()
	{
		var theme = Theme.FromPalette(new Palette
		{
			Primary = Color.FromHex("#2563EB"),
			Background = Color.FromHex("#0F172A"),
		});
		Assert.NotNull(theme.ScrollbarTrackColor);
		Assert.NotNull(theme.ScrollbarTrackUnfocusedColor);
		Assert.NotNull(theme.ScrollbarThumbColor);
		Assert.NotNull(theme.ScrollbarThumbUnfocusedColor);
		Assert.NotNull(theme.TableScrollbarTrackColor);
		Assert.NotNull(theme.TableScrollbarThumbColor);
	}

	// (c) ModernGray invariant: concrete theme pins the two track members to Grey23
	//     ("interface silent, concrete themes pin real values").
	[Fact]
	public void ModernGray_PinsScrollbarTrack_ToGrey23()
	{
		var t = new ModernGrayTheme();
		Assert.Equal(Color.Grey23, t.ScrollbarTrackColor);
		Assert.Equal(Color.Grey23, t.ScrollbarTrackUnfocusedColor);
	}
}
