// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Core;

/// <summary>
/// SynchronizationContext that marshals continuations onto the UI (main-loop) thread via the
/// system's UI-action queue, so <c>await</c> in a handler resumes on the UI thread (WinForms/WPF model).
/// </summary>
internal sealed class ConsoleUISynchronizationContext : SynchronizationContext
{
	private readonly Action<Action> _enqueue;
	private readonly Func<bool> _isOnUIThread;

	/// <summary>Initializes a new instance.</summary>
	public ConsoleUISynchronizationContext(Action<Action> enqueue, Func<bool>? isOnUIThread = null)
	{
		_enqueue = enqueue;
		_isOnUIThread = isOnUIThread ?? (() => false);
	}

	/// <inheritdoc/>
	public override void Post(SendOrPostCallback d, object? state) => _enqueue(() => d(state));

	/// <inheritdoc/>
	public override void Send(SendOrPostCallback d, object? state)
	{
		if (_isOnUIThread())
		{
			d(state);
			return;
		}
		using var done = new ManualResetEventSlim(false);
		Exception? error = null;
		_enqueue(() => { try { d(state); } catch (Exception ex) { error = ex; } finally { done.Set(); } });
		done.Wait();
		if (error != null) throw error;
	}

	/// <inheritdoc/>
	public override SynchronizationContext CreateCopy() => this;
}
