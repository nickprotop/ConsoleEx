using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleSeparatorTests
{
	// The separator's role accent colours the separator line (ColorRoleForeground). The control has no
	// dedicated builder and exposes no resolved-colour getter, so the role is set via the public
	// ColorRole/Outline properties and verified through the resolver.

	[Fact]
	public void DefaultRole_ProducesNoRoleForeground()
	{
		var sep = new SeparatorControl();
		Assert.Null(ColorResolver.ColorRoleForeground(sep.ColorRole, sep.Container, sep.Outline));
	}

	[Fact]
	public void DangerRole_ProducesRoleForeground()
	{
		var sep = new SeparatorControl { ColorRole = ColorRole.Danger };
		Assert.NotNull(ColorResolver.ColorRoleForeground(sep.ColorRole, sep.Container, sep.Outline));
	}

	[Fact]
	public void ExplicitForegroundWins_RoleStillResolvable()
	{
		var sep = new SeparatorControl { ColorRole = ColorRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, sep.ForegroundColor);
		Assert.NotNull(ColorResolver.ColorRoleForeground(sep.ColorRole, sep.Container, sep.Outline));
	}

	[Fact]
	public void Properties_RoundTripRoleAndOutline()
	{
		var sep = new SeparatorControl { ColorRole = ColorRole.Success, Outline = true };
		Assert.Equal(ColorRole.Success, sep.ColorRole);
		Assert.True(sep.Outline);
	}
}
