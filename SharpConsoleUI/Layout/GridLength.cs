// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Layout;

/// <summary>
/// Describes how a grid row or column is sized.
/// </summary>
public enum GridUnitType
{
	/// <summary>The track has a fixed, exact size measured in cells.</summary>
	Fixed,

	/// <summary>The track sizes itself to fit its content.</summary>
	Auto,

	/// <summary>The track takes a proportional share of the leftover space, based on its weight.</summary>
	Star
}

/// <summary>
/// A sizing primitive for a grid row or column. Describes whether the track is
/// <see cref="GridUnitType.Fixed"/> (exact cells), <see cref="GridUnitType.Auto"/>
/// (size-to-content), or <see cref="GridUnitType.Star"/> (proportional share of leftover
/// space), with optional minimum and maximum cell clamps.
/// </summary>
/// <remarks>
/// The <c>default</c> value (<c>new GridLength()</c>) is equivalent to
/// <see cref="Cells(int, int?, int?)"/> with <c>n = 0</c>: a zero-width
/// <see cref="GridUnitType.Fixed"/> track. Callers should always construct instances via the
/// <see cref="Cells(int, int?, int?)"/>, <see cref="Auto(int?, int?)"/>, and
/// <see cref="Star(double, int?, int?)"/> factories rather than relying on the default.
/// </remarks>
public readonly struct GridLength
{
	/// <summary>Gets how the track is sized.</summary>
	public GridUnitType Type { get; }

	/// <summary>Gets the exact cell count for a <see cref="GridUnitType.Fixed"/> track.</summary>
	public int Value { get; }

	/// <summary>Gets the relative weight for a <see cref="GridUnitType.Star"/> track.</summary>
	public double Weight { get; }

	/// <summary>Gets the optional minimum size, in cells, that the track is clamped to.</summary>
	public int? Min { get; }

	/// <summary>Gets the optional maximum size, in cells, that the track is clamped to.</summary>
	public int? Max { get; }

	private GridLength(GridUnitType type, int value, double weight, int? min, int? max)
	{
		Type = type;
		Value = value;
		Weight = weight;
		Min = min;
		Max = max;
	}

	private static void ValidateClamps(int? min, int? max)
	{
		if (min.HasValue && max.HasValue && min.Value > max.Value)
		{
			throw new ArgumentOutOfRangeException(
				nameof(min),
				$"min ({min.Value}) cannot be greater than max ({max.Value}).");
		}
	}

	/// <summary>
	/// Creates a <see cref="GridUnitType.Fixed"/> track of exactly <paramref name="n"/> cells.
	/// </summary>
	/// <param name="n">The exact size of the track, in cells.</param>
	/// <param name="min">An optional minimum size, in cells.</param>
	/// <param name="max">An optional maximum size, in cells.</param>
	/// <returns>A fixed-size <see cref="GridLength"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when both <paramref name="min"/> and <paramref name="max"/> are specified and
	/// <paramref name="min"/> is greater than <paramref name="max"/>.
	/// </exception>
	public static GridLength Cells(int n, int? min = null, int? max = null)
	{
		ValidateClamps(min, max);
		return new GridLength(GridUnitType.Fixed, n, 0, min, max);
	}

	/// <summary>
	/// Creates a <see cref="GridUnitType.Auto"/> track that sizes itself to its content.
	/// </summary>
	/// <param name="min">An optional minimum size, in cells.</param>
	/// <param name="max">An optional maximum size, in cells.</param>
	/// <returns>An auto-sized <see cref="GridLength"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when both <paramref name="min"/> and <paramref name="max"/> are specified and
	/// <paramref name="min"/> is greater than <paramref name="max"/>.
	/// </exception>
	public static GridLength Auto(int? min = null, int? max = null)
	{
		ValidateClamps(min, max);
		return new GridLength(GridUnitType.Auto, 0, 1, min, max);
	}

	/// <summary>
	/// Creates a <see cref="GridUnitType.Star"/> track that takes a proportional share of the
	/// leftover space, based on <paramref name="weight"/>.
	/// </summary>
	/// <param name="weight">The relative weight of the track. Values of 0 or less default to 1.</param>
	/// <param name="min">An optional minimum size, in cells.</param>
	/// <param name="max">An optional maximum size, in cells.</param>
	/// <returns>A proportionally-sized <see cref="GridLength"/>.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when both <paramref name="min"/> and <paramref name="max"/> are specified and
	/// <paramref name="min"/> is greater than <paramref name="max"/>.
	/// </exception>
	public static GridLength Star(double weight = 1, int? min = null, int? max = null)
	{
		ValidateClamps(min, max);
		return new GridLength(GridUnitType.Star, 0, weight <= 0 ? 1 : weight, min, max);
	}
}
