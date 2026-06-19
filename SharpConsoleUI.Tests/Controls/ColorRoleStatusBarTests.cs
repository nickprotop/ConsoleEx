using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleStatusBarTests
{
	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		// For ColorRole=Default the public ForegroundColor getter must equal the legacy resolution.
		var c = new StatusBarControl();
		var plain = new StatusBarControl();
		Assert.Equal(plain.ForegroundColor, c.ForegroundColor);
		// The painted background role helper is also null for ColorRole=Default.
		Assert.Null(ColorResolver.ColorRoleBackground(ColorRole.Default, null, false, ColorRoleState.Normal));
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var c = new StatusBarControl { ColorRole = ColorRole.Danger };
		var plain = new StatusBarControl();
		Assert.NotEqual(plain.ForegroundColor, c.ForegroundColor);
		// The status bar fill (painted background) also takes the role colour.
		Assert.NotNull(ColorResolver.ColorRoleBackground(ColorRole.Danger, null, false, ColorRoleState.Normal));
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var c = new StatusBarControl { ColorRole = ColorRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, c.ForegroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var c = new StatusBarBuilder().WithColorRole(ColorRole.Danger).Outline().Build();
		Assert.Equal(ColorRole.Danger, c.ColorRole);
		Assert.True(c.Outline);
	}
}
