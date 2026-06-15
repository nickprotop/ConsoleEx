using SharpConsoleUI;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests;

public class WindowScrollByTests
{
	[Fact]
	public void ScrollBy_BeforeRender_DoesNotThrow_AndIsNoOp()
	{
		var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
		var window = new Window(system) { IsScrollable = true };
		var ex = Record.Exception(() => window.ScrollBy(5));
		Assert.Null(ex);
		Assert.Equal(0, window.ScrollOffset);
	}

	[Fact]
	public void ScrollBy_WhenNotScrollable_DoesNothing()
	{
		var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
		var window = new Window(system) { IsScrollable = false };
		window.ScrollBy(5);
		Assert.Equal(0, window.ScrollOffset);
	}
}
