using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleSeparatorTests
{
	// The separator's role accent colours the separator line (RoleForeground). The control has no
	// dedicated builder and exposes no resolved-colour getter, so the role is set via the public
	// Role/Outline properties and verified through the resolver.

	[Fact]
	public void DefaultRole_ProducesNoRoleForeground()
	{
		var sep = new SeparatorControl();
		Assert.Null(ColorResolver.RoleForeground(sep.Role, sep.Container, sep.Outline));
	}

	[Fact]
	public void DangerRole_ProducesRoleForeground()
	{
		var sep = new SeparatorControl { Role = ControlRole.Danger };
		Assert.NotNull(ColorResolver.RoleForeground(sep.Role, sep.Container, sep.Outline));
	}

	[Fact]
	public void ExplicitForegroundWins_RoleStillResolvable()
	{
		var sep = new SeparatorControl { Role = ControlRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, sep.ForegroundColor);
		Assert.NotNull(ColorResolver.RoleForeground(sep.Role, sep.Container, sep.Outline));
	}

	[Fact]
	public void Properties_RoundTripRoleAndOutline()
	{
		var sep = new SeparatorControl { Role = ControlRole.Success, Outline = true };
		Assert.Equal(ControlRole.Success, sep.Role);
		Assert.True(sep.Outline);
	}
}
