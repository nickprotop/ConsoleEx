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
	private readonly Action<Action, string?> _enqueue;
	private readonly Func<bool> _isOnUIThread;

	/// <summary>Initializes a new instance.</summary>
	public ConsoleUISynchronizationContext(Action<Action, string?> enqueue, Func<bool>? isOnUIThread = null)
	{
		_enqueue = enqueue;
		_isOnUIThread = isOnUIThread ?? (() => false);
	}

	/// <inheritdoc/>
	// LABELLED WITH THE CONTINUATION'S OWN TARGET. Every `await` in a host app resumes through
	// here, so an unlabelled Post makes the watchdog report the generic "UIAction" for the one
	// thing most likely to stall a frame — a continuation that does real work. The declaring
	// type and method of the state machine are the only identity available at this point, and
	// they are enough to name the await that blocked.
	public override void Post(SendOrPostCallback d, object? state) =>
		_enqueue(() => d(state), DescribeContinuation(d, state));

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
		_enqueue(() => { try { d(state); } catch (Exception ex) { error = ex; } finally { done.Set(); } },
			"syncContext.Send");
		done.Wait();
		if (error != null) throw error;
	}
	/// <summary>
	/// Names the await that is resuming, for the watchdog breadcrumb.
	///
	/// <para>A continuation's delegate targets the compiler-generated state machine of the method that
	/// awaited, so its declaring type carries that method's name — "&lt;DoWorkAsync&gt;d__12" on the
	/// class that declared it. That is ugly and it is also the only identity available here, and a
	/// breadcrumb naming the real method beats one reading "UIAction".</para>
	///
	/// <para>NEVER THROWS AND NEVER ALLOCATES MUCH: this runs on every await resumption in the host,
	/// so it stays a couple of property reads, and a failure to describe must not fail the Post.</para>
	/// </summary>
	private static string? DescribeContinuation(SendOrPostCallback d, object? state)
	{
		try
		{
			var target = d.Target ?? state;
			if (target is null) return d.Method.Name;

			// A PLAIN Action WRAPPER TELLS US NOTHING — "Action`1" is the delegate's own type, not the
			// work. When the target is a delegate, describe what IT points at instead.
			if (target is Delegate inner)
			{
				var m = inner.Method;
				var host = m.DeclaringType;
				var hostName = host?.Name ?? "?";
				var lt = hostName.IndexOf('<');
				var gt = hostName.IndexOf('>');
				if (lt == 0 && gt > 1) hostName = hostName[1..gt];
				var outer = host?.DeclaringType?.Name;
				return outer is null ? $"{hostName}.{m.Name}" : $"{outer}.{hostName}.{m.Name}";
			}

			var type = target.GetType();
			var name = type.Name;

			// "<DoWorkAsync>d__12" -> "DoWorkAsync"; anything else is used as it stands.
			var open = name.IndexOf('<');
			var close = name.IndexOf('>');
			if (open == 0 && close > 1) name = name[1..close];

			return type.DeclaringType is { } owner ? $"{owner.Name}.{name}" : name;
		}
		catch (Exception)
		{
			return null;
		}
	}


	/// <inheritdoc/>
	public override SynchronizationContext CreateCopy() => this;
}
