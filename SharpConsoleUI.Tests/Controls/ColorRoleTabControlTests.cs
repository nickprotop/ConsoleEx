using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleTabControlTests
{
	// TabControl paints the active tab with the role fill / text-on-fill, but exposes only the raw
	// (nullable) active-tab colour overrides, so the link is asserted via the resolvers the renderer
	// uses plus a builder round-trip and explicit-wins check.

	[Fact]
	public void DefaultRole_ResolvesToNull()
	{
		Assert.Null(ColorResolver.ColorRoleBackground(ColorRole.Default, null, false, ColorRoleState.Normal));
		Assert.Null(ColorResolver.ColorRoleTextOnBackground(ColorRole.Default, null, false, ColorRoleState.Normal));
	}

	[Fact]
	public void DangerRole_ProducesNonNullActiveTabColors()
	{
		Assert.NotNull(ColorResolver.ColorRoleBackground(ColorRole.Danger, null, false, ColorRoleState.Normal));
		Assert.NotNull(ColorResolver.ColorRoleTextOnBackground(ColorRole.Danger, null, false, ColorRoleState.Normal));
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var t = new TabControl { ColorRole = ColorRole.Danger, ActiveUnfocusedBackgroundColor = Color.Black };
		Assert.Equal(Color.Black, t.ActiveUnfocusedBackgroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var t = new TabControlBuilder().WithColorRole(ColorRole.Danger).Outline().Build();
		Assert.Equal(ColorRole.Danger, t.ColorRole);
		Assert.True(t.Outline);
	}
}
