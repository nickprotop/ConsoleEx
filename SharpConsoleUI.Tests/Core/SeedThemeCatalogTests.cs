// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class SeedThemeCatalogTests
{
	[Fact]
	public void Registry_HasSeedCatalog_AndKeepsModernGrayDefault()
	{
		var reg = new ThemeRegistryStateService();
		Assert.True(reg.IsThemeRegistered("Classic"));
		Assert.True(reg.IsThemeRegistered("ModernGray"));
		Assert.Equal("ModernGray", reg.DefaultThemeName);   // default invariant
		Assert.True(reg.Count >= 7, $"expected >= 7 themes (2 built-in + >=5 seed), got {reg.Count}");
		foreach (var name in reg.GetAvailableThemeNames())
			Assert.NotNull(reg.GetTheme(name));
	}
}
