// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;

namespace SharpConsoleUI.Helpers;

/// <summary>
/// Caches expensive text measurement operations for improved rendering performance.
/// Eliminates duplicate measurements during rendering cycles (40+ calls per frame in complex controls).
/// Thread-safe: uses ConcurrentDictionary to support concurrent read/write from render and UI threads.
/// </summary>
public class TextMeasurementCache
{
	private const int MaxCacheEntries = 4096;
	private readonly ConcurrentDictionary<string, int> _cache = new();
	private readonly Func<string, int> _measurementFunc;

	/// <summary>
	/// Creates a new text measurement cache with the specified measurement function.
	/// </summary>
	/// <param name="measurementFunc">Function to measure text width (e.g., Parsing.MarkupParser.StripLength)</param>
	public TextMeasurementCache(Func<string, int> measurementFunc)
	{
		_measurementFunc = measurementFunc ?? throw new ArgumentNullException(nameof(measurementFunc));
	}

	/// <summary>
	/// Gets the cached length of the text, or measures and caches it if not already cached.
	/// </summary>
	/// <param name="text">The text to measure</param>
	/// <returns>The visible character length of the text</returns>
	public int GetCachedLength(string text)
	{
		if (string.IsNullOrEmpty(text))
			return 0;

		if (_cache.TryGetValue(text, out int cachedLength))
			return cachedLength;

		int length = _measurementFunc(text);
		if (_cache.Count >= MaxCacheEntries)
			_cache.Clear();
		_cache[text] = length;
		return length;
	}

	/// <summary>
	/// Measures a string, stopping once it is known to be at least <paramref name="cap"/> wide.
	/// </summary>
	/// <param name="text">The text to measure.</param>
	/// <param name="cap">The widest answer the caller can use.</param>
	/// <returns>The width, or <paramref name="cap"/> when the text is at least that wide.</returns>
	/// <remarks>
	/// <para>
	/// For a caller choosing a column width, anything past the width the column
	/// could possibly be given cannot change the answer. A cell holding a whole
	/// file would otherwise be walked and hashed in full, and then KEPT by the
	/// cache, to learn that it is wider than the screen.
	/// </para>
	/// <para>
	/// Not cached, deliberately. The cache is keyed by the whole string, so
	/// storing a megabyte to remember one small number is the cost this avoids.
	/// A short string goes to the ordinary cached path instead, where it belongs.
	/// </para>
	/// </remarks>
	public int GetCappedLength(string text, int cap)
	{
		if (string.IsNullOrEmpty(text) || cap <= 0)
			return 0;

		// Short enough that measuring it in full is cheap, and worth caching
		// since it will be asked for again on the next frame. The bound is on
		// CHARACTERS, which is only ever an over-estimate of columns, so a string
		// that passes it genuinely cannot reach the cap.
		if (text.Length <= cap)
			return GetCachedLength(text);

		// Measured on a prefix rather than the whole string. A column's width is
		// counted in columns, and a character is never less than zero columns
		// wide, so a prefix of cap+1 CHARACTERS either already reaches the cap or
		// proves the rest cannot be needed. Markup makes this an over-measure of
		// the prefix only, never an under-measure of the answer.
		//
		// Substring rather than a span because the measure takes a string; it is
		// cap+1 characters long whatever the cell holds, so it stays small.
		int length = _measurementFunc(text[..(cap + 1)]);

		return length > cap ? cap : length;
	}

	/// <summary>
	/// Gets the total cached length of multiple text strings.
	/// Useful for measuring composite strings like "prefix + content + suffix".
	/// </summary>
	/// <param name="texts">Array of text strings to measure</param>
	/// <returns>Sum of the visible character lengths</returns>
	public int GetCachedTotalLength(params string[] texts)
	{
		int total = 0;
		foreach (var text in texts)
		{
			if (!string.IsNullOrEmpty(text))
				total += GetCachedLength(text);
		}
		return total;
	}

	/// <summary>
	/// Invalidates the entire cache, forcing all subsequent measurements to be recalculated.
	/// Call this when the measurement function behavior changes or when you need to free memory.
	/// </summary>
	public void InvalidateCache()
	{
		_cache.Clear();
	}

	/// <summary>
	/// Invalidates a specific cached entry.
	/// </summary>
	/// <param name="text">The text whose cached measurement should be removed</param>
	public void InvalidateCachedEntry(string text)
	{
		if (!string.IsNullOrEmpty(text))
			_cache.TryRemove(text, out _);
	}

	/// <summary>
	/// Gets the number of cached entries.
	/// </summary>
	public int CachedCount => _cache.Count;
}
