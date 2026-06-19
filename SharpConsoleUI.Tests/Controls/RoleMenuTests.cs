using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleMenuTests
{
	// MenuControl paints the highlighted/open item with the role fill / text-on-fill, but exposes only
	// the raw (nullable) highlight-colour overrides, so the link is asserted via the resolvers the
	// renderer uses plus a builder round-trip and explicit-wins check.

	[Fact]
	public void DefaultRole_ResolvesToNull()
	{
		Assert.Null(ColorResolver.RoleBackground(ControlRole.Default, null, false, RoleState.Focused));
		Assert.Null(ColorResolver.RoleTextOnBackground(ControlRole.Default, null, false, RoleState.Focused));
	}

	[Fact]
	public void DangerRole_ProducesNonNullHighlightColors()
	{
		Assert.NotNull(ColorResolver.RoleBackground(ControlRole.Danger, null, false, RoleState.Focused));
		Assert.NotNull(ColorResolver.RoleTextOnBackground(ControlRole.Danger, null, false, RoleState.Focused));
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var m = new MenuControl { Role = ControlRole.Danger, DropdownHighlightBackgroundColor = Color.Black };
		Assert.Equal(Color.Black, m.DropdownHighlightBackgroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var m = new MenuBuilder().WithRole(ControlRole.Danger).Outline().Build();
		Assert.Equal(ControlRole.Danger, m.Role);
		Assert.True(m.Outline);
	}
}
