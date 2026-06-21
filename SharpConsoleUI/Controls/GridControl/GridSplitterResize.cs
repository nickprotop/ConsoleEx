// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Pure resize logic for a GridControl splitter: given the two adjacent track definitions, their
	/// current arranged sizes (in cells), and a cell delta (positive = track A grows / track B shrinks),
	/// returns the two NEW <see cref="GridLength"/> values per WinUI GridSplitter semantics.
	/// Preserve track type and edit its value; Star|Star conserves the weight sum; an Auto track bakes
	/// to Fixed(currentSize) on resize; min/max are respected and the delta is clamped so neither track
	/// crosses its minimum.
	/// </summary>
	internal static class GridSplitterResize
	{
		public static (GridLength A, GridLength B) ApplyResize(
			GridLength a, GridLength b, int deltaCells, int sizeA, int sizeB)
		{
			if (deltaCells == 0)
				return (a, b);

			int minA = a.Min ?? 0;
			int minB = b.Min ?? 0;
			int maxA = a.Max ?? int.MaxValue;
			int maxB = b.Max ?? int.MaxValue;

			int total = sizeA + sizeB;
			int targetA = sizeA + deltaCells;
			targetA = Math.Clamp(targetA, minA, Math.Min(maxA, total - minB));
			int targetB = total - targetA;
			if (targetB > maxB)
			{
				targetB = maxB;
				targetA = total - targetB;
			}
			if (targetA < minA) { targetA = minA; targetB = total - targetA; }

			int newSizeA = targetA;
			int newSizeB = targetB;

			GridLength ea = a.Type == GridUnitType.Auto ? GridLength.Cells(sizeA, a.Min, a.Max) : a;
			GridLength eb = b.Type == GridUnitType.Auto ? GridLength.Cells(sizeB, b.Min, b.Max) : b;

			bool aStar = ea.Type == GridUnitType.Star;
			bool bStar = eb.Type == GridUnitType.Star;

			if (aStar && bStar)
			{
				double sum = ea.Weight + eb.Weight;
				double frac = total > 0 ? (double)newSizeA / total : 0.5;
				double wA = sum * frac;
				double wB = sum - wA;
				return (GridLength.Star(wA <= 0 ? 0.0001 : wA, ea.Min, ea.Max),
						GridLength.Star(wB <= 0 ? 0.0001 : wB, eb.Min, eb.Max));
			}

			if (aStar && !bStar)
			{
				double scale = sizeA > 0 ? (double)newSizeA / sizeA : 1.0;
				double wA = ea.Weight * (scale <= 0 ? 0.0001 : scale);
				return (GridLength.Star(wA, ea.Min, ea.Max), eb);
			}

			if (!aStar && bStar)
			{
				double scale = sizeB > 0 ? (double)newSizeB / sizeB : 1.0;
				double wB = eb.Weight * (scale <= 0 ? 0.0001 : scale);
				return (ea, GridLength.Star(wB, eb.Min, eb.Max));
			}

			return (GridLength.Cells(newSizeA, ea.Min, ea.Max),
					GridLength.Cells(newSizeB, eb.Min, eb.Max));
		}
	}
}
