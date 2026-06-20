// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout;

/// <summary>
/// Turns a list of <see cref="GridLength"/> track definitions into concrete integer track
/// sizes along a single axis (columns or rows). This is a pure function: it performs no
/// rendering and holds no state, which makes it fully unit-testable in isolation.
/// </summary>
/// <remarks>
/// The sizing pass runs in three stages:
/// <list type="number">
/// <item><description>Gaps between adjacent tracks are reserved up front.</description></item>
/// <item><description><see cref="GridUnitType.Fixed"/> and <see cref="GridUnitType.Auto"/>
/// tracks are sized directly (the latter from <c>autoContentSizes</c>) and clamped to their
/// optional <see cref="GridLength.Min"/>/<see cref="GridLength.Max"/> bounds.</description></item>
/// <item><description><see cref="GridUnitType.Star"/> tracks split the remaining space by
/// weight. Because a star track's <see cref="GridLength.Min"/>/<see cref="GridLength.Max"/>
/// can clamp it, the split runs iteratively: any track that clamps is pinned and removed from
/// the pool, and the remaining unclamped stars re-split the adjusted remainder.</description></item>
/// </list>
/// Integer rounding is distributed via accumulated fractional remainder so star totals stay
/// exact (no cells gained or lost). Track sizes are never negative.
/// <para>
/// When constraints are contradictory the function favours honesty over silently violating a
/// constraint. If fixed/auto tracks plus gaps already exceed <c>available</c>, star tracks
/// collapse to 0 (the grid overflows but no size is negative). If star
/// <see cref="GridLength.Min"/> values over-subscribe the space (their sum exceeds the
/// remaining pool), every star is pinned to its <see cref="GridLength.Min"/> and the returned
/// total may therefore EXCEED <c>available</c> — contradictory minimums cannot all be honoured
/// inside the available space. The iterative star pass always terminates: each pass pins at
/// least one track and removes it from the unclamped set, so it runs at most once per star track.
/// </para>
/// </remarks>
public static class GridTrackSizer
{
	/// <summary>
	/// Computes the concrete integer size of each track.
	/// </summary>
	/// <param name="defs">The track definitions, one per row or column.</param>
	/// <param name="autoContentSizes">
	/// Parallel array giving, for each <see cref="GridUnitType.Auto"/> track, the maximum
	/// desired content size of its cells. Entries for non-Auto tracks are ignored (typically 0).
	/// </param>
	/// <param name="available">The total space available along this axis, in cells.</param>
	/// <param name="gap">The gap size, in cells, between adjacent tracks.</param>
	/// <returns>An array with one resolved integer size per track. Sizes are never negative.</returns>
	public static int[] Size(IReadOnlyList<GridLength> defs, int[] autoContentSizes, int available, int gap)
	{
		int count = defs.Count;
		if (count == 0)
		{
			return Array.Empty<int>();
		}

		// Guard against a negative gap inflating the star pool.
		gap = Math.Max(0, gap);

		int[] sizes = new int[count];
		int totalGap = count > 1 ? (count - 1) * gap : 0;

		// Stage 1 & 2: Fixed and Auto tracks size directly and clamp.
		int consumed = 0;
		for (int i = 0; i < count; i++)
		{
			GridLength def = defs[i];
			switch (def.Type)
			{
				case GridUnitType.Fixed:
					sizes[i] = Clamp(def.Value, def.Min, def.Max);
					consumed += sizes[i];
					break;

				case GridUnitType.Auto:
					// Defensive read: tolerate a short autoContentSizes array in the hot path.
					int contentSize = i < autoContentSizes.Length ? autoContentSizes[i] : 0;
					sizes[i] = Clamp(contentSize, def.Min, def.Max);
					consumed += sizes[i];
					break;
			}
		}

		// Stage 3: Star tracks split the remaining space by weight.
		int remaining = Math.Max(0, available - totalGap - consumed);

		// Track which star tracks are still unclamped (eligible for weight-based splitting).
		bool[] starUnpinned = new bool[count];
		for (int i = 0; i < count; i++)
		{
			starUnpinned[i] = defs[i].Type == GridUnitType.Star;
		}

		// Iteratively assign by weight; pin any track that violates Min/Max, remove it from the
		// pool, and re-split the adjusted remainder among the rest.
		bool clampedThisPass = true;
		while (clampedThisPass)
		{
			clampedThisPass = false;

			// Sum the weight of the still-unpinned stars.
			double activeWeight = 0;
			for (int i = 0; i < count; i++)
			{
				if (starUnpinned[i])
				{
					activeWeight += defs[i].Weight;
				}
			}

			if (activeWeight <= 0)
			{
				break;
			}

			// Provisionally distribute the remaining pool over the active stars, accumulating
			// fractional remainder so the integer total stays exact.
			double accumulator = 0;
			int distributed = 0;
			for (int i = 0; i < count; i++)
			{
				if (!starUnpinned[i])
				{
					continue;
				}

				accumulator += remaining * (defs[i].Weight / activeWeight);
				int share = (int)Math.Round(accumulator) - distributed;
				if (share < 0)
				{
					share = 0;
				}
				distributed += share;
				sizes[i] = share;
			}

			// Check for clamp violations. Pin the first violating track and restart the split.
			for (int i = 0; i < count; i++)
			{
				if (!starUnpinned[i])
				{
					continue;
				}

				GridLength def = defs[i];
				int clamped = Clamp(sizes[i], def.Min, def.Max);
				if (clamped != sizes[i])
				{
					sizes[i] = clamped;
					starUnpinned[i] = false;
					remaining = Math.Max(0, remaining - clamped);
					clampedThisPass = true;
					break;
				}
			}
		}

		return sizes;
	}

	/// <summary>
	/// Clamps <paramref name="value"/> to the optional <paramref name="min"/>/<paramref name="max"/>
	/// bounds and floors the result at 0 so sizes are never negative.
	/// </summary>
	private static int Clamp(int value, int? min, int? max)
	{
		if (min.HasValue && value < min.Value)
		{
			value = min.Value;
		}
		if (max.HasValue && value > max.Value)
		{
			value = max.Value;
		}
		return Math.Max(0, value);
	}
}
