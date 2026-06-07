using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class CursorStateServiceChangeDetectionTests
{
	private static (CursorStateService svc, HeadlessConsoleDriver drv, Window win) Make()
	{
		var driver = new HeadlessConsoleDriver(80, 25);
		var system = new ConsoleWindowSystem(driver);
		var win = new Window(system);
		var svc = new CursorStateService(driver);
		return (svc, driver, win);
	}

	[Fact]
	public void ApplyCursor_DoesNotReemit_WhenStateUnchanged()
	{
		var (svc, drv, win) = Make();
		svc.UpdateFromWindowSystem(win, new Point(2, 3), new Point(2, 3), null, CursorShape.Block);
		for (int i = 0; i < 5; i++) svc.ApplyCursorToConsole(80, 25);
		Assert.Equal(1, drv.SetCursorPositionCallCount);
		Assert.Equal(1, drv.SetCursorVisibleCallCount);
	}

	[Fact]
	public void ApplyCursor_Reemits_AfterPhysicalCursorInvalidated()
	{
		var (svc, drv, win) = Make();
		svc.UpdateFromWindowSystem(win, new Point(2, 3), new Point(2, 3), null, CursorShape.Block);
		svc.ApplyCursorToConsole(80, 25);
		Assert.Equal(1, drv.SetCursorPositionCallCount);
		svc.InvalidatePhysicalCursor();
		svc.ApplyCursorToConsole(80, 25);
		Assert.Equal(2, drv.SetCursorPositionCallCount);
	}

	[Fact]
	public void ApplyCursor_Reemits_WhenPositionChanges()
	{
		var (svc, drv, win) = Make();
		svc.UpdateFromWindowSystem(win, new Point(2, 3), new Point(2, 3), null, CursorShape.Block);
		svc.ApplyCursorToConsole(80, 25);
		svc.UpdateFromWindowSystem(win, new Point(5, 3), new Point(5, 3), null, CursorShape.Block);
		svc.ApplyCursorToConsole(80, 25);
		Assert.Equal(2, drv.SetCursorPositionCallCount);
	}
}
