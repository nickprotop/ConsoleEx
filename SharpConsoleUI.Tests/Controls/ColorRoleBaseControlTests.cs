using SharpConsoleUI;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleBaseControlTests
{
	[Fact]
	public void RoleBackground_DefaultRole_ReturnsNull()
	{
		Assert.Null(ColorResolver.ColorRoleBackground(ColorRole.Default, null, outline: false));
	}

	[Fact]
	public void RoleBackground_DangerRole_ReturnsConcreteColor()
	{
		var c = ColorResolver.ColorRoleBackground(ColorRole.Danger, null, outline: false);
		Assert.NotNull(c);
		Assert.Equal(255, c!.Value.A);
	}

	[Fact]
	public void RoleForeground_And_Border_DangerRole_AreNonNull()
	{
		Assert.NotNull(ColorResolver.ColorRoleForeground(ColorRole.Danger, null, outline: false));
		Assert.NotNull(ColorResolver.ColorRoleBorder(ColorRole.Danger, null, outline: false));
		Assert.NotNull(ColorResolver.ColorRoleTextOnBackground(ColorRole.Danger, null, outline: false));
	}
}
