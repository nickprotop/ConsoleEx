using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RolePromptTests
{
	// PromptControl exposes only raw (nullable) input-color overrides, not the resolved painted
	// colour, so the role link is asserted via the resolver the renderer uses plus a property
	// round-trip — matching the real behaviour without production test hooks.

	[Fact]
	public void DefaultRole_ResolvesToNull()
	{
		// For Role=Default the role helpers return null, so the renderer falls through to legacy.
		Assert.Null(ColorResolver.RoleTextOnBackground(ControlRole.Default, null, false, RoleState.Normal));
		Assert.Null(ColorResolver.RoleBackground(ControlRole.Default, null, false, RoleState.Normal));
	}

	[Fact]
	public void DangerRole_ProducesNonNullPaintColors()
	{
		var fg = ColorResolver.RoleTextOnBackground(ControlRole.Danger, null, false, RoleState.Normal);
		var bg = ColorResolver.RoleBackground(ControlRole.Danger, null, false, RoleState.Normal);
		Assert.NotNull(fg);
		Assert.NotNull(bg);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var p = new PromptBuilder().WithRole(ControlRole.Danger).Outline().Build();
		Assert.Equal(ControlRole.Danger, p.Role);
		Assert.True(p.Outline);
	}
}
