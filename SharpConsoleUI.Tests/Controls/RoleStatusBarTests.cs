using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleStatusBarTests
{
	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		// For Role=Default the public ForegroundColor getter must equal the legacy resolution.
		var c = new StatusBarControl();
		var plain = new StatusBarControl();
		Assert.Equal(plain.ForegroundColor, c.ForegroundColor);
		// The painted background role helper is also null for Role=Default.
		Assert.Null(ColorResolver.RoleBackground(ControlRole.Default, null, false, RoleState.Normal));
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var c = new StatusBarControl { Role = ControlRole.Danger };
		var plain = new StatusBarControl();
		Assert.NotEqual(plain.ForegroundColor, c.ForegroundColor);
		// The status bar fill (painted background) also takes the role colour.
		Assert.NotNull(ColorResolver.RoleBackground(ControlRole.Danger, null, false, RoleState.Normal));
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var c = new StatusBarControl { Role = ControlRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, c.ForegroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var c = new StatusBarBuilder().WithRole(ControlRole.Danger).Outline().Build();
		Assert.Equal(ControlRole.Danger, c.Role);
		Assert.True(c.Outline);
	}
}
