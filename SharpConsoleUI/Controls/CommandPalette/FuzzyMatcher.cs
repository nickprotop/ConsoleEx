// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;

#pragma warning disable CS1591

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// Result of a fuzzy match operation, containing the score and matched character indices.
	/// </summary>
	public sealed class FuzzyMatchResult
	{
		public FuzzyMatchResult(int score, int[] matchedIndices)
		{
			Score = score;
			MatchedIndices = matchedIndices;
		}

		public int Score { get; }
		public int[] MatchedIndices { get; }
	}

	/// <summary>
	/// Provides fuzzy string matching with scoring bonuses for consecutive matches,
	/// word boundaries, camel case boundaries, and prefix matches.
	/// </summary>
	public static class FuzzyMatcher
	{
		/// <summary>
		/// Attempts to fuzzy-match <paramref name="query"/> against <paramref name="target"/>.
		/// Returns null if no match is found.
		/// </summary>
		public static FuzzyMatchResult? Match(string query, string target)
		{
			if (string.IsNullOrEmpty(query))
				return new FuzzyMatchResult(0, Array.Empty<int>());

			if (string.IsNullOrEmpty(target))
				return null;

			var queryLower = query.ToLowerInvariant();
			var targetLower = target.ToLowerInvariant();

			// Check if all query characters exist in target in order
			var matchedIndices = new int[query.Length];
			int targetIdx = 0;

			for (int qi = 0; qi < queryLower.Length; qi++)
			{
				bool found = false;
				while (targetIdx < targetLower.Length)
				{
					if (targetLower[targetIdx] == queryLower[qi])
					{
						matchedIndices[qi] = targetIdx;
						targetIdx++;
						found = true;
						break;
					}
					targetIdx++;
				}

				if (!found)
					return null;
			}

			int score = CalculateScore(query, target, matchedIndices);
			return new FuzzyMatchResult(score, matchedIndices);
		}

		private static int CalculateScore(string query, string target, int[] matchedIndices)
		{
			int score = 0;

			for (int i = 0; i < matchedIndices.Length; i++)
			{
				int idx = matchedIndices[i];

				// First character bonus
				if (idx == 0)
					score += ControlDefaults.CommandPaletteFuzzyFirstCharBonus;

				// Consecutive match bonus
				if (i > 0 && matchedIndices[i] == matchedIndices[i - 1] + 1)
					score += ControlDefaults.CommandPaletteFuzzyConsecutiveBonus;

				// Word boundary bonus (preceded by space, underscore, hyphen, or dot)
				if (idx > 0 && IsWordSeparator(target[idx - 1]))
					score += ControlDefaults.CommandPaletteFuzzyWordBoundaryBonus;

				// Camel case boundary bonus
				if (idx > 0 && char.IsLower(target[idx - 1]) && char.IsUpper(target[idx]))
					score += ControlDefaults.CommandPaletteFuzzyCamelCaseBonus;

				// Gap penalty
				if (i > 0)
				{
					int gap = matchedIndices[i] - matchedIndices[i - 1] - 1;
					score -= gap * ControlDefaults.CommandPaletteFuzzyGapPenalty;
				}
			}

			// Exact prefix bonus
			if (matchedIndices.Length > 0 && matchedIndices[0] == 0)
			{
				bool isPrefix = true;
				for (int i = 1; i < matchedIndices.Length; i++)
				{
					if (matchedIndices[i] != i)
					{
						isPrefix = false;
						break;
					}
				}
				if (isPrefix)
					score += ControlDefaults.CommandPaletteFuzzyPrefixBonus;
			}

			return score;
		}

		private static bool IsWordSeparator(char c)
		{
			return c == ' ' || c == '_' || c == '-' || c == '.';
		}

		/// <summary>
		/// Filters and sorts items by fuzzy matching the query against both label and category.
		/// Returns items sorted by descending score.
		/// </summary>
		public static List<(CommandPaletteItem Item, FuzzyMatchResult Result)> FilterAndSort(
			string query, IEnumerable<CommandPaletteItem> items)
		{
			if (string.IsNullOrEmpty(query))
			{
				return items.Select(item => (item, new FuzzyMatchResult(0, Array.Empty<int>())))
					.ToList();
			}

			var results = new List<(CommandPaletteItem Item, FuzzyMatchResult Result)>();

			foreach (var item in items)
			{
				var labelMatch = Match(query, item.Label);
				if (labelMatch != null)
				{
					results.Add((item, labelMatch));
					continue;
				}

				// Also try matching against category
				if (!string.IsNullOrEmpty(item.Category))
				{
					var categoryMatch = Match(query, item.Category);
					if (categoryMatch != null)
					{
						results.Add((item, new FuzzyMatchResult(
							categoryMatch.Score / 2, Array.Empty<int>())));
					}
				}
			}

			results.Sort((a, b) => b.Result.Score.CompareTo(a.Result.Score));
			return results;
		}
	}
}
