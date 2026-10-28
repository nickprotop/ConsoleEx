// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Core;

/// <summary>
/// A clock that only moves when it is told to.
/// </summary>
/// <remarks>
/// Frozen at the instant it was created, and advanced by
/// <see cref="Advance"/>. For the diagnostics socket, so a running application's
/// time can be stepped by hand - a double click becomes two clicks, or two clicks
/// a double, with no waiting.
/// </remarks>
internal sealed class ManualTimeProvider : TimeProvider
{
	private readonly TimeZoneInfo _zone;
	private long _ticks;

	/// <param name="start">Where the clock starts.</param>
	/// <param name="zone">The timezone to report; the real local zone by default.</param>
	internal ManualTimeProvider(DateTimeOffset start, TimeZoneInfo? zone = null)
	{
		_ticks = start.UtcTicks;
		_zone = zone ?? TimeZoneInfo.Local;
	}

	/// <inheritdoc/>
	public override DateTimeOffset GetUtcNow() =>
		new(Volatile.Read(ref _ticks), TimeSpan.Zero);

	/// <inheritdoc/>
	public override TimeZoneInfo LocalTimeZone => _zone;

	/// <summary>Moves the clock forward.</summary>
	/// <param name="by">How far. Negative values are refused: time running backwards
	/// makes every interval in the system meaningless.</param>
	/// <returns>The time after the move.</returns>
	internal DateTimeOffset Advance(TimeSpan by)
	{
		if (by < TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(by), "a clock may not run backwards");

		return new DateTimeOffset(Interlocked.Add(ref _ticks, by.Ticks), TimeSpan.Zero);
	}
}
