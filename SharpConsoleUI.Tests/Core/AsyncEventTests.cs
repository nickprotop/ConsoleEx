using System;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Core;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class AsyncEventTests
{
	[Fact]
	public void Raise_InvokesBothSyncAndAsyncSubscribers()
	{
		var syncRan = false;
		var asyncStarted = new ManualResetEventSlim(false);
		EventHandler<int> sync = (s, e) => syncRan = true;
		AsyncEventHandler<int> asyncH = (s, e) => { asyncStarted.Set(); return Task.CompletedTask; };

		AsyncEvent.Raise(sync, asyncH, this, 5, log: null);

		Assert.True(syncRan);
		Assert.True(asyncStarted.Wait(1000));
	}

	[Fact]
	public void Raise_DoesNotThrow_WhenAsyncHandlerFaults()
	{
		AsyncEventHandler<int> faulting = (s, e) => Task.FromException(new InvalidOperationException("boom"));
		AsyncEvent.Raise<int>(null, faulting, this, 1, log: null);  // must not throw to caller
	}

	[Fact]
	public void Window_ActivatedAsync_FiresWhenActivated()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem();
		var window = new Window(system) { Title = "Test" };
		var asyncFired = new ManualResetEventSlim(false);
		window.ActivatedAsync += (s, e) => { asyncFired.Set(); return Task.CompletedTask; };

		window.SetIsActive(true);

		Assert.True(asyncFired.Wait(1000));
	}
}
