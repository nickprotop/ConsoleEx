using SharpConsoleUI;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

public class RoleSeedTests
{
	[Fact]
	public void ModernGray_SurfacesAllSevenSeeds()
	{
		var t = new ModernGrayTheme();
		Assert.NotNull(t.PrimaryColor);
		Assert.NotNull(t.SecondaryColor);
		Assert.NotNull(t.TertiaryColor);
		Assert.NotNull(t.InfoColor);
		Assert.NotNull(t.SuccessColor);
		Assert.NotNull(t.WarningColor);
		Assert.NotNull(t.DangerColor);
	}

	[Fact]
	public void Generator_SurfacesAllSevenSeeds()
	{
		var t = Theme.FromPalette(new Palette { Primary = Color.FromHex("#2563EB"), Background = Color.FromHex("#0F172A") });
		Assert.NotNull(t.PrimaryColor);
		Assert.NotNull(t.SecondaryColor);
		Assert.NotNull(t.TertiaryColor);
		Assert.NotNull(t.InfoColor);
		Assert.NotNull(t.SuccessColor);
		Assert.NotNull(t.WarningColor);
		Assert.NotNull(t.DangerColor);
	}

	[Fact]
	public void RoleResolver_UsesThemeSeed_OverBuiltIn()
	{
		var t = new ModernGrayTheme();
		var rc = RoleResolver.Resolve(ControlRole.Danger, t);
		Assert.Equal(Color.Maroon, rc.Background);
	}

	[Fact]
	public void RoleResolver_NullSeed_DerivesSecondaryFromPrimary()
	{
		var t = new MutableTheme { PrimaryColor = Color.FromHex("#2563EB") };
		var rc = RoleResolver.Resolve(ControlRole.Secondary, t);
		Assert.Equal(Color.FromHex("#2563EB").Shade(0.25), rc.Background);
	}
}
