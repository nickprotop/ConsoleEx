// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Logging;

/// <summary>
/// Breaks the cycle when both logging bridges are wired to the same log service.
/// </summary>
/// <remarks>
/// <para>
/// With <see cref="SharpConsoleUILoggerProvider"/> registered in an application's logging
/// pipeline AND <see cref="LogServiceForwarder"/> forwarding that same log service back into
/// the pipeline, an entry would otherwise travel pipeline -> log service -> pipeline without end.
/// </para>
/// <para>
/// Each bridge marks the call while it hands an entry across, and the other checks the mark and
/// drops it. <see cref="ILogService.LogAdded"/> is raised synchronously by the log service, so a
/// full round trip happens on one logical call stack and the mark reliably covers it.
/// </para>
/// <para>
/// The state is <c>[ThreadStatic]</c>, not <see cref="AsyncLocal{T}"/>, and the difference
/// matters. The cycle this guards against is synchronous and happens on ONE thread: the log
/// service raises <see cref="ILogService.LogAdded"/> inline from its add path, so a full round
/// trip is a single call stack with no await in it. An <see cref="AsyncLocal{T}"/> would also
/// flow into work the pipeline starts with <c>Task.Run</c>, and an entry logged from such a
/// thread is a NEW entry, not re-entry: suppressing it loses log lines silently, which is worse
/// than the case this protects. Thread-static state does not flow, so that entry passes.
/// </para>
/// <para>
/// The trade-off: a pipeline that awaits inside its own logging path continues on another
/// thread without the mark, so a cycle routed through such an await is not caught here. That is
/// a different logical operation, and losing entries is the worse failure.
/// </para>
/// </remarks>
internal static class LogBridgeReentry
{
	// Counted rather than boolean: nesting is possible (a sink that logs while logging), and a
	// boolean would be cleared by the inner scope while the outer leg is still in flight.
	[ThreadStatic] private static int _inbound;
	[ThreadStatic] private static int _outbound;

	/// <summary>True while an entry is being carried from the pipeline into the log service.</summary>
	internal static bool IsInbound => _inbound > 0;

	/// <summary>True while an entry is being carried from the log service out to the pipeline.</summary>
	internal static bool IsOutbound => _outbound > 0;

	/// <summary>Marks the inbound leg (pipeline -> log service) until the scope is disposed.</summary>
	internal static IDisposable EnterInbound() => new InboundScope();

	/// <summary>Marks the outbound leg (log service -> pipeline) until the scope is disposed.</summary>
	internal static IDisposable EnterOutbound() => new OutboundScope();

	// Two scope types rather than one holding a ref: a thread-static field cannot be captured
	// by reference, and the alternative (an index plus a switch) buys nothing here.
	private sealed class InboundScope : IDisposable
	{
		private readonly int _thread = Environment.CurrentManagedThreadId;
		private bool _disposed;

		internal InboundScope() => _inbound++;

		public void Dispose()
		{
			// Decrement only on the thread that incremented: disposing on another would
			// corrupt both threads' counts, latching the guard on and dropping entries.
			if (_disposed || Environment.CurrentManagedThreadId != _thread) return;
			_disposed = true;
			_inbound--;
		}
	}

	private sealed class OutboundScope : IDisposable
	{
		private readonly int _thread = Environment.CurrentManagedThreadId;
		private bool _disposed;

		internal OutboundScope() => _outbound++;

		public void Dispose()
		{
			if (_disposed || Environment.CurrentManagedThreadId != _thread) return;
			_disposed = true;
			_outbound--;
		}
	}
}
