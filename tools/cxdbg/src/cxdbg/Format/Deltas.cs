// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Globalization;
using System.Text.Json.Nodes;

namespace Cxdbg.Format;

/// <summary>
/// Shows how far each number moved since the last time the same verb was asked.
/// </summary>
/// <remarks>
/// <para>
/// A live reading answers "what is it now". The question underneath is usually
/// "is it getting worse", and that needs two readings. Watching a rate climb
/// from 53 to 55 to 58 says something a single 58 does not.
/// </para>
/// <para>
/// Only where the comparison means something. A rate or a ratio is measured
/// over a window, so a change between ticks is the application behaving
/// differently - worth showing. A cumulative counter only ever goes up, and by
/// however long you waited, which the interval already told you; annotating it
/// would be noise dressed as information. Configuration does not move at all.
/// </para>
/// <para>
/// Applied to the reply before it is rendered, so it needs no cooperation from
/// the formatter and works for any verb whose numbers are worth comparing.
/// </para>
/// </remarks>
internal static class Deltas
{
	/// <summary>
	/// Fields whose movement is worth seeing: rates, ratios and distributions,
	/// all measured over a window rather than accumulated.
	/// </summary>
	/// <remarks>
	/// A list rather than a rule, because the distinction is about MEANING and
	/// cannot be read off the value: <c>bytesPerSecond</c> and
	/// <c>bytesWritten</c> are both numbers in the same reply, and only one of
	/// them is worth a delta.
	/// </remarks>
	private static readonly HashSet<string> Moving = new(StringComparer.Ordinal)
	{
		// performance: rates, ratios and distributions over the window
		"achievedFps", "meanMs", "p95Ms", "minMs", "maxMs",
		"busyPercent", "bytesPerSecond", "efficiency",
		"layoutMs", "ansiMs", "outputMs", "compareMs",

		// performance: sums over the WINDOW, not lifetime totals. The window
		// slides, so these fall as well as rise - a drop in cellsMarked is the
		// application settling down, which is worth seeing. All three together,
		// because they are the same kind of number and annotating one of them
		// would make the other two look deliberately static.
		"bytesWritten", "cellsRendered", "cellsMarked",

		// metrics: one frame, so every number is this frame's
		"charactersChanged", "dirtyCellsMarked", "ansiSequences", "cursorMovements",
	};

	/// <summary>Separates a value from its change, for the formatter to style.</summary>
	/// <remarks>
	/// A character no number or unit contains, so splitting on it cannot damage a
	/// value that merely looks numeric.
	/// </remarks>
	internal const char Marker = '\u241F';

	/// <summary>How small a change is not worth reporting.</summary>
	/// <remarks>
	/// Frame times wobble in the third decimal every tick. Showing that movement
	/// makes a steady reading look busy, which is the opposite of what a delta
	/// is for.
	/// </remarks>
	private const double Negligible = 0.005;

	/// <summary>
	/// Rewrites each comparable number as "value (+change)", in place.
	/// </summary>
	/// <param name="reply">This tick's reply. Modified.</param>
	/// <param name="previous">The one before it.</param>
	internal static void Annotate(JsonNode reply, JsonNode previous)
	{
		if (reply is not JsonObject now || previous is not JsonObject was) return;

		foreach (string name in Moving)
		{
			if (!now.TryGetPropertyValue(name, out var currentNode)) continue;
			if (!was.TryGetPropertyValue(name, out var earlierNode)) continue;

			if (Number(currentNode) is not { } current) continue;
			if (Number(earlierNode) is not { } earlier) continue;

			double change = current - earlier;

			// Separated by a marker rather than pre-styled. The formatter escapes
			// every value it prints, so markup put here would come out as literal
			// brackets - and letting a reply carry live markup would hand an
			// application the ability to colour cxdbg's own output.
			//
			// A value that held still is marked too, with an empty change: it is
			// dimmed rather than annotated, so the eye goes to what moved. Not
			// skipped, because "unchanged" and "not comparable" look the same on
			// screen otherwise, and only one of them is information.
			now[name] = Math.Abs(change) < Negligible
				? JsonValue.Create($"{Format(current)}{Marker}")
				: JsonValue.Create(
					$"{Format(current)}{Marker}{(change > 0 ? "+" : "")}{Format(change)}");
		}
	}

	/// <summary>The node as a number, or null if it is not one.</summary>
	/// <remarks>
	/// A previous tick's value may already carry its own annotation, since the
	/// same object is kept for comparison - so a plain number is required on
	/// both sides rather than parsed back out of a decorated string.
	/// </remarks>
	private static double? Number(JsonNode? node)
	{
		if (node is not JsonValue value) return null;

		try { return value.GetValue<double>(); }
		catch (Exception ex) when (ex is InvalidOperationException or FormatException)
		{
			return null;
		}
	}

	/// <summary>Trimmed to something readable, without trailing zeroes.</summary>
	private static string Format(double value) =>
		Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
