namespace ConsoleTopExample.Helpers;

internal sealed class HistoryTracker
{
    private readonly List<double> _data = new();
    private readonly int _maxPoints;

    public HistoryTracker(int maxPoints = UIConstants.MaxHistoryPoints)
    {
        _maxPoints = maxPoints;
    }

    public IReadOnlyList<double> Data => _data;
    public List<double> DataMutable => _data;

    public void Add(double value)
    {
        _data.Add(value);
        while (_data.Count > _maxPoints)
            _data.RemoveAt(0);
    }
}

internal sealed class KeyedHistoryTracker<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, HistoryTracker> _trackers = new();
    private readonly int _maxPoints;

    public KeyedHistoryTracker(int maxPoints = UIConstants.MaxHistoryPoints)
    {
        _maxPoints = maxPoints;
    }

    public void Add(TKey key, double value)
    {
        if (!_trackers.TryGetValue(key, out var tracker))
        {
            tracker = new HistoryTracker(_maxPoints);
            _trackers[key] = tracker;
        }
        tracker.Add(value);
    }

    public IReadOnlyList<double> Get(TKey key)
    {
        return _trackers.TryGetValue(key, out var tracker)
            ? tracker.Data
            : Array.Empty<double>();
    }

    public List<double> GetMutable(TKey key)
    {
        if (!_trackers.TryGetValue(key, out var tracker))
        {
            tracker = new HistoryTracker(_maxPoints);
            _trackers[key] = tracker;
        }
        return tracker.DataMutable;
    }

    public bool ContainsKey(TKey key) => _trackers.ContainsKey(key);

    public IEnumerable<TKey> Keys => _trackers.Keys;
}
