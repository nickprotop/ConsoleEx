// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Render-level tests verifying that a <see cref="MarkupControl"/> role sets the DEFAULT text colour
/// passed to the markup parser, while inline [color] tags still override it.
/// </summary>
public class ColorRoleMarkupTests
{
	private const int Width = 20;
	private const int Height = 3;

	private static CharacterBuffer Paint(MarkupControl control)
	{
		var buffer = new CharacterBuffer(Width, Height);
		var bounds = new LayoutRect(0, 0, Width, Height);
		// Paint with a known default foreground (white) and background (black).
		control.PaintDOM(buffer, bounds, bounds, new Color(255, 255, 255), new Color(0, 0, 0));
		return buffer;
	}

	// Returns the foreground colour of the first cell on row 0 carrying the given character.
	private static Color? ForegroundOf(CharacterBuffer buffer, char target)
	{
		for (int x = 0; x < Width; x++)
		{
			var cell = buffer.GetCell(x, 0);
			if (cell.Character.Value == target)
				return cell.Foreground;
		}
		return null;
	}

	[Fact]
	public void DefaultRole_PlainTextUsesDefaultForeground()
	{
		var ctrl = new MarkupControl(new List<string> { "Hello" });
		var buffer = Paint(ctrl);
		Assert.Equal(new Color(255, 255, 255), ForegroundOf(buffer, 'H'));
	}

	[Fact]
	public void Role_ChangesPlainTextForeground()
	{
		var ctrl = new MarkupControl(new List<string> { "Hello" }) { ColorRole = ColorRole.Danger };
		var buffer = Paint(ctrl);
		var fg = ForegroundOf(buffer, 'H');
		Assert.NotNull(fg);
		Assert.NotEqual(new Color(255, 255, 255), fg!.Value);

		var expected = SharpConsoleUI.Helpers.ColorResolver.ColorRoleForeground(ColorRole.Danger, ctrl.Container, ctrl.Outline);
		Assert.Equal(expected, fg);
	}

	[Fact]
	public void ExplicitForegroundWinsOverRole()
	{
		var ctrl = new MarkupControl(new List<string> { "Hello" })
		{
			ColorRole = ColorRole.Danger,
			ForegroundColor = new Color(0, 0, 0)
		};
		var buffer = Paint(ctrl);
		Assert.Equal(new Color(0, 0, 0), ForegroundOf(buffer, 'H'));
	}

	[Fact]
	public void InlineColorTagOverridesRole()
	{
		// The 'X' is wrapped in [red]...[/]; even with a role set, the inline tag must win.
		var ctrl = new MarkupControl(new List<string> { "[red]X[/]" }) { ColorRole = ColorRole.Success };
		var buffer = Paint(ctrl);
		var fg = ForegroundOf(buffer, 'X');
		Assert.NotNull(fg);
		Assert.Equal(Color.Red, fg!.Value);

		// And the role colour (Success) is NOT what got applied to the inline-tagged glyph.
		var roleFg = SharpConsoleUI.Helpers.ColorResolver.ColorRoleForeground(ColorRole.Success, ctrl.Container, ctrl.Outline);
		Assert.NotEqual(roleFg, fg);
	}

	[Fact]
	public void Builder_RoundTripsRoleAndOutline()
	{
		var ctrl = new MarkupBuilder()
			.WithColorRole(ColorRole.Success)
			.Outline()
			.Build();
		Assert.Equal(ColorRole.Success, ctrl.ColorRole);
		Assert.True(ctrl.Outline);
	}
}
