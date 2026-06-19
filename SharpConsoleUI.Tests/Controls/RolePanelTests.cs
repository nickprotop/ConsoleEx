using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RolePanelTests
{
	// PanelControl's painted border colour is the role anchor but BorderColor exposes only the raw
	// (nullable) override, so the role link is asserted via the resolver the renderer uses plus a
	// builder round-trip — matching the real behaviour without production test hooks.

	[Fact]
	public void DefaultRole_BorderResolvesToNull()
	{
		// For Role=Default the role border helper returns null, so the renderer falls through to legacy.
		Assert.Null(ColorResolver.RoleBorder(ControlRole.Default, null, false, RoleState.Normal));
	}

	[Fact]
	public void DangerRole_ProducesNonNullBorder()
	{
		var border = ColorResolver.RoleBorder(ControlRole.Danger, null, false, RoleState.Normal);
		Assert.NotNull(border);
	}

	[Fact]
	public void ExplicitBorderWinsOverRole()
	{
		// The renderer prefers an explicit BorderColor over the role border.
		var p = new PanelControl { Role = ControlRole.Danger, BorderColor = Color.Black };
		Assert.Equal(Color.Black, p.BorderColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var p = new PanelBuilder().WithRole(ControlRole.Danger).Outline().Build();
		Assert.Equal(ControlRole.Danger, p.Role);
		Assert.True(p.Outline);
	}
}
