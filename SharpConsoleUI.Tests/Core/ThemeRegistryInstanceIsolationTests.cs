using SharpConsoleUI;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// End-to-end isolation tests: theme registration is per-ConsoleWindowSystem, so a theme registered
/// (or contributed by a plugin loaded) into one window system does NOT appear in another. This is the
/// behavior the old process-global static ThemeRegistry violated.
/// </summary>
public class ThemeRegistryInstanceIsolationTests
{
	private static ConsoleWindowSystem NewSystem()
		=> new ConsoleWindowSystem(new HeadlessConsoleDriver());

	[Fact]
	public void EachSystem_HasOwnRegistry_WithBuiltIns()
	{
		var a = NewSystem();
		var b = NewSystem();

		Assert.NotSame(a.ThemeRegistryService, b.ThemeRegistryService);
		Assert.True(a.ThemeRegistryService.IsThemeRegistered("ModernGray"));
		Assert.True(b.ThemeRegistryService.IsThemeRegistered("Classic"));
	}

	[Fact]
	public void ThemeRegisteredInOneSystem_DoesNotLeakToAnother()
	{
		var a = NewSystem();
		var b = NewSystem();

		a.ThemeRegistryService.RegisterTheme("PluginTheme", "from a plugin in A", () => new ClassicTheme());

		Assert.True(a.ThemeRegistryService.IsThemeRegistered("PluginTheme"));
		Assert.False(b.ThemeRegistryService.IsThemeRegistered("PluginTheme"));
	}

	[Fact]
	public void SwitchTheme_ResolvesFromOwnRegistry()
	{
		var a = NewSystem();
		// A theme only registered in A is switchable in A...
		a.ThemeRegistryService.RegisterTheme("OnlyA", "desc", () => new ClassicTheme());
		Assert.True(a.ThemeStateService.SwitchTheme("OnlyA"));

		// ...but not in a fresh system B.
		var b = NewSystem();
		Assert.False(b.ThemeStateService.SwitchTheme("OnlyA"));
	}

	[Fact]
	public void SwitchTheme_BuiltInWorks()
	{
		var a = NewSystem();
		Assert.True(a.ThemeStateService.SwitchTheme("Classic"));
		Assert.Equal("Classic", a.Theme.Name);
	}
}
