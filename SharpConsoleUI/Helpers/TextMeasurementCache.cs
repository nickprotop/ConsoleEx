namespace SharpConsoleUI.Helpers;

/// <summary>
/// Caches expensive text measurement operations for improved rendering performance.
/// Eliminates duplicate measurements during rendering cycles (40+ calls per frame in complex controls).
/// </summary>
public class TextMeasurementCache
{
	private readonly Dictionary<string, int> _cache = new();
	private readonly Func<string, int> _measurementFunc;

	/// <summary>
	/// Creates a new text measurement cache with the specified measurement function.
	/// </summary>
	/// <param name="measurementFunc">Function to measure text width (e.g., AnsiConsoleHelper.StripSpectreLength)</param>
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
		_cache[text] = length;
		return length;
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
			_cache.Remove(text);
	}

	/// <summary>
	/// Gets the number of cached entries.
	/// </summary>
	public int CachedCount => _cache.Count;
}
