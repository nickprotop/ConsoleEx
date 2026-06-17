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

/// <summary>Tests for the ThemeMode concept: the ITheme DIM default and the built-in retrofit.</summary>
public class ThemeModeTests
{
	[Fact]
	public void ModernGray_IsDark()
	{
		Assert.Equal(ThemeMode.Dark, new ModernGrayTheme().Mode);
	}

	[Fact]
	public void PaletteLightTheme_IsLight()
	{
		Assert.Equal(ThemeMode.Light, Theme.FromPalette(new Palette { Background = Color.FromHex("#F0F0F0"), Mode = ThemeMode.Light }).Mode);
	}

	[Fact]
	public void ThirdPartyTheme_WithoutOverride_DefaultsToDark()
	{
		ITheme custom = new MinimalCustomTheme();
		Assert.Equal(ThemeMode.Dark, custom.Mode);
	}

	private sealed class MinimalCustomTheme : ModernGrayTheme
	{
		public override string Name => "MinimalCustom";
	}
}
