using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleMenuTests
{
	// MenuControl paints the highlighted/open item with the role fill / text-on-fill, but exposes only
	// the raw (nullable) highlight-colour overrides, so the link is asserted via the resolvers the
	// renderer uses plus a builder round-trip and explicit-wins check.

	[Fact]
	public void DefaultRole_ResolvesToNull()
	{
		Assert.Null(ColorResolver.ColorRoleBackground(ColorRole.Default, null, false, ColorRoleState.Focused));
		Assert.Null(ColorResolver.ColorRoleTextOnBackground(ColorRole.Default, null, false, ColorRoleState.Focused));
	}

	[Fact]
	public void DangerRole_ProducesNonNullHighlightColors()
	{
		Assert.NotNull(ColorResolver.ColorRoleBackground(ColorRole.Danger, null, false, ColorRoleState.Focused));
		Assert.NotNull(ColorResolver.ColorRoleTextOnBackground(ColorRole.Danger, null, false, ColorRoleState.Focused));
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var m = new MenuControl { ColorRole = ColorRole.Danger, DropdownHighlightBackgroundColor = Color.Black };
		Assert.Equal(Color.Black, m.DropdownHighlightBackgroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var m = new MenuBuilder().WithColorRole(ColorRole.Danger).Outline().Build();
		Assert.Equal(ColorRole.Danger, m.ColorRole);
		Assert.True(m.Outline);
	}
}
