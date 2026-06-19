using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleCollapsiblePanelTests
{
	// CollapsiblePanel paints its border/header chrome with the role border colour, but only exposes
	// the raw (nullable) BorderColor override, so the link is asserted via the resolver the renderer
	// uses (ResolveChromeColor → ColorRoleBorder) plus a builder round-trip and explicit-wins check.

	[Fact]
	public void DefaultRole_BorderResolvesToNull()
	{
		Assert.Null(ColorResolver.ColorRoleBorder(ColorRole.Default, null, false, ColorRoleState.Normal));
	}

	[Fact]
	public void DangerRole_ProducesNonNullBorder()
	{
		Assert.NotNull(ColorResolver.ColorRoleBorder(ColorRole.Danger, null, false, ColorRoleState.Normal));
	}

	[Fact]
	public void ExplicitBorderWinsOverRole()
	{
		var p = new CollapsiblePanel { ColorRole = ColorRole.Danger, BorderColor = Color.Black };
		Assert.Equal(Color.Black, p.BorderColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var p = new CollapsiblePanelBuilder().WithColorRole(ColorRole.Danger).Outline().Build();
		Assert.Equal(ColorRole.Danger, p.ColorRole);
		Assert.True(p.Outline);
	}
}
