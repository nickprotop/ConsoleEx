using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class InvokeAsyncTests
{
	[Fact]
	public async Task InvokeAsync_MarshalsWork_AndReturnsResult()
	{
		var sys = new ConsoleWindowSystem(RenderMode.Buffer, options: new ConsoleWindowSystemOptions());
		Assert.False(sys.IsOnUIThread);                  // not running → uiThreadId is -1

		var task = sys.InvokeAsync(() => 21 * 2);
		sys.DrainPendingUIActionsForTest();              // simulate the loop draining the queued action
		Assert.Equal(42, await task);
	}
}
