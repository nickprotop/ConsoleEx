// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using SharpConsoleUI.Animation;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class GridControl
	{
		// In-flight track animations, keyed by track index, so re-animating a track cancels its prior one.
		private readonly Dictionary<int, IAnimation> _colAnims = new();
		private readonly Dictionary<int, IAnimation> _rowAnims = new();

		private AnimationManager? _testAnimationManager;

		/// <summary>Test-only: injects an animation manager, bypassing the parent-window lookup.</summary>
		internal void SetAnimationManagerForTesting(AnimationManager manager) => _testAnimationManager = manager;

		/// <summary>Resolves the animation manager from the parent window, or the test override.</summary>
		private AnimationManager? GetAnimationManager()
		{
			if (_testAnimationManager != null)
				return _testAnimationManager;

			return (this as IWindowControl).GetParentWindow()?.GetConsoleWindowSystem?.Animations;
		}

		/// <summary>
		/// Animates column <paramref name="columnIndex"/> from its current arranged width to
		/// <paramref name="targetCells"/> cells over <paramref name="duration"/>. Returns the animation handle,
		/// or <c>null</c> when the index is invalid or no animation manager is available (in which case the
		/// target size is applied immediately). A target of 0 collapses the column. Starting a new animation on
		/// the same column cancels any in-flight one for it. The track's original sizing type is restored when
		/// the animation completes, so a Star column resumes proportional reflow afterward.
		/// </summary>
		/// <param name="columnIndex">Zero-based column track index.</param>
		/// <param name="targetCells">Desired final width in cells (clamped to the track's Min/Max).</param>
		/// <param name="duration">Transition duration.</param>
		/// <param name="easing">Easing; defaults to <see cref="EasingFunctions.EaseOut"/>.</param>
		public IAnimation? AnimateColumnWidth(int columnIndex, int targetCells, TimeSpan duration, EasingFunction? easing = null)
			=> AnimateTrack(ColumnDefinitions, LayoutAlgorithm.LastArrangeMetrics.ColSizes, _colAnims, columnIndex, targetCells, duration, easing);

		/// <summary>Row counterpart of <see cref="AnimateColumnWidth"/>; animates row height in cells.</summary>
		public IAnimation? AnimateRowHeight(int rowIndex, int targetCells, TimeSpan duration, EasingFunction? easing = null)
			=> AnimateTrack(RowDefinitions, LayoutAlgorithm.LastArrangeMetrics.RowSizes, _rowAnims, rowIndex, targetCells, duration, easing);

		/// <summary>
		/// Shared animate-a-track core. Tweens the arranged cell-size, holding the track as Fixed during the
		/// animation, and restores the original sizing type on completion.
		/// </summary>
		private IAnimation? AnimateTrack(
			IList<GridLength> defs,
			int[] arrangedSizes,
			Dictionary<int, IAnimation> inFlight,
			int index,
			int targetCells,
			TimeSpan duration,
			EasingFunction? easing)
		{
			// Index validation — no throw.
			if (index < 0 || index >= defs.Count)
				return null;

			// Cancel any in-flight animation for this same track so two tweens never fight over it.
			if (inFlight.TryGetValue(index, out var prev))
			{
				prev.Cancel();
				inFlight.Remove(index);
			}

			GridLength original = defs[index];

			// Clamp the target to the track's own Min/Max (if set).
			int min = original.Min ?? 0;
			int max = original.Max ?? int.MaxValue;
			if (targetCells < min) targetCells = min;
			if (targetCells > max) targetCells = max;

			// Current arranged size for this track (0 if layout has not run / index out of metrics range).
			int currentCells = (index >= 0 && index < arrangedSizes.Length) ? arrangedSizes[index] : 0;

			// The end-state GridLength that restores the original sizing type at the final size.
			GridLength RestoredEndState()
			{
				switch (original.Type)
				{
					case GridUnitType.Auto:
						return GridLength.Auto(original.Min, original.Max);
					case GridUnitType.Star:
						double scale = (double)targetCells / Math.Max(1, currentCells);
						double weight = original.Weight * scale;
						if (weight <= 0) weight = 0.0001; // mirror GridSplitterResize floor
						return GridLength.Star(weight, original.Min, original.Max);
					default: // Fixed
						return GridLength.Cells(targetCells, original.Min, original.Max);
				}
			}

			var manager = GetAnimationManager();
			if (manager == null)
			{
				// No manager — apply the final state immediately.
				defs[index] = RestoredEndState();
				Invalidate(true);
				return null;
			}

			var anim = manager.Animate(
				from: currentCells,
				to: targetCells,
				duration: duration,
				easing: easing ?? EasingFunctions.EaseOut,
				onUpdate: cells =>
				{
					// Hold the track as Fixed for the duration of the tween.
					defs[index] = GridLength.Cells(cells, original.Min, original.Max);
					Invalidate(true);
				},
				onComplete: () =>
				{
					defs[index] = RestoredEndState();
					inFlight.Remove(index);
					Invalidate(true);
				});

			inFlight[index] = anim;
			return anim;
		}

		/// <summary>
		/// Gets the current arranged width, in cells, of column <paramref name="index"/>, or -1 if layout has
		/// not run or the index is out of range. Useful for driving animations (e.g. reading a column's width
		/// before collapsing it so it can be restored on expand).
		/// </summary>
		public int GetColumnArrangedWidth(int index)
		{
			var sizes = LayoutAlgorithm.LastArrangeMetrics.ColSizes;
			return (index >= 0 && index < sizes.Length) ? sizes[index] : -1;
		}

		/// <summary>
		/// Gets the current arranged height, in cells, of row <paramref name="index"/>, or -1 if layout has not
		/// run or the index is out of range.
		/// </summary>
		public int GetRowArrangedHeight(int index)
		{
			var sizes = LayoutAlgorithm.LastArrangeMetrics.RowSizes;
			return (index >= 0 && index < sizes.Length) ? sizes[index] : -1;
		}
	}
}
