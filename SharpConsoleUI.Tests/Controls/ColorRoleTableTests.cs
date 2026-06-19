using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleTableTests
{
	// The table's role accent surfaces through the focused row-selection colours
	// (ResolveSelectionBackgroundColor / ResolveSelectionForegroundColor).

	[Fact]
	public void DefaultRole_SelectionColorsMatchLegacy()
	{
		var table = new TableControl();
		Assert.Equal(Color.Blue, table.ResolveSelectionBackgroundColor());
		Assert.Equal(Color.White, table.ResolveSelectionForegroundColor());
	}

	[Fact]
	public void DangerRole_ChangesSelectionBackground()
	{
		var table = new TableControl { ColorRole = ColorRole.Danger };
		var plain = new TableControl();
		Assert.NotEqual(plain.ResolveSelectionBackgroundColor(), table.ResolveSelectionBackgroundColor());
	}

	[Fact]
	public void ExplicitRoleColorMatchesResolver()
	{
		var table = new TableControl { ColorRole = ColorRole.Success };
		var expected = SharpConsoleUI.Helpers.ColorRoleResolver.Resolve(ColorRole.Success, table.Container, false).Background;
		Assert.Equal(expected, table.ResolveSelectionBackgroundColor());
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var table = new TableControlBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, table.ColorRole);
		Assert.True(table.Outline);
	}
}
