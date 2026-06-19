using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRolePromptTests
{
	// PromptControl exposes only raw (nullable) input-color overrides, not the resolved painted
	// colour, so the role link is asserted via the resolver the renderer uses plus a property
	// round-trip — matching the real behaviour without production test hooks.

	[Fact]
	public void DefaultRole_ResolvesToNull()
	{
		// For ColorRole=Default the role helpers return null, so the renderer falls through to legacy.
		Assert.Null(ColorResolver.ColorRoleTextOnBackground(ColorRole.Default, null, false, ColorRoleState.Normal));
		Assert.Null(ColorResolver.ColorRoleBackground(ColorRole.Default, null, false, ColorRoleState.Normal));
	}

	[Fact]
	public void DangerRole_ProducesNonNullPaintColors()
	{
		var fg = ColorResolver.ColorRoleTextOnBackground(ColorRole.Danger, null, false, ColorRoleState.Normal);
		var bg = ColorResolver.ColorRoleBackground(ColorRole.Danger, null, false, ColorRoleState.Normal);
		Assert.NotNull(fg);
		Assert.NotNull(bg);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var p = new PromptBuilder().WithColorRole(ColorRole.Danger).Outline().Build();
		Assert.Equal(ColorRole.Danger, p.ColorRole);
		Assert.True(p.Outline);
	}
}
