using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleFigleTests
{
	// The FIGlet banner's role accent colours the whole banner (RoleForeground). The control exposes
	// no resolved-colour getter, so the role is verified via the resolver + a builder round-trip.

	[Fact]
	public void DefaultRole_ProducesNoRoleForeground()
	{
		var fig = new FigleControl();
		Assert.Null(ColorResolver.RoleForeground(fig.Role, fig.Container, fig.Outline));
	}

	[Fact]
	public void DangerRole_ProducesRoleForeground()
	{
		var fig = new FigleControl { Role = ControlRole.Danger };
		Assert.NotNull(ColorResolver.RoleForeground(fig.Role, fig.Container, fig.Outline));
	}

	[Fact]
	public void ExplicitColorWins_RoleStillResolvable()
	{
		var fig = new FigleControl { Role = ControlRole.Danger, Color = Color.Black };
		Assert.Equal(Color.Black, fig.Color);
		Assert.NotNull(ColorResolver.RoleForeground(fig.Role, fig.Container, fig.Outline));
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var fig = new FigleControlBuilder()
			.WithRole(ControlRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ControlRole.Success, fig.Role);
		Assert.True(fig.Outline);
	}
}
