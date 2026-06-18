using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

public class ScrollbarThemeColorTests
{
	[Fact]
	public void ModernGray_DefaultScrollbarThumb_FollowsActiveBorder()
	{
		ITheme t = new ModernGrayTheme();
		Assert.Equal(t.ActiveBorderForegroundColor, t.ScrollbarThumbColor);
		Assert.Equal(t.InactiveBorderForegroundColor, t.ScrollbarThumbUnfocusedColor);
	}

	[Fact]
	public void DefaultScrollbarTrack_IsDimGrey()
	{
		ITheme t = new ModernGrayTheme();
		Assert.Equal(Color.Grey23, t.ScrollbarTrackColor);
		Assert.Equal(Color.Grey23, t.ScrollbarTrackUnfocusedColor);
	}

	[Fact]
	public void CustomTheme_CanOverrideGeneralScrollbarColors()
	{
		// Verify overrides are visible via the interface — DIM dispatch goes to the
		// most-derived explicit interface implementation when called through ITheme.
		ITheme t = new CustomScrollbarTheme();
		Assert.Equal(Color.Red, t.ScrollbarThumbColor);
		Assert.Equal(Color.Blue, t.ScrollbarTrackColor);
	}

	// A theme that inherits all ModernGrayTheme abstract implementations but
	// overrides only the scrollbar colors via explicit interface members.
	private sealed class CustomScrollbarTheme : ModernGrayTheme, ITheme
	{
		Color? ITheme.ScrollbarThumbColor => Color.Red;
		Color? ITheme.ScrollbarTrackColor => Color.Blue;
	}
}
