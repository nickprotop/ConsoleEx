// SharpConsoleUI/Core/AsyncEvent.cs
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Core;

/// <summary>
/// Raises a paired sync/async notification event. Sync subscribers run inline; async subscribers
/// are fire-and-forgotten (their continuations marshal back via the installed UI SynchronizationContext)
/// and their exceptions are routed to the log instead of being thrown or going unobserved.
/// </summary>
public static class AsyncEvent
{
	/// <summary>Raises both the sync and async handler lists for a notification event.</summary>
	public static void Raise<TArgs>(
		EventHandler<TArgs>? sync,
		AsyncEventHandler<TArgs>? async,
		object? sender,
		TArgs args,
		ILogService? log)
	{
		sync?.Invoke(sender, args);
		RaiseAsync(async, sender, args, log);
	}

	/// <summary>
	/// Raises both the sync and async handler lists for a notification event whose sync side uses
	/// the non-generic <see cref="EventHandler"/> (paired with an <see cref="AsyncEventHandler{T}"/> of <see cref="EventArgs"/>).
	/// </summary>
	public static void Raise(
		EventHandler? sync,
		AsyncEventHandler<EventArgs>? async,
		object? sender,
		EventArgs args,
		ILogService? log)
	{
		sync?.Invoke(sender, args);
		RaiseAsync(async, sender, args, log);
	}

	private static void RaiseAsync<TArgs>(
		AsyncEventHandler<TArgs>? async, object? sender, TArgs args, ILogService? log)
	{
		if (async == null) return;
		foreach (var d in async.GetInvocationList())
		{
			var handler = (AsyncEventHandler<TArgs>)d;
			_ = InvokeSafelyAsync(handler, sender, args, log);
		}
	}

	private static async Task InvokeSafelyAsync<TArgs>(
		AsyncEventHandler<TArgs> handler, object? sender, TArgs args, ILogService? log)
	{
		try { await handler(sender, args); }
		catch (Exception ex) { log?.LogError($"Async event handler error: {ex.Message}", ex, "AsyncEvent"); }
	}
}
