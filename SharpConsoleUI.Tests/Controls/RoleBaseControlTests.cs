using SharpConsoleUI;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleBaseControlTests
{
	[Fact]
	public void RoleBackground_DefaultRole_ReturnsNull()
	{
		Assert.Null(ColorResolver.RoleBackground(ControlRole.Default, null, outline: false));
	}

	[Fact]
	public void RoleBackground_DangerRole_ReturnsConcreteColor()
	{
		var c = ColorResolver.RoleBackground(ControlRole.Danger, null, outline: false);
		Assert.NotNull(c);
		Assert.Equal(255, c!.Value.A);
	}

	[Fact]
	public void RoleForeground_And_Border_DangerRole_AreNonNull()
	{
		Assert.NotNull(ColorResolver.RoleForeground(ControlRole.Danger, null, outline: false));
		Assert.NotNull(ColorResolver.RoleBorder(ControlRole.Danger, null, outline: false));
		Assert.NotNull(ColorResolver.RoleTextOnBackground(ControlRole.Danger, null, outline: false));
	}
}
