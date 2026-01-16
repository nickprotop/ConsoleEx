namespace SharpConsoleUI.Helpers;

/// <summary>
/// Helper class for truncating text with ellipsis, handling Spectre.Console markup correctly.
/// Consolidates 85% similar truncation logic from 4+ locations.
/// </summary>
public static class TextTruncationHelper
{
	/// <summary>
	/// Minimum number of content characters to show before adding ellipsis.
	/// If space is too tight, shows content without ellipsis instead of "ab...".
	/// </summary>
	public const int MinContentCharsBeforeEllipsis = 3;

	/// <summary>
	/// Truncates text to fit within maxWidth, adding ellipsis if truncated.
	/// Handles Spectre.Console markup correctly (e.g., "[red]text[/]").
	/// </summary>
	/// <param name="text">The text to truncate (may contain markup)</param>
	/// <param name="maxWidth">Maximum visible character width</param>
	/// <param name="ellipsis">Ellipsis string to append when truncated (default "...")</param>
	/// <param name="cache">Optional cache for measuring text width</param>
	/// <returns>Truncated text with ellipsis if needed</returns>
	public static string Truncate(
		string text,
		int maxWidth,
		string ellipsis = "...",
		TextMeasurementCache? cache = null)
	{
		if (string.IsNullOrEmpty(text))
			return text ?? string.Empty;

		if (maxWidth <= 0)
			return string.Empty;

		// Measure actual visible width (cache if available)
		int textWidth = cache?.GetCachedLength(text) ?? AnsiConsoleHelper.StripSpectreLength(text);

		// No truncation needed
		if (textWidth <= maxWidth)
			return text;

		// Edge case: maxWidth too small to fit even full ellipsis
		if (maxWidth <= ellipsis.Length)
		{
			// Return truncated ellipsis: ".", "..", etc.
			return ellipsis.Substring(0, maxWidth);
		}

		// Check if we have room for meaningful content + ellipsis
		int availableForContent = maxWidth - ellipsis.Length;

		if (availableForContent < MinContentCharsBeforeEllipsis)
		{
			// Not enough room for MIN_CONTENT + ellipsis
			// Show content only without ellipsis (e.g., "abcde" instead of "ab...")
			return AnsiConsoleHelper.TruncateSpectre(text, maxWidth);
		}

		// Normal case: truncate content and add ellipsis
		return AnsiConsoleHelper.TruncateSpectre(text, availableForContent) + ellipsis;
	}

	/// <summary>
	/// Truncates text with fixed prefix and suffix parts.
	/// Only the content portion is truncated; prefix and suffix are preserved.
	/// Example: "  ├─ " + "[red]VeryLongName[/]" + " [+]" truncated to fit totalMaxWidth.
	/// </summary>
	/// <param name="prefix">Fixed prefix that never truncates (e.g., tree indentation)</param>
	/// <param name="content">The content to truncate if needed</param>
	/// <param name="suffix">Fixed suffix that never truncates (e.g., expand indicator)</param>
	/// <param name="totalMaxWidth">Maximum total width for prefix + content + suffix</param>
	/// <param name="cache">Cache for measuring text widths</param>
	/// <param name="ellipsis">Ellipsis string to append to truncated content (default "...")</param>
	/// <returns>Composite string: prefix + truncatedContent + suffix</returns>
	public static string TruncateWithFixedParts(
		string prefix,
		string content,
		string suffix,
		int totalMaxWidth,
		TextMeasurementCache cache,
		string ellipsis = "...")
	{
		if (totalMaxWidth <= 0)
			return string.Empty;

		// Measure fixed parts (never truncate)
		int prefixWidth = string.IsNullOrEmpty(prefix) ? 0 : cache.GetCachedLength(prefix);
		int suffixWidth = string.IsNullOrEmpty(suffix) ? 0 : cache.GetCachedLength(suffix);

		// Calculate space available for content
		int availableForContent = totalMaxWidth - prefixWidth - suffixWidth;

		if (availableForContent <= 0)
		{
			// Not enough room for any content, return just prefix + suffix
			return (prefix ?? string.Empty) + (suffix ?? string.Empty);
		}

		// Truncate content to fit available space
		string truncatedContent = Truncate(content, availableForContent, ellipsis, cache);

		return (prefix ?? string.Empty) + truncatedContent + (suffix ?? string.Empty);
	}

	/// <summary>
	/// Truncates text with a fixed prefix only.
	/// Convenience overload for TruncateWithFixedParts with empty suffix.
	/// </summary>
	public static string TruncateWithPrefix(
		string prefix,
		string content,
		int totalMaxWidth,
		TextMeasurementCache cache,
		string ellipsis = "...")
	{
		return TruncateWithFixedParts(prefix, content, string.Empty, totalMaxWidth, cache, ellipsis);
	}

	/// <summary>
	/// Truncates text with a fixed suffix only.
	/// Convenience overload for TruncateWithFixedParts with empty prefix.
	/// </summary>
	public static string TruncateWithSuffix(
		string content,
		string suffix,
		int totalMaxWidth,
		TextMeasurementCache cache,
		string ellipsis = "...")
	{
		return TruncateWithFixedParts(string.Empty, content, suffix, totalMaxWidth, cache, ellipsis);
	}
}
