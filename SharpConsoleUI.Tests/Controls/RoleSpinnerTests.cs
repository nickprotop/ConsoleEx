using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleSpinnerTests
{
	// The spinner's role accent surfaces through the glyph foreground colour, which the renderer
	// resolves via ColorResolver.RoleForeground. With no role the resolver returns null and the
	// spinner keeps its legacy foreground; the public Color getter stays a pass-through of the
	// explicit value (null when unset) so no-role behaviour is byte-identical.

	[Fact]
	public void DefaultRole_ColorUnchanged()
	{
		var s = new SpinnerControl();
		Assert.Null(s.Color);
		Assert.Null(ColorResolver.RoleForeground(s.Role, s.Container, s.Outline));
	}

	[Fact]
	public void DangerRole_ResolvesNonNullForeground()
	{
		var s = new SpinnerControl { Role = ControlRole.Danger };
		Assert.NotNull(ColorResolver.RoleForeground(s.Role, s.Container, s.Outline));
	}

	[Fact]
	public void ExplicitColorWins()
	{
		var s = new SpinnerControl { Role = ControlRole.Danger, Color = Color.Black };
		Assert.Equal(Color.Black, s.Color);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var s = new SpinnerBuilder()
			.WithRole(ControlRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ControlRole.Success, s.Role);
		Assert.True(s.Outline);
	}
}
