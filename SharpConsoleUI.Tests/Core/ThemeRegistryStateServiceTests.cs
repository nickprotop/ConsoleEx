using SharpConsoleUI.Core;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// Tests for the per-instance theme registry (replaces the old process-global static ThemeRegistry).
/// The key property is ISOLATION: two registries do not share registered themes.
/// </summary>
public class ThemeRegistryStateServiceTests
{
	[Fact]
	public void NewRegistry_HasBuiltInThemes()
	{
		var reg = new ThemeRegistryStateService();
		Assert.True(reg.IsThemeRegistered("Ocean"));
		Assert.True(reg.IsThemeRegistered("ModernGray"));
		// ModernGray built-in + seed palette catalog.
		Assert.True(reg.Count >= 2);
	}

	[Fact]
	public void GetDefaultTheme_ReturnsModernGray()
	{
		var reg = new ThemeRegistryStateService();
		Assert.IsType<ModernGrayTheme>(reg.GetDefaultTheme());
	}

	[Fact]
	public void RegisterTheme_AddsAndResolves()
	{
		var reg = new ThemeRegistryStateService();
		reg.RegisterTheme("Custom", "desc", () => new ModernGrayTheme());
		Assert.True(reg.IsThemeRegistered("Custom"));
		Assert.IsType<ModernGrayTheme>(reg.GetTheme("Custom"));
	}

	[Fact]
	public void TwoRegistries_AreIsolated()
	{
		// The whole point: registering in one must NOT appear in another.
		var a = new ThemeRegistryStateService();
		var b = new ThemeRegistryStateService();

		a.RegisterTheme("OnlyInA", "desc", () => new ModernGrayTheme());

		Assert.True(a.IsThemeRegistered("OnlyInA"));
		Assert.False(b.IsThemeRegistered("OnlyInA"));
		Assert.Null(b.GetTheme("OnlyInA"));
	}

	[Fact]
	public void DefaultThemeName_IsPerInstance()
	{
		var a = new ThemeRegistryStateService();
		var b = new ThemeRegistryStateService();
		a.DefaultThemeName = "Ocean";
		Assert.Equal("Ocean", a.DefaultThemeName);
		Assert.Equal("ModernGray", b.DefaultThemeName); // unaffected
	}

	[Fact]
	public void GetTheme_UnknownReturnsNull_GetThemeOrDefaultFallsBack()
	{
		var reg = new ThemeRegistryStateService();
		Assert.Null(reg.GetTheme("Nope"));
		var fallback = new ModernGrayTheme();
		Assert.Same(fallback, reg.GetThemeOrDefault("Nope", fallback));
	}

	[Fact]
	public void Unregister_Works()
	{
		var reg = new ThemeRegistryStateService();
		Assert.True(reg.UnregisterTheme("Ocean"));
		Assert.False(reg.IsThemeRegistered("Ocean"));
		Assert.False(reg.UnregisterTheme("Ocean")); // already gone
	}
}
