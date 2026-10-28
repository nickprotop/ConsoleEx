// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Core;

/// <summary>
/// A clock that forwards to another one, which can be changed while running.
/// </summary>
/// <remarks>
/// <para>
/// Consumers capture a <see cref="TimeProvider"/> once - <c>MouseGestureRecognizer</c>
/// holds a closure over the one it was built with - so replacing a property would
/// never reach them. Handing every consumer this instead means the swap happens
/// behind them, and nobody needs to re-read anything.
/// </para>
/// <para>
/// It exists for the diagnostics socket: freezing a running application's clock to
/// step a double click apart by hand is the point. An application that never opens
/// a socket simply forwards to whatever it was configured with.
/// </para>
/// </remarks>
internal sealed class SwappableTimeProvider : TimeProvider
{
	private TimeProvider _inner;

	/// <param name="inner">The clock to forward to until someone swaps it.</param>
	internal SwappableTimeProvider(TimeProvider inner) => _inner = inner;

	/// <summary>The clock currently being forwarded to.</summary>
	internal TimeProvider Inner => Volatile.Read(ref _inner);

	/// <summary>Whether this is still forwarding to the real clock.</summary>
	internal bool IsSystem => ReferenceEquals(Inner, System);

	/// <summary>Points this at another clock.</summary>
	/// <returns>The clock it was pointing at before.</returns>
	internal TimeProvider Swap(TimeProvider next) =>
		Interlocked.Exchange(ref _inner, next);

	/// <inheritdoc/>
	public override DateTimeOffset GetUtcNow() => Inner.GetUtcNow();

	/// <inheritdoc/>
	public override TimeZoneInfo LocalTimeZone => Inner.LocalTimeZone;

	/// <inheritdoc/>
	public override long GetTimestamp() => Inner.GetTimestamp();

	/// <inheritdoc/>
	public override long TimestampFrequency => Inner.TimestampFrequency;

	/// <inheritdoc/>
	public override ITimer CreateTimer(
		TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
		Inner.CreateTimer(callback, state, dueTime, period);
}
