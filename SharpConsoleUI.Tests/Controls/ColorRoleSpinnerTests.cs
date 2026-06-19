using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleSpinnerTests
{
	// The spinner's role accent surfaces through the glyph foreground colour, which the renderer
	// resolves via ColorResolver.ColorRoleForeground. With no role the resolver returns null and the
	// spinner keeps its legacy foreground; the public Color getter stays a pass-through of the
	// explicit value (null when unset) so no-role behaviour is byte-identical.

	[Fact]
	public void DefaultRole_ColorUnchanged()
	{
		var s = new SpinnerControl();
		Assert.Null(s.Color);
		Assert.Null(ColorResolver.ColorRoleForeground(s.ColorRole, s.Container, s.Outline));
	}

	[Fact]
	public void DangerRole_ResolvesNonNullForeground()
	{
		var s = new SpinnerControl { ColorRole = ColorRole.Danger };
		Assert.NotNull(ColorResolver.ColorRoleForeground(s.ColorRole, s.Container, s.Outline));
	}

	[Fact]
	public void ExplicitColorWins()
	{
		var s = new SpinnerControl { ColorRole = ColorRole.Danger, Color = Color.Black };
		Assert.Equal(Color.Black, s.Color);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var s = new SpinnerBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, s.ColorRole);
		Assert.True(s.Outline);
	}
}
