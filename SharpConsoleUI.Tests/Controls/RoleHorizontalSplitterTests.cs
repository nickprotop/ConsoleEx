using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleHorizontalSplitterTests
{
	// The horizontal splitter's role accent colours the resting-state line via ForegroundColor.

	[Fact]
	public void DefaultRole_ForegroundMatchesLegacy()
	{
		var s = new HorizontalSplitterControl();
		Assert.Equal(Color.White, s.ForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesForeground()
	{
		var s = new HorizontalSplitterControl { Role = ControlRole.Danger };
		var plain = new HorizontalSplitterControl();
		Assert.NotEqual(plain.ForegroundColor, s.ForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var s = new HorizontalSplitterControl { Role = ControlRole.Danger, ForegroundColor = Color.Black };
		Assert.Equal(Color.Black, s.ForegroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var s = new HorizontalSplitterBuilder()
			.WithRole(ControlRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ControlRole.Success, s.Role);
		Assert.True(s.Outline);
	}
}
