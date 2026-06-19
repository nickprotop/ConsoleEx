using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleCollapsiblePanelTests
{
	// CollapsiblePanel paints its border/header chrome with the role border colour, but only exposes
	// the raw (nullable) BorderColor override, so the link is asserted via the resolver the renderer
	// uses (ResolveChromeColor → RoleBorder) plus a builder round-trip and explicit-wins check.

	[Fact]
	public void DefaultRole_BorderResolvesToNull()
	{
		Assert.Null(ColorResolver.RoleBorder(ControlRole.Default, null, false, RoleState.Normal));
	}

	[Fact]
	public void DangerRole_ProducesNonNullBorder()
	{
		Assert.NotNull(ColorResolver.RoleBorder(ControlRole.Danger, null, false, RoleState.Normal));
	}

	[Fact]
	public void ExplicitBorderWinsOverRole()
	{
		var p = new CollapsiblePanel { Role = ControlRole.Danger, BorderColor = Color.Black };
		Assert.Equal(Color.Black, p.BorderColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var p = new CollapsiblePanelBuilder().WithRole(ControlRole.Danger).Outline().Build();
		Assert.Equal(ControlRole.Danger, p.Role);
		Assert.True(p.Outline);
	}
}
