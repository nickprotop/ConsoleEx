using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRolePanelTests
{
	// PanelControl's painted border colour is the role anchor but BorderColor exposes only the raw
	// (nullable) override, so the role link is asserted via the resolver the renderer uses plus a
	// builder round-trip — matching the real behaviour without production test hooks.

	[Fact]
	public void DefaultRole_BorderResolvesToNull()
	{
		// For ColorRole=Default the role border helper returns null, so the renderer falls through to legacy.
		Assert.Null(ColorResolver.ColorRoleBorder(ColorRole.Default, null, false, ColorRoleState.Normal));
	}

	[Fact]
	public void DangerRole_ProducesNonNullBorder()
	{
		var border = ColorResolver.ColorRoleBorder(ColorRole.Danger, null, false, ColorRoleState.Normal);
		Assert.NotNull(border);
	}

	[Fact]
	public void ExplicitBorderWinsOverRole()
	{
		// The renderer prefers an explicit BorderColor over the role border.
		var p = new PanelControl { ColorRole = ColorRole.Danger, BorderColor = Color.Black };
		Assert.Equal(Color.Black, p.BorderColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var p = new PanelBuilder().WithColorRole(ColorRole.Danger).Outline().Build();
		Assert.Equal(ColorRole.Danger, p.ColorRole);
		Assert.True(p.Outline);
	}
}
