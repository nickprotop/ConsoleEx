using System.Collections.Concurrent;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

public class ConsoleUISynchronizationContextTests
{
	[Fact]
	public void Post_QueuesWork_ToTheProvidedEnqueueDelegate()
	{
		// THE LABEL IS CAPTURED TOO, because the watchdog's breadcrumb is the only thing that names a
		// stalled frame's work, and a continuation posted here is the likeliest thing to stall one.
		var queue = new ConcurrentQueue<System.Action>();
		var labels = new ConcurrentQueue<string?>();
		var ctx = new ConsoleUISynchronizationContext((a, label) =>
		{
			queue.Enqueue(a);
			labels.Enqueue(label);
		});

		var ran = false;
		ctx.Post(_ => ran = true, null);

		Assert.False(ran);
		Assert.True(queue.TryDequeue(out var work));
		work!();
		Assert.True(ran);

		// NAMED, NOT NULL: an unlabelled post is what made a five-second stall report only
		// "in UIAction: UIAction" — true, and useless for finding the action that blocked.
		Assert.True(labels.TryDequeue(out var posted));
		Assert.False(string.IsNullOrEmpty(posted));
	}
}
