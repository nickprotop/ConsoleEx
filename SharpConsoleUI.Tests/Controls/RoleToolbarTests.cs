using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleToolbarTests
{
	[Fact]
	public void DefaultRole_BackgroundMatchesLegacy()
	{
		// For Role=Default the public getters must equal the legacy resolution (no-role path unchanged).
		var c = new ToolbarControl();
		var plain = new ToolbarControl();
		Assert.Equal(plain.BackgroundColor, c.BackgroundColor);
		Assert.Equal(plain.ForegroundColor, c.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesColors()
	{
		var c = new ToolbarControl { Role = ControlRole.Danger };
		var plain = new ToolbarControl();
		Assert.NotEqual(plain.BackgroundColor, c.BackgroundColor);
		Assert.NotEqual(plain.ForegroundColor, c.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var c = new ToolbarControl { Role = ControlRole.Danger, BackgroundColor = Color.Black };
		Assert.Equal(Color.Black, c.BackgroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var c = new ToolbarBuilder().WithRole(ControlRole.Danger).Outline().Build();
		Assert.Equal(ControlRole.Danger, c.Role);
		Assert.True(c.Outline);
	}
}
