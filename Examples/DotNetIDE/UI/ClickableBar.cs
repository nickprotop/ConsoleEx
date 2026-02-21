namespace DotNetIDE;

/// <summary>
/// A single bar item: a shortcut+label pair with an optional click action and a
/// tracked column range populated by the subclass during Render().
/// </summary>
public class BarItem
{
    public string  Shortcut { get; set; } = string.Empty;
    public string  Label    { get; set; } = string.Empty;
    public Action? OnClick  { get; set; }
    public int     StartX   { get; set; }
    public int     EndX     { get; set; }
}

/// <summary>
/// Abstract base for rendered bars that contain clickable items.
/// Subclasses implement Render() to build Spectre markup and record each item's
/// plain-text column range; HandleClickAt() dispatches a pre-adjusted column to
/// the matching item without fragile position math in the caller.
/// </summary>
public abstract class ClickableBar
{
    protected readonly List<BarItem> _items = new();

    /// <summary>Total plain-text column width produced by the last Render() call.</summary>
    public int TotalRenderedLength { get; protected set; }

    public virtual void Clear()
    {
        _items.Clear();
        TotalRenderedLength = 0;
    }

    public abstract string Render();

    protected bool HandleClickAt(int col)
    {
        foreach (var item in _items)
        {
            if (item.OnClick != null && col >= item.StartX && col <= item.EndX)
            {
                item.OnClick.Invoke();
                return true;
            }
        }
        return false;
    }
}
