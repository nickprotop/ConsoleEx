using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleProgressBarTests
{
	// The progress bar's role accent surfaces through the filled-bar colour.

	[Fact]
	public void DefaultRole_FilledColorMatchesLegacy()
	{
		var p = new ProgressBarControl();
		var plain = new ProgressBarControl();
		Assert.Equal(plain.FilledColor, p.FilledColor);
	}

	[Fact]
	public void DangerRole_ChangesFilledColor()
	{
		var p = new ProgressBarControl { Role = ControlRole.Danger };
		var plain = new ProgressBarControl();
		Assert.NotEqual(plain.FilledColor, p.FilledColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var p = new ProgressBarControl { Role = ControlRole.Danger, FilledColor = Color.Black };
		Assert.Equal(Color.Black, p.FilledColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var p = new ProgressBarBuilder()
			.WithRole(ControlRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ControlRole.Success, p.Role);
		Assert.True(p.Outline);
	}
}
