using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleTreeTests
{
	// The tree's role accent surfaces through the selected-node foreground
	// (HighlightForegroundColor) and the resolved selection background at render time.

	[Fact]
	public void DefaultRole_HighlightForegroundMatchesLegacy()
	{
		var tree = new TreeControl();
		Assert.Equal(Color.White, tree.HighlightForegroundColor);
	}

	[Fact]
	public void DangerRole_ChangesHighlightForeground()
	{
		var tree = new TreeControl { Role = ControlRole.Danger };
		var plain = new TreeControl();
		Assert.NotEqual(plain.HighlightForegroundColor, tree.HighlightForegroundColor);
	}

	[Fact]
	public void ExplicitWinsOverRole()
	{
		var tree = new TreeControl
		{
			Role = ControlRole.Danger,
			HighlightForegroundColor = Color.Black
		};
		Assert.Equal(Color.Black, tree.HighlightForegroundColor);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var tree = new TreeControlBuilder()
			.WithRole(ControlRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ControlRole.Success, tree.Role);
		Assert.True(tree.Outline);
	}

	[Fact]
	public void Builder_NoRole_KeepsLegacyHighlightColors()
	{
		var tree = new TreeControlBuilder().Build();
		Assert.Equal(Color.Blue, tree.HighlightBackgroundColor);
		Assert.Equal(Color.White, tree.HighlightForegroundColor);
	}
}
