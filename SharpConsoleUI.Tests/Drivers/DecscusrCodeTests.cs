using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

public class DecscusrCodeTests
{
	[Theory]
	[InlineData(CursorShape.Block,       CursorBlink.Blinking, 1)]
	[InlineData(CursorShape.Block,       CursorBlink.Steady,   2)]
	[InlineData(CursorShape.Underline,   CursorBlink.Blinking, 3)]
	[InlineData(CursorShape.Underline,   CursorBlink.Steady,   4)]
	[InlineData(CursorShape.VerticalBar, CursorBlink.Blinking, 5)]
	[InlineData(CursorShape.VerticalBar, CursorBlink.Steady,   6)]
	[InlineData(CursorShape.Block,       CursorBlink.TerminalDefault, 0)]
	public void DecscusrCode_MapsShapeAndBlink(CursorShape shape, CursorBlink blink, int expected)
	{
		Assert.Equal(expected, NetConsoleDriver.DecscusrCode(shape, blink));
	}
}
