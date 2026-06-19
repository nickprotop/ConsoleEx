using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleTableTests
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
		var table = new TableControl { Role = ControlRole.Danger };
		var plain = new TableControl();
		Assert.NotEqual(plain.ResolveSelectionBackgroundColor(), table.ResolveSelectionBackgroundColor());
	}

	[Fact]
	public void ExplicitRoleColorMatchesResolver()
	{
		var table = new TableControl { Role = ControlRole.Success };
		var expected = SharpConsoleUI.Helpers.RoleResolver.Resolve(ControlRole.Success, table.Container, false).Background;
		Assert.Equal(expected, table.ResolveSelectionBackgroundColor());
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var table = new TableControlBuilder()
			.WithRole(ControlRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ControlRole.Success, table.Role);
		Assert.True(table.Outline);
	}
}
