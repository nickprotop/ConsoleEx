using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// Verifies that the per-cursor blink value carried on <see cref="CursorState"/> is applied to the
/// driver via the two-arg <c>SetCursorShape(shape, blink)</c> overload, and re-emitted when it changes.
/// </summary>
public class CursorBlinkResolutionTests
{
	private static (CursorStateService svc, HeadlessConsoleDriver drv, Window win) Make()
	{
		var driver = new HeadlessConsoleDriver(80, 25);
		var system = new ConsoleWindowSystem(driver);
		var win = new Window(system);
		var svc = new CursorStateService(driver);
		return (svc, driver, win);
	}

	[Theory]
	[InlineData(CursorBlink.Steady)]
	[InlineData(CursorBlink.Blinking)]
	[InlineData(CursorBlink.TerminalDefault)]
	public void ApplyCursor_ForwardsBlinkToDriver(CursorBlink blink)
	{
		var (svc, drv, win) = Make();
		svc.UpdateFromWindowSystem(win, new Point(2, 3), new Point(2, 3), null, CursorShape.Block, blink);
		svc.ApplyCursorToConsole(80, 25);
		Assert.Equal(blink, drv.LastCursorBlink);
	}

	[Fact]
	public void ApplyCursor_Reemits_WhenOnlyBlinkChanges()
	{
		var (svc, drv, win) = Make();
		svc.UpdateFromWindowSystem(win, new Point(2, 3), new Point(2, 3), null, CursorShape.Block, CursorBlink.Blinking);
		svc.ApplyCursorToConsole(80, 25);
		int afterFirst = drv.SetCursorShapeCallCount;

		// Same shape/position, only blink changes -> shape must be re-emitted (it carries blink).
		svc.UpdateFromWindowSystem(win, new Point(2, 3), new Point(2, 3), null, CursorShape.Block, CursorBlink.Steady);
		svc.ApplyCursorToConsole(80, 25);

		Assert.Equal(afterFirst + 1, drv.SetCursorShapeCallCount);
		Assert.Equal(CursorBlink.Steady, drv.LastCursorBlink);
	}

	[Fact]
	public void ApplyCursor_DoesNotReemit_WhenBlinkUnchanged()
	{
		var (svc, drv, win) = Make();
		svc.UpdateFromWindowSystem(win, new Point(2, 3), new Point(2, 3), null, CursorShape.Block, CursorBlink.Steady);
		svc.ApplyCursorToConsole(80, 25);
		int afterFirst = drv.SetCursorShapeCallCount;

		for (int i = 0; i < 3; i++) svc.ApplyCursorToConsole(80, 25);

		Assert.Equal(afterFirst, drv.SetCursorShapeCallCount);
	}
}
