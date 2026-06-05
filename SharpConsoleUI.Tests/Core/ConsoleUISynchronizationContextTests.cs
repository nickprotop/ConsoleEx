using System.Collections.Concurrent;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class ConsoleUISynchronizationContextTests
{
	[Fact]
	public void Post_QueuesWork_ToTheProvidedEnqueueDelegate()
	{
		var queue = new ConcurrentQueue<System.Action>();
		var ctx = new ConsoleUISynchronizationContext(a => queue.Enqueue(a));

		var ran = false;
		ctx.Post(_ => ran = true, null);

		Assert.False(ran);
		Assert.True(queue.TryDequeue(out var work));
		work!();
		Assert.True(ran);
	}
}
