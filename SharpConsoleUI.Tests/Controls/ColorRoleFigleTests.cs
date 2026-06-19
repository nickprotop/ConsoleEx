using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleFigleTests
{
	// The FIGlet banner's role accent colours the whole banner (ColorRoleForeground). The control exposes
	// no resolved-colour getter, so the role is verified via the resolver + a builder round-trip.

	[Fact]
	public void DefaultRole_ProducesNoRoleForeground()
	{
		var fig = new FigleControl();
		Assert.Null(ColorResolver.ColorRoleForeground(fig.ColorRole, fig.Container, fig.Outline));
	}

	[Fact]
	public void DangerRole_ProducesRoleForeground()
	{
		var fig = new FigleControl { ColorRole = ColorRole.Danger };
		Assert.NotNull(ColorResolver.ColorRoleForeground(fig.ColorRole, fig.Container, fig.Outline));
	}

	[Fact]
	public void ExplicitColorWins_RoleStillResolvable()
	{
		var fig = new FigleControl { ColorRole = ColorRole.Danger, Color = Color.Black };
		Assert.Equal(Color.Black, fig.Color);
		Assert.NotNull(ColorResolver.ColorRoleForeground(fig.ColorRole, fig.Container, fig.Outline));
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var fig = new FigleControlBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, fig.ColorRole);
		Assert.True(fig.Outline);
	}
}
