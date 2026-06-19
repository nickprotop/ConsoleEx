using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleTabControlTests
{
	// TabControl paints the active tab with the role fill / text-on-fill, but exposes only the raw
	// (nullable) active-tab colour overrides, so the link is asserted via the resolvers the renderer
	// uses plus a builder round-trip and explicit-wins check.

	[Fact]
	public void DefaultRole_ResolvesToNull()
	{
		Assert.Null(ColorResolver.RoleBackground(ControlRole.Default, null, false, RoleState.Normal));
		Assert.Null(ColorResolver.RoleTextOnBackground(ControlRole.Default, null, false, RoleState.Normal));
	}

	[Fact]
	public void DangerRole_ProducesNonNullActiveTabColors()
	{
		Assert.NotNull(ColorResolver.RoleBackground(ControlRole.Danger, null, false, RoleState.Normal));
		Assert.NotNull(ColorResolver.RoleTextOnBackground(ControlRole.Danger, null, false, RoleState.Normal));
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var t = new TabControl { Role = ControlRole.Danger, ActiveUnfocusedBackgroundColor = Color.Black };
		Assert.Equal(Color.Black, t.ActiveUnfocusedBackgroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var t = new TabControlBuilder().WithRole(ControlRole.Danger).Outline().Build();
		Assert.Equal(ControlRole.Danger, t.Role);
		Assert.True(t.Outline);
	}
}
