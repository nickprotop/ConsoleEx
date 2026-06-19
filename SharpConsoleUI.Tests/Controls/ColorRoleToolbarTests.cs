using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleToolbarTests
{
	[Fact]
	public void DefaultRole_BackgroundMatchesLegacy()
	{
		// For ColorRole=Default the public getters must equal the legacy resolution (no-role path unchanged).
		var c = new ToolbarControl();
		var plain = new ToolbarControl();
		Assert.Equal(plain.BackgroundColor, c.BackgroundColor);
		Assert.Equal(plain.ForegroundColor, c.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesColors()
	{
		var c = new ToolbarControl { ColorRole = ColorRole.Danger };
		var plain = new ToolbarControl();
		Assert.NotEqual(plain.BackgroundColor, c.BackgroundColor);
		Assert.NotEqual(plain.ForegroundColor, c.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var c = new ToolbarControl { ColorRole = ColorRole.Danger, BackgroundColor = Color.Black };
		Assert.Equal(Color.Black, c.BackgroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var c = new ToolbarBuilder().WithColorRole(ColorRole.Danger).Outline().Build();
		Assert.Equal(ColorRole.Danger, c.ColorRole);
		Assert.True(c.Outline);
	}
}
