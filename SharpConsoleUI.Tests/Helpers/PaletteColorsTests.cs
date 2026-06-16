// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

public class PaletteColorsTests
{
	[Fact]
	public void Tint_BlendsTowardWhite()
	{
		var c = new Color(100, 100, 100);
		var t = c.Tint(0.5);
		Assert.True(t.R > c.R && t.G > c.G && t.B > c.B, "tint should lighten");
	}

	[Fact]
	public void Shade_BlendsTowardBlack()
	{
		var c = new Color(100, 100, 100);
		var s = c.Shade(0.5);
		Assert.True(s.R < c.R && s.G < c.G && s.B < c.B, "shade should darken");
	}

	[Fact]
	public void Mix_BlendsTowardOther()
	{
		var a = new Color(0, 0, 0);
		var b = new Color(200, 200, 200);
		var m = a.Mix(b, 0.5);
		Assert.InRange((int)m.R, 95, 105);
	}

	[Fact]
	public void Luminance_DarkIsLow_LightIsHigh()
	{
		Assert.True(new Color(0, 0, 0).Luminance() < 10);
		Assert.True(new Color(255, 255, 255).Luminance() > 245);
	}

	[Fact]
	public void IsDark_TrueForDarkBackground()
	{
		Assert.True(new Color(20, 20, 20).IsDark());
		Assert.False(new Color(240, 240, 240).IsDark());
	}

	[Fact]
	public void ContrastOn_ReturnsLightForDarkBg_AndDarkForLightBg()
	{
		Assert.True(PaletteColors.ContrastOn(new Color(20, 20, 20)).Luminance() > 200);
		Assert.True(PaletteColors.ContrastOn(new Color(240, 240, 240)).Luminance() < 60);
	}
}
